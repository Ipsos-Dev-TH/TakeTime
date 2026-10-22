using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Text;
using System.Web.UI;

namespace Take_Time_BangPhra.TaxReports
{
    /// <summary>
    /// กระทบยอดรายวัน TakeTime ↔ เอกสาร/JE ที่เข้าคิวส่ง NextAcc (Accounting_Sync_Queue):
    ///   รายได้/ภาษีขาย: ยอดขายจาก <see cref="TaxReportKit.CollectSales"/> เทียบกับ CREATE_RECEIPT_DOCUMENT /
    ///                   CREATE_OTA_SALES_DOCUMENT ล่าสุดต่อเลขเอกสาร (ไม่นับที่ VOID สำเร็จตามหลัง)
    ///   ต้นทุนขาย:      แถวขาย Product_Out + รายการรูมเซอร์วิส × Product.Cost_Price เทียบ STOCK_OUT_COGS (− REVERSE)
    ///   ตัดจำหน่าย:      Product_Out ที่ไม่ใช่การขาย เทียบ STOCK_WRITEOFF + STOCK_ADJUSTMENT (ขาลด) + STOCK_* อื่น
    ///                   (เช่น journal ตามเหตุผลจ่ายออก PHASE19_30)
    /// อ่านอย่างเดียว — เป็นยอดที่ "ส่ง/เข้าคิว" ฝั่ง TakeTime ไม่ได้ดึงยอด GL จริงจาก NextAcc
    /// </summary>
    public partial class NextAccReconciliation : Page
    {
        private sealed class DayRec
        {
            public DateTime Day;
            public decimal TtRev, TtVat, NaRev, NaRevUnsent, NaVat, TtCogs, NaCogs, NaCogsUnsent, TtWo, NaWo, NaWoUnsent;
        }

        private sealed class Model
        {
            public DateTime From, To;
            public decimal Tol;
            public TaxReportKit.SalesResult Sales;
            public readonly SortedDictionary<DateTime, DayRec> Days = new SortedDictionary<DateTime, DayRec>();
            public readonly List<TaxReportKit.QueueItem> Problems = new List<TaxReportKit.QueueItem>();
            public readonly List<string> Notes = new List<string>();
        }

        private static readonly HashSet<string> KnownStock = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "STOCK_IN", "STOCK_OUT_COGS", "STOCK_OUT_COGS_REVERSE", "STOCK_ADJUSTMENT", "STOCK_WRITEOFF", "STOCK_QTY_PUSH" };

        private string Conn => TaxReportKit.Conn;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!TaxReportKit.EnsureAccess(this)) return;
            litCss.Text = "<style>" + TaxReportKit.Css + "</style>";
            if (!IsPostBack)
            {
                var today = DateTime.Today;
                txtFrom.Text = TaxReportKit.Iso(new DateTime(today.Year, today.Month, 1));
                txtTo.Text = TaxReportKit.Iso(today);
                Render();
            }
        }

        protected void btnShow_Click(object sender, EventArgs e) { Render(); }

        private static bool Mismatch(decimal a, decimal b, decimal tol) => Math.Abs(a - b) > tol;

        private bool DayMismatch(DayRec d, decimal tol) =>
            Mismatch(d.TtRev, d.NaRev, tol) || Mismatch(d.TtVat, d.NaVat, tol) || Mismatch(d.TtCogs, d.NaCogs, tol) || Mismatch(d.TtWo, d.NaWo, tol);

        private Model Build()
        {
            var m = new Model();
            DateTime from = TaxReportKit.ParseDate(txtFrom.Text) ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime to = TaxReportKit.ParseDate(txtTo.Text) ?? DateTime.Today;
            if (to < from) { var t = from; from = to; to = t; }
            if ((to - from).TotalDays > 400) from = to.AddDays(-400);
            m.From = from; m.To = to;
            decimal tol;
            if (!decimal.TryParse((txtTol.Text ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out tol) || tol < 0) tol = 1m;
            m.Tol = tol;
            string conn = Conn;
            DateTime toEx = to.AddDays(1);

            for (var d = from; d <= to; d = d.AddDays(1)) m.Days[d] = new DayRec { Day = d };
            Func<DateTime, DayRec> Day = d =>
            {
                DayRec r;
                return m.Days.TryGetValue(d.Date, out r) ? r : null;
            };

            // ── รายได้/VAT ฝั่ง TakeTime ──────────────────────────────────────────
            m.Sales = TaxReportKit.CollectSales(conn, from, to);
            foreach (var g in m.Sales.Groups)
            {
                if (g.InfoOnly) continue;
                var r = Day(g.Day);
                if (r == null) continue;
                // ยอดที่นโยบายตั้ง "ไม่ส่งบัญชี" ตั้งใจไม่ขึ้น NextAcc → ไม่เอามาเทียบ (ไม่งั้นทุกวันขึ้นว่าไม่ตรง)
                r.TtRev += g.Gross - g.PolicyOffGross; r.TtVat += g.Vat - g.PolicyOffVat;
            }

            // ── รายได้/VAT ฝั่งคิว NextAcc ─────────────────────────────────────────
            foreach (var kv in m.Sales.Receipts)
            {
                var st = kv.Value;
                if (st.Create == null || st.Voided) continue;
                var r = Day(st.Create.Day);
                if (r == null) continue;
                decimal total = st.Create.Num("totalAmount");
                decimal vat = st.Create.Num("vatAmount");
                if (vat <= 0) vat = TaxReportKit.VatFromGross(total, m.Sales.UseVat);
                if (st.Create.Status == "COMPLETED") { r.NaRev += total; r.NaVat += vat; }
                else r.NaRevUnsent += total;
            }

            // ── ต้นทุน/ตัดจำหน่าย ฝั่ง TakeTime ─────────────────────────────────────
            try { LoadTtStock(m, conn, from, toEx, Day); }
            catch (Exception ex) { m.Notes.Add("อ่านความเคลื่อนไหวสต๊อกไม่ได้: " + ex.Message); }

            // ── ต้นทุน/ตัดจำหน่าย ฝั่งคิว NextAcc ───────────────────────────────────
            var stockQ = new List<TaxReportKit.QueueItem>();
            try { stockQ = TaxReportKit.LoadQueue(conn, null, new[] { "STOCK_" }, from.AddDays(-3)); }
            catch (Exception ex) { m.Notes.Add("อ่านคิวสต๊อกไม่ได้: " + ex.Message); }
            foreach (var q in stockQ)
            {
                if (q.Action == "STOCK_IN" || q.Action == "STOCK_QTY_PUSH") continue;
                if (q.Status.StartsWith("SKIPPED", StringComparison.OrdinalIgnoreCase)) continue;
                var r = Day(q.Day);
                if (r == null) continue;
                bool sent = q.Status == "COMPLETED";
                decimal qty = q.Num("quantity"), cost = q.Num("costPerUnit"), total = q.Num("totalCost");
                if (q.Action == "STOCK_OUT_COGS" || q.Action == "STOCK_OUT_COGS_REVERSE")
                {
                    decimal v = qty * cost * (q.Action == "STOCK_OUT_COGS_REVERSE" ? -1 : 1);
                    if (sent) r.NaCogs += v; else r.NaCogsUnsent += v;
                }
                else if (q.Action == "STOCK_WRITEOFF" || q.Action == "STOCK_ADJUSTMENT" || !KnownStock.Contains(q.Action))
                {
                    decimal v;
                    if (q.Action == "STOCK_ADJUSTMENT")
                    {
                        decimal diff = q.Num("quantityDiff");
                        if (diff >= 0) continue;          // ปรับเพิ่ม ไม่ใช่การตัดจำหน่าย
                        v = -diff * cost;
                    }
                    else v = total > 0 ? total : Math.Abs(qty) * cost;
                    if (q.Action.IndexOf("REVERSE", StringComparison.OrdinalIgnoreCase) >= 0) v = -v;
                    if (sent) r.NaWo += v; else r.NaWoUnsent += v;
                }
            }

            // ── คิวที่มีปัญหา ─────────────────────────────────────────────────────
            DateTime stale = DateTime.Now.AddMinutes(-30);
            var all = new List<TaxReportKit.QueueItem>(m.Sales.Queue);
            all.AddRange(stockQ);
            foreach (var q in all)
            {
                if (q.Action == "STOCK_QTY_PUSH") continue;
                if (q.Day < from || q.Day > to) continue;
                bool bad = q.Status == "FAILED" || ((q.Status == "PENDING" || q.Status == "PROCESSING") && q.Created < stale);
                if (bad) m.Problems.Add(q);
            }
            m.Problems.Sort((a, b) => b.Id.CompareTo(a.Id));
            return m;
        }

        private static void LoadTtStock(Model m, string conn, DateTime from, DateTime toEx, Func<DateTime, DayRec> Day)
        {
            bool hasOutType = TaxReportKit.ColumnExists(conn, "Product_Out", "OutType");
            bool hasReason = TaxReportKit.ColumnExists(conn, "Product_Out", "Out_Reason_Code");
            bool hasApproval = TaxReportKit.ColumnExists(conn, "Product_Out", "Approval_Status");
            bool hasReversed = TaxReportKit.ColumnExists(conn, "Product_Out", "Is_Reversed");
            bool hasRollupRef = TaxReportKit.ColumnExists(conn, "Product_Out", "Pos_Rollup_Ref");
            bool rsTables = TaxReportKit.TableExists(conn, "Guest_Room_Service_Orders") && TaxReportKit.TableExists(conn, "Guest_Room_Service_Items");
            bool rsHasRef = rsTables && TaxReportKit.ColumnExists(conn, "Guest_Room_Service_Orders", "Acct_Post_Ref");
            string cat = "CASE ";
            if (hasReason) cat += "WHEN ISNULL(o.Out_Reason_Code, '') NOT IN ('', 'SALE') THEN 'OTHER' ";
            if (hasOutType) cat += "WHEN o.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS','RETURN_TO_VENDOR') THEN 'OTHER' ";
            cat += "WHEN o.Remark = N'NEXTACC_SYNC' THEN 'NEXTACC' ELSE 'SALE' END";
            string excl = (hasReversed ? " AND ISNULL(o.Is_Reversed, 0) = 0" : "") +
                          (hasApproval ? " AND ISNULL(o.Approval_Status, '') NOT IN ('REJECTED', 'PENDING')" : "") +
                          // แถวขายที่ยกเลิกแล้ว (คืนสต๊อก+กลับต้นทุน) — กันกรณียังไม่มีคอลัมน์ Is_Reversed
                          (hasRollupRef ? " AND ISNULL(o.Pos_Rollup_Ref, '') NOT LIKE 'VOIDREV-%'" : "") +
                          // รูมเซอร์วิสนับจากรายการออเดอร์ด้านล่างที่เดียว (แถว Product_Out 'รูมเซอร์วิส' = ออเดอร์เดียวกัน — เดิมนับสองรอบ)
                          (rsHasRef ? " AND ISNULL(o.Remark, '') <> N'รูมเซอร์วิส'" : "");
            var dt = TaxReportKit.Query(conn,
                @"SELECT x.D, x.Cat, SUM(x.Q * x.C) AS VC, SUM(x.Q * CASE WHEN x.C > 0 THEN x.C ELSE x.P END) AS VW
                    FROM (SELECT CAST(o.DateTime_Out AS DATE) AS D, " + cat + @" AS Cat,
                                 CAST(o.Amount AS DECIMAL(18,4)) AS Q, CAST(ISNULL(p.Cost_Price, 0) AS DECIMAL(18,4)) AS C,
                                 CAST(ISNULL(o.PricePerUnit, 0) AS DECIMAL(18,4)) AS P
                            FROM Product_Out o LEFT JOIN Product p ON p.ID = o.Product_ID
                           WHERE o.DateTime_Out >= @f AND o.DateTime_Out < @t" + excl + @") x
                   GROUP BY x.D, x.Cat",
                new Dictionary<string, object> { { "@f", from }, { "@t", toEx } }, 90);
            foreach (DataRow r in dt.Rows)
            {
                var d = Day(TaxReportKit.NormalizeEra(Convert.ToDateTime(r["D"])).Date);
                if (d == null) continue;
                string c = TaxReportKit.Str(r["Cat"]);
                if (c == "SALE") d.TtCogs += TaxReportKit.Dec(r["VC"]);
                else if (c == "OTHER") d.TtWo += TaxReportKit.Dec(r["VW"]);
            }

            // รูมเซอร์วิส: RevenuePostingService เขียนแถว Product_Out (Remark 'รูมเซอร์วิส') ตอนออกเอกสาร/ลงต้นทุน — ออเดอร์
            // ยังไม่ลงบัญชีไม่มีแถวนั้น ⇒ นับจากรายการออเดอร์ "ที่เดียว" (แถว Product_Out รูมเซอร์วิสถูกตัดออกจาก query ด้านบน)
            // ไม่มีคอลัมน์ Acct_Post_Ref (ระบบเก่า: ไม่มีแถว Product_Out รูมเซอร์วิส) → นับจากออเดอร์อย่างเดียวเหมือนเดิม
            if (rsTables)
            {
                var dr = TaxReportKit.Query(conn,
                    @"SELECT CAST(o.Order_Date AS DATE) AS D, SUM(CAST(i.Quantity AS DECIMAL(18,4)) * ISNULL(p.Cost_Price, 0)) AS V
                        FROM Guest_Room_Service_Orders o
                        JOIN Guest_Room_Service_Items i ON i.Order_ID = o.ID
                        LEFT JOIN Product p ON p.ID = i.Product_ID
                       WHERE o.Order_Date >= @f AND o.Order_Date < @t
                         AND ISNULL(o.Order_Status, '') NOT IN ('CANCELLED', 'PENDING')
                       GROUP BY CAST(o.Order_Date AS DATE)",
                    new Dictionary<string, object> { { "@f", from }, { "@t", toEx } }, 60);
                foreach (DataRow r in dr.Rows)
                {
                    var d = Day(TaxReportKit.NormalizeEra(Convert.ToDateTime(r["D"])).Date);
                    if (d != null) d.TtCogs += TaxReportKit.Dec(r["V"]);
                }
            }
        }

        private string Cell(decimal v) => "<td class='n'>" + TaxReportKit.Money(v) + "</td>";

        private string DiffCell(decimal a, decimal b, decimal tol)
        {
            decimal d = a - b;
            return Mismatch(a, b, tol)
                ? "<td class='n tr-diff'>" + TaxReportKit.Money(d) + "</td>"
                : "<td class='n' style='color:#90a4ae'>" + (d == 0 ? "—" : TaxReportKit.Money(d)) + "</td>";
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            Model m;
            try { m = Build(); }
            catch (Exception ex) { litMsg.Text = "<div class='tr-note'>กระทบยอดไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>"; return; }
            var rows = new List<string[]>
            {
                new[] { "กระทบยอด TakeTime กับ NextAcc", TaxReportKit.Iso(m.From), TaxReportKit.Iso(m.To) },
                new[] { "วันที่", "รายได้ TakeTime", "รายได้ NextAcc (ส่งแล้ว)", "รายได้รอส่ง/ล้ม", "ต่าง",
                        "ภาษีขาย TakeTime", "ภาษีขาย NextAcc", "ต่าง",
                        "ต้นทุนขาย TakeTime", "ต้นทุนขาย NextAcc", "รอส่ง/ล้ม", "ต่าง",
                        "ตัดจำหน่าย TakeTime", "ตัดจำหน่าย NextAcc", "รอส่ง/ล้ม", "ต่าง", "ไม่ตรง" }
            };
            foreach (var d in m.Days.Values)
            {
                bool mm = DayMismatch(d, m.Tol);
                if (chkMismatchOnly.Checked && !mm) continue;
                rows.Add(new[]
                {
                    TaxReportKit.Iso(d.Day),
                    TaxReportKit.Money(d.TtRev), TaxReportKit.Money(d.NaRev), TaxReportKit.Money(d.NaRevUnsent), TaxReportKit.Money(d.TtRev - d.NaRev),
                    TaxReportKit.Money(d.TtVat), TaxReportKit.Money(d.NaVat), TaxReportKit.Money(d.TtVat - d.NaVat),
                    TaxReportKit.Money(d.TtCogs), TaxReportKit.Money(d.NaCogs), TaxReportKit.Money(d.NaCogsUnsent), TaxReportKit.Money(d.TtCogs - d.NaCogs),
                    TaxReportKit.Money(d.TtWo), TaxReportKit.Money(d.NaWo), TaxReportKit.Money(d.NaWoUnsent), TaxReportKit.Money(d.TtWo - d.NaWo),
                    mm ? "ไม่ตรง" : ""
                });
            }
            rows.Add(new string[0]);
            rows.Add(new[] { "คิวที่ล้มเหลว/ค้าง" });
            rows.Add(new[] { "คิว#", "ชนิด", "อ้างอิง", "วันที่เอกสาร", "ยอด", "สถานะ", "ลองซ้ำ", "ข้อผิดพลาด" });
            foreach (var q in m.Problems)
                rows.Add(new[] { q.Id.ToString(TaxReportKit.Inv), q.Action, q.Key, TaxReportKit.Iso(q.Day), TaxReportKit.Money(QueueAmount(q)),
                                 q.Status, q.Retry.ToString(TaxReportKit.Inv), Short(q.Error, 400) });
            rows.Add(new string[0]);
            rows.Add(new[] { "การขายที่ยังไม่มีเอกสาร" });
            rows.Add(new[] { "วันที่", "ช่องทาง", "แหล่งเงิน", "ยอด", "ธง", "รายการ" });
            foreach (var g in m.Sales.Groups)
            {
                if (!MissingGroup(g)) continue;
                var f = new List<string>(); foreach (var x in g.Flags) f.Add(TaxReportKit.FlagText(x));
                rows.Add(new[] { TaxReportKit.Iso(g.Day), TaxReportKit.SalesChannelLabel(g.Channel), g.PaidHowName, TaxReportKit.Money(g.Gross),
                                 string.Join(", ", f), string.Join("; ", g.ProblemIds) });
            }
            TaxReportKit.SendCsv(Response, "nextacc_recon_" + m.From.ToString("yyyyMMdd", TaxReportKit.Inv) + "_" +
                m.To.ToString("yyyyMMdd", TaxReportKit.Inv) + ".csv", rows);
        }

        private static bool MissingGroup(TaxReportKit.SaleGroup g) =>
            !g.InfoOnly && (g.Flags.Contains(TaxReportKit.FNoDoc) || g.Flags.Contains(TaxReportKit.FLate)
                            || g.Flags.Contains(TaxReportKit.FFailed) || g.Flags.Contains(TaxReportKit.FExcluded));

        private static decimal QueueAmount(TaxReportKit.QueueItem q)
        {
            decimal t = q.Num("totalAmount");
            if (t != 0) return t;
            t = q.Num("totalCost");
            if (t != 0) return t;
            decimal qty = q.Num("quantity");
            if (qty == 0) qty = Math.Abs(q.Num("quantityDiff"));
            return qty * q.Num("costPerUnit");
        }

        private static string Short(string s, int n)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ");
            return s.Length > n ? s.Substring(0, n) + "…" : s;
        }

        private void Render()
        {
            Model m;
            try { m = Build(); }
            catch (Exception ex)
            {
                litMsg.Text = "<div class='tr-note'>กระทบยอดไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>";
                litDays.Text = litQueue.Text = litMissing.Text = "";
                return;
            }
            litMsg.Text = "";
            litStatus.Text = TaxReportKit.ConfigStatusHtml(m.Sales);

            // ── ตารางรายวัน ──
            decimal sTtRev = 0, sNaRev = 0, sUns = 0, sTtVat = 0, sNaVat = 0, sTtC = 0, sNaC = 0, sNaCU = 0, sTtW = 0, sNaW = 0, sNaWU = 0;
            int mmDays = 0;
            var sb = new StringBuilder("<table class='tr-t'><tr><th rowspan='2'>วันที่</th>" +
                "<th colspan='4' style='text-align:center'>รายได้ (รวม VAT)</th><th colspan='3' style='text-align:center'>ภาษีขาย</th>" +
                "<th colspan='4' style='text-align:center'>ต้นทุนขาย</th><th colspan='4' style='text-align:center'>ตัดจำหน่ายสต๊อก</th><th rowspan='2'></th></tr><tr>" +
                "<th class='n'>TakeTime</th><th class='n'>NextAcc ส่งแล้ว</th><th class='n'>รอ/ล้ม</th><th class='n'>ต่าง</th>" +
                "<th class='n'>TakeTime</th><th class='n'>NextAcc</th><th class='n'>ต่าง</th>" +
                "<th class='n'>TakeTime</th><th class='n'>NextAcc ส่งแล้ว</th><th class='n'>รอ/ล้ม</th><th class='n'>ต่าง</th>" +
                "<th class='n'>TakeTime</th><th class='n'>NextAcc ส่งแล้ว</th><th class='n'>รอ/ล้ม</th><th class='n'>ต่าง</th></tr>");
            string sumUrl = ResolveUrl("~/Report/DailySalesTaxSummary");
            foreach (var d in m.Days.Values)
            {
                bool mm = DayMismatch(d, m.Tol);
                if (mm) mmDays++;
                sTtRev += d.TtRev; sNaRev += d.NaRev; sUns += d.NaRevUnsent; sTtVat += d.TtVat; sNaVat += d.NaVat;
                sTtC += d.TtCogs; sNaC += d.NaCogs; sNaCU += d.NaCogsUnsent; sTtW += d.TtWo; sNaW += d.NaWo; sNaWU += d.NaWoUnsent;
                if (chkMismatchOnly.Checked && !mm) continue;
                bool empty = d.TtRev == 0 && d.NaRev == 0 && d.NaRevUnsent == 0 && d.TtCogs == 0 && d.NaCogs == 0 && d.TtWo == 0 && d.NaWo == 0;
                if (empty && !chkMismatchOnly.Checked) continue;
                sb.Append(mm ? "<tr class='bad'>" : "<tr>")
                  .Append("<td>").Append(TaxReportKit.D(d.Day)).Append("</td>")
                  .Append(Cell(d.TtRev)).Append(Cell(d.NaRev)).Append(Cell(d.NaRevUnsent)).Append(DiffCell(d.TtRev, d.NaRev, m.Tol))
                  .Append(Cell(d.TtVat)).Append(Cell(d.NaVat)).Append(DiffCell(d.TtVat, d.NaVat, m.Tol))
                  .Append(Cell(d.TtCogs)).Append(Cell(d.NaCogs)).Append(Cell(d.NaCogsUnsent)).Append(DiffCell(d.TtCogs, d.NaCogs, m.Tol))
                  .Append(Cell(d.TtWo)).Append(Cell(d.NaWo)).Append(Cell(d.NaWoUnsent)).Append(DiffCell(d.TtWo, d.NaWo, m.Tol))
                  .Append("<td>");
                if (mm)
                    sb.Append("<a href='").Append(sumUrl).Append("?from=").Append(TaxReportKit.Iso(d.Day)).Append("&to=")
                      .Append(TaxReportKit.Iso(d.Day)).Append("&problems=1'>ดูรายการ</a>");
                sb.Append("</td></tr>");
            }
            sb.Append("<tr class='tot'><td>รวม</td>")
              .Append(Cell(sTtRev)).Append(Cell(sNaRev)).Append(Cell(sUns)).Append(DiffCell(sTtRev, sNaRev, m.Tol))
              .Append(Cell(sTtVat)).Append(Cell(sNaVat)).Append(DiffCell(sTtVat, sNaVat, m.Tol))
              .Append(Cell(sTtC)).Append(Cell(sNaC)).Append(Cell(sNaCU)).Append(DiffCell(sTtC, sNaC, m.Tol))
              .Append(Cell(sTtW)).Append(Cell(sNaW)).Append(Cell(sNaWU)).Append(DiffCell(sTtW, sNaW, m.Tol))
              .Append("<td></td></tr></table>");
            litDays.Text = sb.ToString();

            int missingGroups = 0; decimal missingAmt = 0;
            foreach (var g in m.Sales.Groups) if (MissingGroup(g)) { missingGroups++; missingAmt += g.Gross; }
            litKpis.Text = "<div class='tr-kpis'>" +
                Kpi(mmDays.ToString(TaxReportKit.Inv), "วันที่ยอดไม่ตรง", mmDays > 0) +
                Kpi(m.Problems.Count.ToString(TaxReportKit.Inv), "คิวล้มเหลว/ค้าง", m.Problems.Count > 0) +
                Kpi(missingGroups + " กลุ่ม · " + TaxReportKit.Money(missingAmt), "ขายที่ยังไม่มีเอกสาร", missingGroups > 0) +
                Kpi(TaxReportKit.Money(sTtRev - sNaRev), "รายได้ต่าง (TakeTime − NextAcc)", Mismatch(sTtRev, sNaRev, m.Tol)) +
                "</div>";

            // ── คิวที่มีปัญหา ──
            string qUrl = ResolveUrl(TaxReportKit.QueuePageUrl);
            var qb = new StringBuilder("<table class='tr-t'><tr><th>คิว#</th><th>ชนิด</th><th>อ้างอิง</th><th>วันที่เอกสาร</th>" +
                "<th class='n'>ยอด</th><th>สถานะ</th><th class='n'>ลองซ้ำ</th><th>ข้อผิดพลาด</th><th></th></tr>");
            int shown = 0;
            foreach (var q in m.Problems)
            {
                if (shown++ >= 300) { qb.Append("<tr><td colspan='9' class='tr-sub'>… แสดง 300 รายการแรก (ดาวน์โหลด CSV เพื่อดูทั้งหมด)</td></tr>"); break; }
                qb.Append("<tr><td>#").Append(q.Id).Append("</td><td>").Append(TaxReportKit.H(q.Action)).Append("</td><td><code>")
                  .Append(TaxReportKit.H(q.Key)).Append("</code></td><td>").Append(TaxReportKit.D(q.Day)).Append("</td>")
                  .Append(Cell(QueueAmount(q))).Append("<td>").Append(TaxReportKit.Chip(TaxReportKit.StatusLabel(q.Status), TaxReportKit.StatusCss(q.Status)))
                  .Append("</td><td class='n'>").Append(q.Retry).Append("</td><td><span class='tr-sub'>")
                  .Append(TaxReportKit.H(Short(q.Error, 220))).Append("</span></td><td><a href='").Append(qUrl).Append("'>เปิดหน้าคิว</a></td></tr>");
            }
            if (m.Problems.Count == 0) qb.Append("<tr><td colspan='9' style='padding:14px;color:#1e7e42'>ไม่มีคิวที่ล้มเหลวหรือค้างในช่วงนี้</td></tr>");
            litQueue.Text = qb.Append("</table>").ToString();

            // ── ขายที่ยังไม่มีเอกสาร ──
            var mb = new StringBuilder("<table class='tr-t'><tr><th>วันที่</th><th>ช่องทาง</th><th>แหล่งเงิน</th><th class='n'>ยอด</th><th>ธง</th><th>รายการที่ต้องตรวจ</th></tr>");
            int mc = 0;
            foreach (var g in m.Sales.Groups)
            {
                if (!MissingGroup(g)) continue;
                mc++;
                mb.Append("<tr><td>").Append(TaxReportKit.D(g.Day)).Append("</td><td>").Append(TaxReportKit.H(TaxReportKit.SalesChannelLabel(g.Channel)))
                  .Append("</td><td>").Append(TaxReportKit.H(g.PaidHowName)).Append("</td>").Append(Cell(g.Gross)).Append("<td>");
                foreach (var f in g.Flags) mb.Append(TaxReportKit.FlagChip(f));
                mb.Append("</td><td class='tr-sub'>");
                int n = 0;
                foreach (var id in g.ProblemIds) { if (n++ >= 10) { mb.Append(" …"); break; } mb.Append(n > 1 ? ", " : "").Append(TaxReportKit.H(id)); }
                mb.Append("</td></tr>");
            }
            if (mc == 0) mb.Append("<tr><td colspan='6' style='padding:14px;color:#1e7e42'>การขายทุกกลุ่มมีเอกสาร NextAcc ครอบแล้ว</td></tr>");
            litMissing.Text = mb.Append("</table>").ToString();

            var nb = new StringBuilder("<div class='tr-infobox'><b>วิธีอ่าน / ข้อจำกัด</b><ul style='margin:6px 0 0 18px;padding:0'>");
            nb.Append("<li>ฝั่ง NextAcc คือยอดที่ TakeTime <b>ส่งสำเร็จ</b> ตามคิว (ไม่ได้ดึงยอด GL จริงจาก NextAcc) · ภาษีขายฝั่ง NextAcc ถอด 7% จากยอดเอกสารเมื่อ payload ไม่ระบุ VAT</li>");
            nb.Append("<li>ต้นทุนขายฝั่ง TakeTime = จำนวนขาย × ราคาทุนปัจจุบัน (รูมเซอร์วิสนับจากรายการออเดอร์ครั้งเดียว · ไม่นับขายที่ยกเลิกแล้ว) — " +
                      "สินค้าที่ไม่มีราคาทุนไม่มี COGS ฝั่ง NextAcc (ส่งเฉพาะจำนวน) และราคาทุนที่เปลี่ยนหลังวันขายทำให้ต่างเล็กน้อย ⇒ แสดงเป็นส่วนต่าง</li>");
            nb.Append("<li>รายได้ที่นโยบายเอกสารขายตั้ง <i>ไม่ส่งบัญชี</i> ไม่นำมาเทียบ (ดูได้ที่รายงานสรุปยอดขายรายวัน ธง \"ไม่ส่งตามนโยบาย\")</li>");
            nb.Append("<li>ตัดจำหน่าย = จ่ายออกที่ไม่ใช่การขาย (เหตุผลจ่ายออก/ตัดจำหน่ายเดิม/ปรับลดจากตรวจนับ/คืนผู้ขาย) · ไม่นับรายการรออนุมัติหรือถูกปฏิเสธ</li>");
            nb.Append("<li>วันที่ฝั่งคิวอ่านจากวันที่เอกสารใน payload (ไม่มีใช้วันที่เข้าคิว) · ยอดที่ยกเลิก (VOID สำเร็จ) ไม่นับ</li>");
            foreach (var n in m.Notes) nb.Append("<li class='tr-diff'>").Append(TaxReportKit.H(n)).Append("</li>");
            foreach (var n in m.Sales.Notes) nb.Append("<li class='tr-diff'>").Append(TaxReportKit.H(n)).Append("</li>");
            litNotes.Text = nb.Append("</ul></div>").ToString();
        }

        private static string Kpi(string value, string label, bool bad) =>
            "<div class='tr-kpi'><div class='v' style='color:" + (bad ? "#c62828" : "#1e7e42") + "'>" + TaxReportKit.H(value) +
            "</div><div class='l'>" + TaxReportKit.H(label) + "</div></div>";
    }
}
