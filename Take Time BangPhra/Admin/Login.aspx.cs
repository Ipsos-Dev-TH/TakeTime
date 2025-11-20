using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Admin
{
    public partial class Login : System.Web.UI.Page
    {
        _Default code = new _Default();
        string conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                if (Session["permission"].ToString() == "True")
                {
                    //Response.Redirect("/ReserveTable.aspx");
                    Button1.Visible = false;
                    Button2.Visible = true;
                    Label9.Text = "Password";
                    Label10.Text = "Confirm Pssword";
                }
            }
            catch
            {
                Button1.Visible = true;
                Button2.Visible = false;
                Label9.Text = "User";
                Label10.Text = "Pssword";
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                // 🔒 SECURE: Using parameterized query to prevent SQL Injection
                // ⚡ OPTIMIZED: Query database with WHERE clause instead of fetching all and looping
                var parameters = new Dictionary<string, object>
                {
                    { "@username", TextBox1.Text?.Trim() ?? "" },
                    { "@password", TextBox2.Text ?? "" }
                };

                // Query only matching records instead of all active admins
                code code2 = new code();
                DataTable dtAdmin = code2.DatabaseQuerySafe(conn,
                    "SELECT * FROM Admin WHERE Username = @username AND Password = @password AND Status = 1",
                    parameters);

                if (dtAdmin.Rows.Count >= 1)
                {
                    Session["permission"] = "True";
                    Session["UserName"] = TextBox1.Text.ToLower();
                    Session["User"] = dtAdmin.Rows[0]["Role"].ToString();
                    Session["UserID"] = dtAdmin.Rows[0]["ID"].ToString();

                    // Log successful login
                    code2.Logs(conn, "Admin-Login-Success", TextBox1.Text, TextBox1.Text);
                    Response.Redirect("/ReserveTable.aspx");
                }
                else
                {
                    // Log failed login attempt
                    code2.Logs(conn, "Admin-Login-Failed", TextBox1.Text, TextBox1.Text);
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง');", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Admin Login Error: {ex.Message}");
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง');", true);
            }
        }

        protected void Button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (TextBox1.Text == TextBox2.Text)
                {
                    // 🔒 SECURE: Using parameterized query to prevent SQL Injection
                    var parameters = new Dictionary<string, object>
                    {
                        { "@password", TextBox2.Text },
                        { "@userId", Session["UserID"] }
                    };

                    code code2 = new code();
                    code2.DatabaseInsertSafe(conn,
                        "UPDATE [dbo].[Admin] SET [Password] = @password WHERE ID = @userId",
                        parameters);

                    // Log password change
                    code2.Logs(conn, "Admin-Password-Changed", "User ID: " + Session["UserID"], Session["UserName"]?.ToString() ?? "Unknown");

                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เปลี่ยนรหัสผ่านสำเร็จ');", true);
                    Session.Clear();
                    Response.Redirect("/Admin/Login");
                }
                else
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('รหัสผ่านยืนยันไม่ตรงกัน');", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Admin Password Change Error: {ex.Message}");
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง');", true);
            }
        }
    }
}