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
            // ต้องล็อกอินอยู่ (การผูกบัญชีต้องรู้ว่าเป็นของใคร)
            if (Session["permission"]?.ToString() != "True" || Session["UserID"] == null)
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

            int adminId;
            if (!int.TryParse(Session["UserID"].ToString(), out adminId) || adminId <= 0)
            {
                Show(false, "ไม่พบผู้ใช้", "กรุณาเข้าสู่ระบบใหม่อีกครั้ง");
                return;
            }

            var svc = new LineLoginService(_conn);
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
