using System;
using System.Configuration;
using System.Data;
using System.Text;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Mobile
{
    /// <summary>
    /// หน้า "เลือกชื่อตัวเองเพื่อผูกบัญชี LINE" — เข้ามาหลังล็อกอิน LINE สำเร็จ
    /// แต่ยังไม่มีบัญชีระบบผูกไว้ (ไม่ต้องเข้าระบบด้วยรหัสผ่านมาก่อน)
    ///
    /// ⚠️ ความปลอดภัย: เลือกชื่อแล้วผูกทันทีไม่ได้ เพราะใครก็ตามที่มี LINE จะสวมสิทธิ์
    /// บัญชี Owner ได้ จึงต้องยืนยันทางใดทางหนึ่ง — ใส่รหัสผ่านเอง (ผูกทันที)
    /// หรือส่งคำขอให้ผู้ดูแลกดอนุมัติ (สำหรับคนที่จำรหัสไม่ได้)
    /// </summary>
    public partial class LineLink : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private LineLoginService _svc;

        /// <summary>โปรไฟล์ LINE ที่ callback เก็บไว้ให้ (session — ไม่ส่งผ่าน URL)</summary>
        private LineLoginService.LinkResult Profile => Session["LinePendingProfile"] as LineLoginService.LinkResult;

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new LineLoginService(_conn);

            var p = Profile;
            if (p == null || string.IsNullOrWhiteSpace(p.UserId))
            {
                // ไม่มีข้อมูล LINE ในเซสชัน → เริ่ม flow ใหม่
                StartLineLogin();
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
                if (already != null) { SignInAndGo(already); return; }

                LoadPeople();
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

        // ── ขั้นที่ 1: รายชื่อให้เลือก ────────────────────────────────────────────
        private void LoadPeople()
        {
            var sb = new StringBuilder();
            try
            {
                DataTable dt = _svc.GetUnlinkedAdmins();
                if (dt == null || dt.Rows.Count == 0)
                {
                    litPeople.Text = "<div class='empty'>ทุกบัญชีถูกผูกกับ LINE ไปหมดแล้ว<br/>" +
                                     "หากนี่คือบัญชีของคุณ กรุณาติดต่อผู้ดูแลระบบ</div>";
                    return;
                }
                foreach (DataRow r in dt.Rows)
                {
                    string id = r["ID"].ToString();
                    string full = r["FullName"]?.ToString() ?? "";
                    string user = r["Username"]?.ToString() ?? "";
                    string role = r["Role"]?.ToString() ?? "";
                    string search = Server.HtmlEncode((full + " " + user + " " + role).ToLowerInvariant());
                    sb.Append($"<button type='button' class='who' data-s='{search}' " +
                              $"onclick=\"pick(this,'{id}','')\">" +
                              $"<b>{Server.HtmlEncode(full)}</b>" +
                              $"<small>{Server.HtmlEncode(user)} · {Server.HtmlEncode(role)}</small></button>");
                }
            }
            catch (Exception ex)
            {
                sb.Append($"<div class='empty'>{Server.HtmlEncode(ex.Message)}</div>");
            }
            litPeople.Text = sb.ToString();
        }

        /// <summary>เลือกชื่อแล้ว (postback จาก hidden field) → ไปขั้นยืนยัน</summary>
        protected void Page_LoadComplete(object sender, EventArgs e)
        {
            // ตรวจว่ามีการเลือกชื่อจาก JS หรือไม่ (postback ที่ target = hidden field)
            if (IsPostBack && Request["__EVENTTARGET"] == hfPicked.UniqueID)
                ShowConfirmStep();
        }

        private void ShowConfirmStep()
        {
            if (!int.TryParse(hfPicked.Value, out int adminId) || adminId <= 0) return;

            var dt = new code().DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 ISNULL(NULLIF(LTRIM(RTRIM(ISNULL(FirstName,'') + ' ' + ISNULL(LastName,''))), ''), Username) AS FullName,
                         Username, Role
                    FROM [dbo].[Admin] WHERE ID = @id AND Status = 1",
                new System.Collections.Generic.Dictionary<string, object> { { "@id", adminId } });
            if (dt == null || dt.Rows.Count == 0) { Msg("ไม่พบบัญชีนี้", false); return; }

            litPickedName.Text = Server.HtmlEncode(dt.Rows[0]["FullName"]?.ToString()) +
                                 $" <small style='color:#7d8f9c;'>({Server.HtmlEncode(dt.Rows[0]["Username"]?.ToString())})</small>";
            pnlPick.Visible = false;
            pnlConfirm.Visible = true;
            pnlDone.Visible = false;
        }

        // ── ขั้นที่ 2: ยืนยัน ─────────────────────────────────────────────────────
        protected void btnLinkNow_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfPicked.Value, out int adminId) || adminId <= 0)
            { Msg("กรุณาเลือกชื่อของคุณก่อน", false); ResetToPick(); return; }

            var (ok, msg) = _svc.LinkWithPassword(adminId, txtPassword.Text, Profile);
            txtPassword.Text = "";

            if (!ok) { Msg(msg, false); ShowConfirmStep(); return; }

            var admin = _svc.FindAdminByLineUserId(Profile.UserId);
            if (admin != null) { SignInAndGo(admin); return; }

            ShowDone("ผูกบัญชีสำเร็จ", "คุณจะได้รับแจ้งเตือนจากระบบทาง LINE นี้");
        }

        protected void btnAskApproval_Click(object sender, EventArgs e)
        {
            if (!int.TryParse(hfPicked.Value, out int adminId) || adminId <= 0)
            { Msg("กรุณาเลือกชื่อของคุณก่อน", false); ResetToPick(); return; }

            var (ok, msg) = _svc.RequestLinkApproval(adminId, Profile);
            if (!ok) { Msg(msg, false); ShowConfirmStep(); return; }

            ShowDone("ส่งคำขอแล้ว",
                "ผู้ดูแลระบบได้รับคำขอของคุณแล้ว เมื่ออนุมัติจะมีข้อความแจ้งกลับมาทาง LINE นี้");
        }

        protected void btnBack_Click(object sender, EventArgs e)
        {
            hfPicked.Value = "";
            ResetToPick();
        }

        private void ResetToPick()
        {
            pnlConfirm.Visible = false;
            pnlDone.Visible = false;
            pnlPick.Visible = true;
            LoadPeople();
        }

        private void ShowDone(string title, string text)
        {
            pnlPick.Visible = false;
            pnlConfirm.Visible = false;
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
