using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using Take_Time_BangPhra.Inventory;

namespace Take_Time_BangPhra.Product
{
    /// <summary>
    /// รายงานสินค้าเสียหาย / เสื่อมสภาพ / ทำลาย (พิมพ์) — ประกอบการตัดสินค้าเป็นรายจ่ายตามแนวคำสั่ง
    /// กรมสรรพากรที่ ป.79/2541 (แก้ไขโดย ป.84/2542): รายการ จำนวน ต้นทุน สาเหตุ ผู้อนุมัติ พยาน ลายมือชื่อ.
    /// GET: ?ids=1,2,3 หรือ ?from=yyyy-MM-dd&amp;to=yyyy-MM-dd&amp;reasons=DAMAGE,EXPIRED,DESTROYED
    /// ไม่รวมแถวที่ถูกยกเลิก/ปฏิเสธ. ยังไม่รัน PHASE19_30 → แสดงแถวเบิกแบบเดิม (OutType WRITEOFF)
    /// </summary>
    public partial class StockOutReport : System.Web.UI.Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private static readonly string[] DefaultReasons = { "DAMAGE", "EXPIRED", "DESTROYED" };

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SalesStock)) return;
            string role = Session["User"]?.ToString();
            if (Session["permission"]?.ToString() != "True" || (role != "Owner" && role != "Admin"))
            {
                Response.Redirect("/Default", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            try { litBody.Text = Render(); }
            catch (Exception ex)
            {
                litBody.Text = "<div class='sheet'><div class='empty'>เกิดข้อผิดพลาด: " + H(ex.Message) + "</div></div>";
            }
        }

        private string Render()
        {
            var inv = CultureInfo.InvariantCulture;
            DateTime from, to;
            if (!DateTime.TryParseExact(Request.QueryString["from"] ?? "", "yyyy-MM-dd", inv, DateTimeStyles.None, out from))
                from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (!DateTime.TryParseExact(Request.QueryString["to"] ?? "", "yyyy-MM-dd", inv, DateTimeStyles.None, out to))
                to = DateTime.Today;

            var ids = (Request.QueryString["ids"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => { long v; return long.TryParse(x.Trim(), out v) ? v : 0; }).Where(v => v > 0).Distinct().Take(500).ToList();

            var allReasons = StockOutReasonCatalog.All(Conn);
            var picked = (Request.QueryString["reasons"] ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpperInvariant())
                .Where(x => allReasons.Any(r => r.Code == x)).Distinct().ToList();
            if (picked.Count == 0) picked = DefaultReasons.ToList();

            bool schema = StockOutService.SchemaReady(Conn);
            DataTable dt = Query(schema, ids, picked, from, to);

            var sb = new StringBuilder();
            // ── เครื่องมือ (ไม่พิมพ์) ──
            sb.Append("<form method='get' class='tools'>");
            sb.Append("<div><label>ตั้งแต่</label><input type='date' name='from' value='").Append(from.ToString("yyyy-MM-dd", inv)).Append("'/></div>");
            sb.Append("<div><label>ถึง</label><input type='date' name='to' value='").Append(to.ToString("yyyy-MM-dd", inv)).Append("'/></div>");
            sb.Append("<div><label>เหตุผล</label><select name='reasons'>");
            foreach (var opt in new[] { "DAMAGE,EXPIRED,DESTROYED", "DAMAGE", "EXPIRED", "DESTROYED", "LOST_THEFT", "DAMAGE,EXPIRED,DESTROYED,LOST_THEFT" })
            {
                string label = string.Join(" + ", opt.Split(',').Select(c => allReasons.FirstOrDefault(r => r.Code == c)?.NameTh ?? c));
                bool sel = string.Join(",", picked) == opt;
                sb.Append("<option value='").Append(H(opt)).Append("'").Append(sel ? " selected" : "").Append(">").Append(H(label)).Append("</option>");
            }
            sb.Append("</select></div>");
            sb.Append("<button type='submit'>แสดง</button>");
            sb.Append("<button type='button' class='alt' onclick='window.print()'>🖨 พิมพ์</button>");
            sb.Append("<a href='/Product/Stock' style='margin-left:auto;font-size:13px;'>← กลับหน้าสต็อก</a>");
            sb.Append("</form>");

            // ── หัวรายงาน ──
            string coName = "", coTax = "", coAddr = "", coBranch = "";
            try
            {
                var b = _code.DatabaseQuerySafe(Conn, "SELECT TOP 1 * FROM Business_Info", null);
                if (b != null && b.Rows.Count > 0)
                {
                    Func<string, string> col = c => b.Columns.Contains(c) && b.Rows[0][c] != DBNull.Value ? b.Rows[0][c].ToString() : "";
                    coName = col("Company_Name"); coTax = col("LegalEntity_Number"); coAddr = col("Address"); coBranch = col("Branch_Number");
                }
            }
            catch { }

            sb.Append("<div class='sheet'>");
            sb.Append("<div class='co'><b>").Append(H(string.IsNullOrEmpty(coName) ? "Take Time BangPhra" : coName)).Append("</b>");
            if (!string.IsNullOrEmpty(coTax)) sb.Append(" · เลขประจำตัวผู้เสียภาษี ").Append(H(coTax));
            if (!string.IsNullOrEmpty(coBranch)) sb.Append(" · สาขา ").Append(H(coBranch));
            if (!string.IsNullOrEmpty(coAddr)) sb.Append("<br/>").Append(H(coAddr));
            sb.Append("</div>");
            sb.Append("<h1>รายงานสินค้าเสียหาย / เสื่อมสภาพ / ทำลาย</h1>");
            string reasonNames = string.Join(", ", picked.Select(c => allReasons.FirstOrDefault(r => r.Code == c)?.NameTh ?? c));
            sb.Append("<div class='meta'><span>")
              .Append(ids.Count > 0 ? "รายการที่เลือก " + ids.Count + " รายการ" : "ช่วงวันที่ " + ThDate(from) + " – " + ThDate(to))
              .Append(" · เหตุผล: ").Append(H(reasonNames)).Append("</span>")
              .Append("<span>เลขที่รายงาน: SOR-").Append(DateTime.Now.ToString("yyyyMMdd-HHmm", inv))
              .Append(" · พิมพ์ ").Append(ThDate(DateTime.Now)).Append(" ").Append(DateTime.Now.ToString("HH:mm", inv)).Append("</span></div>");

            if (dt == null || dt.Rows.Count == 0)
            {
                sb.Append("<div class='empty'>ไม่มีรายการในเงื่อนไขนี้</div></div>");
                return sb.ToString();
            }

            sb.Append("<table class='r'><tr><th>ลำดับ</th><th>วันที่</th><th>สินค้า</th><th>จำนวน</th><th>ต้นทุน/หน่วย</th>")
              .Append("<th>มูลค่าต้นทุน</th><th>เหตุผล</th><th>สาเหตุ / รายละเอียด</th><th>ผู้บันทึก</th><th>ผู้อนุมัติ</th>")
              .Append("<th>อ้างอิง</th><th>หลักฐาน</th></tr>");
            decimal totQty = 0, totCost = 0;
            int i = 0;
            foreach (DataRow r in dt.Rows)
            {
                i++;
                decimal qty = StockOutService.Dec(r["Amount"]);
                decimal unit = StockOutService.Dec(r["PricePerUnit"]);
                decimal cost = Math.Round(qty * unit, 2);
                totQty += qty; totCost += cost;
                string rc = Convert.ToString(r["Out_Reason_Code"]);
                string rName = allReasons.FirstOrDefault(x => x.Code == rc)?.NameTh ?? (string.IsNullOrEmpty(rc) ? "รายการเบิกเดิม" : rc);
                DateTime d = r["DateTime_Out"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(r["DateTime_Out"]);
                bool hasEv = !string.IsNullOrWhiteSpace(Convert.ToString(r["Evidence_Path"]));
                sb.Append("<tr><td class='n'>").Append(i).Append("</td>")
                  .Append("<td>").Append(ThDate(d)).Append("</td>")
                  .Append("<td>").Append(H(Convert.ToString(r["Product_Name"]))).Append("</td>")
                  .Append("<td class='n'>").Append(qty.ToString("N0")).Append("</td>")
                  .Append("<td class='n'>").Append(unit.ToString("N2")).Append("</td>")
                  .Append("<td class='n'>").Append(cost.ToString("N2")).Append("</td>")
                  .Append("<td>").Append(H(rName)).Append("</td>")
                  .Append("<td>").Append(H(Convert.ToString(r["Reason"]))).Append("</td>")
                  .Append("<td>").Append(H(Convert.ToString(r["Recorded_By"]))).Append("</td>")
                  .Append("<td>").Append(H(Convert.ToString(r["Approved_By"]))).Append("</td>")
                  .Append("<td>STOCKOUT-").Append(H(Convert.ToString(r["ID"]))).Append("</td>")
                  .Append("<td>").Append(hasEv ? "มี (ภาพ/เอกสารแนบ)" : "—").Append("</td></tr>");
            }
            sb.Append("<tr class='tot'><td colspan='3'>รวม</td><td class='n'>").Append(totQty.ToString("N0"))
              .Append("</td><td></td><td class='n'>").Append(totCost.ToString("N2")).Append("</td><td colspan='6'></td></tr></table>");

            // ── การทำลาย ──
            sb.Append("<div class='sec'><b>การทำลาย:</b> วันที่ทำลาย <span class='line' contenteditable='true'></span> ")
              .Append("สถานที่ <span class='line' contenteditable='true'></span><br/>")
              .Append("วิธีการทำลาย (เช่น ทุบ/เททิ้ง/ฝังกลบ/ส่งกำจัดโดยผู้รับจ้าง) <span class='line' style='min-width:420px' contenteditable='true'></span><br/>")
              .Append("แจ้งสรรพากรพื้นที่ (ถ้ามี) เลขที่/วันที่ <span class='line' contenteditable='true'></span> ")
              .Append("ซากที่ขายได้ (ถ้ามี — ต้องออกใบขายและเสีย VAT ตามปกติ) <span class='line' contenteditable='true'></span></div>");

            // ── ลายมือชื่อ ──
            sb.Append("<div class='sig'>");
            foreach (var who in new[] { "ผู้จัดทำรายงาน", "ผู้อนุมัติ (หุ้นส่วนผู้จัดการ)", "พยาน 1", "พยาน 2", "ผู้สอบบัญชี (ผู้สังเกตการณ์/รับรอง)" })
            {
                sb.Append("<div><div class='s'></div>(<input type='text' placeholder='ชื่อ-นามสกุล'/>)<br/>")
                  .Append(H(who)).Append("<br/>วันที่ ____/____/______</div>");
            }
            sb.Append("</div>");

            sb.Append("<div class='note'>เอกสารนี้จัดทำเพื่อประกอบการตัดสินค้าที่เสียหาย เสื่อมสภาพ หรือหมดอายุเป็นรายจ่ายตามแนวคำสั่งกรมสรรพากรที่ ป.79/2541 ")
              .Append("(แก้ไขเพิ่มเติมโดย ป.84/2542): ระบุรายการ จำนวน ต้นทุน สาเหตุ ผู้อนุมัติ ทำลายต่อหน้าพยานและผู้สอบบัญชี พร้อมภาพถ่าย/บันทึกการทำลาย ")
              .Append("(กรณีต้องแจ้งเจ้าพนักงานล่วงหน้า ให้ผู้ทำบัญชีตรวจสอบเงื่อนไขฉบับแก้ไขอีกครั้ง). การทำลายสินค้าเสียหายจริงที่มีหลักฐาน ไม่ถือเป็นการขายตาม ม.77/1(8) จึงไม่มีภาษีขาย; ")
              .Append("สินค้าที่ขาดหายโดยไม่มีหลักฐานการทำลายถือเป็นสินค้าขาดจากรายงาน (ม.77/1(8)(จ)). มูลค่าคิดตามต้นทุนที่ระบบใช้ลงบัญชี (อ้างอิง STOCKOUT-{เลขรายการ} ใน NextAcc). ")
              .Append("เอกสารนี้ไม่ใช่ใบกำกับภาษี — ให้ผู้ทำบัญชียืนยันก่อนใช้</div>");
            sb.Append("</div>");
            return sb.ToString();
        }

        private DataTable Query(bool schema, List<long> ids, List<string> reasons, DateTime from, DateTime to)
        {
            var ps = new Dictionary<string, object>();
            var where = new StringBuilder();
            if (ids.Count > 0)
            {
                var names = new List<string>();
                for (int k = 0; k < ids.Count; k++) { names.Add("@id" + k); ps["@id" + k] = ids[k]; }
                where.Append(" po.ID IN (").Append(string.Join(",", names)).Append(")");
            }
            else
            {
                ps["@from"] = from.Date;
                ps["@to"] = to.Date.AddDays(1);
                where.Append(" po.DateTime_Out >= @from AND po.DateTime_Out < @to");
            }

            if (schema)
            {
                var rn = new List<string>();
                if (ids.Count == 0)
                {
                    for (int k = 0; k < reasons.Count; k++) { rn.Add("@r" + k); ps["@r" + k] = reasons[k]; }
                    where.Append(" AND po.Out_Reason_Code IN (").Append(string.Join(",", rn)).Append(")");
                }
                else
                {
                    where.Append(" AND (po.Out_Reason_Code IS NOT NULL OR po.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS'))");
                }
                where.Append(" AND po.Is_Reversed = 0 AND ISNULL(po.Approval_Status,'APPROVED') <> 'REJECTED'");
                return _code.DatabaseQuerySafe(Conn,
                    @"SELECT po.ID, po.DateTime_Out, p.Product_Name, po.Amount, po.PricePerUnit, po.Out_Reason_Code, po.Reason,
                             po.Recorded_By, po.Approved_By, po.Evidence_Path
                        FROM Product_Out po LEFT JOIN Product p ON p.ID = po.Product_ID
                       WHERE" + where + " ORDER BY po.DateTime_Out, po.ID", ps);
            }

            where.Append(" AND po.OutType IN ('WRITEOFF','ADJUSTMENT_LOSS')");
            return _code.DatabaseQuerySafe(Conn,
                @"SELECT po.ID, po.DateTime_Out, p.Product_Name, po.Amount, po.PricePerUnit,
                         CAST(NULL AS NVARCHAR(30)) AS Out_Reason_Code, po.Reason,
                         CAST(NULL AS NVARCHAR(100)) AS Recorded_By, CAST(NULL AS NVARCHAR(100)) AS Approved_By,
                         CAST(NULL AS NVARCHAR(400)) AS Evidence_Path
                    FROM Product_Out po LEFT JOIN Product p ON p.ID = po.Product_ID
                   WHERE" + where + " ORDER BY po.DateTime_Out, po.ID", ps);
        }

        private static string ThDate(DateTime d)
        {
            if (d == DateTime.MinValue) return "";
            int y = d.Year < 2400 ? d.Year + 543 : d.Year;
            return d.ToString("dd/MM/", CultureInfo.InvariantCulture) + y;
        }

        private static string H(string s) => HttpUtility.HtmlEncode(s ?? "");
    }
}
