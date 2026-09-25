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
    ///
    /// เบอร์ต่างประเทศ (แขกต่างชาติ เช่น "+852 9545 6676") เก็บเป็น "+" + ตัวเลขล้วน — ห้ามเติม 0 นำหน้า
    /// (รุ่นก่อนตัด + แล้วเติม 0 → "085295456676" ผิดและแขกประจำถูกแยกเป็นหลายแถวลูกค้า)
    /// </summary>
    public static class GuestPhone
    {
        /// <summary>
        /// ตัดอักขระที่ไม่ใช่ตัวเลข + แปลง +66 / 66 ให้เป็น 0 (สำหรับเบอร์ไทยที่คนกรอก เช่นหน้าสมาชิก)
        /// ⚠ ห้ามใช้ก่อน <see cref="ResolveKey"/> / <see cref="Normalize"/> — ตัด '+' ทิ้งแล้วเติม 0
        ///   ทำให้เบอร์ต่างประเทศเพี้ยน ("+852 9545 6676" → "085295456676")
        /// </summary>
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

        /// <summary>
        /// เป็นเบอร์ที่ "โทรออกได้จริง" ไหม — มือถือ/เบอร์บ้านไทย ("0…") หรือเบอร์ต่างประเทศ ("+…")
        /// (เส้นอีเมลแก้ไขใช้ตัวนี้ตัดสินว่าจะย้ายใบจองไปผูกลูกค้าเบอร์ใหม่ไหม ⇒ ต้องรับเบอร์ต่างประเทศด้วย
        ///  ไม่งั้นแขกต่างชาติที่เปลี่ยนเบอร์ในอีเมลแก้ไขจะไม่ถูกย้าย)
        /// </summary>
        public static bool IsUsable(string value)
        {
            return IsMobile(value) || IsLandline(value) || IsInternational(value);
        }

        /// <summary>
        /// ค่านี้เป็น "เบอร์ต่างประเทศ" ในรูปที่ระบบเก็บไหม: "+" ตามด้วยตัวเลข 8-15 หลัก
        /// (หลักแรก 1-9, ไม่ใช่ค่าหลอก) — ตรงกับรูปแบบที่ <see cref="Normalize"/> คืนให้
        /// </summary>
        public static bool IsInternational(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 2 || value[0] != '+') return false;
            return IsInternationalDigits(value.Substring(1));
        }

        /// <summary>ตัวเลขล้วน (ไม่มี +) ใช้เป็นเบอร์ต่างประเทศได้ไหม: 8-15 หลัก หลักแรก 1-9 และไม่ใช่ค่าหลอก</summary>
        private static bool IsInternationalDigits(string digits)
        {
            return !string.IsNullOrEmpty(digits)
                && Regex.IsMatch(digits, @"^[1-9][0-9]{7,14}$")
                && !LooksFake(digits);
        }

        /// <summary>
        /// รหัสประเทศที่ยอมรับเมื่อเบอร์ "ไม่มี + / 00 นำหน้า" (OTA บางเจ้าตัด + ทิ้ง เช่น 85295456676)
        /// — ต้องมีรายการ เพราะตัวเลขล้วนยาว ๆ ที่ไม่มี + อาจเป็นเลขจอง/PIN ก็ได้
        /// 66 (ไทย) ไม่อยู่ในรายการ — ถูกแปลงเป็นเบอร์ไทย "0…" ก่อนถึงขั้นนี้แล้ว
        /// ⚠ รายการเดียวกับใน Database/PHASE19_Migration_16_Fix_Placeholder_Guest_Phone.sql
        /// </summary>
        private static readonly string[] KnownCountryCodes =
        {
            "852", "853", "855", "856", "880", "886", "971", "972", "973", "974", "977",
            "20", "27", "30", "31", "32", "33", "34", "36", "39", "40", "41",
            "43", "44", "45", "46", "47", "48", "49",
            "51", "52", "53", "54", "55", "56", "57", "58",
            "60", "61", "62", "63", "64", "65",
            "81", "82", "84", "86",
            "90", "91", "92", "93", "94", "95",
            "1", "7"
        };

        /// <summary>ตัวเลขล้วนขึ้นต้นด้วยรหัสประเทศที่รู้จักไหม (ดู <see cref="KnownCountryCodes"/>)</summary>
        public static bool StartsWithKnownCountryCode(string digits)
        {
            if (string.IsNullOrEmpty(digits)) return false;
            foreach (string cc in KnownCountryCodes)
                if (digits.StartsWith(cc, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// ความยาวสูงสุดของ Customer key ที่ยอมเก็บ — DDL ของตาราง Customer/Reservation ไม่อยู่ใน repo
        /// (คอลัมน์เบอร์อาจเป็น VARCHAR(20)) ⇒ กันไว้ก่อน: "+" + 15 หลัก = 16 ตัว, OTA_ key ≤ 16 ตัว
        /// ถ้าค่าที่เลือกได้ยาวเกินนี้ (ไม่ควรเกิด) ให้ใช้ OTA_ key แทน ดีกว่าให้ INSERT ถูกตัดท้าย/ล้มทั้งใบจอง
        /// </summary>
        public const int MaxKeyLength = 20;

        private const string FakeReasonFormat = "เบอร์ที่ส่งมาเป็นค่าคงที่/ค่าหลอก ({0})";

        /// <summary>
        /// เลขในวงเล็บของเลขที่จอง (Booking.com/Expedia: "1114600000001755 (5904188856)" → "5904188856")
        /// — เป็น PIN/เลขอ้างอิงที่สอง ไม่ใช่เบอร์โทร (โค้ดรุ่นเก่าเคยเอาไปเก็บเป็นเบอร์) คืน "" ถ้าไม่มี
        /// </summary>
        public static string BookingPin(string bookingId)
        {
            if (string.IsNullOrWhiteSpace(bookingId)) return "";
            var m = Regex.Match(bookingId, @"\((\d{6,})\)");
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>
        /// แปลง "ข้อความเบอร์ดิบ" จากอีเมล → ค่าที่เก็บเป็นเบอร์ลูกค้าได้
        ///   • เบอร์ไทย → "0…" (มือถือ 10 หลัก / เบอร์บ้าน 9 หลัก)
        ///   • เบอร์ต่างประเทศ → "+" + ตัวเลขล้วน (ห้ามเติม 0 นำหน้าเด็ดขาด)
        /// ใช้ไม่ได้ → คืน "" และบอกเหตุผลใน <paramref name="reason"/> (ใช้ได้ → reason = null)
        ///
        /// ⚠ ต้องได้ข้อความ "ดิบ" — ห้ามผ่าน <see cref="Sanitize"/> มาก่อน: Sanitize ตัด '+' ทิ้งแล้วเติม 0
        ///   ⇒ "+852 9545 6676" (แขกฮ่องกง) กลายเป็น "085295456676" ซึ่งทั้งผิดและแยกไม่ออกว่าเป็นเบอร์ต่างประเทศ
        ///
        /// กฎ:
        ///  1) ขึ้นต้น '+' หรือตัวเลขขึ้นต้น "00" = เบอร์ต่างประเทศ
        ///     - รหัส 66 → แปลงเป็นไทย "0…" แล้วตรวจแบบเบอร์ไทย
        ///     - อื่น ๆ → 8-15 หลัก หลักแรก 1-9 ไม่ใช่ค่าหลอก → "+ตัวเลข"
        ///  2) ไม่มี + / 00: ขึ้นต้น 0 = เบอร์ไทย; 9 หลักขึ้นต้น 6/8/9 = มือถือไทยที่หาย 0 นำหน้า;
        ///     66 + 8-10 หลัก = ไทย; ≥ 11 หลักและขึ้นต้นรหัสประเทศที่รู้จัก = ต่างประเทศ "+ตัวเลข"
        ///  3) ปฏิเสธ: ค่าหลอก, ตรงกับเลขในวงเล็บของเลขที่จอง (PIN), หรือรูปแบบอื่น
        /// </summary>
        public static string Normalize(string raw, string bookingId, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(raw)) { reason = "ไม่มีเบอร์โทรมาในอีเมล"; return ""; }

            string trimmed = raw.Trim();
            bool plus = trimmed.StartsWith("+", StringComparison.Ordinal);
            // "+44 (0) 7911 123456" — (0) คือเลขนำหน้าในประเทศ ไม่ใช่ส่วนของเบอร์สากล
            if (plus) trimmed = Regex.Replace(trimmed, @"\(\s*0\s*\)", "");
            string digits = Regex.Replace(trimmed, @"[^0-9]", "");   // [0-9] ไม่ใช่ \d — \d ของ .NET รับเลขไทย/อารบิกด้วย
            if (digits.Length == 0) { reason = $"ไม่มีตัวเลขในช่องเบอร์โทร ({Trunc(trimmed)})"; return ""; }

            // PIN ของ Booking.com/Expedia หลุดมาเป็นเบอร์ (รุ่นเก่าเคยเก็บ "5904188856" จาก "(5904188856)")
            string pin = BookingPin(bookingId);
            if (pin.Length > 0 && (digits == pin || digits == "0" + pin))
            {
                reason = $"ตัวเลขที่ได้คือเลขอ้างอิงในวงเล็บของเลขที่จอง ({pin}) ไม่ใช่เบอร์โทร";
                return "";
            }

            if (LooksFake(digits)) { reason = string.Format(FakeReasonFormat, digits); return ""; }

            bool intl = plus || digits.StartsWith("00", StringComparison.Ordinal);
            if (intl)
            {
                string d = digits.StartsWith("00", StringComparison.Ordinal) ? digits.Substring(2) : digits;
                if (d.StartsWith("66", StringComparison.Ordinal))
                    return ThaiFromCountryCode(d, out reason);
                if (IsInternationalDigits(d)) return "+" + d;
                reason = LooksFake(d)
                    ? string.Format(FakeReasonFormat, d)
                    : $"เบอร์ต่างประเทศรูปแบบไม่ถูกต้อง (+{d} = {d.Length} หลัก — ต้อง 8-15 หลัก หลักแรก 1-9)";
                return "";
            }

            if (digits.StartsWith("0", StringComparison.Ordinal))
            {
                if (IsMobile(digits) || IsLandline(digits)) return digits;
                reason = digits.Length != 9 && digits.Length != 10
                    ? $"ความยาวไม่ใช่เบอร์ไทย ({digits} = {digits.Length} หลัก)"
                    : $"รูปแบบไม่ใช่เบอร์ไทย ({digits})";
                return "";
            }

            // มือถือไทยที่ OTA ตัด 0 นำหน้าทิ้ง: 812345678 → 0812345678
            if (digits.Length == 9 && (digits[0] == '6' || digits[0] == '8' || digits[0] == '9'))
            {
                string th = "0" + digits;
                if (IsMobile(th)) return th;
                reason = $"รูปแบบไม่ใช่เบอร์ไทย ({th})";
                return "";
            }

            // 66 ไม่มี + นำหน้า (66812345678 / 6638123456) — ช่วงความยาวเดียวกับที่ Sanitize เดิมแปลง
            if (digits.StartsWith("66", StringComparison.Ordinal) && digits.Length >= 10 && digits.Length <= 12)
                return ThaiFromCountryCode(digits, out reason);

            // เบอร์ต่างประเทศที่ OTA ตัด + ทิ้ง (85295456676) — ต้องยาวพอและขึ้นต้นรหัสประเทศที่รู้จัก
            if (digits.Length >= 11 && StartsWithKnownCountryCode(digits) && IsInternationalDigits(digits))
                return "+" + digits;

            reason = $"ไม่ใช่รูปแบบเบอร์ไทย และไม่มีรหัสประเทศ (+) นำหน้า ({digits} = {digits.Length} หลัก)";
            return "";
        }

        /// <summary>"66…" (ตัด + / 00 แล้ว) → เบอร์ไทย "0…" ถ้าถูกรูปแบบ ("+66 (0)81…" ก็รับ)</summary>
        private static string ThaiFromCountryCode(string digitsWith66, out string reason)
        {
            reason = null;
            string th = "0" + digitsWith66.Substring(2).TrimStart('0');
            if (IsMobile(th) || IsLandline(th)) return th;
            reason = LooksFake(th)
                ? string.Format(FakeReasonFormat, th)
                : $"เบอร์ไทย (+66) รูปแบบไม่ถูกต้อง ({th} = {th.Length} หลัก)";
            return "";
        }

        private static string Trunc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= 30 ? s : s.Substring(0, 30) + "…";
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
            return RejectReason(raw, null);
        }

        /// <summary>เหมือน <see cref="RejectReason(string)"/> แต่ดัก PIN ในวงเล็บของเลขที่จองด้วย</summary>
        public static string RejectReason(string raw, string bookingId)
        {
            string reason;
            Normalize(raw, bookingId, out reason);
            return reason;
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
        /// ลำดับ: เบอร์มือถือไทยจริง / เบอร์ต่างประเทศจริง ("+…") → รหัสอ้างอิงจากเลขจอง → เบอร์บ้านจริง
        ///       → ค่าตั้งต้นที่ตั้งไว้ → OTA_UNKNOWN
        ///
        /// เบอร์บ้านถูกจัดหลังรหัสอ้างอิงตามตรรกะเดิมของระบบ (เบอร์บ้านในอีเมล OTA มักเป็นเบอร์รีสอร์ทเอง
        /// ไม่ใช่ของลูกค้า) — แต่ถ้าไม่มีเลขจองเลยก็ยังดีกว่าไม่มีอะไร
        ///
        /// ⚠ <paramref name="rawPhone"/> ต้องเป็นข้อความดิบจากอีเมล (ห้าม Sanitize ก่อน — ดู <see cref="Normalize"/>)
        /// </summary>
        /// <param name="note">เหตุผลที่ไม่ได้ใช้เบอร์ที่ส่งมา (null = ใช้เบอร์จริงตามที่ส่งมา)</param>
        public static string ResolveKey(string rawPhone, string bookingId, string defaultPhone, out string note)
        {
            note = null;
            string reason;
            string d = Normalize(rawPhone, bookingId, out reason);

            // กันคอลัมน์เบอร์ล้น (ดู MaxKeyLength) — ปกติ Normalize ไม่คืนค่ายาวเกิน 16 ตัวอยู่แล้ว
            if (d.Length > MaxKeyLength)
            {
                reason = $"เบอร์ยาวเกินคอลัมน์ที่เก็บได้ ({d.Length} ตัว > {MaxKeyLength})";
                d = "";
            }

            if (IsMobile(d) || IsInternational(d)) return d;

            if (IsLandline(d))
                reason = $"ได้เบอร์บ้าน/สำนักงาน ({d}) ไม่ใช่มือถือ — มักเป็นเบอร์ของที่พักเอง";

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
