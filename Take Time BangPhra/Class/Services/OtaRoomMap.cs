using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// กติกากลางของ "mapping ห้อง OTA → ห้องจริง" (ตาราง MapDataWithSTAAH) — pure, ทดสอบด้วย mono ได้
    ///
    /// หลักการ (พ.ย. 2026 — เคส Agoda "Nordic Tent - RNB - … - Room no breakfast" ได้ห้องกระจก 3):
    ///   • ระบบ <b>ไม่เชื่อชื่อประเภทห้องที่ OTA ส่งมา</b> — ห้องที่จัดให้มาจากแถว mapping ที่ "ช่องทาง + ชื่อห้องเต็ม" ตรงกันเท่านั้น
    ///     (เทียบหลังปรับรูป: entity, ช่องว่าง, ขีด, ตัวพิมพ์) ไม่ตัดชื่อสั้น ไม่เทียบ 15 ตัวแรก ไม่ข้ามช่องทาง
    ///   • อาหารเช้า = ค่าที่ติ๊กไว้ในแถว mapping (คอลัมน์ Breakfast) — ไม่เดาจากชื่อแผนราคาของ OTA
    ///     (รหัสเดียวกันความหมายต่างกันได้ตามช่องทาง)
    /// </summary>
    public static class OtaRoomMap
    {
        public const string BfIncluded = "INCLUDED";
        public const string BfExcluded = "EXCLUDED";
        public const string BfConflict = "CONFLICT";

        /// <summary>คำนำบรรทัดอาหารเช้าในหมายเหตุการจอง (ExtractStaffNote ตัดบรรทัดนี้ออกเมื่อเขียนใหม่)</summary>
        public const string RemarkBreakfastPrefix = "อาหารเช้า:";

        // ── อาหารเช้า ──────────────────────────────────────────────────────────

        /// <summary>ค่าจากฐานข้อมูล/ฟอร์ม → INCLUDED / EXCLUDED / null (ไม่ระบุ)</summary>
        public static string NormalizeBreakfast(string v)
        {
            string s = (v ?? "").Trim().ToUpperInvariant();
            if (s == BfIncluded || s == "1" || s == "Y" || s == "YES" || s == "TRUE" || s == "IN") return BfIncluded;
            if (s == BfExcluded || s == "0" || s == "N" || s == "NO" || s == "FALSE" || s == "EX" || s == "OUT") return BfExcluded;
            return null;
        }

        public static string BreakfastLabel(string v)
        {
            if (v == BfConflict) return "ขัดกัน (มีทั้งรวม/ไม่รวม)";
            string n = NormalizeBreakfast(v);
            return n == BfIncluded ? "รวมอาหารเช้า" : n == BfExcluded ? "ไม่รวมอาหารเช้า" : "ไม่ระบุ";
        }

        private static readonly string[] NoBfWords =
        {
            "ไม่รวมอาหารเช้า", "ไม่มีอาหารเช้า", "ไม่รวมอาหาร", "งดอาหารเช้า",
            "no breakfast", "without breakfast", "breakfast not included", "breakfast excluded",
            "excl. breakfast", "excl breakfast", "exclude breakfast", "excludes breakfast", "excluding breakfast",
            "room only", "roomonly"
        };
        private static readonly string[] BfWords =
        {
            "รวมอาหารเช้า", "มีอาหารเช้า", "พร้อมอาหารเช้า",
            "with breakfast", "breakfast included", "incl. breakfast", "incl breakfast", "include breakfast",
            "includes breakfast", "including breakfast", "bed and breakfast", "bed & breakfast", "+ breakfast", "+breakfast"
        };

        /// <summary>
        /// อ่านเรื่องอาหารเช้าจาก "ข้อความที่เจ้าหน้าที่พิมพ์เอง" (คอลัมน์หมายเหตุของ mapping) — ใช้ย้ายข้อมูลเก่ามาเป็นช่องติ๊ก
        /// ห้ามใช้กับชื่อแผนราคาที่ OTA ส่งมา. คำปฏิเสธมาก่อนเสมอ ("ไม่รวมอาหารเช้า" มีคำว่า "รวมอาหารเช้า" อยู่ข้างใน)
        /// </summary>
        public static string BreakfastFromText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string low = Regex.Replace(text.ToLowerInvariant(), @"\s+", " ");
            foreach (string w in NoBfWords) if (low.Contains(w)) return BfExcluded;
            if (Regex.IsMatch(low, @"(^|[\s_\-(/])ro($|[\s_\-)/])")) return BfExcluded;
            foreach (string w in BfWords) if (low.Contains(w)) return BfIncluded;
            if (Regex.IsMatch(low, @"(^|[\s_\-(/])(abf|b&b|bb)($|[\s_\-)/])")) return BfIncluded;
            return null;
        }

        /// <summary>หมายเหตุนี้พูดแค่เรื่องอาหารเช้า (เมื่อมีช่องติ๊กแล้ว ไม่ต้องแสดงซ้ำ)</summary>
        public static bool IsBreakfastOnlyNote(string note)
        {
            if (string.IsNullOrWhiteSpace(note) || BreakfastFromText(note) == null) return false;
            string low = note.ToLowerInvariant();
            foreach (string w in NoBfWords.Concat(BfWords).OrderByDescending(x => x.Length))
                low = low.Replace(w, " ");
            low = Regex.Replace(low, @"(^|[\s_\-(/])(ro|abf|b&b|bb)($|[\s_\-)/])", " ");
            return Regex.Replace(low, @"[\s\p{P}\p{S}]", "").Length == 0;
        }

        /// <summary>รวมค่าอาหารเช้าของหลายแถว/หลายห้อง: ค่าเดียว = ค่านั้น · ต่างกัน = CONFLICT · ไม่มี = null</summary>
        public static string Combine(IEnumerable<string> values)
        {
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (string v in values ?? Enumerable.Empty<string>())
            {
                if (v == BfConflict) return BfConflict;
                string n = NormalizeBreakfast(v);
                if (n != null) set.Add(n);
            }
            return set.Count == 0 ? null : set.Count == 1 ? set.First() : BfConflict;
        }

        /// <summary>บรรทัดในหมายเหตุการจอง — null = ไม่ระบุ (ไม่เขียนบรรทัด)</summary>
        public static string RemarkLine(string bf)
        {
            if (bf == BfConflict) return RemarkBreakfastPrefix + " ⚠ mapping ขัดกัน (มีทั้งรวม/ไม่รวม) — ตรวจกับ OTA";
            string n = NormalizeBreakfast(bf);
            if (n == BfExcluded) return RemarkBreakfastPrefix + " ไม่รวม (ตาม mapping ห้อง OTA)";
            if (n == BfIncluded) return RemarkBreakfastPrefix + " รวม (ตาม mapping ห้อง OTA)";
            return null;
        }

        /// <summary>อ่านบรรทัด "อาหารเช้า:" ที่ระบบเขียนไว้ในหมายเหตุ → INCLUDED / EXCLUDED / CONFLICT / null (ไม่มีบรรทัด)</summary>
        public static string BreakfastFromRemark(string remark)
        {
            if (string.IsNullOrEmpty(remark)) return null;
            foreach (string l in remark.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
            {
                string t = l.Trim();
                if (!t.StartsWith(RemarkBreakfastPrefix, StringComparison.Ordinal)) continue;
                string v = t.Substring(RemarkBreakfastPrefix.Length).Trim();
                if (v.Contains("ขัดกัน")) return BfConflict;
                if (v.StartsWith("ไม่รวม", StringComparison.Ordinal)) return BfExcluded;
                if (v.StartsWith("รวม", StringComparison.Ordinal)) return BfIncluded;
            }
            return null;
        }

        // ── จับคู่ชื่อห้อง / ช่องทาง ───────────────────────────────────────────

        /// <summary>
        /// ปรับรูปชื่อห้องให้เทียบกันได้ — ถอด entity, nbsp/ขีดยาว → ปกติ, ช่องว่างรอบขีด, ยุบช่องว่าง, ตัวพิมพ์เล็ก
        /// (ไม่ตัดส่วนใดของชื่อทิ้ง — แผนราคาต่างกัน = คนละ mapping)
        /// </summary>
        public static string Key(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string v = s.Replace(' ', ' ').Replace('–', '-').Replace('—', '-').Replace('−', '-');
            v = Regex.Replace(v, @"\s*-\s*", " - ");
            v = Regex.Replace(v, @"\s+", " ").Trim();
            return v.ToLowerInvariant();
        }

        /// <summary>
        /// รูปที่เป็นไปได้ของชื่อห้องหนึ่งชื่อ — ถอด entity แบบปกติ + แบบที่โปรแกรมเดิมทำ ("ลบ" &amp;nbsp; ทิ้ง)
        /// แถวเก่าในตารางถูกสร้างให้เข้ากับแบบเดิม จึงต้องยอมทั้งสองแบบ
        /// </summary>
        public static List<string> Keys(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            Action<string> add = x => { string k = Key(x); if (k.Length > 0 && !list.Contains(k)) list.Add(k); };
            string dec = raw;
            try { dec = System.Net.WebUtility.HtmlDecode(raw) ?? raw; } catch { }
            add(dec);
            add(Regex.Replace(raw, @"&[#a-zA-Z0-9]+;", ""));
            add(Regex.Replace(raw, @"&[#a-zA-Z0-9]+;", " "));
            return list;
        }

        /// <summary>ชื่อห้องในแถว mapping ตรงกับชื่อเต็มในอีเมลไหม (ไม่มีการตัดชื่อสั้น / เทียบบางส่วน)</summary>
        public static bool RoomTypeEquals(string rowRoomType, string emailRoomType)
        {
            var a = Keys(rowRoomType);
            if (a.Count == 0) return false;
            var b = Keys(emailRoomType);
            return a.Any(b.Contains);
        }

        /// <summary>ยุบชื่อช่องทางให้เทียบกันได้ — ตัดอักขระพิเศษ + คำว่า "dot" ที่คนพิมพ์แทนจุด</summary>
        public static string NormChannel(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            string v = Regex.Replace(s.ToLowerInvariant(), @"\bdot\b", ".");
            return Regex.Replace(v, @"[^a-z0-9ก-๙]", "");
        }

        /// <summary>ชื่อช่องทางสองค่านี้หมายถึงเจ้าเดียวกันไหม ("Booking dot com" = "Booking.com")</summary>
        public static bool ChannelAlike(string a, string b)
        {
            string x = NormChannel(a), y = NormChannel(b);
            if (x.Length == 0 || y.Length == 0) return false;
            if (x.Length < 4 || y.Length < 4) return x == y;
            return x == y || x.IndexOf(y, StringComparison.Ordinal) >= 0 || y.IndexOf(x, StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// ประเภทห้องของห้องที่ผูกไว้ปนกันไหม (เช่น Nordic Tent + ห้องกระจก) — คืนรายชื่อประเภทเมื่อปน, ว่าง = ไม่ปน
        /// ไม่ได้แปลว่าผิดเสมอ (บางแผนตั้งใจให้ขายรวม) แต่ต้องให้คนเห็น
        /// </summary>
        public static List<string> MixedTypes(IEnumerable<string> roomTypes)
        {
            var t = (roomTypes ?? Enumerable.Empty<string>())
                .Select(x => (x ?? "").Trim()).Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            return t.Count > 1 ? t : new List<string>();
        }

        /// <summary>
        /// อ่านเหตุ "ไม่มี mapping" ที่ตัวอ่านอีเมล log ไว้ → (ช่องทาง, ชื่อห้อง) ไว้ทำรายการ "ชื่อจาก OTA ที่ยังไม่ map"
        /// รูปแบบ: ... Agency='Agoda' ROOM_TYPE='Nordic Tent - RNB - ...' ...
        /// </summary>
        public static bool TryParseUnmappedLog(string detail, out string channel, out string roomType)
        {
            channel = roomType = null;
            if (string.IsNullOrEmpty(detail)) return false;
            var m = Regex.Match(detail, @"Agency='(?<c>[^']*)'\s+ROOM_TYPE='(?<r>.*?)'(?=\s|\(|$)");
            if (!m.Success) return false;
            channel = m.Groups["c"].Value.Trim();
            roomType = m.Groups["r"].Value.Trim();
            return roomType.Length > 0;
        }
    }
}
