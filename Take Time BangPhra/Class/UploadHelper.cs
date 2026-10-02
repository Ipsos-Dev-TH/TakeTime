using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// อัปโหลดไฟล์แบบปลอดภัย — ใช้กับทุกจุดที่รับไฟล์จากผู้ใช้ (สลิป/รูป/เอกสาร)
    ///
    /// ⚠️ เหตุผล: จุดอัปโหลดเดิมบางที่บันทึกไฟล์ด้วย "ชื่อไฟล์เดิมจากผู้ใช้" ลงโฟลเดอร์ที่เว็บเข้าถึงได้
    /// (เช่น /Uploads/PaymentSlips) โดยไม่ตรวจนามสกุล → ผู้ใช้แนบ x.aspx แล้วเปิด URL = รันโค้ดบนเซิร์ฟเวอร์
    /// (web shell / RCE) และชื่อไฟล์ที่มี ../ ยังทำ path traversal ได้
    ///
    /// ตัวช่วยนี้: (1) ตรวจนามสกุลกับ whitelist (2) จำกัดขนาด (3) สร้างชื่อไฟล์ใหม่เองเสมอ
    /// (ไม่เอาชื่อจากผู้ใช้) → กันทั้ง 2 ช่องโหว่
    /// </summary>
    public static class UploadHelper
    {
        /// <summary>นามสกุลรูป/สลิป/เอกสารที่ยอมรับตามค่าเริ่มต้น</summary>
        public static readonly string[] ImageDoc = { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf" };
        public static readonly string[] ImageOnly = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        public class SaveResult
        {
            public bool Success;
            public string WebPath;     // path สำหรับเก็บลง DB/แสดงผล (เริ่มด้วย /)
            public string Error;       // ข้อความไทยเมื่อไม่ผ่าน
        }

        /// <summary>
        /// บันทึกไฟล์ที่อัปโหลดอย่างปลอดภัย ลงโฟลเดอร์ virtual (เช่น "~/Uploads/PaymentSlips")
        /// คืน WebPath (เช่น /Uploads/PaymentSlips/xxxx.jpg) เมื่อสำเร็จ
        /// </summary>
        /// <param name="prefix">คำนำหน้าชื่อไฟล์ที่ระบบสร้าง (ไม่ใช่ชื่อจากผู้ใช้) เช่น "Payment_12"</param>
        /// <param name="allowed">whitelist นามสกุล (null = ImageDoc)</param>
        /// <param name="maxBytes">ขนาดสูงสุด (default 8MB)</param>
        public static SaveResult Save(FileUpload upload, string virtualFolder, string prefix,
            string[] allowed = null, long maxBytes = 8 * 1024 * 1024)
        {
            if (upload == null || !upload.HasFile)
                return new SaveResult { Success = false, Error = "ไม่พบไฟล์ที่แนบมา" };

            allowed = allowed ?? ImageDoc;
            string ext = (Path.GetExtension(upload.FileName) ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                return new SaveResult
                {
                    Success = false,
                    Error = "รองรับเฉพาะไฟล์: " + string.Join(", ", allowed)
                };

            if (upload.PostedFile == null || upload.PostedFile.ContentLength <= 0)
                return new SaveResult { Success = false, Error = "ไฟล์ว่างเปล่า" };
            if (upload.PostedFile.ContentLength > maxBytes)
                return new SaveResult { Success = false, Error = $"ไฟล์ใหญ่เกิน {maxBytes / (1024 * 1024)} MB" };

            try
            {
                string absFolder = HttpContext.Current.Server.MapPath(virtualFolder);
                Directory.CreateDirectory(absFolder);

                // ชื่อไฟล์สร้างเองล้วน — ไม่มีส่วนใดจากผู้ใช้ → กัน path traversal + เดาชื่อไม่ได้
                string safePrefix = SanitizePrefix(prefix);
                string name = $"{safePrefix}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}".TrimStart('_') + ext;
                upload.SaveAs(Path.Combine(absFolder, name));

                string webBase = virtualFolder.TrimStart('~').TrimEnd('/');
                return new SaveResult { Success = true, WebPath = webBase + "/" + name };
            }
            catch (Exception ex)
            {
                return new SaveResult { Success = false, Error = "บันทึกไฟล์ไม่สำเร็จ: " + ex.Message };
            }
        }

        /// <summary>
        /// เหมือน Save(FileUpload…) แต่รับ HttpPostedFile ตรง ๆ — ใช้ในตัวจัดการ .ashx
        /// (เช่น แชทหน้าเว็บที่แนบสลิป) ที่ไม่มี control FileUpload
        /// </summary>
        public static SaveResult Save(HttpPostedFile file, string virtualFolder, string prefix,
            string[] allowed = null, long maxBytes = 8 * 1024 * 1024)
        {
            if (file == null || file.ContentLength <= 0)
                return new SaveResult { Success = false, Error = "ไม่พบไฟล์ที่แนบมา" };

            allowed = allowed ?? ImageDoc;
            string ext = (Path.GetExtension(file.FileName) ?? "").ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
                return new SaveResult { Success = false, Error = "รองรับเฉพาะไฟล์: " + string.Join(", ", allowed) };
            if (file.ContentLength > maxBytes)
                return new SaveResult { Success = false, Error = $"ไฟล์ใหญ่เกิน {maxBytes / (1024 * 1024)} MB" };

            try
            {
                string absFolder = HttpContext.Current.Server.MapPath(virtualFolder);
                Directory.CreateDirectory(absFolder);
                string safePrefix = SanitizePrefix(prefix);
                string name = $"{safePrefix}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}".TrimStart('_') + ext;
                file.SaveAs(Path.Combine(absFolder, name));
                string webBase = virtualFolder.TrimStart('~').TrimEnd('/');
                return new SaveResult { Success = true, WebPath = webBase + "/" + name };
            }
            catch (Exception ex)
            {
                return new SaveResult { Success = false, Error = "บันทึกไฟล์ไม่สำเร็จ: " + ex.Message };
            }
        }

        private static string SanitizePrefix(string prefix)
        {
            if (string.IsNullOrEmpty(prefix)) return "file";
            var chars = prefix.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray();
            string s = new string(chars);
            return string.IsNullOrEmpty(s) ? "file" : (s.Length > 40 ? s.Substring(0, 40) : s);
        }
    }
}
