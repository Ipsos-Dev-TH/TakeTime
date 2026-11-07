using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// สรุปนโยบาย (ยกเลิก/คืนเงิน) จากข้อความอิสระใน <see cref="BookingPolicy"/> ให้เป็นรายการข้อสั้น ๆ
    /// ที่อ่านง่ายบนหน้ายืนยันการจองและในรูปยืนยันการจอง — แหล่งข้อความเดียวกับหน้าจอง/หน้า /Policy
    ///
    /// · บรรทัดละ 1 ข้อ (ตัดบรรทัดว่าง + เครื่องหมายนำหน้า • - 1. ๑.) · ข้อแรก ๆ = "กฎสำคัญ" แสดงเสมอ ที่เหลือพับไว้
    /// · ตัวเลขที่ตัดสินผล (จำนวนวัน / % / บาท / ชั่วโมง) ถูกเน้นให้สแกนเห็นเร็ว · **ตัวหนา** ใช้ได้เหมือนเดิม
    /// · encode ก่อนเสมอ (ผ่าน <see cref="HttpUtility.HtmlEncode(string)"/>) — แทรก HTML/สคริปต์จากข้อความไม่ได้
    /// · โค้ดบริสุทธิ์ ไม่แตะ DB (ทดสอบบน mono ได้)
    /// </summary>
    public static class PolicySummary
    {
        /// <summary>จำนวนข้อที่แสดงทันทีก่อนพับ "อ่านเพิ่มเติม"</summary>
        public const int DefaultKeyLines = 4;

        private static readonly Regex LeadMarker = new Regex(
            @"^\s*(?:[•\-–—*·●○■►▪✓✔]|\(?(?:\d{1,2}|[๐-๙]{1,2})[.)]|\(\d{1,2}\))\s*", RegexOptions.Compiled);

        // ตัวเลข + หน่วยที่ตัดสินผลของนโยบาย: "7 วัน", "50%", "100 %", "1,500 บาท", "24 ชั่วโมง", "48 ชม."
        private static readonly Regex NumberUnit = new Regex(
            @"(\d[\d,]*(?:\.\d+)?)\s*(%|เปอร์เซ็นต์|วัน|คืน|ชั่วโมง|ชม\.|บาท|฿)", RegexOptions.Compiled);

        private static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        /// <summary>แยกข้อความเป็นรายการข้อ: ตัดบรรทัดว่างและเครื่องหมายนำหน้า (ไม่ encode — ข้อความดิบ)</summary>
        public static List<string> Lines(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;
            string s = text.Replace("\r\n", "\n").Replace("\r", "\n");
            foreach (string raw in s.Split('\n'))
            {
                string line = LeadMarker.Replace(raw ?? "", "").Trim();
                if (line.Length == 0) continue;
                list.Add(line);
            }
            return list;
        }

        /// <summary>
        /// HTML ของข้อเดียว: encode → **ตัวหนา** → เน้นตัวเลขที่ตัดสินผล (ทำบน HTML ที่ encode แล้ว —
        /// ตัวเลข/% ไม่ถูก encode จึงจับได้ปลอดภัย)
        /// </summary>
        public static string Inline(string line)
        {
            if (string.IsNullOrEmpty(line)) return "";
            string s = HttpUtility.HtmlEncode(line.Trim());
            s = Bold.Replace(s, "<strong>$1</strong>");
            s = NumberUnit.Replace(s, "<b class=\"rc-pol-num\">$1 $2</b>");
            return s;
        }

        /// <summary>
        /// บล็อกสรุปสำหรับหน้ายืนยันการจอง: ข้อแรก ๆ (<paramref name="keyLines"/>) แสดงเสมอ
        /// ที่เหลือพับใน &lt;details&gt; "อ่านเพิ่มเติม" (สคริปต์บันทึกรูปเปิดให้เองตอนจับภาพ)
        /// ข้อความว่าง = คืน "" (ผู้เรียกซ่อนส่วนนี้ทั้งก้อน) · ข้อเดียว = ย่อหน้า ไม่ใช่รายการ
        /// </summary>
        public static string BuildHtml(string text, int keyLines)
        {
            List<string> lines = Lines(text);
            if (lines.Count == 0) return "";
            if (keyLines < 1) keyLines = 1;

            var sb = new StringBuilder();
            if (lines.Count == 1)
            {
                sb.Append("<p class=\"rc-pol-p\">").Append(Inline(lines[0])).Append("</p>");
                return sb.ToString();
            }

            sb.Append("<ul class=\"rc-pol-list\">");
            int shown = Math.Min(keyLines, lines.Count);
            for (int i = 0; i < shown; i++)
                sb.Append("<li>").Append(Inline(lines[i])).Append("</li>");
            sb.Append("</ul>");

            int rest = lines.Count - shown;
            if (rest > 0)
            {
                sb.Append("<details class=\"rc-pol-more\"><summary>อ่านเพิ่มเติม (อีก ").Append(rest)
                  .Append(" ข้อ) <span lang=\"en\">Read more</span></summary><ul class=\"rc-pol-list\">");
                for (int i = shown; i < lines.Count; i++)
                    sb.Append("<li>").Append(Inline(lines[i])).Append("</li>");
                sb.Append("</ul></details>");
            }
            return sb.ToString();
        }

        /// <summary>เหมือน <see cref="BuildHtml(string,int)"/> โดยใช้ <see cref="DefaultKeyLines"/></summary>
        public static string BuildHtml(string text)
        {
            return BuildHtml(text, DefaultKeyLines);
        }
    }
}
