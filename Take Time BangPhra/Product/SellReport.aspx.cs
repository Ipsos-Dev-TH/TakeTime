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
                // SECURE: DELETE with parameterized query
                var deleteParams = new Dictionary<string, object>
                {
                    { "@ID", GridView1.Rows[Convert.ToInt32(e.CommandArgument)].Cells[0].Text }
                };

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
        }
    }
}