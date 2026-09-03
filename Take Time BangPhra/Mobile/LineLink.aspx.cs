using System;
using System.Configuration;
using System.Data;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Mobile
{
    /// <summary>
    /// ผูกบัญชี LINE ครั้งแรก — เข้ามาหลังล็อกอิน LINE สำเร็จ แต่ LINE นี้ยังไม่ผูกกับใคร
    ///
    /// ⚠️ ความปลอดภัย: ต้องกรอก "ชื่อผู้ใช้ + รหัสผ่าน" ของตัวเองเสมอ
    /// เดิมให้เลือกชื่อจากรายการ ซึ่งเปิดเผยรายชื่อพนักงาน/username/ตำแหน่งทั้งหมด
    /// ให้คนแปลกหน้าที่ล็อกอิน LINE เห็น — เปลี่ยนมาเป็นกรอกเองเพื่อไม่ให้หลุดข้อมูล
    /// และกันคนนอกสวมสิทธิ์บัญชีในระบบ
    /// </summary>
    public partial class LineLink : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private readonly code _code = new code();
        private LineLoginService _svc;

        /// <summary>โปรไฟล์ LINE ที่ callback เก็บไว้ให้ (session — ไม่ส่งผ่าน URL)</summary>
        private LineLoginService.LinkResult Profile => Session["LinePendingProfile"] as LineLoginService.LinkResult;

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new LineLoginService(_conn);

            var p = Profile;
            if (p == null || string.IsNullOrWhiteSpace(p.UserId))
            {
                StartLineLogin();   // ไม่มีข้อมูล LINE ในเซสชัน → เริ่ม flow ใหม่
                return;
            }

            if (!IsPostBack)
            {
                litAvatar.Text = string.IsNullOrWhiteSpace(p.PictureUrl)
                    ? "<i class='fab fa-line' style='font-size:56px;'></i>"
                    : $"<img src='{Server.HtmlEncode(p.PictureUrl)}' alt='' />";
                litLineName.Text = Server.HtmlEncode(p.DisplayName ?? "บัญชี LINE");

                // ผูกไปแล้วระหว่างทาง → เข้าระบบให้เลย
                var already = _svc.FindAdminByLineUserId(p.UserId);
                if (already != null) SignInAndGo(already);
            }
        }

        private void StartLineLogin()
        {
            if (!_svc.IsEnabled || !_svc.IsConfigured)
            {
                Response.Redirect("~/Admin/Login", false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }
            string state = Guid.NewGuid().ToString("N");
            Session["LineLinkState"] = state;
            Session["LineLinkPurpose"] = "login";
            Session["LineLinkReturn"] = "/Mobile/LineLink";
            Response.Redirect(_svc.BuildAuthorizeUrl(state), false);
            Context.ApplicationInstance.CompleteRequest();
        }

        /// <summary>ยืนยันด้วย username+password ของบัญชีระบบ แล้วผูกกับ LINE ปัจจุบัน</summary>
        protected void btnLinkNow_Click(object sender, EventArgs e)
        {
            var p = Profile;
            if (p == null || string.IsNullOrWhiteSpace(p.UserId))
            { StartLineLogin(); return; }

            string username = (txtUsername.Text ?? "").Trim();
            string password = txtPassword.Text ?? "";
            txtPassword.Text = "";

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            { Msg("กรุณากรอกชื่อผู้ใช้และรหัสผ่าน", false); return; }

            // หาบัญชีจาก username (ไม่บอกว่าชื่อผู้ใช้มีจริงไหม — กันเดาชื่อผู้ใช้)
            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID FROM [dbo].[Admin] WHERE Username = @u AND Status = 1",
                new System.Collections.Generic.Dictionary<string, object> { { "@u", username } });

            if (dt == null || dt.Rows.Count == 0)
            {
                _code.Logs(_conn, "LineLogin",
                    $"ผูกบัญชีล้มเหลว: ไม่พบผู้ใช้ '{username}' (line {LineLoginService.Mask(p.UserId)})", "SYSTEM");
                Msg("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง", false);
                return;
            }

            int adminId = Convert.ToInt32(dt.Rows[0]["ID"]);
            var (ok, msg) = _svc.LinkWithPassword(adminId, password, p);
            if (!ok) { Msg(msg, false); return; }

            var admin = _svc.FindAdminByLineUserId(p.UserId);
            if (admin != null) { SignInAndGo(admin); return; }

            ShowDone("ผูกบัญชีสำเร็จ", "คุณจะได้รับแจ้งเตือนจากระบบทาง LINE นี้");
        }

        private void ShowDone(string title, string text)
        {
            pnlVerify.Visible = false;
            pnlDone.Visible = true;
            litDoneTitle.Text = Server.HtmlEncode(title);
            litDoneText.Text = Server.HtmlEncode(text);
        }

        private void SignInAndGo(DataRow admin)
        {
            Session["permission"] = "True";
            Session["UserID"] = admin["ID"].ToString();
            Session["UserName"] = admin["Username"]?.ToString();
            Session["User"] = admin["Role"]?.ToString();
            Session["LinePendingProfile"] = null;

            string ret = Session["LineAfterLink"]?.ToString();
            Session["LineAfterLink"] = null;
            string target = (!string.IsNullOrEmpty(ret) && ret.StartsWith("/") && !ret.StartsWith("//"))
                ? ret : "/Mobile/Leave";
            Response.Redirect(target, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "msg " + (ok ? "ok" : "err");
            litMsg.Text = $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> {Server.HtmlEncode(text)}";
        }
    }
}
