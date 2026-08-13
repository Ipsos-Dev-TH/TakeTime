using System;
using System.Collections.Generic;
using System.Data;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// จับคู่ "บทสนทนา" กับ "การจอง" อัตโนมัติ — ทำให้ปุ่ม 💬 แชทลูกค้า ในตารางผู้เข้าพักรายวัน
    /// ขึ้นกับลูกค้าที่ทักมาทาง LINE / Facebook / TikTok ฯลฯ ไม่ใช่แค่ลูกค้า OTA (อีเมล)
    ///
    /// เดิม: ฝั่งอีเมล OTA จับคู่ให้ตอน ingest (`EmailChatService` อ่านเลข Booking ในอีเมล)
    ///        แต่ LINE/FB ไม่มีข้อมูลอะไรผูกกับการจองเลย → `OmniChannel_Contacts.Reservation_ID`
    ///        เป็น NULL → ปุ่มแชทจึงไม่ขึ้น
    ///
    /// ตัวนี้พยายามจับคู่จากหลายสัญญาณ เรียงตามความน่าเชื่อถือ:
    ///   1) การจองที่เกิดจากบทสนทนานี้เอง (AI_Booking_Actions.ConversationID) — แน่นอนที่สุด
    ///   2) เบอร์โทรที่ลูกค้าพิมพ์ในแชท → หาการจองของเบอร์นั้น
    ///   3) เลขการจอง / เลข Booking ของ OTA ที่ลูกค้าพิมพ์มา
    ///   4) เบอร์โทรที่เคยผูกกับ contact ไว้แล้ว (เช่น WhatsApp ที่ platform id คือเบอร์)
    ///
    /// พบแล้วเขียนลง `OmniChannel_Contacts.Reservation_ID` (+ เบอร์) และติด tag "จอง #id"
    /// ให้บทสนทนา → หน้าอื่น ๆ ที่อ่าน Reservation_ID ใช้ได้ทันทีโดยไม่ต้องแก้
    ///
    /// ปลอดภัย: ผูกเฉพาะการจองที่ยัง "มีชีวิต" (ยังไม่ยกเลิก และยังไม่เลยเช็คเอาท์นานเกินไป)
    /// และไม่เขียนทับถ้าจับคู่ไว้แล้ว
    /// </summary>
    public class ChatBookingLinker
    {
        private readonly string _conn;
        private readonly code _code = new code();

        /// <summary>ผูกย้อนหลังได้ไม่เกินกี่วันหลังเช็คเอาท์ (ลูกค้ามักทักหลังกลับ เช่น ลืมของ)</summary>
        private const int GraceDaysAfterCheckout = 14;

        public ChatBookingLinker(string connectionString)
        {
            _conn = connectionString;
        }

        /// <summary>
        /// พยายามจับคู่บทสนทนากับการจอง — เรียกได้ทุกข้อความ (no-op ถ้าจับคู่แล้ว).
        /// คืนเลขการจองที่ผูกได้ (0 = ยังจับคู่ไม่ได้). ห้ามโยน exception ออกไปรบกวน flow แชท
        /// </summary>
        public int TryLink(long conversationId, string messageText)
        {
            if (conversationId <= 0) return 0;
            try
            {
                // ผูกไว้แล้ว → ไม่ต้องทำอะไร (และไม่เขียนทับของเดิม)
                var cur = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ct.ID AS ContactID, ct.Reservation_ID, ct.MobilePhone, ct.Customer_MobilePhone
                        FROM OmniChannel_Conversations c
                        JOIN OmniChannel_Contacts ct ON ct.ID = c.ContactID
                       WHERE c.ID = @c",
                    new Dictionary<string, object> { { "@c", conversationId } });
                if (cur == null || cur.Rows.Count == 0) return 0;

                DataRow row = cur.Rows[0];
                if (row["Reservation_ID"] != DBNull.Value && Convert.ToInt64(row["Reservation_ID"]) > 0)
                    return Convert.ToInt32(row["Reservation_ID"]);

                long contactId = Convert.ToInt64(row["ContactID"]);
                string knownPhone = FirstNonEmpty(
                    row["Customer_MobilePhone"] == DBNull.Value ? null : row["Customer_MobilePhone"].ToString(),
                    row["MobilePhone"] == DBNull.Value ? null : row["MobilePhone"].ToString());

                // ── 1) การจองที่สร้างจากบทสนทนานี้ (AI/แชท) ──
                int resId = FindByConversation(conversationId);

                // ── 2) เบอร์โทรที่พิมพ์ในข้อความ ──
                if (resId <= 0)
                {
                    foreach (string phone in ExtractPhones(messageText))
                    {
                        resId = FindByPhone(phone);
                        if (resId > 0) { knownPhone = phone; break; }
                    }
                }

                // ── 3) เลขการจอง / เลข Booking ของ OTA ที่พิมพ์มา ──
                if (resId <= 0) resId = FindByBookingRef(messageText);

                // ── 4) เบอร์ที่ผูกกับ contact อยู่แล้ว ──
                if (resId <= 0 && !string.IsNullOrEmpty(knownPhone)) resId = FindByPhone(knownPhone);

                if (resId <= 0) return 0;

                Apply(contactId, conversationId, resId, knownPhone);
                return resId;
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "ChatBookingLink", $"conv {conversationId}: {ex.Message}", "SYSTEM"); }
                catch { }
                return 0;
            }
        }

        // ── สัญญาณที่ 1: การจองที่เกิดจากบทสนทนานี้ ───────────────────────────────
        private int FindByConversation(long conversationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 ba.ReservationID
                        FROM AI_Booking_Actions ba
                       WHERE ba.ConversationID = @c AND ba.ReservationID IS NOT NULL
                       ORDER BY ba.ID DESC",
                    new Dictionary<string, object> { { "@c", conversationId } });
                return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        // ── สัญญาณที่ 2/4: จากเบอร์โทร ─────────────────────────────────────────────
        private int FindByPhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 9) return 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 r.ID
                        FROM Reservation r
                       WHERE r.Customer_MobilePhone = @p
                         AND r.Status NOT IN (N'ยกเลิก', N'ไม่มาเช็คอิน')
                         AND r.CheckoutDate >= DATEADD(DAY, -@grace, CAST(GETDATE() AS DATE))
                       ORDER BY r.CheckinDate DESC",
                    new Dictionary<string, object> { { "@p", phone }, { "@grace", GraceDaysAfterCheckout } });
                return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0][0]) : 0;
            }
            catch { return 0; }
        }

        // ── สัญญาณที่ 3: เลขการจองในระบบ / เลข Booking ของ OTA ─────────────────────
        private int FindByBookingRef(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            try
            {
                // เลขการจองในระบบ: "จอง 1234", "การจอง #1234", "booking no 1234"
                foreach (Match m in Regex.Matches(text,
                    @"(?:จอง|การจอง|booking|reservation)\s*(?:no\.?|number|id|เลขที่)?\s*#?\s*(\d{1,8})",
                    RegexOptions.IgnoreCase))
                {
                    int id;
                    if (!int.TryParse(m.Groups[1].Value, out id) || id <= 0) continue;
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 ID FROM Reservation
                           WHERE ID = @id AND Status NOT IN (N'ยกเลิก', N'ไม่มาเช็คอิน')
                             AND CheckoutDate >= DATEADD(DAY, -@grace, CAST(GETDATE() AS DATE))",
                        new Dictionary<string, object> { { "@id", id }, { "@grace", GraceDaysAfterCheckout } });
                    if (dt?.Rows.Count > 0) return id;
                }

                // เลข Booking ของ OTA (ยาว 7-14 หลัก) — เทียบกับ OTA_Booking_ID
                foreach (Match m in Regex.Matches(text, @"\b(\d{7,14})\b"))
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 ID FROM Reservation
                           WHERE OTA_Booking_ID LIKE @b AND Status NOT IN (N'ยกเลิก', N'ไม่มาเช็คอิน')
                           ORDER BY ID DESC",
                        new Dictionary<string, object> { { "@b", "%" + m.Groups[1].Value + "%" } });
                    if (dt?.Rows.Count > 0) return Convert.ToInt32(dt.Rows[0][0]);
                }
            }
            catch { /* คอลัมน์ OTA ยังไม่มี → ข้าม */ }
            return 0;
        }

        // ── เขียนผลการจับคู่ ───────────────────────────────────────────────────────
        private void Apply(long contactId, long conversationId, int reservationId, string phone)
        {
            // ดึงชื่อ/เบอร์ลูกค้าจริงมาเติมให้ contact ด้วย (กล่องแชทจะได้โชว์ชื่อคน ไม่ใช่รหัส platform)
            string custName = null, custPhone = phone;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 r.Customer_MobilePhone, c.Name
                        FROM Reservation r
                        LEFT JOIN Customer c ON c.MobilePhone = r.Customer_MobilePhone
                       WHERE r.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt?.Rows.Count > 0)
                {
                    custPhone = FirstNonEmpty(dt.Rows[0]["Customer_MobilePhone"]?.ToString(), phone);
                    custName = dt.Rows[0]["Name"]?.ToString();
                }
            }
            catch { }

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE OmniChannel_Contacts
                     SET Reservation_ID = @res,
                         Customer_MobilePhone = COALESCE(NULLIF(@phone, ''), Customer_MobilePhone),
                         DisplayName = CASE WHEN NULLIF(@name, '') IS NOT NULL THEN @name ELSE DisplayName END,
                         Updated_Date = GETDATE()
                   WHERE ID = @cid AND Reservation_ID IS NULL",
                new Dictionary<string, object>
                {
                    { "@res", reservationId }, { "@phone", custPhone ?? "" },
                    { "@name", custName ?? "" }, { "@cid", contactId }
                });

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE OmniChannel_Conversations
                     SET Tags = CASE WHEN Tags IS NULL OR Tags = '' THEN @tag ELSE Tags END,
                         Updated_Date = GETDATE()
                   WHERE ID = @conv",
                new Dictionary<string, object> { { "@tag", "จอง #" + reservationId }, { "@conv", conversationId } });

            try
            {
                _code.Logs(_conn, "ChatBookingLink",
                    $"ผูกบทสนทนา {conversationId} กับการจอง #{reservationId}" +
                    (string.IsNullOrEmpty(custPhone) ? "" : $" (เบอร์ {custPhone})"), "SYSTEM");
            }
            catch { }
        }

        // ── helpers ────────────────────────────────────────────────────────────────

        /// <summary>ดึงเบอร์โทรไทยจากข้อความ (รองรับ 0812345678 / 081-234-5678 / +66812345678)</summary>
        private static List<string> ExtractPhones(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            foreach (Match m in Regex.Matches(text, @"(?:\+?66|0)[\s\-]?\d{1,2}[\s\-]?\d{3}[\s\-]?\d{3,4}"))
            {
                string digits = Regex.Replace(m.Value, @"[^\d]", "");
                if (digits.StartsWith("66") && digits.Length >= 11) digits = "0" + digits.Substring(2);
                if (digits.Length >= 9 && digits.Length <= 10 && !list.Contains(digits))
                    list.Add(digits);
                if (list.Count >= 3) break;   // กันข้อความยาวยิง query รัว
            }
            return list;
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (var v in values)
                if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
            return null;
        }
    }
}
