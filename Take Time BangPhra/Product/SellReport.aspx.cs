using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Data;
using System.Configuration;
using System.IO;
using Microsoft.Ajax.Utilities;
using Take_Time_BangPhra.Account.Report;
using System.Security.Cryptography;
using ECertificateAPI;
using System.Net.Mail;
using Microsoft.Reporting.WebForms;

namespace Take_Time_BangPhra.Product
{
    public partial class SellReport : System.Web.UI.Page
    {
        _Default codeDefault = new _Default();
        Receipt codeReceipt = new Receipt();
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner"))
                {
                    GridView1.Columns[10].Visible = true;
                
                }
                if (Session["permission"].ToString() == "True" && (Session["User"].ToString() == "Owner" || Session["User"].ToString() == "Admin"))
                {

                }
                else
                {
                    Response.Redirect("/Default");
                }
            }
            catch
            {
                Response.Redirect("/Default");
            }

            try
            {
                if (!IsPostBack)
                {
                    TextBox1.Text = DateTime.Now.ToString("yyyy-MM-dd");
                    TextBox2.Text = DateTime.Now.ToString("yyyy-MM-dd");
                    Button3_Click1(null,null);
                }
            }
            catch(Exception ex) {
                //Response.Redirect("/Default"); 
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('" + ex + "');", true);
            }

            
        }

        
        protected void GridView1_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            if (e.CommandName == "DeleteItem")
            {
                var deleteParams = new Dictionary<string, object>
                {
                    { "@ID", GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[0].Text }
                };

                // กันลบแถวขายที่ "ลงบัญชี NextAcc ไปแล้ว" — ลบดิบจะทำให้รายได้/สต๊อกบน NextAcc
                // ค้างเป็น phantom (ไม่มีการ reverse):
                //   - ขายแบบออกใบกำกับ (Account_Receipt_ID != '0') → ให้ไปยกเลิกใบเสร็จที่หน้าจัดการเอกสาร (void ตาม flow)
                //   - ขายที่ถูกรวบรายวันแล้ว (Pos_Rollup_Ref มีค่า ไม่ใช่ LEGACY) → รวมอยู่ในใบ POSDAY แล้ว
                DataTable chk = null;
                try
                {
                    chk = code.DatabaseQuerySafe(conn,
                        @"SELECT ISNULL(Account_Receipt_ID, '0') AS RcptId, Pos_Rollup_Ref
                          FROM [dbo].[Product_Out] WHERE ID = @ID", deleteParams);
                }
                catch
                {
                    // DB ยังไม่มีคอลัมน์ Pos_Rollup_Ref (ยังไม่รัน PHASE18_01) → เช็คเฉพาะใบกำกับ
                    try
                    {
                        chk = code.DatabaseQuerySafe(conn,
                            @"SELECT ISNULL(Account_Receipt_ID, '0') AS RcptId
                              FROM [dbo].[Product_Out] WHERE ID = @ID", deleteParams);
                    }
                    catch { chk = null; }
                }
                if (chk != null && chk.Rows.Count > 0)
                {
                    string rcptId = chk.Rows[0]["RcptId"]?.ToString() ?? "0";
                    string rollupRef = chk.Rows[0].Table.Columns.Contains("Pos_Rollup_Ref")
                        && chk.Rows[0]["Pos_Rollup_Ref"] != DBNull.Value
                        ? chk.Rows[0]["Pos_Rollup_Ref"].ToString() : "";

                    if (!string.IsNullOrEmpty(rcptId) && rcptId != "0")
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "delBlocked",
                            "alert('รายการนี้ออกใบกำกับ/ใบเสร็จแล้ว (" + rcptId + ") — ลบตรงนี้ไม่ได้\\nกรุณายกเลิกใบเสร็จที่หน้าจัดการเอกสาร (จะ void บน NextAcc ให้ด้วย)');", true);
                        return;
                    }
                    if (!string.IsNullOrEmpty(rollupRef) && rollupRef != "LEGACY")
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "delBlocked2",
                            "alert('รายการนี้ถูกรวบเข้าใบรับเงินสดรายวัน (" + rollupRef + ") และลงบัญชี NextAcc แล้ว — ลบตรงนี้ไม่ได้\\nต้องปรับปรุงผ่านเอกสารบัญชีแทน');", true);
                        return;
                    }
                }

                code.DatabaseInsertSafe(conn,
                    "DELETE FROM [dbo].[Product_Out] WHERE ID = @ID",
                    deleteParams);

                Button3_Click1(null, null);
            }
        }

        protected void Button3_Click1(object sender, EventArgs e)
        {
            // SECURE: SELECT with parameterized date range
            var selectParams = new Dictionary<string, object>
            {
                { "@StartDate", Convert.ToDateTime(TextBox1.Text) },
                { "@EndDate", Convert.ToDateTime(TextBox2.Text) }
            };

            DataTable dt = code.DatabaseQuerySafe(conn,
                "SELECT * FROM [Taketime].[dbo].[Product_Out] " +
                "INNER JOIN Product ON Product.ID = Product_ID " +
                "INNER JOIN Product_Category ON Product_Category.ID = Category_ID " +
                "LEFT JOIN Account_Paid_How ON Account_Paid_How.ID = Account_Paid_How_ID " +
                "WHERE CAST(DateTime_Out AS DATE) >= CAST(@StartDate AS DATE) " +
                "AND CAST(DateTime_Out AS DATE) <= CAST(@EndDate AS DATE) " +
                "ORDER BY Category_ID ASC",
                selectParams);
            try
            {
                dt.Columns.Add("Price_Total");
            }
            catch
            {

            }
            for(int i = 0;i<dt.Rows.Count;i++)
            {
                dt.Rows[i]["Price_Total"] = Convert.ToDouble(dt.Rows[i]["Amount"].ToString()) * Convert.ToDouble(dt.Rows[i]["PricePerUnit"]);
            }
            GridView1.DataSource = dt;
            GridView1.DataBind();

            // Calculate and display statistics
            LoadStatistics(dt);
        }

        private void LoadStatistics(DataTable dt)
        {
            if (dt == null || dt.Rows.Count == 0)
            {
                pnlStats.Visible = false;
                return;
            }

            pnlStats.Visible = true;

            // Calculate totals
            double totalSales = 0;
            int totalQuantity = 0;
            var uniqueReceipts = new HashSet<string>();

            foreach (DataRow row in dt.Rows)
            {
                totalSales += Convert.ToDouble(row["Price_Total"]);
                totalQuantity += Convert.ToInt32(row["Amount"]);

                string receiptId = row["Account_Receipt_ID"]?.ToString();
                if (!string.IsNullOrEmpty(receiptId) && receiptId != "0")
                {
                    uniqueReceipts.Add(receiptId);
                }
            }

            lblTotalSales.Text = totalSales.ToString("N2");
            lblTotalItems.Text = dt.Rows.Count.ToString("N0");
            lblTotalQuantity.Text = totalQuantity.ToString("N0");
            lblTotalReceipts.Text = uniqueReceipts.Count.ToString("N0");
        }
    }
}