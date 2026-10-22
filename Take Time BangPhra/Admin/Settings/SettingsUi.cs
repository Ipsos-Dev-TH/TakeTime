using System;
using System.Text.RegularExpressions;
using System.Web;

namespace Take_Time_BangPhra.Admin.Settings
{
    /// <summary>
    /// ตัวช่วยหน้าตาร่วมของหน้าตั้งค่า (ศูนย์ตั้งค่า / นโยบายการจอง / สัตว์เลี้ยงเข้าพัก …)
    /// — ไม่มีตรรกะธุรกิจ แค่จัดรูปข้อความให้ทุกหน้าพูดภาษาเดียวกัน
    /// คู่กับ /Content/admin-settings.css และ /Scripts/admin-settings.js
    /// </summary>
    public static class SettingsUi
    {
        /// <summary>ต่อท้าย URL ของ css/js ส่วนกลาง — เปลี่ยนเมื่อแก้ไฟล์ จะได้ไม่ติด cache เก่า</summary>
        public const string AssetVersion = "20261001";

        /// <summary>
        /// ข้อความตัวอย่างที่ยังไม่ได้แก้ในนโยบาย: ช่อง "[แก้ไข: …]" และบรรทัด "⚠ ตัวอย่าง — …"
        /// (ข้อความตั้งต้นจาก PHASE19_Migration_22 / 23) — ใช้ทั้งนับและไฮไลต์
        /// รูปเดียวกับ PH_RE_SRC ใน /Scripts/admin-settings.js
        /// </summary>
        private static readonly Regex Placeholder =
            new Regex(@"\[แก้ไข[^\]]*\]|⚠?\s*ตัวอย่าง —[^\n<]*", RegexOptions.Compiled);

        public static int CountPlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            try { return Placeholder.Matches(text).Count; }
            catch { return 0; }
        }

        /// <summary>ใส่ &lt;mark&gt; รอบข้อความตัวอย่างใน HTML ที่ encode แล้ว (ผลจาก BookingPolicy.ToHtml)</summary>
        public static string HighlightPlaceholders(string html)
        {
            if (string.IsNullOrEmpty(html)) return html ?? "";
            try { return Placeholder.Replace(html, "<mark class=\"as-ph\">$0</mark>"); }
            catch { return html; }
        }

        /// <summary>ป้ายสถานะ — cls: ok | warn | err | off | info</summary>
        public static string Chip(string text, string cls)
        {
            return "<span class=\"as-chip " + (cls ?? "") + "\">" + HttpUtility.HtmlEncode(text ?? "") + "</span>";
        }

        /// <summary>
        /// ตัวระบุ "หน้านี้มีค่าที่ยังไม่ได้บันทึก" — ใส่ต่อท้ายข้อความแจ้งเมื่อบันทึกไม่ผ่าน
        /// (สคริปต์ส่วนกลางจะเตือนก่อนออกจากหน้า)
        /// </summary>
        public const string StartDirtyMarker = "<span data-as-startdirty=\"1\" hidden></span>";
    }
}
