using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตั้งค่าลงบัญชีรายสินค้า — สวิตช์ต่อสินค้า Product.Include_In_Daily_Rollup (PHASE18_24):
    /// รวมการขายเข้า "ใบสรุปรายได้รายวัน" (POS rollup + รูมเซอร์วิส) หรือไม่
    ///
    /// ค่าเริ่มต้นทุกสินค้า = รวม (พฤติกรรมเดิม) — หน้าอื่น/เส้นทางอื่นไม่ถูกกระทบ:
    /// ขายออกใบกำกับรายใบ, ชาร์จเข้าห้อง, การตัดจำนวนสต๊อก ทำงานเหมือนเดิมทั้งหมด
    /// </summary>
    public partial class ProductAccounting : Page
    {
        private readonly code _code = new code();
        private string Conn => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Perm.Guard(this, Perm.SysAccounting)) return;   // กลุ่มสิทธิ์ไม่อนุญาตส่วนนี้
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }
            if (!IsPostBack) RenderRows();
        }

        private bool ColumnReady()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(Conn,
                    @"SELECT TOP 1 1 FROM INFORMATION_SCHEMA.COLUMNS
                       WHERE TABLE_NAME = 'Product' AND COLUMN_NAME = 'Include_In_Daily_Rollup'", null);
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        private void RenderRows()
        {
            if (!ColumnReady())
            {
                litRows.Text = "<tr><td colspan='3' style='color:#c62828; padding:16px;'>" +
                    "ยังไม่ได้รัน migration <b>PHASE18_24_Product_Rollup_Flag.sql</b> — รันก่อนแล้วโหลดหน้านี้ใหม่</td></tr>";
                btnSave.Enabled = false;
                litCount.Text = "";
                return;
            }
            btnSave.Enabled = true;

            string search = (txtSearch.Text ?? "").Trim();
            var ps = new Dictionary<string, object>();
            string where = "WHERE (p.Status = 'True' OR p.Status = '1')";
            if (!string.IsNullOrEmpty(search))
            {
                where += " AND (p.Product_Name LIKE @q OR p.Barcode LIKE @q)";
                ps["@q"] = "%" + search + "%";
            }

            DataTable dt = _code.DatabaseQuerySafe(Conn,
                $@"SELECT p.ID, p.Product_Name, p.Barcode, p.Sell_Price,
                          ISNULL(p.Include_In_Daily_Rollup, 1) AS Inc
                     FROM Product p
                     {where}
                    ORDER BY ISNULL(p.Include_In_Daily_Rollup, 1) ASC, p.Product_Name", ps);

            var sb = new StringBuilder();
            int total = 0, off = 0;
            if (dt != null)
            {
                foreach (DataRow r in dt.Rows)
                {
                    total++;
                    int id = Convert.ToInt32(r["ID"]);
                    bool inc = Convert.ToBoolean(r["Inc"]);
                    if (!inc) off++;
                    string barcode = r["Barcode"] == DBNull.Value ? "" : r["Barcode"].ToString();
                    decimal price = r["Sell_Price"] == DBNull.Value ? 0m : Convert.ToDecimal(r["Sell_Price"]);

                    sb.Append($"<tr{(inc ? "" : " class='pa-off'")}>");
                    sb.Append("<td><div class='pa-name'>" + Server.HtmlEncode(r["Product_Name"].ToString()) + "</div>");
                    if (!string.IsNullOrEmpty(barcode))
                        sb.Append("<div class='pa-sub'>บาร์โค้ด: " + Server.HtmlEncode(barcode) + "</div>");
                    sb.Append("</td>");
                    sb.Append($"<td style='text-align:right;'>{price:N2}</td>");
                    // row_{id} = แถวนี้ถูกแสดงในหน้า (บันทึกเฉพาะแถวที่แสดง — ค้นหาแล้วเซฟไม่ไปรีเซ็ตตัวอื่น)
                    sb.Append($"<td class='pa-chk'><input type='hidden' name='row_{id}' value='1' />" +
                              $"<input type='checkbox' name='inc_{id}' value='1'{(inc ? " checked" : "")} /></td>");
                    sb.Append("</tr>");
                }
            }
            litRows.Text = sb.Length > 0 ? sb.ToString()
                : "<tr><td colspan='3' style='color:#90a4ae; padding:16px;'>ไม่พบสินค้า</td></tr>";
            litCount.Text = $"ทั้งหมด {total} รายการ · ไม่รวมใบสรุป {off} รายการ";
        }

        protected void btnSearch_Click(object sender, EventArgs e) => RenderRows();

        protected void btnSave_Click(object sender, EventArgs e)
        {
            if (!ColumnReady()) { RenderRows(); return; }
            try
            {
                int changed = 0;
                // อัปเดตเฉพาะแถวที่แสดงอยู่ (มี hidden row_{id}) — การค้นหา/กรองจึงไม่รีเซ็ตสินค้าอื่น
                foreach (string key in Request.Form.AllKeys)
                {
                    if (key == null || !key.StartsWith("row_")) continue;
                    int id;
                    if (!int.TryParse(key.Substring(4), out id)) continue;
                    bool inc = Request.Form["inc_" + id] == "1";

                    changed += _code.DatabaseInsertSafe(Conn,
                        @"UPDATE Product SET Include_In_Daily_Rollup = @v
                           WHERE ID = @id AND ISNULL(Include_In_Daily_Rollup, 1) <> @v",
                        new Dictionary<string, object> { { "@v", inc }, { "@id", id } });
                }

                pnlMsg.Visible = true;
                divMsg.Attributes["class"] = "pa-msg pa-ok";
                litMsg.Text = changed > 0
                    ? $"บันทึกแล้ว — เปลี่ยน {changed} รายการ (มีผลกับใบสรุปของวันที่ยังไม่ถูกรวบ)"
                    : "บันทึกแล้ว — ไม่มีรายการเปลี่ยนแปลง";
                if (changed > 0)
                    _code.Logs(Conn, "ProductAccounting",
                        $"เปลี่ยนการตั้งค่ารวมใบสรุปรายวัน {changed} รายการ", Session["UserName"]?.ToString() ?? "SYSTEM");
            }
            catch (Exception ex)
            {
                pnlMsg.Visible = true;
                divMsg.Attributes["class"] = "pa-msg pa-err";
                litMsg.Text = "บันทึกไม่สำเร็จ: " + Server.HtmlEncode(ex.Message);
            }
            RenderRows();
        }
    }
}
