using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra.TaxReports
{
    /// <summary>
    /// รายงานสินค้าและวัตถุดิบ (ม.87(3)) — ต่อสินค้า: ยกมา (จำนวน/มูลค่า), รับเข้า (ซื้อ/อื่น), ขาย,
    /// จ่ายออกอื่นแยกตามเหตุผล (Product_Out.Out_Reason_Code — PHASE19_30, guarded; ไม่มีใช้ OutType เดิม),
    /// คงเหลือ (จำนวน/มูลค่าตามราคาทุน) + บัญชีคุมรายวันเมื่อเลือกสินค้ารายตัว. อ่านอย่างเดียว
    /// </summary>
    public partial class StockMovementReport : Page
    {
        private sealed class Row
        {
            public int Pid;
            public string Code = "", Name = "";
            public decimal CostPrice, AvgIn, OpenQty, InPurch, InPurchVal, InOther, Sold, Pending;
            public readonly Dictionary<string, decimal> Other = new Dictionary<string, decimal>();
            public decimal OtherTotal { get { decimal t = 0; foreach (var v in Other.Values) t += v; return t; } }
            public decimal CloseQty => OpenQty + InPurch + InOther - Sold - OtherTotal;
            public decimal UnitCost => CostPrice > 0 ? CostPrice : AvgIn;
            public string CostBasis => CostPrice > 0 ? "ราคาทุนสินค้า" : (AvgIn > 0 ? "เฉลี่ยราคารับเข้า" : "ไม่มีต้นทุน");
            public bool Active;
        }

        private sealed class LedgerLine
        {
            public DateTime When;
            public bool IsIn;
            public decimal Qty, Price;
            public string Type = "", Ref = "", Note = "";
        }

        private sealed class Model
        {
            public DateTime From, To;
            public readonly List<Row> Rows = new List<Row>();
            public readonly List<string> Cats = new List<string>();
            public readonly Dictionary<string, string> CatNames = new Dictionary<string, string>();
            public readonly List<LedgerLine> Ledger = new List<LedgerLine>();
            public Row LedgerRow;
            public readonly List<string> Notes = new List<string>();
            public bool HasReasonCode, HasApproval;
        }

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
                LoadProducts();
                string pid = Request.QueryString["pid"];
                if (!string.IsNullOrEmpty(pid) && ddlProduct.Items.FindByValue(pid) != null) ddlProduct.SelectedValue = pid;
                Render();
            }
        }

        protected void btnShow_Click(object sender, EventArgs e) { Render(); }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            Model m;
            try { m = Build(); }
            catch (Exception ex) { litMsg.Text = "<div class='tr-note'>สร้างรายงานไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>"; return; }
            var rows = new List<string[]>();
            string bn, tax, addr;
            TaxReportKit.BusinessHeader(Conn, out bn, out tax, out addr);
            rows.Add(new[] { "รายงานสินค้าและวัตถุดิบ" });
            rows.Add(new[] { "ชื่อผู้ประกอบการ", bn, "เลขประจำตัวผู้เสียภาษี", tax });
            rows.Add(new[] { "ที่อยู่", addr });
            rows.Add(new[] { "ช่วงวันที่", TaxReportKit.Iso(m.From), TaxReportKit.Iso(m.To) });
            rows.Add(new string[0]);
            rows.Add(SummaryHeader(m).ToArray());
            foreach (var r in m.Rows) rows.Add(SummaryCells(m, r).ToArray());
            if (m.LedgerRow != null)
            {
                rows.Add(new string[0]);
                rows.Add(new[] { "บัญชีคุมสินค้ารายวัน", m.LedgerRow.Code, m.LedgerRow.Name });
                rows.Add(new[] { "วันที่", "เวลา", "เอกสารอ้างอิง", "รายการ", "รับ", "จ่าย", "คงเหลือ", "ราคาทุน/หน่วย", "มูลค่าคงเหลือ", "หมายเหตุ" });
                decimal bal = m.LedgerRow.OpenQty;
                rows.Add(new[] { TaxReportKit.Iso(m.From), "", "", "ยกมา", "", "", TaxReportKit.Qty(bal), "", "", "" });
                foreach (var l in m.Ledger)
                {
                    bal += l.IsIn ? l.Qty : -l.Qty;
                    rows.Add(new[]
                    {
                        TaxReportKit.Iso(l.When), l.When.ToString("HH:mm", TaxReportKit.Inv), l.Ref, l.Type,
                        l.IsIn ? TaxReportKit.Qty(l.Qty) : "", l.IsIn ? "" : TaxReportKit.Qty(l.Qty), TaxReportKit.Qty(bal),
                        TaxReportKit.Money(m.LedgerRow.UnitCost), TaxReportKit.Money(bal * m.LedgerRow.UnitCost), l.Note
                    });
                }
            }
            TaxReportKit.SendCsv(Response, "stock_movement_" + m.From.ToString("yyyyMMdd", TaxReportKit.Inv) + "_" +
                m.To.ToString("yyyyMMdd", TaxReportKit.Inv) + ".csv", rows);
        }

        private void LoadProducts()
        {
            ddlProduct.Items.Clear();
            ddlProduct.Items.Add(new ListItem("— ทุกสินค้า —", ""));
            try
            {
                var dt = TaxReportKit.Query(Conn, "SELECT ID, Product_Name FROM Product ORDER BY Product_Name", null, 30);
                foreach (DataRow r in dt.Rows)
                    ddlProduct.Items.Add(new ListItem(TaxReportKit.Str(r["Product_Name"]), TaxReportKit.Str(r["ID"])));
            }
            catch { }
        }

        private Model Build()
        {
            var m = new Model();
            DateTime from = TaxReportKit.ParseDate(txtFrom.Text) ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            DateTime to = TaxReportKit.ParseDate(txtTo.Text) ?? DateTime.Today;
            if (to < from) { var t = from; from = to; to = t; }
            m.From = from; m.To = to;
            DateTime toEx = to.AddDays(1);
            string conn = Conn;

            bool hasOutType = TaxReportKit.ColumnExists(conn, "Product_Out", "OutType");
            bool hasInType = TaxReportKit.ColumnExists(conn, "Product_In", "InType");
            m.HasReasonCode = TaxReportKit.ColumnExists(conn, "Product_Out", "Out_Reason_Code");
            m.HasApproval = TaxReportKit.ColumnExists(conn, "Product_Out", "Approval_Status");
            bool hasReversed = TaxReportKit.ColumnExists(conn, "Product_Out", "Is_Reversed");

            string cat = "CASE ";
            if (m.HasReasonCode) cat += "WHEN ISNULL(o.Out_Reason_Code, '') <> '' THEN 'R:' + o.Out_Reason_Code ";
            if (hasOutType) cat += "WHEN o.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS','RETURN_TO_VENDOR') THEN 'T:' + o.OutType ";
            cat += "WHEN o.Remark = N'NEXTACC_SYNC' THEN 'T:NEXTACC_SYNC' ELSE 'SALE' END";
            string pend = m.HasApproval ? "CASE WHEN o.Approval_Status = 'PENDING' THEN 1 ELSE 0 END" : "0";
            // แถวที่ถูกกลับรายการ/ไม่อนุมัติ ไม่นับเป็นการจ่ายออก
            string excl = (hasReversed ? " AND ISNULL(o.Is_Reversed, 0) = 0" : "") +
                          (m.HasApproval ? " AND ISNULL(o.Approval_Status, '') <> 'REJECTED'" : "");
            string purch = hasInType ? "CASE WHEN ISNULL(i.InType, 'PURCHASE') IN ('PURCHASE', '') THEN 1 ELSE 0 END" : "1";

            int selPid = 0;
            int.TryParse(ddlProduct.SelectedValue, out selPid);
            string pidFilterO = selPid > 0 ? " AND o.Product_ID = @pid" : "";
            string pidFilterI = selPid > 0 ? " AND i.Product_ID = @pid" : "";
            var ps = new Dictionary<string, object> { { "@f", from }, { "@t", toEx } };
            if (selPid > 0) ps["@pid"] = selPid;

            // ── สินค้า ─────────────────────────────────────────────────────────
            var rows = new Dictionary<int, Row>();
            var dtp = TaxReportKit.Query(conn,
                "SELECT ID, ISNULL(CAST(Barcode AS NVARCHAR(100)), '') AS Barcode, Product_Name, ISNULL(Cost_Price, 0) AS Cost_Price FROM Product" +
                (selPid > 0 ? " WHERE ID = @pid" : ""), ps, 30);
            foreach (DataRow r in dtp.Rows)
            {
                int id;
                if (!int.TryParse(TaxReportKit.Str(r["ID"]), out id)) continue;
                rows[id] = new Row
                {
                    Pid = id, Code = TaxReportKit.Str(r["Barcode"]), Name = TaxReportKit.Str(r["Product_Name"]),
                    CostPrice = TaxReportKit.Dec(r["Cost_Price"])
                };
            }
            Func<int, Row> RowOf = id =>
            {
                Row rw;
                if (!rows.TryGetValue(id, out rw)) { rw = new Row { Pid = id, Name = "(สินค้า #" + id + " — ไม่พบในทะเบียน)" }; rows[id] = rw; }
                return rw;
            };

            // ── รับเข้า ────────────────────────────────────────────────────────
            var dti = TaxReportKit.Query(conn,
                @"SELECT x.PID,
                         SUM(CASE WHEN x.DT < @f THEN x.Q ELSE 0 END) AS QB,
                         SUM(CASE WHEN x.DT >= @f AND x.Purch = 1 THEN x.Q ELSE 0 END) AS QPur,
                         SUM(CASE WHEN x.DT >= @f AND x.Purch = 1 THEN x.Q * x.P ELSE 0 END) AS VPur,
                         SUM(CASE WHEN x.DT >= @f AND x.Purch = 0 THEN x.Q ELSE 0 END) AS QOth,
                         SUM(CASE WHEN x.Purch = 1 AND x.P > 0 THEN x.Q * x.P ELSE 0 END) AS AvgV,
                         SUM(CASE WHEN x.Purch = 1 AND x.P > 0 THEN x.Q ELSE 0 END) AS AvgQ
                    FROM (SELECT i.Product_ID AS PID, CAST(i.Amount AS DECIMAL(18,4)) AS Q,
                                 CAST(ISNULL(i.PricePerUnit, 0) AS DECIMAL(18,4)) AS P, i.DateTime_In AS DT, " + purch + @" AS Purch
                            FROM Product_In i WHERE i.DateTime_In < @t" + pidFilterI + @") x
                   GROUP BY x.PID", ps, 90);
            foreach (DataRow r in dti.Rows)
            {
                int id;
                if (!int.TryParse(TaxReportKit.Str(r["PID"]), out id)) continue;
                var rw = RowOf(id);
                rw.OpenQty += TaxReportKit.Dec(r["QB"]);
                rw.InPurch = TaxReportKit.Dec(r["QPur"]);
                rw.InPurchVal = TaxReportKit.Dec(r["VPur"]);
                rw.InOther = TaxReportKit.Dec(r["QOth"]);
                decimal aq = TaxReportKit.Dec(r["AvgQ"]);
                rw.AvgIn = aq > 0 ? Math.Round(TaxReportKit.Dec(r["AvgV"]) / aq, 4) : 0m;
            }

            // ── จ่ายออก ────────────────────────────────────────────────────────
            var dto = TaxReportKit.Query(conn,
                @"SELECT x.PID, x.Cat,
                         SUM(CASE WHEN x.DT < @f THEN x.Q ELSE 0 END) AS QB,
                         SUM(CASE WHEN x.DT >= @f THEN x.Q ELSE 0 END) AS QP,
                         SUM(CASE WHEN x.DT >= @f AND x.Pend = 1 THEN x.Q ELSE 0 END) AS QPend
                    FROM (SELECT o.Product_ID AS PID, " + cat + @" AS Cat, CAST(o.Amount AS DECIMAL(18,4)) AS Q,
                                 o.DateTime_Out AS DT, " + pend + @" AS Pend
                            FROM Product_Out o WHERE o.DateTime_Out < @t" + excl + pidFilterO + @") x
                   GROUP BY x.PID, x.Cat", ps, 90);
            foreach (DataRow r in dto.Rows)
            {
                int id;
                if (!int.TryParse(TaxReportKit.Str(r["PID"]), out id)) continue;
                var rw = RowOf(id);
                string c = TaxReportKit.Str(r["Cat"]);
                if (c.Equals("R:SALE", StringComparison.OrdinalIgnoreCase)) c = "SALE";
                rw.OpenQty -= TaxReportKit.Dec(r["QB"]);
                decimal qp = TaxReportKit.Dec(r["QP"]);
                rw.Pending += TaxReportKit.Dec(r["QPend"]);
                if (qp == 0) continue;
                if (c == "SALE") rw.Sold += qp;
                else
                {
                    decimal cur;
                    rw.Other.TryGetValue(c, out cur);
                    rw.Other[c] = cur + qp;
                    if (!m.Cats.Contains(c)) m.Cats.Add(c);
                }
            }

            // ── ชื่อเหตุผล ──────────────────────────────────────────────────────
            var reasonNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (TaxReportKit.TableExists(conn, "Stock_Out_Reason"))
            {
                try
                {
                    var dr = TaxReportKit.Query(conn, "SELECT Code, Name_TH FROM Stock_Out_Reason", null, 15);
                    foreach (DataRow r in dr.Rows) reasonNames[TaxReportKit.Str(r[0]).Trim()] = TaxReportKit.Str(r[1]).Trim();
                }
                catch { }
            }
            foreach (string c in m.Cats)
            {
                string label;
                if (c.StartsWith("R:"))
                {
                    string code = c.Substring(2), nameTh;
                    label = reasonNames.TryGetValue(code, out nameTh) && !string.IsNullOrEmpty(nameTh) ? nameTh + " (" + code + ")" : code;
                }
                else switch (c)
                {
                    case "T:WRITEOFF": label = "เบิกใช้/ตัดจำหน่าย (ระบบเดิม)"; break;
                    case "T:ADJUSTMENT_LOSS": label = "ปรับลดจากตรวจนับ"; break;
                    case "T:RETURN_TO_VENDOR": label = "คืนผู้ขาย"; break;
                    case "T:NEXTACC_SYNC": label = "ปรับจาก NextAcc"; break;
                    default: label = c; break;
                }
                m.CatNames[c] = label;
            }
            m.Cats.Sort((a, b) => string.Compare(m.CatNames[a], m.CatNames[b], StringComparison.CurrentCulture));

            // ── เลือกแถวที่แสดง ────────────────────────────────────────────────
            foreach (var rw in rows.Values)
            {
                rw.Active = rw.OpenQty != 0 || rw.InPurch != 0 || rw.InOther != 0 || rw.Sold != 0 || rw.OtherTotal != 0;
                if (rw.Active || chkAll.Checked || selPid > 0) m.Rows.Add(rw);
            }
            m.Rows.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));

            // ── บัญชีคุมรายวัน (สินค้ารายตัว) ──────────────────────────────────────
            if (selPid > 0 && rows.ContainsKey(selPid))
            {
                m.LedgerRow = rows[selPid];
                BuildLedger(m, conn, ps, cat, excl, hasInType);
            }

            if (!m.HasReasonCode)
                m.Notes.Add("ยังไม่ได้รัน migration PHASE19_30 (เหตุผลการจ่ายออก) — จ่ายออกอื่นจัดกลุ่มตามชนิดเดิม (OutType)");
            int noCost = 0, neg = 0;
            foreach (var r in m.Rows) { if (r.UnitCost <= 0 && r.CloseQty != 0) noCost++; if (r.CloseQty < 0) neg++; }
            if (noCost > 0) m.Notes.Add(noCost + " สินค้าไม่มีราคาทุน (Cost_Price = 0 และไม่มีราคารับเข้า) — มูลค่าคงเหลือแสดงเป็น 0");
            if (neg > 0) m.Notes.Add(neg + " สินค้าคงเหลือติดลบ — มีการจ่ายออกโดยไม่มีการรับเข้าในระบบ (ตรวจการรับของ/ตรวจนับ)");
            return m;
        }

        private void BuildLedger(Model m, string conn, Dictionary<string, object> ps, string cat, string excl, bool hasInType)
        {
            bool inHasSupDoc = TaxReportKit.ColumnExists(conn, "Product_In", "Supplier_Doc_No");
            bool inHasBatch = TaxReportKit.ColumnExists(conn, "Product_In", "Receipt_Batch");
            bool inHasSup = TaxReportKit.ColumnExists(conn, "Product_In", "Supplier_Name");
            bool outHasRef = TaxReportKit.ColumnExists(conn, "Product_Out", "Pos_Rollup_Ref");
            bool outHasReason = TaxReportKit.ColumnExists(conn, "Product_Out", "Reason");
            bool outHasJr = TaxReportKit.ColumnExists(conn, "Product_Out", "Nexaacc_Journal_Ref");

            string inRef = "CAST('' AS NVARCHAR(200))";
            if (inHasSupDoc && inHasBatch) inRef = "CAST(COALESCE(NULLIF(i.Supplier_Doc_No, ''), NULLIF(i.Receipt_Batch, ''), '') AS NVARCHAR(200))";
            else if (inHasSupDoc) inRef = "CAST(ISNULL(i.Supplier_Doc_No, '') AS NVARCHAR(200))";
            else if (inHasBatch) inRef = "CAST(ISNULL(i.Receipt_Batch, '') AS NVARCHAR(200))";
            string inType = hasInType ? "CAST(ISNULL(i.InType, 'PURCHASE') AS NVARCHAR(200))" : "CAST('PURCHASE' AS NVARCHAR(200))";
            string inNote = inHasSup ? "CAST(ISNULL(i.Supplier_Name, '') AS NVARCHAR(500))" : "CAST('' AS NVARCHAR(500))";

            string outRef = "CASE WHEN ISNULL(CAST(o.Account_Receipt_ID AS NVARCHAR(100)), '0') NOT IN ('0', '') " +
                            "THEN CAST(o.Account_Receipt_ID AS NVARCHAR(200)) ELSE " +
                            (outHasRef ? "CAST(ISNULL(o.Pos_Rollup_Ref, '') AS NVARCHAR(200))" : "CAST('' AS NVARCHAR(200))") + " END";
            if (outHasJr) outRef = "CASE WHEN ISNULL(o.Nexaacc_Journal_Ref, '') <> '' THEN CAST(o.Nexaacc_Journal_Ref AS NVARCHAR(200)) ELSE " + outRef + " END";
            string outNote = outHasReason
                ? "CAST(ISNULL(o.Reason, ISNULL(o.Remark, '')) AS NVARCHAR(500))"
                : "CAST(ISNULL(o.Remark, '') AS NVARCHAR(500))";

            var dt = TaxReportKit.Query(conn,
                "SELECT 'I' AS K, i.DateTime_In AS DT, CAST(i.Amount AS DECIMAL(18,4)) AS Q, CAST(ISNULL(i.PricePerUnit, 0) AS DECIMAL(18,4)) AS P, " +
                inType + " AS T, " + inRef + " AS REF, " + inNote + " AS NOTE " +
                "FROM Product_In i WHERE i.Product_ID = @pid AND i.DateTime_In >= @f AND i.DateTime_In < @t " +
                "UNION ALL " +
                "SELECT 'O', o.DateTime_Out, CAST(o.Amount AS DECIMAL(18,4)), CAST(ISNULL(o.PricePerUnit, 0) AS DECIMAL(18,4)), " +
                "CAST(" + cat + " AS NVARCHAR(200)), " + outRef + ", " + outNote + " " +
                "FROM Product_Out o WHERE o.Product_ID = @pid AND o.DateTime_Out >= @f AND o.DateTime_Out < @t" + excl + " " +
                "ORDER BY DT, K", ps, 60);
            foreach (DataRow r in dt.Rows)
            {
                bool isIn = TaxReportKit.Str(r["K"]) == "I";
                string t = TaxReportKit.Str(r["T"]);
                string label;
                if (isIn)
                {
                    switch (t.ToUpperInvariant())
                    {
                        case "RETURN_FROM_CHARGE": label = "รับคืน (ยกเลิกชาร์จห้อง)"; break;
                        case "ADJUSTMENT_GAIN": label = "ปรับเพิ่มจากตรวจนับ"; break;
                        default: label = "รับเข้า/ซื้อ"; break;
                    }
                }
                else if (t == "SALE" || t.Equals("R:SALE", StringComparison.OrdinalIgnoreCase)) label = "ขาย";
                else label = m.CatNames.ContainsKey(t) ? m.CatNames[t] : t;
                m.Ledger.Add(new LedgerLine
                {
                    When = TaxReportKit.NormalizeEra(Convert.ToDateTime(r["DT"])), IsIn = isIn,
                    Qty = TaxReportKit.Dec(r["Q"]), Price = TaxReportKit.Dec(r["P"]),
                    Type = label, Ref = TaxReportKit.Str(r["REF"]), Note = TaxReportKit.Str(r["NOTE"])
                });
            }
        }

        private static List<string> SummaryHeader(Model m)
        {
            var h = new List<string> { "รหัส/บาร์โค้ด", "สินค้า", "ยกมา (จำนวน)", "ยกมา (มูลค่า)", "รับเข้า-ซื้อ", "มูลค่าซื้อ", "รับเข้า-อื่น", "ขาย" };
            foreach (string c in m.Cats) h.Add("จ่ายออก: " + m.CatNames[c]);
            h.AddRange(new[] { "คงเหลือ (จำนวน)", "ราคาทุน/หน่วย", "คงเหลือ (มูลค่า)", "ฐานต้นทุน", "หมายเหตุ" });
            return h;
        }

        private static string RowRemark(Row r)
        {
            var notes = new List<string>();
            if (r.CloseQty < 0) notes.Add("คงเหลือติดลบ");
            if (r.UnitCost <= 0 && (r.CloseQty != 0 || r.OpenQty != 0)) notes.Add("ไม่มีราคาทุน");
            if (r.Pending > 0) notes.Add("รออนุมัติจ่ายออก " + TaxReportKit.Qty(r.Pending));
            return string.Join(" · ", notes);
        }

        private static List<string> SummaryCells(Model m, Row r)
        {
            var c = new List<string>
            {
                r.Code, r.Name, TaxReportKit.Qty(r.OpenQty), TaxReportKit.Money(r.OpenQty * r.UnitCost),
                TaxReportKit.Qty(r.InPurch), TaxReportKit.Money(r.InPurchVal), TaxReportKit.Qty(r.InOther), TaxReportKit.Qty(r.Sold)
            };
            foreach (string k in m.Cats) { decimal v; r.Other.TryGetValue(k, out v); c.Add(TaxReportKit.Qty(v)); }
            c.Add(TaxReportKit.Qty(r.CloseQty));
            c.Add(TaxReportKit.Money(r.UnitCost));
            c.Add(TaxReportKit.Money(r.CloseQty * r.UnitCost));
            c.Add(r.CostBasis);
            c.Add(RowRemark(r));
            return c;
        }

        private void Render()
        {
            Model m;
            try { m = Build(); }
            catch (Exception ex)
            {
                litMsg.Text = "<div class='tr-note'>สร้างรายงานไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>";
                litSummary.Text = ""; pnlLedger.Visible = false;
                return;
            }
            litMsg.Text = "";

            string bn, tax, addr;
            TaxReportKit.BusinessHeader(Conn, out bn, out tax, out addr);
            litHeader.Text = "<div style='margin-bottom:10px;font-size:13px'><b>รายงานสินค้าและวัตถุดิบ</b> · ชื่อผู้ประกอบการ: <b>" +
                TaxReportKit.H(bn) + "</b> · เลขประจำตัวผู้เสียภาษี: <b>" + TaxReportKit.H(tax) + "</b><br/><span class='tr-sub'>" +
                TaxReportKit.H(addr) + "</span><br/>ช่วงวันที่ <b>" + TaxReportKit.D(m.From) + "</b> ถึง <b>" + TaxReportKit.D(m.To) +
                "</b> · " + m.Rows.Count + " รายการสินค้า</div>";

            var sb = new StringBuilder("<table class='tr-t'><tr>");
            var head = SummaryHeader(m);
            for (int i = 0; i < head.Count; i++)
                sb.Append(i >= 2 && i < head.Count - 2 ? "<th class='n'>" : "<th>").Append(TaxReportKit.H(head[i])).Append("</th>");
            sb.Append("</tr>");
            decimal tOpenV = 0, tPurV = 0, tCloseV = 0;
            foreach (var r in m.Rows)
            {
                var cells = SummaryCells(m, r);
                bool bad = r.CloseQty < 0;
                sb.Append(bad ? "<tr class='bad'>" : "<tr>");
                for (int i = 0; i < cells.Count; i++)
                {
                    if (i == 1)
                    {
                        string href = ResolveUrl("~/Report/StockMovementReport") + "?pid=" + r.Pid;
                        sb.Append("<td><a href='").Append(href).Append("'>").Append(TaxReportKit.H(cells[i])).Append("</a></td>");
                    }
                    else sb.Append(i >= 2 && i < cells.Count - 2 ? "<td class='n'>" : "<td>").Append(TaxReportKit.H(cells[i])).Append("</td>");
                }
                sb.Append("</tr>");
                tOpenV += r.OpenQty * r.UnitCost; tPurV += r.InPurchVal; tCloseV += r.CloseQty * r.UnitCost;
            }
            if (m.Rows.Count == 0)
                sb.Append("<tr><td colspan='").Append(head.Count).Append("' style='padding:16px;color:#78909c'>ไม่มีความเคลื่อนไหวของสินค้าในช่วงนี้</td></tr>");
            else
            {
                sb.Append("<tr class='tot'><td></td><td>รวมมูลค่า</td><td></td><td class='n'>").Append(TaxReportKit.Money(tOpenV))
                  .Append("</td><td></td><td class='n'>").Append(TaxReportKit.Money(tPurV)).Append("</td><td></td><td></td>");
                for (int i = 0; i < m.Cats.Count; i++) sb.Append("<td></td>");
                sb.Append("<td></td><td></td><td class='n'>").Append(TaxReportKit.Money(tCloseV)).Append("</td><td></td><td></td></tr>");
            }
            sb.Append("</table>");
            litSummary.Text = sb.ToString();

            // บัญชีคุมรายวัน
            pnlLedger.Visible = m.LedgerRow != null;
            if (m.LedgerRow != null)
            {
                var lr = m.LedgerRow;
                var lb = new StringBuilder();
                lb.Append("<div style='margin-bottom:8px;font-size:13px'>สินค้า: <b>").Append(TaxReportKit.H(lr.Name)).Append("</b> ")
                  .Append(TaxReportKit.H(lr.Code)).Append(" · ราคาทุน/หน่วย ").Append(TaxReportKit.Money(lr.UnitCost))
                  .Append(" (").Append(TaxReportKit.H(lr.CostBasis)).Append(")</div>");
                lb.Append("<table class='tr-t'><tr><th>วันที่</th><th>เอกสารอ้างอิง</th><th>รายการ</th><th class='n'>รับ</th>" +
                          "<th class='n'>จ่าย</th><th class='n'>คงเหลือ</th><th class='n'>มูลค่าคงเหลือ</th><th>หมายเหตุ</th></tr>");
                decimal bal = lr.OpenQty;
                lb.Append("<tr class='sub'><td>").Append(TaxReportKit.D(m.From)).Append("</td><td></td><td>ยกมา</td><td></td><td></td><td class='n'>")
                  .Append(TaxReportKit.Qty(bal)).Append("</td><td class='n'>").Append(TaxReportKit.Money(bal * lr.UnitCost)).Append("</td><td></td></tr>");
                foreach (var l in m.Ledger)
                {
                    bal += l.IsIn ? l.Qty : -l.Qty;
                    lb.Append(bal < 0 ? "<tr class='bad'>" : "<tr>")
                      .Append("<td>").Append(TaxReportKit.D(l.When)).Append(" <span class='tr-sub'>").Append(l.When.ToString("HH:mm", TaxReportKit.Inv)).Append("</span></td>")
                      .Append("<td>").Append(TaxReportKit.H(l.Ref)).Append("</td>")
                      .Append("<td>").Append(TaxReportKit.H(l.Type)).Append("</td>")
                      .Append("<td class='n'>").Append(l.IsIn ? TaxReportKit.Qty(l.Qty) : "").Append("</td>")
                      .Append("<td class='n'>").Append(l.IsIn ? "" : TaxReportKit.Qty(l.Qty)).Append("</td>")
                      .Append("<td class='n'>").Append(TaxReportKit.Qty(bal)).Append("</td>")
                      .Append("<td class='n'>").Append(TaxReportKit.Money(bal * lr.UnitCost)).Append("</td>")
                      .Append("<td>").Append(TaxReportKit.H(l.Note)).Append("</td></tr>");
                }
                lb.Append("<tr class='tot'><td>").Append(TaxReportKit.D(m.To)).Append("</td><td></td><td>คงเหลือยกไป</td><td></td><td></td><td class='n'>")
                  .Append(TaxReportKit.Qty(bal)).Append("</td><td class='n'>").Append(TaxReportKit.Money(bal * lr.UnitCost)).Append("</td><td></td></tr></table>");
                litLedger.Text = lb.ToString();
            }

            if (m.Notes.Count > 0)
            {
                var nb = new StringBuilder("<div class='tr-infobox'><b>ข้อสังเกต</b><ul style='margin:6px 0 0 18px;padding:0'>");
                foreach (var n in m.Notes) nb.Append("<li>").Append(TaxReportKit.H(n)).Append("</li>");
                litNotes.Text = nb.Append("</ul></div>").ToString();
            }
            else litNotes.Text = "";
        }
    }
}
