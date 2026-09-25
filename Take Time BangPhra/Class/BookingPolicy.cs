using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Take_Time_BangPhra
{
    /// <summary>
    /// นโยบายการจอง — ข้อกำหนดและเงื่อนไข / ความเป็นส่วนตัว / การคืนเงิน / การยกเลิก
    ///
    /// เก็บในตาราง Booking_Policy_Config (PHASE19 migration 22) ไม่ใช่ System_Config เพราะหน้า
    /// SystemSettings วาดทุกแถวของ System_Config เป็นช่องกรอกบรรทัดเดียว — บันทึกหน้านั้นครั้งเดียว
    /// ข้อความหลายบรรทัดจะโดนตัดขึ้นบรรทัดทิ้ง
    ///
    /// ยังไม่รัน migration / อ่าน DB ไม่ได้ → ใช้ข้อความสำรองในโค้ด หน้าจองไม่พัง
    /// ทุกครั้งที่บันทึกแล้วข้อความเปลี่ยน → Policy_Version +1 และเก็บสำเนาใน Booking_Policy_History
    /// </summary>
    public static class BookingPolicy
    {
        public const string KeyTerms = "Policy_Terms";
        public const string KeyPrivacy = "Policy_Privacy";
        public const string KeyRefund = "Policy_Refund";
        public const string KeyCancellation = "Policy_Cancellation";
        public const string KeyVersion = "Policy_Version";

        /// <summary>คีย์ข้อความทั้ง 4 ตามลำดับที่แสดง</summary>
        public static readonly string[] TextKeys = { KeyTerms, KeyPrivacy, KeyRefund, KeyCancellation };

        private static readonly object _lock = new object();
        private static Dictionary<string, string> _cache;
        private static DateTime? _updatedAt;
        private static DateTime _loadedAt = DateTime.MinValue;
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

        private static string ConnStr
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"];
                return cs == null ? null : cs.ConnectionString;
            }
        }

        // ── อ่าน ─────────────────────────────────────────────────────────────

        /// <summary>ข้อความของนโยบาย — ไม่มีใน DB ใช้ข้อความสำรอง</summary>
        public static string Get(string key)
        {
            try
            {
                var map = GetCache();
                string v;
                if (map != null && map.TryGetValue(key, out v) && !string.IsNullOrWhiteSpace(v))
                    return v;
            }
            catch { }
            return DefaultText(key);
        }

        /// <summary>ฉบับที่ปัจจุบัน (ขึ้นเองทุกครั้งที่บันทึกแล้วข้อความเปลี่ยน)</summary>
        public static int Version
        {
            get
            {
                try
                {
                    var map = GetCache();
                    string v;
                    int n;
                    if (map != null && map.TryGetValue(KeyVersion, out v) && int.TryParse(v, out n) && n > 0)
                        return n;
                }
                catch { }
                return 1;
            }
        }

        /// <summary>วันที่แก้ไขล่าสุด (null = ยังไม่มีในฐานข้อมูล)</summary>
        public static DateTime? UpdatedAt
        {
            get
            {
                try { GetCache(); } catch { }
                return _updatedAt;
            }
        }

        public static string Title(string key)
        {
            switch (key)
            {
                case KeyTerms: return "ข้อกำหนดและเงื่อนไข";
                case KeyPrivacy: return "นโยบายความเป็นส่วนตัว";
                case KeyRefund: return "นโยบายการคืนเงิน";
                case KeyCancellation: return "นโยบายการยกเลิกการจอง";
                default: return key;
            }
        }

        public static string TitleEn(string key)
        {
            switch (key)
            {
                case KeyTerms: return "Terms & Conditions";
                case KeyPrivacy: return "Privacy Policy";
                case KeyRefund: return "Refund Policy";
                case KeyCancellation: return "Cancellation Policy";
                default: return key;
            }
        }

        /// <summary>ชื่อย่อสำหรับ HTML id / data attribute</summary>
        public static string Slug(string key)
        {
            switch (key)
            {
                case KeyTerms: return "terms";
                case KeyPrivacy: return "privacy";
                case KeyRefund: return "refund";
                case KeyCancellation: return "cancel";
                default: return "policy";
            }
        }

        /// <summary>
        /// แปลงข้อความธรรมดาเป็น HTML อย่างปลอดภัย: encode ทุกตัวก่อน แล้วค่อยเติม
        /// ขึ้นบรรทัด (&lt;br/&gt;) และตัวหนาแบบ **ข้อความ** — ไม่มีทางแทรกแท็ก/สคริปต์ได้
        /// </summary>
        public static string ToHtml(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string s = HttpUtility.HtmlEncode(text.Replace("\r\n", "\n").Replace("\r", "\n").Trim());
            s = Regex.Replace(s, @"\*\*(.+?)\*\*", "<strong>$1</strong>");
            return s.Replace("\n", "<br/>");
        }

        // ── เขียน ────────────────────────────────────────────────────────────

        /// <summary>
        /// บันทึกข้อความทั้งชุด — มีข้อความเปลี่ยนอย่างน้อย 1 ส่วน = ขึ้นฉบับใหม่ + เก็บสำเนา
        /// คืนเลขฉบับหลังบันทึก (ไม่เปลี่ยน = เลขเดิม)
        /// </summary>
        public static int Save(IDictionary<string, string> values, string modifiedBy)
        {
            if (values == null) throw new ArgumentNullException("values");
            string cs = ConnStr;
            if (string.IsNullOrEmpty(cs)) throw new InvalidOperationException("ไม่พบ connection string");

            // ค่าปัจจุบันจาก DB ตรง ๆ (ไม่ใช้ cache) เพื่อเทียบว่าเปลี่ยนจริงไหม
            var current = LoadFromDb(cs);
            bool changed = false;
            foreach (string k in TextKeys)
            {
                string nv;
                if (!values.TryGetValue(k, out nv)) continue;
                string ov;
                current.TryGetValue(k, out ov);
                if (!string.Equals(Normalize(nv), Normalize(ov), StringComparison.Ordinal)) { changed = true; break; }
            }

            int ver = 1;
            string verStr;
            if (current.TryGetValue(KeyVersion, out verStr)) int.TryParse(verStr, out ver);
            if (ver < 1) ver = 1;
            if (!changed) return ver;

            int newVer = ver + 1;
            using (var con = new SqlConnection(cs))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    foreach (string k in TextKeys)
                    {
                        string nv;
                        if (!values.TryGetValue(k, out nv)) continue;
                        Upsert(con, tx, k, Normalize(nv), modifiedBy);
                    }
                    Upsert(con, tx, KeyVersion, newVer.ToString(), modifiedBy);

                    using (var cmd = new SqlCommand(@"
                        INSERT INTO Booking_Policy_History
                            ([Version], Policy_Terms, Policy_Privacy, Policy_Refund, Policy_Cancellation, Created_Date, Created_By)
                        SELECT @ver,
                               (SELECT Config_Value FROM Booking_Policy_Config WHERE Config_Key = @kT),
                               (SELECT Config_Value FROM Booking_Policy_Config WHERE Config_Key = @kP),
                               (SELECT Config_Value FROM Booking_Policy_Config WHERE Config_Key = @kR),
                               (SELECT Config_Value FROM Booking_Policy_Config WHERE Config_Key = @kC),
                               GETDATE(), @by", con, tx))
                    {
                        cmd.Parameters.AddWithValue("@ver", newVer);
                        cmd.Parameters.AddWithValue("@kT", KeyTerms);
                        cmd.Parameters.AddWithValue("@kP", KeyPrivacy);
                        cmd.Parameters.AddWithValue("@kR", KeyRefund);
                        cmd.Parameters.AddWithValue("@kC", KeyCancellation);
                        cmd.Parameters.AddWithValue("@by", (object)modifiedBy ?? DBNull.Value);
                        cmd.ExecuteNonQuery();
                    }
                    tx.Commit();
                }
            }
            Invalidate();
            return newVer;
        }

        /// <summary>
        /// บันทึกว่าลูกค้ายอมรับนโยบายฉบับไหน เมื่อไหร่ บนใบจอง
        /// ยังไม่รัน migration (ไม่มีคอลัมน์) → เขียนลง Logs แทน ไม่ทำให้การจองล้ม
        /// </summary>
        public static bool RecordAcceptance(int reservationId, int version, string ip, string by)
        {
            if (reservationId <= 0) return false;
            string cs = ConnStr;
            string ipSafe = ip == null ? "" : (ip.Length > 64 ? ip.Substring(0, 64) : ip);
            try
            {
                using (var con = new SqlConnection(cs))
                using (var cmd = new SqlCommand(@"
                    UPDATE Reservation
                       SET Policy_Accepted_At = GETDATE(),
                           Policy_Accepted_Version = @ver,
                           Policy_Accepted_IP = @ip
                     WHERE ID = @id", con))
                {
                    cmd.Parameters.AddWithValue("@ver", version);
                    cmd.Parameters.AddWithValue("@ip", ipSafe);
                    cmd.Parameters.AddWithValue("@id", reservationId);
                    con.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                try
                {
                    new code().Logs(cs, "Booking Policy Accepted",
                        "Reservation " + reservationId + ": ยอมรับนโยบายฉบับที่ " + version
                        + " เมื่อ " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                        + " IP " + ipSafe + " (บันทึกบนใบจองไม่ได้: " + ex.Message
                        + " — รัน PHASE19_Migration_22)", by ?? "Customer");
                }
                catch { }
                return false;
            }
        }

        public static void Invalidate()
        {
            lock (_lock) { _cache = null; _loadedAt = DateTime.MinValue; }
        }

        // ── ภายใน ────────────────────────────────────────────────────────────

        private static string Normalize(string s)
        {
            return (s ?? "").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        }

        private static void Upsert(SqlConnection con, SqlTransaction tx, string key, string value, string by)
        {
            using (var cmd = new SqlCommand(@"
                IF EXISTS (SELECT 1 FROM Booking_Policy_Config WHERE Config_Key = @k)
                    UPDATE Booking_Policy_Config
                       SET Config_Value = @v, Modified_Date = GETDATE(), Modified_By = @by
                     WHERE Config_Key = @k;
                ELSE
                    INSERT INTO Booking_Policy_Config (Config_Key, Config_Value, Modified_Date, Modified_By)
                    VALUES (@k, @v, GETDATE(), @by);", con, tx))
            {
                cmd.Parameters.AddWithValue("@k", key);
                cmd.Parameters.AddWithValue("@v", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);
                cmd.Parameters.AddWithValue("@by", (object)by ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
        }

        private static Dictionary<string, string> LoadFromDb(string cs)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var con = new SqlConnection(cs))
            using (var cmd = new SqlCommand("SELECT Config_Key, Config_Value FROM Booking_Policy_Config", con))
            {
                con.Open();
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        string k = rd[0] == DBNull.Value ? null : rd[0].ToString();
                        if (string.IsNullOrEmpty(k)) continue;
                        map[k] = rd[1] == DBNull.Value ? null : rd[1].ToString();
                    }
                }
            }
            return map;
        }

        private static Dictionary<string, string> GetCache()
        {
            lock (_lock)
            {
                if (_cache != null && (DateTime.UtcNow - _loadedAt) < CacheTtl) return _cache;

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                DateTime? updated = null;
                string cs = ConnStr;
                if (!string.IsNullOrEmpty(cs))
                {
                    try
                    {
                        using (var con = new SqlConnection(cs))
                        using (var cmd = new SqlCommand(
                            "SELECT Config_Key, Config_Value, Modified_Date FROM Booking_Policy_Config", con))
                        {
                            con.Open();
                            using (var rd = cmd.ExecuteReader())
                            {
                                while (rd.Read())
                                {
                                    string k = rd[0] == DBNull.Value ? null : rd[0].ToString();
                                    if (string.IsNullOrEmpty(k)) continue;
                                    map[k] = rd[1] == DBNull.Value ? null : rd[1].ToString();
                                    if (rd[2] != DBNull.Value)
                                    {
                                        DateTime d = Convert.ToDateTime(rd[2]);
                                        if (!updated.HasValue || d > updated.Value) updated = d;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ยังไม่รัน migration 22 → ใช้ข้อความสำรอง
                    }
                }

                _cache = map;
                _updatedAt = updated;
                _loadedAt = DateTime.UtcNow;
                return map;
            }
        }

        /// <summary>ข้อความสำรองเมื่อยังไม่มีในฐานข้อมูล (สั้น ๆ — ฉบับเต็มอยู่ใน migration 22)</summary>
        private static string DefaultText(string key)
        {
            switch (key)
            {
                case KeyTerms:
                    return "การจองจะสมบูรณ์เมื่อที่พักได้รับยอดมัดจำหรือยอดชำระเต็มจำนวนแล้ว\n"
                         + "ยอดคงเหลือชำระ ณ วันเช็คอิน · ผู้เข้าพักต้องแสดงบัตรประชาชน/หนังสือเดินทาง\n"
                         + "งดใช้เสียงดังหลังเวลา 22.30 น. และปฏิบัติตามกฎระเบียบของที่พัก";
                case KeyPrivacy:
                    return "ที่พักใช้ข้อมูลของท่าน (ชื่อ เบอร์โทร หลักฐานการชำระเงิน) เพื่อจัดการการจอง "
                         + "ออกเอกสารการชำระเงิน และติดต่อเรื่องการเข้าพักเท่านั้น "
                         + "ไม่เปิดเผยแก่บุคคลภายนอก ยกเว้นตามที่กฎหมายกำหนด";
                case KeyRefund:
                    return "การคืนเงินเป็นไปตามนโยบายการยกเลิกการจอง "
                         + "โอนเงิน: คืนเข้าบัญชีที่แจ้ง · บัตร/ออนไลน์: คืนผ่านช่องทางเดิม";
                case KeyCancellation:
                    return "กรุณาติดต่อที่พักเพื่อยกเลิกหรือเลื่อนวันเข้าพัก พร้อมหมายเลขการจอง "
                         + "เงื่อนไขการคืนมัดจำเป็นไปตามที่ที่พักกำหนด";
                default:
                    return "";
            }
        }
    }
}
