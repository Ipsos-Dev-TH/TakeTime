using System;
using System.Web;
using System.Web.UI;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// ตัวช่วยยืนยันตัวตนสำหรับหน้า Mobile ที่เปิดจากลิงก์ในแชท LINE:
    /// ถ้ายังไม่ได้ล็อกอิน → พาไป LINE Login แล้วจับคู่กับ Admin ที่ผูก Line_UserId ไว้
    /// (บนมือถือผู้ใช้ล็อกอิน LINE อยู่แล้ว จึงแทบจะกดครั้งเดียวเข้าได้เลย)
    /// </summary>
    public static class MobileAuth
    {
        /// <summary>
        /// คืน adminId ถ้าล็อกอินอยู่แล้ว; ถ้ายัง จะ redirect ไป LINE Login (หรือหน้า login ปกติ
        /// ถ้ายังไม่ได้ตั้งค่า LINE Login) แล้วคืน 0 — ผู้เรียกต้อง return ทันทีเมื่อได้ 0
        /// </summary>
        public static int RequireAdmin(Page page, string connectionString)
        {
            var session = page.Session;
            if (session["permission"]?.ToString() == "True" && session["UserID"] != null
                && int.TryParse(session["UserID"].ToString(), out int id) && id > 0)
                return id;

            string returnUrl = page.Request.Url.PathAndQuery;
            var svc = new LineLoginService(connectionString);

            if (svc.IsEnabled && svc.IsConfigured)
            {
                string state = Guid.NewGuid().ToString("N");
                session["LineLinkState"] = state;
                session["LineLinkPurpose"] = "login";
                session["LineLinkReturn"] = returnUrl;
                Redirect(page, svc.BuildAuthorizeUrl(state));
            }
            else
            {
                Redirect(page, "/Admin/Login?returnUrl=" + HttpUtility.UrlEncode(returnUrl));
            }
            return 0;
        }

        private static void Redirect(Page page, string url)
        {
            // Page.Context เป็น protected internal — เรียกจากคลาสภายนอกไม่ได้ (CS0122)
            // ใช้ HttpContext.Current แทน (เป็น context เดียวกันของ request นี้)
            page.Response.Redirect(url, false);
            HttpContext.Current?.ApplicationInstance?.CompleteRequest();
        }
    }
}
