using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Data;
using System.Text.RegularExpressions;
using Microsoft.ReportingServices.ReportProcessing.ReportObjectModel;

namespace Take_Time_BangPhra.Affiliate
{
    public partial class Login : System.Web.UI.Page
    {
        code code = new code();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            if (!IsPostBack)
            {
                string id = Request.QueryString["id"];
                try
                {
                    if (id.Length > 0)
                    {
                        ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('สมัครเรียบร้อยแล้ว กรุณา Login เพื่อรับรหัสส่วนลด');", true);
                    }
                    TextBox1.Text = id;
                }
                catch { }
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                // 🔒 SECURE: Using parameterized query to prevent SQL Injection
                // 🐛 FIXED: Changed >= 0 to >= 1 (was allowing everyone to login!)
                var parameters = new Dictionary<string, object>
                {
                    { "@username", TextBox1.Text?.Trim() ?? "" },
                    { "@password", TextBox2.Text ?? "" }
                };

                DataTable dtUser = code.DatabaseQuerySafe(conn,
                    @"SELECT * FROM [Taketime].[dbo].[Affiliate_Member]
                      WHERE (ID_Number = @username OR Coupon_Code = @username)
                      AND Password = @password",
                    parameters);

                // CRITICAL FIX: Changed >= 0 to >= 1
                // Previous code: if(dtUser.Rows.Count >= 0) was ALWAYS true!
                if (dtUser.Rows.Count >= 1)
                {
                    Session["AffiliateID"] = dtUser.Rows[0]["ID_Number"].ToString();
                    code.Logs(conn, "Affiliate-Login-Success", TextBox1.Text, TextBox1.Text);
                    Response.Redirect("../Affiliate/Default");
                }
                else
                {
                    code.Logs(conn, "Affiliate-Login-Failed", TextBox1.Text, TextBox1.Text);
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');", true);
                }
            }
            catch (Exception ex)
            {
                // Log the actual error for debugging
                System.Diagnostics.Trace.TraceError($"Affiliate Login Error: {ex.Message}");
                code.Logs(conn, "Affiliate-Login-Error", TextBox1.Text + " - " + ex.Message, TextBox1.Text);
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');", true);
            }
        }

        
    }
}