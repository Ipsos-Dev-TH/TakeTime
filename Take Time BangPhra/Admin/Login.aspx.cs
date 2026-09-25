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
            bool loggedIn = false;
            try
            {
                if (Session["permission"].ToString() == "True")
                {
                    // โหมดเปลี่ยนรหัสผ่าน (ล็อกอินอยู่แล้ว)
                    loggedIn = true;
                    Button1.Visible = false;
                    Button2.Visible = true;
                    Label9.Text = "Password";
                    Label10.Text = "Confirm Pssword";
                    litTitle.Text = "เปลี่ยนรหัสผ่าน";
                    litSub.Text = "ตั้งรหัสผ่านใหม่สำหรับบัญชีของคุณ";
                }
            }
            catch
            {
                Button1.Visible = true;
                Button2.Visible = false;
                Label9.Text = "User";
                Label10.Text = "Pssword";
            }

            // ปุ่ม "เข้าสู่ระบบด้วย LINE" — โชว์เฉพาะตอนยังไม่ล็อกอิน และตั้งค่า LINE Login ครบแล้ว
            if (!loggedIn)
            {
                try
                {
                    var line = new Take_Time_BangPhra.Services.LineLoginService(conn);
                    pnlLineLogin.Visible = line.IsEnabled && line.IsConfigured;
                }
                catch { pnlLineLogin.Visible = false; }
            }
        }

        /// <summary>
        /// เข้าสู่ระบบด้วย LINE — จับคู่จาก Line_UserId ที่ผูกไว้
        /// ยังไม่เคยผูก → ระบบพาไปหน้าเลือกชื่อตัวเองให้เอง (Mobile/LineLink)
        /// </summary>
        protected void btnLineLogin_Click(object sender, EventArgs e)
        {
            try
            {
                var line = new Take_Time_BangPhra.Services.LineLoginService(conn);
                if (!line.IsEnabled || !line.IsConfigured)
                {
                    ClientScript.RegisterStartupScript(GetType(), "nocfg",
                        "alert('ยังไม่ได้ตั้งค่า LINE Login กรุณาติดต่อผู้ดูแลระบบ');", true);
                    return;
                }

                // กลับไปหน้าที่ตั้งใจจะไป (ถ้าถูกส่งมาจากหน้าอื่น) — เฉพาะ path ภายใน กัน open redirect
                string ret = Request.QueryString["returnUrl"];
                if (string.IsNullOrEmpty(ret) || !ret.StartsWith("/") || ret.StartsWith("//"))
                    ret = "/ReserveTable";

                string state = Guid.NewGuid().ToString("N");
                Session["LineLinkState"] = state;
                Session["LineLinkPurpose"] = "login";
                Session["LineLinkReturn"] = ret;
                Response.Redirect(line.BuildAuthorizeUrl(state), false);
                Context.ApplicationInstance.CompleteRequest();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("LINE login start error: " + ex.Message);
                ClientScript.RegisterStartupScript(GetType(), "lnerr",
                    "alert('เริ่มเข้าสู่ระบบด้วย LINE ไม่สำเร็จ กรุณาลองใหม่');", true);
            }
        }

        protected void Button1_Click(object sender, EventArgs e)
        {
            try
            {
                string username = TextBox1.Text?.Trim()?.ToLower() ?? "";
                string password = TextBox2.Text ?? "";

                if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('กรุณากรอกชื่อผู้ใช้และรหัสผ่าน');", true);
                    return;
                }

                // Query user by username only — password is verified separately against
                // the stored hash (SecurityHelper.VerifyPassword handles both PBKDF2 hashes
                // and legacy plain-text passwords)
                var parameters = new Dictionary<string, object>
                {
                    { "@username", username }
                };

                code code2 = new code();
                DataTable dtAdmin = code2.DatabaseQuerySafe(conn,
                    "SELECT * FROM Admin WHERE Username = @username AND Status = 1",
                    parameters);

                bool passwordValid = dtAdmin.Rows.Count >= 1
                    && SecurityHelper.VerifyPassword(password, dtAdmin.Rows[0]["Password"]?.ToString() ?? "");

                if (passwordValid)
                {
                    string adminId = dtAdmin.Rows[0]["ID"].ToString();

                    // Upgrade legacy plain-text password to a hash on successful login
                    string storedPwd = dtAdmin.Rows[0]["Password"]?.ToString() ?? "";
                    if (!storedPwd.Contains("."))
                    {
                        try
                        {
                            code2.DatabaseInsertSafe(conn,
                                "UPDATE [dbo].[Admin] SET [Password] = @hash WHERE ID = @id",
                                new Dictionary<string, object>
                                {
                                    { "@hash", SecurityHelper.HashPassword(password) },
                                    { "@id", adminId }
                                });
                        }
                        catch { /* non-critical — login still succeeds */ }
                    }

                    Session["permission"] = "True";
                    Session["UserName"] = username;
                    Session["User"] = dtAdmin.Rows[0]["Role"].ToString();
                    Session["UserID"] = adminId;

                    // Log successful login
                    code2.Logs(conn, "Admin-Login-Success", username, username);
                    Response.Redirect("/ReserveTable.aspx");
                }
                else
                {
                    // Log failed login attempt
                    code2.Logs(conn, "Admin-Login-Failed", username, username);
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
                string newPassword = TextBox1.Text ?? "";
                string confirmPassword = TextBox2.Text ?? "";

                if (newPassword != confirmPassword)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('รหัสผ่านยืนยันไม่ตรงกัน');", true);
                    return;
                }

                if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 4)
                {
                    ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('รหัสผ่านต้องมีอย่างน้อย 4 ตัวอักษร');", true);
                    return;
                }

                // Store the hashed password (consistent with employee creation/update)
                var parameters = new Dictionary<string, object>
                {
                    { "@password", SecurityHelper.HashPassword(newPassword) },
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
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Admin Password Change Error: {ex.Message}");
                ClientScript.RegisterStartupScript(this.GetType(), "myalert", "alert('เกิดข้อผิดพลาด กรุณาลองใหม่อีกครั้ง');", true);
            }
        }
    }
}