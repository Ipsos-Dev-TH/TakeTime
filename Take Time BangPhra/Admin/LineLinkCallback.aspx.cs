using System;
using System.Configuration;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin
{
    /// <summary>
    /// ปลายทางที่ LINE ส่งผู้ใช้กลับมาหลังกดอนุญาต (Callback URL ที่ตั้งใน LINE Developers Console)
    /// รับ code + state → แลก token → อ่าน userId → ผูกกับ Admin ที่ล็อกอินอยู่
    /// </summary>
    public partial class LineLinkCallback : Page
    {
        private readonly string _conn = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;

        protected void Page_Load(object sender, EventArgs e)
        {
            // purpose = "link" (ผูกบัญชี ต้องล็อกอินอยู่) หรือ "login" (เข้าสู่ระบบด้วย LINE
            // จากลิงก์ในแชท — ยังไม่ได้ล็อกอินก็ได้ ระบบจะจับคู่จาก Line_UserId ที่ผูกไว้)
            string purpose = Session["LineLinkPurpose"]?.ToString() ?? "link";

            if (purpose == "link" && (Session["permission"]?.ToString() != "True" || Session["UserID"] == null))
            {
                Show(false, "ยังไม่ได้เข้าสู่ระบบ", "กรุณาเข้าสู่ระบบก่อนแล้วลองผูกบัญชีใหม่อีกครั้ง");
                return;
            }

            string error = Request.QueryString["error"];
            if (!string.IsNullOrEmpty(error))
            {
                string desc = Request.QueryString["error_description"] ?? "";
                Show(false, "ยกเลิกการผูกบัญชี",
                     Server.HtmlEncode(error == "access_denied" ? "คุณไม่ได้อนุญาตให้เข้าถึงข้อมูล" : error + " " + desc));
                return;
            }

            string code = Request.QueryString["code"];
            string state = Request.QueryString["state"];
            string expected = Session["LineLinkState"]?.ToString();
            Session["LineLinkState"] = null;   // ใช้ได้ครั้งเดียว

            // state ต้องตรงกับที่ออกให้ตอนเริ่ม — กัน CSRF
            if (string.IsNullOrEmpty(expected) || state != expected)
            {
                Show(false, "คำขอไม่ถูกต้อง",
                     "การยืนยันตัวตนหมดอายุหรือไม่ตรงกัน กรุณากดผูกบัญชีใหม่จากหน้าตั้งค่า");
                return;
            }

            var svc = new LineLoginService(_conn);

            // ── เข้าสู่ระบบด้วย LINE (กดลิงก์จากแชท) ────────────────────────────
            if (purpose == "login")
            {
                string returnUrl = Session["LineLinkReturn"]?.ToString();
                Session["LineLinkPurpose"] = null;
                Session["LineLinkReturn"] = null;

                var prof = svc.ResolveProfile(code);
                if (!prof.Success) { Show(false, "เข้าสู่ระบบไม่สำเร็จ", Server.HtmlEncode(prof.Message)); return; }

                var admin = svc.FindAdminByLineUserId(prof.UserId);
                if (admin == null)
                {
                    Show(false, "ยังไม่ได้ผูกบัญชี",
                         $"บัญชี LINE <b>{Server.HtmlEncode(prof.DisplayName)}</b> ยังไม่ได้ผูกกับผู้ใช้ในระบบ<br/>" +
                         "กรุณาเข้าสู่ระบบด้วยรหัสผ่านครั้งแรก แล้วไปที่เมนู \"บัญชี LINE ของฉัน\" เพื่อผูกบัญชี");
                    return;
                }

                Session["permission"] = "True";
                Session["UserID"] = admin["ID"].ToString();
                Session["UserName"] = admin["Username"]?.ToString();
                Session["User"] = admin["Role"]?.ToString();

                // เปิดเฉพาะ path ภายในเว็บเรา — กัน open redirect จากลิงก์ที่ถูกแก้
                if (!string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith("/") && !returnUrl.StartsWith("//"))
                {
                    Response.Redirect(returnUrl, false);
                    Context.ApplicationInstance.CompleteRequest();
                    return;
                }
                Show(true, "เข้าสู่ระบบสำเร็จ", $"ยินดีต้อนรับ {Server.HtmlEncode(admin["Username"]?.ToString())}");
                return;
            }

            // ── ผูกบัญชี ──────────────────────────────────────────────────────
            int adminId;
            if (!int.TryParse(Session["UserID"].ToString(), out adminId) || adminId <= 0)
            {
                Show(false, "ไม่พบผู้ใช้", "กรุณาเข้าสู่ระบบใหม่อีกครั้ง");
                return;
            }

            var result = svc.HandleCallback(code, adminId);

            if (result.Success)
            {
                Show(true, "ผูกบัญชี LINE สำเร็จ",
                     $"บัญชี <b>{Server.HtmlEncode(result.DisplayName)}</b> ถูกผูกกับผู้ใช้นี้แล้ว<br/>" +
                     "ระบบจะส่งแจ้งเตือนเข้าไลน์ส่วนตัวของคุณได้จากนี้<br/>" +
                     "<small>หากยังไม่ได้รับข้อความ กรุณาเพิ่ม LINE OA ของที่พักเป็นเพื่อนก่อน</small>");
            }
            else
            {
                Show(false, "ผูกบัญชีไม่สำเร็จ", Server.HtmlEncode(result.Message));
            }
        }

        private void Show(bool ok, string title, string detail)
        {
            litIcon.Text = ok ? "✅" : "⚠️";
            litTitle.Text = $"<span class='{(ok ? "ok" : "err")}'>{Server.HtmlEncode(title)}</span>";
            litDetail.Text = detail;
        }
    }
}
