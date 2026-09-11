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
    ///   5) "ชื่อผู้ติดต่อ" ตรงกับชื่อผู้เข้าพัก — ใช้กับอีเมล OTA เป็นหลัก (Agoda/Booking ส่งมาด้วย
    ///      อีเมล alias ไม่มีเบอร์ ไม่มีเลขจองในเนื้อความ เหลือแต่ชื่อ) ผูกเฉพาะเมื่อ **ตรงใบเดียว**
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

        /// <summary>จับคู่ด้วย "ชื่อ" มองไปข้างหน้าได้ไม่เกินกี่วัน (กันจองล่วงหน้าข้ามปีมาชนกัน)</summary>
        private const int NameMatchFutureDays = 400;

        /// <summary>มี Reservation.OTA_Guest_Name ไหม (บาง deployment ยังไม่ได้รัน migration) — cache ทั้ง process</summary>
        private static bool? _hasOtaGuestName;

        public ChatBookingLinker(string connectionString)
        {
            _conn = connectionString;
        }

        /// <summary>
        /// พยายามจับคู่บทสนทนากับการจอง — เรียกได้ทุกข้อความ (no-op ถ้าจับคู่แล้ว).
        /// คืนเลขการจองที่ผูกได้ (0 = ยังจับคู่ไม่ได้). ห้ามโยน exception ออกไปรบกวน flow แชท
        /// </summary>
        /// <param name="messageText">เนื้อความ "ต้นฉบับ" (ก่อนตัดของแถม OTA — เลขจองมักอยู่ในส่วนหัวที่ถูกตัดทิ้ง)</param>
        /// <param name="guestName">ชื่อผู้ส่ง/ผู้เข้าพักจากช่องทางนั้น (อีเมล OTA ใส่ชื่อผู้เข้าพักมาให้) — ใช้เป็นสัญญาณสุดท้าย</param>
        public int TryLink(long conversationId, string messageText, string guestName = null)
        {
            if (conversationId <= 0) return 0;
            try
            {
                // ผูกไว้แล้ว → ไม่ต้องทำอะไร (และไม่เขียนทับของเดิม)
                var cur = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ct.ID AS ContactID, ct.Reservation_ID, ct.MobilePhone, ct.Customer_MobilePhone,
                             ct.DisplayName
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
                string displayName = row["DisplayName"] == DBNull.Value ? null : row["DisplayName"].ToString();

                string via = "";

                // ── 1) การจองที่สร้างจากบทสนทนานี้ (AI/แชท) ──
                int resId = FindByConversation(conversationId);
                if (resId > 0) via = "บทสนทนาสร้างการจอง";

                // ── 2) เบอร์โทรที่พิมพ์ในข้อความ ──
                if (resId <= 0)
                {
                    foreach (string phone in ExtractPhones(messageText))
                    {
                        resId = FindByPhone(phone);
                        if (resId > 0) { knownPhone = phone; via = "เบอร์ในข้อความ"; break; }
                    }
                }

                // ── 3) เลขการจอง / เลข Booking ของ OTA ที่พิมพ์มา ──
                if (resId <= 0)
                {
                    resId = FindByBookingRef(messageText);
                    if (resId > 0) via = "เลขการจองในข้อความ";
                }

                // ── 4) เบอร์ที่ผูกกับ contact อยู่แล้ว ──
                if (resId <= 0 && !string.IsNullOrEmpty(knownPhone))
                {
                    resId = FindByPhone(knownPhone);
                    if (resId > 0) via = "เบอร์ที่ผูกกับผู้ติดต่อ";
                }

                // ── 5) ชื่อผู้เข้าพัก (สัญญาณสุดท้าย — ผูกเฉพาะเมื่อตรงใบเดียวเท่านั้น) ──
                //     เคสจริง: Agoda ส่งข้อความลูกค้ามาทางอีเมล alias (ไม่มีเบอร์ ไม่มีเลขจองในเนื้อความ)
                //     เหลือแต่ชื่อผู้ส่ง = ชื่อผู้เข้าพัก → ถ้าไม่ใช้สัญญาณนี้ ปุ่ม 💬 ไม่มีวันขึ้น
                if (resId <= 0)
                {
                    resId = FindByGuestName(guestName);
                    if (resId <= 0 && !NamesLookSame(guestName, displayName))
                        resId = FindByGuestName(displayName);
                    if (resId > 0) via = "ชื่อผู้เข้าพัก";
                }

                if (resId <= 0) return 0;

                Apply(contactId, conversationId, resId, knownPhone, via);
                return resId;
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "ChatBookingLink", $"conv {conversationId}: {ex.Message}", "SYSTEM"); }
                catch { }
                return 0;
            }
        }

        private static DateTime _lastSweep = DateTime.MinValue;
        private static readonly object _sweepLock = new object();

        /// <summary>
        /// เรียกจาก timer หลัก (Global.asax) — กวาดจับคู่ย้อนหลังทุก ~10 นาที
        /// แยกจากรอบอ่านอีเมล เพราะแชท LINE/Facebook/เว็บ ก็ต้องได้ผูกด้วย แม้ปิดแชททางอีเมลไว้
        /// </summary>
        public static void SweepIfDue(string connectionString, int everyMinutes = 10)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return;
            lock (_sweepLock)
            {
                if ((DateTime.Now - _lastSweep).TotalMinutes < everyMinutes) return;
                _lastSweep = DateTime.Now;
            }
            try { new ChatBookingLinker(connectionString).RelinkUnlinked(); }
            catch { }
        }

        /// <summary>
        /// กวาดจับคู่ย้อนหลัง — บทสนทนาที่ยังไม่ผูกการจอง (ปุ่ม 💬 ในตารางจองยังไม่ขึ้น) ลองผูกใหม่อีกครั้ง
        ///
        /// จำเป็นเพราะ `TryLink` ทำงานตอน "รับข้อความ" เท่านั้น — ข้อความที่เข้ามาก่อนจะมีสัญญาณใหม่
        /// (เช่น ชื่อผู้เข้าพัก) หรือเข้ามา "ก่อน" การจองถูกสร้าง จะค้างเป็น NULL ตลอดไปถ้าไม่กวาดซ้ำ
        /// เรียกจากรอบ poll อีเมล / timer — ไม่โยน exception และจำกัดจำนวนต่อรอบ
        /// </summary>
        /// <returns>จำนวนบทสนทนาที่ผูกได้ในรอบนี้</returns>
        public int RelinkUnlinked(int maxConversations = 60, int lookbackDays = 45)
        {
            int linked = 0;
            try
            {
                var pending = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@max) c.ID AS ConvID, ISNULL(ct.DisplayName, N'') AS DisplayName
                        FROM OmniChannel_Conversations c
                        JOIN OmniChannel_Contacts ct ON ct.ID = c.ContactID
                       WHERE ct.Reservation_ID IS NULL
                         AND ISNULL(c.LastMessageDate, c.Created_Date) >= DATEADD(DAY, -@days, GETDATE())
                       ORDER BY ISNULL(c.LastMessageDate, c.Created_Date) DESC",
                    new Dictionary<string, object>
                    {
                        { "@max", maxConversations < 1 ? 1 : maxConversations },
                        { "@days", lookbackDays < 1 ? 1 : lookbackDays }
                    });
                if (pending == null) return 0;

                foreach (DataRow p in pending.Rows)
                {
                    long convId = Convert.ToInt64(p["ConvID"]);
                    string name = p["DisplayName"].ToString();

                    // รวมข้อความขาเข้าล่าสุดมาเป็นแหล่งค้นเลขจอง/เบอร์
                    // (Metadata เก็บเนื้อความต้นฉบับก่อนตัดของแถม OTA ไว้ — เลขจองมักอยู่ตรงนั้น)
                    string text = "";
                    try
                    {
                        var msgs = _code.DatabaseQuerySafe(_conn,
                            @"SELECT TOP 5 ISNULL(Content, N'') AS Content, ISNULL(Metadata, N'') AS Metadata
                                FROM OmniChannel_Messages
                               WHERE ConversationID = @c AND Direction = 'IN'
                               ORDER BY ID DESC",
                            new Dictionary<string, object> { { "@c", convId } });
                        if (msgs != null)
                            foreach (DataRow m in msgs.Rows)
                                text += m["Content"] + "\n" + m["Metadata"] + "\n";
                    }
                    catch { }

                    if (TryLink(convId, text, name) > 0) linked++;
                }
            }
            catch (Exception ex)
            {
                try { _code.Logs(_conn, "ChatBookingLink", $"relink sweep failed: {ex.Message}", "SYSTEM"); }
                catch { }
            }
            return linked;
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

        // ── สัญญาณที่ 5: ชื่อผู้เข้าพัก ─────────────────────────────────────────────
        // ดึงเฉพาะการจองที่ยัง "มีชีวิต" มาเทียบในหน่วยความจำ (ชุดเล็ก) แทนการเทียบใน SQL
        // เพราะต้อง normalize (ตัดคำนำหน้า/ช่องว่าง/เครื่องหมาย) และยอมสลับชื่อ-นามสกุล
        // **กฎกันผูกผิด: ต้องตรง "ใบเดียว" เท่านั้น — ตรงหลายใบ = กำกวม ปล่อยว่างไว้ดีกว่าผูกผิดคน**
        private int FindByGuestName(string rawName)
        {
            string[] keys = NameKeys(rawName);
            if (keys == null) return 0;

            try
            {
                bool hasOta = HasOtaGuestNameColumn();
                string sql =
                    "SELECT r.ID, ISNULL(cu.Name, N'') AS CustName, " +
                    (hasOta ? "ISNULL(r.OTA_Guest_Name, N'')" : "N''") + @" AS OtaName
                        FROM Reservation r
                        LEFT JOIN Customer cu ON cu.MobilePhone = r.Customer_MobilePhone
                       WHERE r.Status NOT IN (N'ยกเลิก', N'ไม่มาเช็คอิน')
                         AND r.CheckoutDate >= DATEADD(DAY, -@grace,  CAST(GETDATE() AS DATE))
                         AND r.CheckinDate  <= DATEADD(DAY,  @future, CAST(GETDATE() AS DATE))";

                var dt = _code.DatabaseQuerySafe(_conn, sql, new Dictionary<string, object>
                {
                    { "@grace", GraceDaysAfterCheckout }, { "@future", NameMatchFutureDays }
                });
                if (dt == null || dt.Rows.Count == 0) return 0;

                var hits = new List<int>();
                foreach (DataRow r in dt.Rows)
                {
                    if (!KeysMatch(keys, r["CustName"].ToString()) &&
                        !KeysMatch(keys, r["OtaName"].ToString())) continue;

                    int id = Convert.ToInt32(r["ID"]);
                    if (!hits.Contains(id)) hits.Add(id);
                    if (hits.Count > 1) break;      // กำกวมแล้ว ไม่ต้องไล่ต่อ
                }

                if (hits.Count == 1) return hits[0];
                if (hits.Count > 1)
                {
                    try
                    {
                        _code.Logs(_conn, "ChatBookingLink",
                            $"ชื่อ \"{rawName}\" ตรงกับการจองมากกว่า 1 ใบ — ไม่ผูกอัตโนมัติ (กันผูกผิดคน)", "SYSTEM");
                    }
                    catch { }
                }
            }
            catch { /* คอลัมน์/ตารางไม่ครบ → ข้ามสัญญาณนี้ */ }
            return 0;
        }

        /// <summary>Reservation.OTA_Guest_Name มีจริงไหม (deployment เก่ายังไม่ได้รัน PHASE18_12)</summary>
        private bool HasOtaGuestNameColumn()
        {
            if (_hasOtaGuestName.HasValue) return _hasOtaGuestName.Value;
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT CASE WHEN COL_LENGTH('Reservation', 'OTA_Guest_Name') IS NULL THEN 0 ELSE 1 END", null);
                _hasOtaGuestName = dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { _hasOtaGuestName = false; }
            return _hasOtaGuestName.Value;
        }

        // ── เขียนผลการจับคู่ ───────────────────────────────────────────────────────
        private void Apply(long contactId, long conversationId, int reservationId, string phone, string via = null)
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
                    (string.IsNullOrEmpty(via) ? "" : $" [จาก{via}]") +
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

        // ── การเทียบ "ชื่อ" ────────────────────────────────────────────────────────

        /// <summary>ชื่อกลาง ๆ / ชื่อช่องทาง ที่ห้ามใช้จับคู่เด็ดขาด (จะไปชนการจองสุ่ม ๆ)</summary>
        private static readonly string[] GenericNames =
        {
            "guest", "guests", "customer", "user", "test", "admin", "support", "reception",
            "agoda", "agodaguest", "booking", "bookingcom", "expedia", "trip", "tripcom",
            "ctrip", "airbnb", "staah", "traveloka", "hotelbeds", "noreply", "donotreply",
            "ลูกค้า", "แขก", "ผู้เข้าพัก", "ทดสอบ", "ไม่ระบุ"
        };

        /// <summary>คำนำหน้าที่เขียนแยกคำ (ภาษาอังกฤษ) — ตัดทั้ง token</summary>
        private static readonly string[] NameTitles =
        {
            "mr", "mrs", "ms", "miss", "mister", "dr", "prof"
        };

        /// <summary>คำนำหน้าไทย — เขียนติดกับชื่อ (คุณสมชาย) ⇒ ต้องตัดแบบ "ขึ้นต้นด้วย" ไม่ใช่ทั้ง token
        /// เรียงยาว→สั้น เพราะ "นางสาว" ต้องถูกตัดก่อน "นาง"
        /// จงใจไม่ใส่ ดร/ดช/ดญ/นส — ชนกับชื่อจริง (ดรุณี, นภา) และรูปย่อมีจุดคั่นอยู่แล้ว
        /// (ตัวอักษรเดี่ยวที่เหลือจากจุดถูกทิ้งด้วยกฎ "token ยาว 1 ตัวไม่นับ")</summary>
        private static readonly string[] ThaiTitlePrefixes = { "นางสาว", "นาง", "นาย", "คุณ" };

        /// <summary>
        /// แปลงชื่อเป็น "กุญแจเทียบ" 2 แบบ: [0] ตัวอักษรล้วนเรียงตามที่เขียน, [1] เรียงคำตามตัวอักษร
        /// (รองรับสลับชื่อ-นามสกุล เช่น "Somchai Jaidee" กับ "Jaidee Somchai")
        /// คืน null ถ้าชื่อ "ไม่น่าเชื่อถือพอ" จะเอามาจับคู่
        /// </summary>
        private static string[] NameKeys(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            string s = raw.Trim().ToLowerInvariant();
            if (s.IndexOf('@') >= 0) return null;                    // อีเมล ไม่ใช่ชื่อ

            // เหลือแต่ตัวอักษร/ตัวเลข + ช่องว่าง
            // ⚠ ต้องเก็บ "สระ/วรรณยุกต์ไทย" ไว้ด้วย — char.IsLetterOrDigit มองว่าไม่ใช่ตัวอักษร
            //   (Unicode NonSpacingMark) ถ้าปล่อยให้กลายเป็นช่องว่าง "ใจดี" จะเหลือ "ใจด"
            //   ⇒ ไปชนกับ "ใจดา" "ใจโด" ได้ = เสี่ยงผูกผิดคน
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                bool keep = char.IsLetterOrDigit(c) ||
                    System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) ==
                        System.Globalization.UnicodeCategory.NonSpacingMark;
                sb.Append(keep ? c : ' ');
            }

            var tokens = new List<string>();
            foreach (string t in sb.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Array.IndexOf(NameTitles, t) >= 0) continue;     // ตัดคำนำหน้าภาษาอังกฤษ
                tokens.Add(t);
            }
            if (tokens.Count == 0) return null;

            // ตัดคำนำหน้าไทยที่เขียนติดชื่อ (คุณสมชาย / นางสาวสมหญิง) — เฉพาะคำแรกเท่านั้น
            foreach (string p in ThaiTitlePrefixes)
            {
                if (tokens[0] == p) { tokens[0] = ""; break; }
                if (tokens[0].Length > p.Length && tokens[0].StartsWith(p, StringComparison.Ordinal))
                { tokens[0] = tokens[0].Substring(p.Length); break; }
            }

            // ทิ้งตัวอักษรเดี่ยว — เกิดจากคำย่อที่มีจุดคั่น (น.ส. / ด.ช.) ไม่ได้ช่วยเทียบชื่อ
            tokens.RemoveAll(t => t.Length <= 1);
            if (tokens.Count == 0) return null;

            string joined = string.Join("", tokens);
            if (joined.Length < 6) return null;                      // สั้นเกิน เสี่ยงชนมั่ว
            bool allDigits = true;
            foreach (char c in joined) if (!char.IsDigit(c)) { allDigits = false; break; }
            if (allDigits) return null;                              // เลขล้วน = platform id
            if (tokens.Count < 2 && joined.Length < 8) return null;   // คำเดียวสั้น ๆ ไม่พอ

            foreach (string g in GenericNames)
                if (joined == g ||
                    (joined.StartsWith(g, StringComparison.Ordinal) && joined.Length <= g.Length + 2)) return null;

            var sorted = new List<string>(tokens);
            sorted.Sort(StringComparer.Ordinal);
            return new[] { joined, string.Join("", sorted) };
        }

        /// <summary>กุญแจของชื่อที่ผู้ติดต่อส่งมา ตรงกับชื่อในการจองใบนี้ไหม</summary>
        private static bool KeysMatch(string[] keys, string candidate)
        {
            string[] other = NameKeys(candidate);
            if (other == null) return false;
            return keys[0] == other[0] || keys[1] == other[1];
        }

        /// <summary>สองชื่อนี้คือชื่อเดียวกันไหม (ใช้กันเทียบซ้ำโดยเปล่าประโยชน์)</summary>
        private static bool NamesLookSame(string a, string b)
        {
            string[] ka = NameKeys(a), kb = NameKeys(b);
            if (ka == null || kb == null) return false;
            return ka[0] == kb[0];
        }
    }
}
