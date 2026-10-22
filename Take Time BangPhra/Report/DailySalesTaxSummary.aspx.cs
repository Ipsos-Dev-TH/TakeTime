using System;
using System.Collections.Generic;
using System.Text;
using System.Web.UI;

namespace Take_Time_BangPhra.TaxReports
{
    /// <summary>
    /// สรุปยอดขายและภาษีขายรายวัน — วัน × ช่องทางขาย × แหล่งเงิน: จำนวนการขาย, ยอดรวม, VAT,
    /// เอกสาร NextAcc ที่ครอบ (รายใบ/ใบสรุป POSDAY-/RSDAY- + สถานะจาก Accounting_Sync_Queue)
    /// พร้อมธง: ไม่มีเอกสาร / คิวล้มเหลว / รายการมาช้าหลังรวบ / โอนยังไม่ตรวจ / ไม่ตรงนโยบาย.
    /// ใช้เป็นบันทึกสรุปยอดขายเงินสดรายวันที่รองรับใบสรุปรายวันด้วย. อ่านอย่างเดียว
    /// </summary>
    public partial class DailySalesTaxSummary : Page
    {
        private string Conn => TaxReportKit.Conn;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!TaxReportKit.EnsureAccess(this)) return;
            litCss.Text = "<style>" + TaxReportKit.Css + "</style>";
            if (!IsPostBack)
            {
                var today = DateTime.Today;
                DateTime? qf = TaxReportKit.ParseDate(Request.QueryString["from"]);
                DateTime? qt = TaxReportKit.ParseDate(Request.QueryString["to"]);
                txtFrom.Text = TaxReportKit.Iso(qf ?? new DateTime(today.Year, today.Month, 1));
                txtTo.Text = TaxReportKit.Iso(qt ?? today);
                if (Request.QueryString["problems"] == "1") chkProblems.Checked = true;
                Render();
            }
        }

        protected void btnShow_Click(object sender, EventArgs e) { Render(); }

        private void Range(out DateTime from, out DateTime to)
        {
            from = TaxReportKit.ParseDate(txtFrom.Text) ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            to = TaxReportKit.ParseDate(txtTo.Text) ?? DateTime.Today;
            if (to < from) { var t = from; from = to; to = t; }
            if ((to - from).TotalDays > 400) from = to.AddDays(-400);   // กันดึงทั้งประวัติ
        }

        private List<TaxReportKit.SaleGroup> Filter(TaxReportKit.SalesResult res)
        {
            string ch = ddlChannel.SelectedValue ?? "";
            var list = new List<TaxReportKit.SaleGroup>();
            foreach (var g in res.Groups)
            {
                if (ch.Length > 0 && g.Channel != ch) continue;
                if (chkProblems.Checked && !g.HasProblem) continue;
                list.Add(g);
            }
            return list;
        }

        private static string Mode(TaxReportKit.SaleGroup g) => string.IsNullOrEmpty(g.PolicyMode) ? "" : TaxReportKit.ModeLabel(g.PolicyMode);

        private static string DocsText(TaxReportKit.SaleGroup g)
        {
            var parts = new List<string>();
            int per = 0; var byStatus = new SortedDictionary<string, int>();
            foreach (var d in g.Docs)
            {
                if (d.Rollup)
                    parts.Add(d.Id + " [รวบรายวัน: " + TaxReportKit.StatusLabel(d.Status) + (string.IsNullOrEmpty(d.DocNo) ? "" : " " + d.DocNo) + "]");
                else
                {
                    per++;
                    int c; byStatus.TryGetValue(d.Status, out c); byStatus[d.Status] = c + 1;
                }
            }
            if (per > 0)
            {
                var s = new List<string>();
                foreach (var kv in byStatus) s.Add(TaxReportKit.StatusLabel(kv.Key) + " " + kv.Value);
                parts.Add("รายใบ " + per + " ใบ (" + string.Join(", ", s) + ")");
            }
            return string.Join(" | ", parts);
        }

        private static string FlagsText(TaxReportKit.SaleGroup g)
        {
            var s = new List<string>();
            foreach (var f in g.Flags) s.Add(TaxReportKit.FlagText(f));
            return string.Join(", ", s);
        }

        protected void btnCsv_Click(object sender, EventArgs e)
        {
            DateTime from, to;
            Range(out from, out to);
            TaxReportKit.SalesResult res;
            try { res = TaxReportKit.CollectSales(Conn, from, to); }
            catch (Exception ex) { litMsg.Text = "<div class='tr-note'>สร้างรายงานไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>"; return; }
            string bn, tax, addr;
            TaxReportKit.BusinessHeader(Conn, out bn, out tax, out addr);
            var rows = new List<string[]>
            {
                new[] { "สรุปยอดขายและภาษีขายรายวัน (บันทึกสรุปยอดขาย — ไม่ใช่ใบกำกับภาษี)" },
                new[] { "ชื่อผู้ประกอบการ", bn, "เลขประจำตัวผู้เสียภาษี", tax },
                new[] { "ช่วงวันที่", TaxReportKit.Iso(from), TaxReportKit.Iso(to) },
                new string[0],
                new[] { "วันที่", "ช่องทางขาย", "แหล่งเงิน", "ชนิดการชำระ", "จำนวนการขาย", "ยอดรวม (รวม VAT)", "ภาษีขาย", "ยอดก่อน VAT",
                        "นับเป็นยอดขาย", "นโยบายเอกสาร", "เอกสาร NextAcc", "ธง", "รายการที่ต้องตรวจ", "หมายเหตุ" }
            };
            foreach (var g in Filter(res))
            {
                rows.Add(new[]
                {
                    TaxReportKit.Iso(g.Day), TaxReportKit.SalesChannelLabel(g.Channel), g.PaidHowName, TaxReportKit.ChannelTypeLabel(g.ChannelType),
                    g.Count.ToString(TaxReportKit.Inv), TaxReportKit.Money(g.Gross), TaxReportKit.Money(g.Vat), TaxReportKit.Money(g.Gross - g.Vat),
                    g.InfoOnly ? "ไม่นับ" : "นับ", Mode(g), DocsText(g), FlagsText(g), string.Join("; ", g.ProblemIds), g.Note
                });
            }
            TaxReportKit.SendCsv(Response, "daily_sales_tax_" + from.ToString("yyyyMMdd", TaxReportKit.Inv) + "_" +
                to.ToString("yyyyMMdd", TaxReportKit.Inv) + ".csv", rows);
        }

        private void Render()
        {
            DateTime from, to;
            Range(out from, out to);
            TaxReportKit.SalesResult res;
            try { res = TaxReportKit.CollectSales(Conn, from, to); }
            catch (Exception ex)
            {
                litMsg.Text = "<div class='tr-note'>สร้างรายงานไม่สำเร็จ: " + TaxReportKit.H(ex.Message) + "</div>";
                litTable.Text = "";
                return;
            }
            litMsg.Text = "";
            litStatus.Text = TaxReportKit.ConfigStatusHtml(res);

            var groups = Filter(res);
            decimal tGross = 0, tVat = 0; int tCount = 0, problemGroups = 0, missing = 0, failed = 0;
            foreach (var g in res.Groups)
            {
                if (ddlChannel.SelectedValue.Length > 0 && g.Channel != ddlChannel.SelectedValue) continue;
                if (g.HasProblem) problemGroups++;
                if (g.Flags.Contains(TaxReportKit.FNoDoc) || g.Flags.Contains(TaxReportKit.FLate)) missing++;
                if (g.Flags.Contains(TaxReportKit.FFailed)) failed++;
                if (g.InfoOnly) continue;
                tGross += g.Gross; tVat += g.Vat; tCount += g.Count;
            }
            litKpis.Text = "<div class='tr-kpis'>" +
                Kpi(TaxReportKit.Money(tGross), "ยอดขายรวม VAT (นับแล้ว)") +
                Kpi(TaxReportKit.Money(tVat), "ภาษีขาย") +
                Kpi(TaxReportKit.Money(tGross - tVat), "ยอดก่อน VAT") +
                Kpi(tCount.ToString("#,##0", TaxReportKit.Inv), "จำนวนการขาย/ใบ") +
                Kpi("<span style='color:" + (missing > 0 ? "#c62828" : "#1e7e42") + "'>" + missing + "</span>", "กลุ่มที่ยังไม่มีเอกสาร/มาช้า") +
                Kpi("<span style='color:" + (failed > 0 ? "#c62828" : "#1e7e42") + "'>" + failed + "</span>", "กลุ่มที่คิวล้มเหลว") +
                "</div>";

            string bn, tax, addr;
            TaxReportKit.BusinessHeader(Conn, out bn, out tax, out addr);
            litHeader.Text = "<div style='margin-bottom:10px;font-size:13px'><b>สรุปยอดขายและภาษีขายรายวัน</b> · " + TaxReportKit.H(bn) +
                (tax.Length > 0 ? " · เลขผู้เสียภาษี " + TaxReportKit.H(tax) : "") + " · ช่วง <b>" + TaxReportKit.D(from) + "</b> ถึง <b>" +
                TaxReportKit.D(to) + "</b>" + (problemGroups > 0 ? " · <span class='tr-diff'>มีปัญหา " + problemGroups + " กลุ่ม</span>" : "") + "</div>";

            var sb = new StringBuilder("<table class='tr-t'><tr><th>วันที่</th><th>ช่องทางขาย</th><th>แหล่งเงิน</th><th class='n'>จำนวน</th>" +
                "<th class='n'>ยอดรวม VAT</th><th class='n'>ภาษีขาย</th><th class='n'>ก่อน VAT</th><th>นโยบาย</th><th>เอกสาร NextAcc</th><th>สถานะ</th></tr>");
            DateTime? curDay = null;
            decimal dGross = 0, dVat = 0; int dCount = 0;
            Action flushDay = () =>
            {
                if (curDay == null) return;
                sb.Append("<tr class='sub'><td>").Append(TaxReportKit.D(curDay.Value)).Append("</td><td colspan='2'>รวมทั้งวัน</td><td class='n'>")
                  .Append(dCount).Append("</td><td class='n'>").Append(TaxReportKit.Money(dGross)).Append("</td><td class='n'>")
                  .Append(TaxReportKit.Money(dVat)).Append("</td><td class='n'>").Append(TaxReportKit.Money(dGross - dVat))
                  .Append("</td><td></td><td></td><td></td></tr>");
            };
            foreach (var g in groups)
            {
                if (curDay != g.Day) { flushDay(); curDay = g.Day; dGross = dVat = 0; dCount = 0; }
                if (!g.InfoOnly) { dGross += g.Gross; dVat += g.Vat; dCount += g.Count; }
                sb.Append(g.InfoOnly ? "<tr class='info'>" : (g.HasProblem ? "<tr class='bad'>" : "<tr>"));
                sb.Append("<td>").Append(TaxReportKit.D(g.Day)).Append("</td>");
                sb.Append("<td>").Append(TaxReportKit.H(TaxReportKit.SalesChannelLabel(g.Channel))).Append("</td>");
                sb.Append("<td>").Append(TaxReportKit.H(g.PaidHowName)).Append("<br/><span class='tr-sub'>")
                  .Append(TaxReportKit.H(TaxReportKit.ChannelTypeLabel(g.ChannelType))).Append("</span></td>");
                sb.Append("<td class='n'>").Append(g.Count).Append("</td>");
                sb.Append("<td class='n'>").Append(TaxReportKit.Money(g.Gross)).Append("</td>");
                sb.Append("<td class='n'>").Append(TaxReportKit.Money(g.Vat)).Append("</td>");
                sb.Append("<td class='n'>").Append(TaxReportKit.Money(g.Gross - g.Vat)).Append("</td>");
                sb.Append("<td>").Append(TaxReportKit.H(Mode(g))).Append("</td>");
                sb.Append("<td>").Append(DocsHtml(g)).Append("</td>");
                sb.Append("<td>");
                foreach (var f in g.Flags) sb.Append(TaxReportKit.FlagChip(f));
                if (g.Note.Length > 0) sb.Append("<div class='tr-sub'>").Append(TaxReportKit.H(g.Note)).Append("</div>");
                if (g.ProblemIds.Count > 0)
                {
                    sb.Append("<div class='tr-sub'>ตรวจ: ");
                    int n = 0;
                    foreach (var id in g.ProblemIds) { if (n++ >= 8) { sb.Append(" …"); break; } sb.Append(n > 1 ? ", " : "").Append(TaxReportKit.H(id)); }
                    sb.Append("</div>");
                }
                sb.Append("</td></tr>");
            }
            flushDay();
            if (groups.Count == 0)
                sb.Append("<tr><td colspan='10' style='padding:16px;color:#78909c'>ไม่มีรายการในช่วงนี้</td></tr>");
            else
                sb.Append("<tr class='tot'><td colspan='3'>รวมทั้งช่วง (ไม่นับแถวตัวเอียง)</td><td class='n'>").Append(tCount)
                  .Append("</td><td class='n'>").Append(TaxReportKit.Money(tGross)).Append("</td><td class='n'>").Append(TaxReportKit.Money(tVat))
                  .Append("</td><td class='n'>").Append(TaxReportKit.Money(tGross - tVat)).Append("</td><td colspan='3'></td></tr>");
            sb.Append("</table>");
            litTable.Text = sb.ToString();

            var nb = new StringBuilder("<div class='tr-infobox'><b>วิธีอ่าน</b><ul style='margin:6px 0 0 18px;padding:0'>");
            nb.Append("<li><b>ขายหน้าร้าน</b>: ขายที่ออกใบในระบบนับจากใบเสร็จ (รายใบ) · ขายที่ไม่ออกใบนับจากแถวขาย — จำนวนบิลประมาณจากเวลาบันทึก · VAT ถอด 7% จากยอดรวมแบบเดียวกับใบสรุป</li>");
            nb.Append("<li><b>รายการมาช้า/ตกหล่น</b> = ขายที่ลงวันที่ของวันที่รวบใบสรุปไปแล้ว หรือยอดแถวขายมากกว่ายอดใบสรุป — รายได้ส่วนนี้ยังไม่ถึง NextAcc</li>");
            nb.Append("<li><b>แถวตัวเอียง</b> (ลงบิลห้อง/สลิปรอตรวจ) แสดงเพื่อทราบ ไม่นับเป็นยอดขายของวัน — รายได้ลงบิลห้องอยู่ในใบเสร็จเช็คเอาท์</li>");
            nb.Append("<li><b>ไม่ส่งตามนโยบาย</b> = นโยบายเอกสารขายตั้ง \"ไม่ส่งบัญชี\" (ตั้งใจ ไม่ใช่ตกหล่น — ยังนับเป็นยอดขาย) · <b>รอส่ง</b> = กำลังรวบ/รอเข้าคิว " +
                      "หรือใบสรุปถูกยกเลิกแล้วรอรวบใหม่เป็นใบเพิ่มเติม · รายการขายที่ยกเลิกแล้วไม่นับ · กิจกรรม/ของใช้/เกตเวย์หน้าร้าน/ค่าเสียหาย นับจากสมุดการขายรายช่องทางตามวันขาย</li>");
            nb.Append("<li>สถานะเอกสารอ่านจากคิวส่ง NextAcc — แก้คิวที่ล้มเหลวที่ <a href='").Append(ResolveUrl(TaxReportKit.QueuePageUrl))
              .Append("'>หน้าคิว Accounting Integration</a> · กระทบยอดกับ NextAcc ที่ <a href='").Append(ResolveUrl("~/Report/NextAccReconciliation"))
              .Append("'>รายงานกระทบยอด NextAcc</a></li>");
            foreach (var n in res.Notes) nb.Append("<li class='tr-diff'>").Append(TaxReportKit.H(n)).Append("</li>");
            litNotes.Text = nb.Append("</ul></div>").ToString();
        }

        private static string Kpi(string valueHtml, string label) =>
            "<div class='tr-kpi'><div class='v'>" + valueHtml + "</div><div class='l'>" + TaxReportKit.H(label) + "</div></div>";

        private static string DocsHtml(TaxReportKit.SaleGroup g)
        {
            var sb = new StringBuilder();
            int per = 0; var byStatus = new SortedDictionary<string, int>();
            foreach (var d in g.Docs)
            {
                if (d.Rollup)
                {
                    sb.Append("<div><code>").Append(TaxReportKit.H(d.Id)).Append("</code> ")
                      .Append(TaxReportKit.Chip("รวบรายวัน", "info"))
                      .Append(TaxReportKit.Chip(TaxReportKit.StatusLabel(d.Status), TaxReportKit.StatusCss(d.Status)));
                    if (!string.IsNullOrEmpty(d.DocNo)) sb.Append(" <span class='tr-sub'>").Append(TaxReportKit.H(d.DocNo)).Append("</span>");
                    if (d.QueueId > 0) sb.Append(" <span class='tr-sub'>คิว #").Append(d.QueueId).Append("</span>");
                    sb.Append("</div>");
                }
                else
                {
                    per++;
                    int c; byStatus.TryGetValue(d.Status, out c); byStatus[d.Status] = c + 1;
                }
            }
            if (per > 0)
            {
                sb.Append("<div>").Append(TaxReportKit.Chip("รายใบ " + per + " ใบ", "info"));
                foreach (var kv in byStatus)
                    sb.Append(TaxReportKit.Chip(TaxReportKit.StatusLabel(kv.Key) + " " + kv.Value, TaxReportKit.StatusCss(kv.Key)));
                sb.Append("</div>");
            }
            if (sb.Length == 0) sb.Append("<span class='tr-sub'>—</span>");
            return sb.ToString();
        }
    }
}
