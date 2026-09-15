using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// ตรวจ "เบอร์โทรลูกค้า" ที่รับมาจากภายนอก (อีเมลจอง OTA / channel manager)
    ///
    /// ทำไมต้องมี: เบอร์โทรเป็น **key ของตาราง Customer** (Reservation.Customer_MobilePhone →
    /// Customer.MobilePhone) ⇒ ถ้า OTA ส่ง "ค่าคงที่" มาแทนเบอร์จริง (เคสจริง: STAAH/Expedia ส่ง
    /// 01111111111111 มาทุกใบ) การจองทุกใบจะไปผูกกับลูกค้า **แถวเดียวกัน** → ชื่อผู้จองที่แสดง
    /// กลายเป็นชื่อคนแรกที่เคยจองด้วยเบอร์นั้นตลอดไป (EnsureCustomer เจอแถวแล้วก็ไม่แก้ชื่อ)
    /// เคสจริง: ใบจอง 149419 ของ "Samput Ekapan" ขึ้นชื่อ "Kanthicha Suparojwathin"
    ///
    /// กฎเดิมหลวมเกินไป (ยาว 9-20 ตัวและไม่ขึ้นต้น 02 = ผ่าน) → 01111111111111 ผ่านฉลุย
    /// ที่นี่ตรวจให้ตรงรูปแบบเบอร์ไทยจริง ๆ และดักค่าคงที่/ค่าหลอกด้วย
    /// </summary>
    public static class GuestPhone
    {
        /// <summary>ตัดอักขระที่ไม่ใช่ตัวเลข + แปลง +66 / 66 ให้เป็น 0</summary>
        public static string Sanitize(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            p = p.Replace("66 66", "0").Replace("+66", "0");
            p = Regex.Replace(p, @"[^\d]", "");
            if (p.StartsWith("66") && p.Length > 9) p = "0" + p.Substring(2);
            if (p.Length > 0 && !p.StartsWith("0")) p = "0" + p;
            return p;
        }

        /// <summary>เบอร์มือถือไทย: 10 หลัก ขึ้นต้น 06 / 08 / 09</summary>
        public static bool IsMobile(string digits)
        {
            return !string.IsNullOrEmpty(digits)
                && digits.Length == 10
                && Regex.IsMatch(digits, @"^0[689]\d{8}$")
                && !LooksFake(digits);
        }

        /// <summary>เบอร์บ้าน/สำนักงานไทย: 9 หลัก ขึ้นต้น 0 + รหัสพื้นที่ 2-7 (02, 032, 038, ...)</summary>
        public static bool IsLandline(string digits)
        {
            return !string.IsNullOrEmpty(digits)
                && digits.Length == 9
                && Regex.IsMatch(digits, @"^0[2-7]\d{7}$")
                && !LooksFake(digits);
        }

        /// <summary>เป็นเบอร์โทรไทยที่ "โทรออกได้จริง" ไหม (มือถือหรือเบอร์บ้าน)</summary>
        public static bool IsUsable(string digits)
        {
            return IsMobile(digits) || IsLandline(digits);
        }

        /// <summary>
        /// หน้าตาเป็น "ค่าคงที่/ค่าหลอก" ไหม — ใช้ได้แม้ความยาวถูกต้อง
        /// ตัวอย่างที่ดัก: 01111111111111, 0111111111, 0000000000, 0999999999,
        ///                0123456789, 0987654321
        /// </summary>
        public static bool LooksFake(string digits)
        {
            if (string.IsNullOrWhiteSpace(digits)) return true;
            if (!Regex.IsMatch(digits, @"^\d+$")) return false;   // มีตัวอักษรปน = ไม่ใช่เคสนี้

            // เลขซ้ำติดกันตั้งแต่ 7 ตัว (0811111111 ก็ไม่ใช่เบอร์จริง)
            if (Regex.IsMatch(digits, @"(\d)\1{6,}")) return true;

            // ตัดเลข 0 นำหน้าแล้วเหลือเลขเดียวกันทั้งหมด
            string body = digits.Length > 1 ? digits.Substring(1) : digits;
            if (body.Length >= 6 && body.Replace(body[0].ToString(), "").Length == 0) return true;

            // เรียงขึ้น/ลงติดกันเกือบทั้งเบอร์ (0123456789 / 0987654321)
            // ใช้เกณฑ์ 9 ตัว ไม่ใช่ 8 — 08xxxxxxxx ที่บังเอิญเรียง 8 ตัว (0812345678) อาจเป็นเบอร์จริงได้
            int up = 1, down = 1, maxUp = 1, maxDown = 1;
            for (int i = 1; i < digits.Length; i++)
            {
                int d = digits[i] - digits[i - 1];
                up = d == 1 ? up + 1 : 1;
                down = d == -1 ? down + 1 : 1;
                if (up > maxUp) maxUp = up;
                if (down > maxDown) maxDown = down;
            }
            if (maxUp >= 9 || maxDown >= 9) return true;

            return false;
        }

        /// <summary>
        /// เหตุผลที่ "ใช้เบอร์นี้เป็นเบอร์ลูกค้าไม่ได้" — คืน null ถ้าใช้ได้ (ใช้ทำข้อความเตือน)
        /// </summary>
        public static string RejectReason(string raw)
        {
            string d = Sanitize(raw);
            if (string.IsNullOrEmpty(d)) return "ไม่มีเบอร์โทรมาในอีเมล";
            if (LooksFake(d)) return $"เบอร์ที่ส่งมาเป็นค่าคงที่/ค่าหลอก ({d})";
            if (d.Length != 9 && d.Length != 10) return $"ความยาวไม่ใช่เบอร์ไทย ({d} = {d.Length} หลัก)";
            if (!IsUsable(d)) return $"รูปแบบไม่ใช่เบอร์ไทย ({d})";
            return null;
        }

        /// <summary>คำนำหน้าของ "รหัสอ้างอิง" ที่ใช้แทนเบอร์ — ทั้งระบบเช็คด้วยคำนี้ว่าไม่ใช่เบอร์จริง</summary>
        public const string RefPrefix = "OTA_";

        /// <summary>เป็นรหัสอ้างอิง (ไม่ใช่เบอร์โทรจริง) ไหม</summary>
        public static bool IsReference(string value)
        {
            return !string.IsNullOrEmpty(value)
                && value.StartsWith(RefPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// สร้าง key ประจำใบจองจากเลขที่จองของ OTA — **ต้องไม่ซ้ำกันข้ามใบ** เพราะเป็น key ของ Customer
        ///
        /// รูปแบบ: "OTA_" + ตัวเลข ≤ 12 หลัก  (สั้นพอสำหรับคอลัมน์เบอร์โทรที่อาจเป็น VARCHAR(20))
        ///   • มีเลขในวงเล็บ (Booking.com/Expedia ใส่เลขอ้างอิงที่สองไว้) → ใช้เลขนั้น
        ///   • ไม่มี → ใช้ 12 หลักท้ายของเลขจองท่อนแรก
        /// คืน "" ถ้าเลขจองว่าง/ไม่มีตัวเลขเลย
        /// </summary>
        public static string ReferenceKey(string bookingId)
        {
            if (string.IsNullOrWhiteSpace(bookingId)) return "";

            string pick = null;
            var m = Regex.Match(bookingId, @"\((\d{6,})\)");
            if (m.Success) pick = m.Groups[1].Value;

            if (string.IsNullOrEmpty(pick))
            {
                string first = Regex.Replace(bookingId.Split(' ')[0] ?? "", @"[^\d]", "");
                if (first.Length == 0) first = Regex.Replace(bookingId, @"[^\d]", "");
                pick = first;
            }
            if (string.IsNullOrEmpty(pick)) return "";
            // สั้นเกินไปก็ไม่รับ — key ต้องไม่ซ้ำข้ามใบจอง ("OTA_99" ชนกันได้ง่าย)
            if (pick.Length < 6) return "";

            if (pick.Length > 12) pick = pick.Substring(pick.Length - 12);   // ท้ายสุดต่างกันแน่กว่าหัว
            return RefPrefix + pick;
        }

        /// <summary>
        /// เลือกค่าที่จะใช้เป็น Customer key ของใบจองนี้
        /// ลำดับ: เบอร์มือถือจริง → รหัสอ้างอิงจากเลขจอง → เบอร์บ้านจริง → ค่าตั้งต้นที่ตั้งไว้ → OTA_UNKNOWN
        ///
        /// เบอร์บ้านถูกจัดหลังรหัสอ้างอิงตามตรรกะเดิมของระบบ (เบอร์บ้านในอีเมล OTA มักเป็นเบอร์รีสอร์ทเอง
        /// ไม่ใช่ของลูกค้า) — แต่ถ้าไม่มีเลขจองเลยก็ยังดีกว่าไม่มีอะไร
        /// </summary>
        /// <param name="note">เหตุผลที่ไม่ได้ใช้เบอร์ที่ส่งมา (null = ใช้เบอร์จริงตามที่ส่งมา)</param>
        public static string ResolveKey(string rawPhone, string bookingId, string defaultPhone, out string note)
        {
            note = null;
            string d = Sanitize(rawPhone);

            if (IsMobile(d)) return d;

            string reason = RejectReason(rawPhone);
            string key = ReferenceKey(bookingId);
            if (!string.IsNullOrEmpty(key))
            {
                note = $"{reason} → ใช้เลขที่จองเป็นรหัสอ้างอิงแทน ({key})";
                return key;
            }

            if (IsLandline(d))
            {
                note = "ได้เบอร์บ้าน ไม่ใช่มือถือ และไม่มีเลขที่จองให้อ้างอิง — ใช้เบอร์บ้านไปก่อน";
                return d;
            }

            if (!string.IsNullOrWhiteSpace(defaultPhone))
            {
                note = $"{reason} และไม่มีเลขที่จอง — ใช้เบอร์ตั้งต้นที่ตั้งไว้";
                return defaultPhone;
            }

            note = $"{reason} และไม่มีเลขที่จอง";
            return RefPrefix + "UNKNOWN";
        }

        /// <summary>รายการค่าคงที่ที่เคยเจอจาก OTA — ไว้ให้เอกสาร/ข้อความเตือนอ้างถึง</summary>
        public static readonly IReadOnlyList<string> KnownPlaceholders = new List<string>
        {
            "01111111111111",   // STAAH / Expedia
            "0000000000",
            "0999999999"
        };
    }
}
