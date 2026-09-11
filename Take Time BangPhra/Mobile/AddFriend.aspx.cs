using System;
using System.Configuration;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Mobile
{
    /// <summary>
    /// ด่านบังคับเพิ่มเพื่อน LINE OA — LINE ไม่มี API ให้ "บังคับ" ตอนล็อกอิน (ผู้ใช้กดข้ามได้เสมอ)
    /// จึงใช้วิธี: ชวนตอน login (bot_prompt=aggressive) → ตรวจผลด้วย friendship API →
    /// ถ้ายังไม่เพิ่ม ให้ค้างที่หน้านี้จนกว่าจะเพิ่มจริง (เท่ากับบังคับในทางปฏิบัติ)
    ///
    /// เข้ามาที่นี่จาก 2 ทาง: หลัง LINE Login (มี access token ใน session)
    /// หรือหลังล็อกอินด้วยรหัสผ่านแล้วยังไม่ได้เป็นเพื่อน
    /// </summary>
    public partial class AddFriend : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        private LineLoginService _svc;

        protected void Page_Load(object sender, EventArgs e)
        {
            _svc = new LineLoginService(_conn);

            if (!IsPostBack)
            {
                string oa = _svc.BotBasicId;
                litOaName.Text = string.IsNullOrWhiteSpace(oa) ? "บัญชีทางการของที่พัก" : Server.HtmlEncode(oa);

                string addUrl = _svc.AddFriendUrl;
                if (string.IsNullOrEmpty(addUrl))
                {
                    // ยังไม่ได้ตั้ง Basic ID → บอกให้ผู้ดูแลตั้งค่า แทนที่จะปล่อยหน้าเปล่า
                    Msg("ผู้ดูแลยังไม่ได้ตั้ง LINE OA Basic ID ในหน้าตั้งค่า — กรุณาแจ้งผู้ดูแลระบบ", false);
                    lnkAdd.Visible = false;
                }
                else
                {
                    lnkAdd.NavigateUrl = addUrl;
                    string qr = _svc.AddFriendQrUrl;
                    if (!string.IsNullOrEmpty(qr))
                        litQr.Text = $"<img class='qr' src='{Server.HtmlEncode(qr)}' alt='QR เพิ่มเพื่อน' " +
                                     "onerror=\"this.style.display='none'\" />";
                }
            }
        }

        protected void btnRecheck_Click(object sender, EventArgs e)
        {
            string token = Session["LineAccessToken"]?.ToString();

            if (string.IsNullOrEmpty(token))
            {
                // ไม่มี token ให้ตรวจ (เช่นเข้ามาหลังล็อกอินด้วยรหัสผ่าน) → ให้ล็อกอิน LINE ใหม่
                // เพื่อดึงสถานะเพื่อนล่าสุด
                if (_svc.IsEnabled && _svc.IsConfigured)
                {
                    string state = Guid.NewGuid().ToString("N");
                    Session["LineLinkState"] = state;
                    Session["LineLinkPurpose"] = "login";
                    Session["LineLinkReturn"] = Session["LineAfterFriend"]?.ToString() ?? "/Mobile/Leave";
                    Response.Redirect(_svc.BuildAuthorizeUrl(state), false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                Msg("ตรวจสอบไม่ได้ กรุณาเข้าสู่ระบบด้วย LINE อีกครั้ง", false);
                return;
            }

            if (_svc.CheckIsFriend(token))
            {
                Session["LineFriendOk"] = "1";
                string ret = Session["LineAfterFriend"]?.ToString();
                Session["LineAfterFriend"] = null;
                string target = (!string.IsNullOrEmpty(ret) && ret.StartsWith("/") && !ret.StartsWith("//"))
                    ? ret : "/Mobile/Leave";
                Response.Redirect(target, false);
                Context.ApplicationInstance.CompleteRequest();
                return;
            }

            Msg("ยังไม่พบว่าเพิ่มเพื่อนแล้ว — กรุณาแตะปุ่มเขียวเพื่อเพิ่มใน LINE ก่อน แล้วกลับมากดตรวจสอบอีกครั้ง", false);
        }

        private void Msg(string text, bool ok)
        {
            pnlMsg.Visible = true;
            divMsg.Attributes["class"] = "msg " + (ok ? "ok" : "err");
            litMsg.Text = $"<i class='fas {(ok ? "fa-circle-check" : "fa-circle-exclamation")}'></i> {Server.HtmlEncode(text)}";
        }
    }
}
