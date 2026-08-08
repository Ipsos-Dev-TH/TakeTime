using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// อ่านอีเมลจอง STAAH (Agoda/Booking.com ฯลฯ) → ลงจองในระบบ TakeTime.
    /// พอร์ตจาก external console app (Wachira-d/GetReservationfromGmail) เข้าระบบ:
    /// config อยู่ใน Accounting_Integration_Config (แก้ผ่านหน้า Admin), เก็บ gross(refsell_amt)/
    /// net(AMOUNT)/paymentType เพื่อต่อกับ OTA settlement, เลือกได้ว่าจะยิงสร้างเอกสารหรือแค่ลงจอง.
    /// เรียกจาก timer (Global.asax) หรือปุ่ม "ดึงตอนนี้" ในหน้า Admin. dedup ด้วย OTA_Booking_ID/Remark.
    /// </summary>
    public class EmailReservationService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        // config (โหลดครั้งเดียวตอนสร้าง)
        private readonly string _imapServer, _imapUser, _imapPassword, _processedLabel, _failedLabel, _fromContains;
        private readonly int _imapPort, _maxStayDays, _maxDaysFuture;
        private readonly bool _moveFailed, _notifyTelegram, _createDocument;

        public EmailReservationService(string connectionString)
        {
            _conn = connectionString;
            _imapServer = Cfg("Email_Rsv_ImapServer", "imap.gmail.com");
            _imapPort = int.TryParse(Cfg("Email_Rsv_ImapPort", "993"), out var p) ? p : 993;
            _imapUser = Cfg("Email_Rsv_Username", "");
            _imapPassword = _code.Derypt(Cfg("Email_Rsv_Password_Encrypted", ""));
            _processedLabel = Cfg("Email_Rsv_ProcessedLabel", "STAAH-Processed");
            _failedLabel = Cfg("Email_Rsv_FailedLabel", "STAAH-Failed");
            _fromContains = Cfg("Email_Rsv_FromContains", "staah");
            _maxStayDays = int.TryParse(Cfg("Email_Rsv_MaxStayDays", "30"), out var m) ? m : 30;
            _maxDaysFuture = int.TryParse(Cfg("Email_Rsv_MaxDaysFuture", "365"), out var f) ? f : 365;
            _moveFailed = Cfg("Email_Rsv_MoveFailed", "1") == "1";
            _notifyTelegram = Cfg("Email_Rsv_NotifyTelegram", "1") == "1";
            _createDocument = Cfg("Email_Rsv_CreateDocument", "0") == "1";
        }

        private string Cfg(string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString();
                    return string.IsNullOrEmpty(v) ? def : v;
                }
            }
            catch { }
            return def;
        }

        public static bool IsEnabled(string conn)
        {
            try
            {
                var c = new code();
                var dt = c.DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = 'Email_Rsv_Enabled'", null);
                return dt?.Rows.Count > 0 && dt.Rows[0][0]?.ToString() == "1";
            }
            catch { return false; }
        }

        public class IntakeResult
        {
            public int Fetched, Created, Duplicate, Cancelled, Failed;
            public List<string> Messages = new List<string>();
            public string Error;
            public override string ToString() =>
                Error != null ? "ผิดพลาด: " + Error
                : $"ดึง {Fetched} อีเมล → สร้าง {Created}, ซ้ำ {Duplicate}, ยกเลิก {Cancelled}, ล้มเหลว {Failed}";
        }

        /// <summary>ทดสอบเชื่อมต่อ IMAP + auth (ไม่แตะอีเมล). ใช้จากปุ่ม "ทดสอบการเชื่อมต่อ".</summary>
        public (bool, string) TestConnection()
        {
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
                return (false, "ยังไม่ได้ตั้งค่าอีเมล/รหัสผ่าน");
            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect(_imapServer, _imapPort, true);
                    client.Authenticate(_imapUser, _imapPassword);
                    var inbox = client.Inbox;
                    inbox.Open(FolderAccess.ReadOnly);
                    int total = inbox.Count;
                    client.Disconnect(true);
                    return (true, $"เชื่อมต่อสำเร็จ ({_imapUser}) — Inbox มี {total} ฉบับ");
                }
            }
            catch (Exception ex)
            {
                return (false, "เชื่อมต่อไม่สำเร็จ: " + ex.Message);
            }
        }

        /// <summary>ดึง+ประมวลผลอีเมลจองทั้งหมดที่ยังไม่อ่าน. ปลอดภัยเรียกซ้ำ (idempotent ด้วย dedup).</summary>
        public IntakeResult ProcessEmails()
        {
            var res = new IntakeResult();
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
            {
                res.Error = "ยังไม่ได้ตั้งค่าอีเมล/รหัสผ่าน (หน้า Admin)";
                return res;
            }

            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect(_imapServer, _imapPort, true);
                    client.Authenticate(_imapUser, _imapPassword);
                    var inbox = client.Inbox;
                    inbox.Open(FolderAccess.ReadWrite);

                    var processed = GetOrCreateFolder(client, _processedLabel);
                    var failed = _moveFailed ? GetOrCreateFolder(client, _failedLabel) : null;

                    // กรองแค่ผู้ส่ง + ยังไม่อ่าน — ไม่กรองหัวเรื่อง เพราะอีเมล "แก้ไข" ของ STAAH
                    // ใช้หัวเรื่อง "New Reservation ..." เหมือนจองใหม่ (สถานะจริงอยู่ในเนื้อเมล)
                    // อีเมลที่ไม่ใช่ใบจองจะ parse ไม่ได้ แล้วถูกรายงานเป็นล้มเหลวตามปกติ
                    var query = SearchQuery.And(
                        SearchQuery.FromContains(_fromContains),
                        SearchQuery.NotSeen);
                    var uids = inbox.Search(query);
                    res.Fetched = uids.Count;

                    foreach (var uid in uids)
                    {
                        MimeMessage msg = null;
                        string subject = "";
                        bool ok = false, dup = false;
                        try
                        {
                            msg = inbox.GetMessage(uid);
                            subject = msg.Subject ?? "";
                            string kind;
                            var (success, isDup, cancelled, message) = ProcessOne(msg, subject, out kind);
                            ok = success; dup = isDup;
                            if (success && cancelled) res.Cancelled++;
                            else if (success) res.Created++;
                            else if (isDup) res.Duplicate++;
                            else res.Failed++;
                            res.Messages.Add($"[{kind}] {message}");
                            if (_notifyTelegram && (success || !isDup))
                                Notify($"{(success ? "✅" : "⚠️")} STAAH {kind}: {message}");
                        }
                        catch (Exception ex)
                        {
                            res.Failed++;
                            res.Messages.Add($"[error] {subject}: {ex.Message}");
                            _code.Logs(_conn, "EmailReservation", $"process email failed: {ex.Message}", "SYSTEM");
                        }

                        try
                        {
                            if (ok || dup) inbox.MoveTo(uid, processed);
                            else if (_moveFailed && failed != null) inbox.MoveTo(uid, failed);
                            else inbox.AddFlags(uid, MessageFlags.Seen, true);
                        }
                        catch { }
                    }
                    client.Disconnect(true);
                }
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
                _code.Logs(_conn, "EmailReservation", $"IMAP error: {ex.Message}", "SYSTEM");
                if (_notifyTelegram) Notify("❌ STAAH intake error: " + ex.Message);
            }
            return res;
        }

        // คืน (success, isDuplicate, isCancellation, message)
        private (bool, bool, bool, string) ProcessOne(MimeMessage msg, string subject, out string kind)
        {
            // ⚠️ สถานะจริงอยู่ใน "เนื้ออีเมล" (Bookings Status: Confirmed/Modified/Cancelled)
            // ไม่ใช่หัวเรื่อง — อีเมลแก้ไขของ STAAH ใช้หัวเรื่อง "New Reservation ..." เหมือนเดิม
            // ถ้าดูแต่หัวเรื่องจะถูกมองเป็นจองใหม่ แล้ว dedup ตัดทิ้ง = การแก้ไขหายเงียบ ๆ
            var rooms = ExtractRoomBookings(msg.HtmlBody);
            string bodyStatus = rooms.Count > 0 ? (rooms[0].BookingsStatus ?? "") : "";
            string signal = bodyStatus + " | " + (subject ?? "");

            if (Has(signal, "Cancel"))
            {
                kind = "ยกเลิก";
                return ProcessCancellation(rooms);
            }
            if (Has(signal, "Modif") || Has(signal, "Amend") || Has(signal, "Change") || Has(signal, "Update"))
            {
                kind = "แก้ไข";
                return ProcessModification(rooms);
            }
            kind = "จองใหม่";
            return ProcessNewReservation(rooms);
        }

        private static bool Has(string haystack, string needle) =>
            (haystack ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Parse ──────────────────────────────────────────────────────────────
        private class RoomBooking
        {
            public string ChannelName, BookingsStatus, BookingId, GuestName, MobilePhone, RoomType, PaymentType;
            public int NoOfRooms = 1, Adults = 1;
            public double NetAmount;      // AMOUNT ต่อคืน (net ที่ OTA จะโอน)
            public double GrossTotal;     // refsell_amt (ราคาลูกค้าจ่าย OTA — ระดับ booking)
            public DateTime CheckIn, CheckOut;
        }

        private List<RoomBooking> ExtractRoomBookings(string html)
        {
            var list = new List<RoomBooking>();
            if (string.IsNullOrEmpty(html)) return list;
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            string text = doc.DocumentNode.InnerText;

            string channel = Rx(text, @"Channel Name:\s*(.+?)\s*(?:\n|Bookings Status)");
            string status = Rx(text, @"Bookings Status:\s*(.+?)\s*(?:\n|Booking Id)");
            string bookingId = Rx(text, @"Booking Id#?:\s*(.+?)\s*(?:\n|$)");
            string paymentType = Rx(text, @"Payment Type:\s*(.+?)\s*(?:\n|$)");
            // gross ที่ลูกค้าจ่าย OTA (STAAH ใส่ใน refsell_amt) + Total (All Inclusive) fallback
            double gross = ParseMoney(Rx(text, @"refsell_amt\s*:?\s*([\d,\.]+)"));
            if (gross <= 0) gross = ParseMoney(Rx(text, @"Total\s*\(All Inclusive\)\s*:?\s*THB\s*([\d,\.]+)"));

            var nameNode = doc.DocumentNode.SelectSingleNode("//span[contains(., 'CONTACT NAME')]/following-sibling::span[1]");
            string guest = nameNode?.InnerText?.Trim().Replace("'", "") ?? "";
            var mobileNode = doc.DocumentNode.SelectSingleNode("//span[contains(., 'CONTACT NUMBER')]/following-sibling::span[1]");
            string phone = SanitizePhone(mobileNode?.InnerText?.Trim() ?? "");
            if (string.IsNullOrWhiteSpace(phone) || phone.Length < 9)
                phone = PhoneFromBookingId(bookingId);

            var roomRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'ROOM TYPE')]]");
            var dateRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'CHECK-IN')]]");
            var adultRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'ADULTS')]]");
            if (roomRows == null || roomRows.Count == 0) return list;

            for (int a = 0; a < roomRows.Count; a++)
            {
                try
                {
                    var b = new RoomBooking
                    {
                        ChannelName = channel, BookingsStatus = status, BookingId = bookingId,
                        GuestName = guest, MobilePhone = phone, PaymentType = paymentType, GrossTotal = gross
                    };
                    var rt = roomRows[a].SelectSingleNode(".//span[contains(., 'ROOM TYPE')]/following-sibling::span[1]");
                    b.RoomType = rt?.InnerText?.Trim() ?? "";
                    if (string.IsNullOrWhiteSpace(b.RoomType)) continue;
                    var roomsNode = roomRows[a].SelectSingleNode(".//span[contains(., 'NO OF ROOMS')]/following-sibling::text()[1]");
                    if (roomsNode != null && int.TryParse(roomsNode.InnerText?.Trim(), out int rooms)) b.NoOfRooms = rooms;

                    if (dateRows != null && a < dateRows.Count)
                    {
                        var ci = dateRows[a].SelectSingleNode(".//span[contains(., 'CHECK-IN')]/following-sibling::text()[1]");
                        var co = dateRows[a].SelectSingleNode(".//span[contains(., 'CHECK-OUT')]/following-sibling::text()[1]");
                        DateTime.TryParseExact(ci?.InnerText?.Trim() ?? "", "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cid);
                        DateTime.TryParseExact(co?.InnerText?.Trim() ?? "", "dd MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var cod);
                        b.CheckIn = cid; b.CheckOut = cod;
                    }
                    if (adultRows != null && a < adultRows.Count)
                    {
                        var ad = adultRows[a].SelectSingleNode(".//span[contains(., 'ADULTS')]/following-sibling::span[1]");
                        if (ad != null && int.TryParse(ad.InnerText?.Trim(), out int adults)) b.Adults = adults;
                        var am = adultRows[a].SelectSingleNode(".//span[contains(., 'AMOUNT')]/following-sibling::span[1]");
                        if (am != null) b.NetAmount = ParseMoney((am.InnerText ?? "").Replace("THB", ""));
                    }
                    list.Add(b);
                }
                catch (Exception ex) { _code.Logs(_conn, "EmailReservation", $"parse row {a}: {ex.Message}", "SYSTEM"); }
            }
            return list;
        }

        // ── New reservation ─────────────────────────────────────────────────────
        private (bool, bool, bool, string) ProcessNewReservation(List<RoomBooking> rooms)
        {
            if (rooms.Count == 0) return (false, false, false, "แยกข้อมูลจากอีเมลไม่ได้ (format เปลี่ยน?)");
            var head = rooms[0];
            if (string.IsNullOrWhiteSpace(head.BookingId)) return (false, false, false, "ไม่พบ Booking ID");

            // dedup — ข้ามการจองที่ถูกยกเลิกแล้ว เพื่อให้ Modified (cancel เดิม+สร้างใหม่) สร้างใหม่ได้
            var existing = _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 ID FROM Reservation
                  WHERE (OTA_Booking_ID = @b OR Remark LIKE @p)
                    AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน')",
                new Dictionary<string, object> { { "@b", head.BookingId }, { "@p", "%" + head.BookingId + "%" } });
            if (existing?.Rows.Count > 0) return (false, true, false, $"Booking {head.BookingId} มีในระบบแล้ว (#{existing.Rows[0][0]})");

            // validate
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return (false, false, false, "วันเช็คอิน/เอาท์ไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays) return (false, false, false, $"จำนวนคืน {stayDays} เกิน {_maxStayDays}");
            if ((head.CheckIn - DateTime.Today).TotalDays > _maxDaysFuture) return (false, false, false, "จองล่วงหน้าเกินกำหนด");

            int resId = SaveReservation(rooms, stayDays);
            if (resId <= 0) return (false, false, false, "บันทึกการจองไม่สำเร็จ (ห้องไม่ว่าง/ไม่มี mapping)");

            if (_createDocument) TryEnqueueDocument(resId, head);
            return (true, false, false, $"จอง #{resId} {head.GuestName} {head.CheckIn:dd/MM} ({head.ChannelName}) gross={head.GrossTotal:N2}");
        }

        // ── Save (SERIALIZABLE txn, ตรงตาม external app) ──────────────────────────
        private int SaveReservation(List<RoomBooking> rooms, int stayDays)
        {
            var head = rooms[0];
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var tx = con.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var plan = new List<(int accomId, int adults, double amt)>();
                        var chosen = new HashSet<int>();
                        foreach (var r in rooms)
                        {
                            var map = MappedAccommodations(con, tx, r.ChannelName, r.RoomType);
                            if (map.Count == 0) { tx.Rollback(); _code.Logs(_conn, "EmailReservation", $"no mapping Channel='{r.ChannelName}' Room='{r.RoomType}'", "SYSTEM"); return 0; }
                            var reserved = OverlappingAccomIds(con, tx, r.CheckIn, r.CheckOut);
                            var avail = map.Where(id => !reserved.Contains(id) && !chosen.Contains(id)).Take(r.NoOfRooms).ToList();
                            if (avail.Count < r.NoOfRooms) { tx.Rollback(); _code.Logs(_conn, "EmailReservation", $"not enough rooms {r.RoomType}: need {r.NoOfRooms} have {avail.Count}", "SYSTEM"); return 0; }
                            foreach (var id in avail) { chosen.Add(id); plan.Add((id, r.Adults, r.NetAmount)); }
                        }

                        // gross รวม (ราคาขายจริง) → TotalPrice/Deposit; net รวม → OTA_Net_Amount
                        double grossTotal = head.GrossTotal > 0 ? head.GrossTotal
                            : rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);   // fallback net ถ้าไม่มี gross
                        double netTotal = rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);
                        bool channelCollect = (head.PaymentType ?? "").IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0
                                              || string.IsNullOrEmpty(head.PaymentType); // STAAH default = channel collect

                        int resId;
                        using (var cmd = new SqlCommand(@"INSERT INTO [dbo].[Reservation]
                            ([Customer_MobilePhone],[CheckinDate],[CheckoutDate],[StayDays],[Status],[TotalPrice],[Deposit],
                             [Remark],[Reserve_By],[Created_Date],NoCreateReceipt,NoNameinReceipt,
                             OTA_Channel,OTA_Booking_ID,OTA_Gross_Amount,OTA_Net_Amount,OTA_Payment_Type,OTA_Guest_Name)
                            VALUES (@Phone,@In,@Out,@Days,N'มัดจำแล้ว',@Total,@Dep,@Remark,N'System(Email)',GETDATE(),1,1,
                             @Ch,@Bk,@Gross,@Net,@Pay,@Guest); SELECT SCOPE_IDENTITY();", con, tx))
                        {
                            EnsureCustomer(con, tx, head.MobilePhone, head.GuestName, head.PaymentType);
                            cmd.Parameters.AddWithValue("@Phone", (object)head.MobilePhone ?? "OTA_" + head.BookingId);
                            cmd.Parameters.AddWithValue("@In", head.CheckIn);
                            cmd.Parameters.AddWithValue("@Out", head.CheckOut);
                            cmd.Parameters.AddWithValue("@Days", stayDays);
                            // Deposit = gross ก็ต่อเมื่อ Channel Collect (OTA เก็บเงินแล้ว); Hotel Collect = 0 (เก็บหน้างาน)
                            cmd.Parameters.AddWithValue("@Total", (decimal)grossTotal);
                            cmd.Parameters.AddWithValue("@Dep", channelCollect ? (decimal)grossTotal : 0m);
                            cmd.Parameters.AddWithValue("@Remark", $"จองผ่าน {head.ChannelName}\r\nBooking ID:{head.BookingId}");
                            cmd.Parameters.AddWithValue("@Ch", (object)head.ChannelName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Bk", (object)head.BookingId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Gross", (decimal)grossTotal);
                            cmd.Parameters.AddWithValue("@Net", (decimal)netTotal);
                            cmd.Parameters.AddWithValue("@Pay", (object)(head.PaymentType ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Guest", (object)head.GuestName ?? DBNull.Value);
                            resId = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                        foreach (var pl in plan)
                            using (var cmd = new SqlCommand(@"INSERT INTO [dbo].[Reservation_Accommodation]
                                ([Reservation_ID],[Accommodation_ID],[Amount],[Price],Use_Coupon)
                                VALUES (@R,@A,@Adults,@Price,'0')", con, tx))
                            {
                                cmd.Parameters.AddWithValue("@R", resId);
                                cmd.Parameters.AddWithValue("@A", pl.accomId);
                                cmd.Parameters.AddWithValue("@Adults", pl.adults);
                                cmd.Parameters.AddWithValue("@Price", (int)Math.Floor(pl.amt));
                                cmd.ExecuteNonQuery();
                            }
                        tx.Commit();
                        _code.Logs(_conn, "EmailReservation", $"created reservation {resId} ({plan.Count} rooms) booking={head.BookingId}", "SYSTEM");
                        return resId;
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        _code.Logs(_conn, "EmailReservation", $"save failed booking={head.BookingId}: {ex.Message}", "SYSTEM");
                        return 0;
                    }
                }
            }
        }

        private List<int> MappedAccommodations(SqlConnection con, SqlTransaction tx, string channel, string roomType)
        {
            var ids = new List<int>();
            channel = CleanEntities(channel); roomType = CleanEntities(roomType);
            // ตรงก่อน แล้ว fallback LIKE (ตาม external: MapDataWithSTAAH: Agency + ROOM_TYPE)
            foreach (var (sql, exact) in new[] {
                ("SELECT Accommodation_ID FROM MapDataWithSTAAH WHERE Agency = @c AND ROOM_TYPE = @r", true),
                ("SELECT Accommodation_ID FROM MapDataWithSTAAH WHERE Agency = @c AND ROOM_TYPE LIKE @rp", false) })
            {
                using (var cmd = new SqlCommand(sql, con, tx))
                {
                    cmd.Parameters.AddWithValue("@c", channel);
                    if (exact) cmd.Parameters.AddWithValue("@r", roomType);
                    else cmd.Parameters.AddWithValue("@rp", "%" + roomType + "%");
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read()) if (rd[0] != DBNull.Value) ids.Add(Convert.ToInt32(rd[0]));
                }
                if (ids.Count > 0) break;
            }
            return ids;
        }

        /// <param name="excludeReservationId">ไม่นับห้องของการจองนี้ (ใช้ตอนแก้ไข — ย้ายวันทับตัวเองได้)</param>
        private HashSet<int> OverlappingAccomIds(SqlConnection con, SqlTransaction tx, DateTime ci, DateTime co,
            int excludeReservationId = 0)
        {
            var set = new HashSet<int>();
            using (var cmd = new SqlCommand(@"SELECT ra.Accommodation_ID
                FROM Reservation r WITH (HOLDLOCK)
                INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                WHERE r.CheckinDate < @co AND r.CheckoutDate > @ci
                  AND r.ID <> @exclude
                  AND r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')", con, tx))
            {
                cmd.Parameters.AddWithValue("@exclude", excludeReservationId);
                cmd.Parameters.AddWithValue("@ci", ci);
                cmd.Parameters.AddWithValue("@co", co);
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read()) if (rd[0] != DBNull.Value) set.Add(Convert.ToInt32(rd[0]));
            }
            return set;
        }

        private void EnsureCustomer(SqlConnection con, SqlTransaction tx, string phone, string name, string paymentType)
        {
            if (string.IsNullOrWhiteSpace(phone)) return;
            using (var chk = new SqlCommand("SELECT COUNT(*) FROM Customer WHERE MobilePhone = @p", con, tx))
            {
                chk.Parameters.AddWithValue("@p", phone);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0) return;
            }
            using (var ins = new SqlCommand(
                "INSERT INTO Customer (MobilePhone, Name, Created_Date) VALUES (@p, @n, GETDATE())", con, tx))
            {
                ins.Parameters.AddWithValue("@p", phone);
                ins.Parameters.AddWithValue("@n", (object)name ?? phone);
                ins.ExecuteNonQuery();
            }
        }

        // ── Modification (อีเมล "Bookings Status: Modified") ──────────────────────
        /// <summary>
        /// อัปเดตการจองเดิม "ในที่เดิม" (คง Reservation ID / ประวัติชำระ / เอกสารที่ผูกไว้)
        /// — OTA ส่ง Booking Id เดิมมาพร้อมข้อมูลใหม่ (วันที่/ราคา/จำนวนคน เปลี่ยนได้หมด)
        /// ไม่ใช้วิธี "ยกเลิกเดิม + สร้างใหม่" เพราะจะได้เลขจองใหม่ และประวัติเงิน/เอกสารขาดจากกัน
        /// เคสที่แก้อัตโนมัติไม่ปลอดภัย (เช็คอินแล้ว / ออกใบเสร็จแล้ว) → ไม่แตะ + แจ้งให้คนตรวจ
        /// </summary>
        private (bool, bool, bool, string) ProcessModification(List<RoomBooking> rooms)
        {
            if (rooms.Count == 0) return (false, false, false, "แยกข้อมูลจากอีเมลแก้ไขไม่ได้ (format เปลี่ยน?)");
            var head = rooms[0];
            if (string.IsNullOrWhiteSpace(head.BookingId)) return (false, false, false, "ไม่พบ Booking ID ในอีเมลแก้ไข");

            // หาการจองเดิมที่ยังใช้งานอยู่
            var dt = _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 ID, Status, CheckinDate, CheckoutDate, TotalPrice, Deposit
                    FROM Reservation
                   WHERE (OTA_Booking_ID = @b OR Remark LIKE @p)
                     AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
                   ORDER BY ID DESC",
                new Dictionary<string, object> { { "@b", head.BookingId }, { "@p", "%" + head.BookingId + "%" } });

            // ไม่เคยมีในระบบ (หรือถูกยกเลิกไปแล้ว) → ถือเป็นการจองใหม่
            if (dt == null || dt.Rows.Count == 0)
                return ProcessNewReservation(rooms);

            int resId = Convert.ToInt32(dt.Rows[0]["ID"]);
            string curStatus = dt.Rows[0]["Status"]?.ToString() ?? "";

            // ── ตรวจความปลอดภัยก่อนแก้ ─────────────────────────────────────────
            if (curStatus.IndexOf("เช็คอิน", StringComparison.Ordinal) >= 0
                || curStatus.IndexOf("เช็คเอาท์", StringComparison.Ordinal) >= 0)
            {
                string warn = $"⚠️ Booking {head.BookingId} (จอง #{resId}) มีอีเมลแก้ไข แต่สถานะเป็น \"{curStatus}\" แล้ว — ไม่แก้อัตโนมัติ กรุณาตรวจสอบเอง " +
                              $"(ข้อมูลใหม่: {head.CheckIn:dd/MM/yyyy}-{head.CheckOut:dd/MM/yyyy} ยอด {head.GrossTotal:N2})";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn);
                return (false, false, false, warn);
            }

            var rcpt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID FROM Account_Receipt WHERE Reservation_ID = @id AND Status = 'Normal'",
                new Dictionary<string, object> { { "@id", resId } });
            if (rcpt?.Rows.Count > 0)
            {
                string warn = $"⚠️ Booking {head.BookingId} (จอง #{resId}) มีอีเมลแก้ไข แต่ออกใบเสร็จไปแล้ว ({rcpt.Rows[0][0]}) — ไม่แก้อัตโนมัติ " +
                              $"กรุณาปรับเอกสาร/ยอดเงินเอง (ข้อมูลใหม่: {head.CheckIn:dd/MM/yyyy}-{head.CheckOut:dd/MM/yyyy} ยอด {head.GrossTotal:N2})";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn);
                return (false, false, false, warn);
            }

            // ── validate ข้อมูลใหม่ ────────────────────────────────────────────
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return (false, false, false, "วันเช็คอิน/เอาท์ในอีเมลแก้ไขไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays) return (false, false, false, $"จำนวนคืน {stayDays} เกิน {_maxStayDays}");

            DateTime oldIn = Convert.ToDateTime(dt.Rows[0]["CheckinDate"]);
            DateTime oldOut = Convert.ToDateTime(dt.Rows[0]["CheckoutDate"]);
            decimal oldTotal = dt.Rows[0]["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(dt.Rows[0]["TotalPrice"]) : 0m;

            var (ok, msg) = UpdateReservation(resId, rooms, stayDays);
            if (!ok) return (false, false, false, msg);

            string changes = BuildChangeSummary(oldIn, oldOut, oldTotal, head, stayDays);
            string done = $"แก้ไขการจอง #{resId} ({head.BookingId}) เรียบร้อย — {changes}";
            _code.Logs(_conn, "EmailReservation", done, "SYSTEM");
            return (true, false, false, done);
        }

        private static string BuildChangeSummary(DateTime oldIn, DateTime oldOut, decimal oldTotal,
            RoomBooking head, int stayDays)
        {
            var parts = new List<string>();
            if (oldIn.Date != head.CheckIn.Date || oldOut.Date != head.CheckOut.Date)
                parts.Add($"วันที่ {oldIn:dd/MM}-{oldOut:dd/MM} → {head.CheckIn:dd/MM}-{head.CheckOut:dd/MM} ({stayDays} คืน)");
            decimal newTotal = (decimal)(head.GrossTotal > 0 ? head.GrossTotal : 0);
            if (newTotal > 0 && Math.Abs(newTotal - oldTotal) > 0.01m)
                parts.Add($"ยอด {oldTotal:N2} → {newTotal:N2}");
            return parts.Count > 0 ? string.Join(", ", parts) : "ข้อมูลเหมือนเดิม";
        }

        /// <summary>
        /// เขียนทับข้อมูลการจองเดิม + จัดห้องใหม่ใน transaction เดียว
        /// (วันที่เปลี่ยน = ต้องเช็คห้องว่างรอบใหม่ โดยไม่นับห้องของการจองนี้เอง)
        /// </summary>
        private (bool Ok, string Message) UpdateReservation(int resId, List<RoomBooking> rooms, int stayDays)
        {
            var head = rooms[0];
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var tx = con.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        // ห้องที่ถืออยู่เดิม — ใช้จัดลำดับให้ "ได้ห้องเดิมก่อน" ถ้ายังว่างในวันใหม่
                        // (ไม่งั้นแค่เลื่อนวันก็อาจถูกสลับห้อง ทั้งที่ประเภทห้องเท่าเดิม —
                        //  สร้างความสับสนกับแม่บ้าน/ลูกค้าที่รู้เลขห้องไปแล้ว)
                        var currentRooms = new HashSet<int>();
                        using (var cur = new SqlCommand(
                            "SELECT Accommodation_ID FROM Reservation_Accommodation WHERE Reservation_ID = @id", con, tx))
                        {
                            cur.Parameters.AddWithValue("@id", resId);
                            using (var rd = cur.ExecuteReader())
                                while (rd.Read()) if (rd[0] != DBNull.Value) currentRooms.Add(Convert.ToInt32(rd[0]));
                        }

                        // จัดห้องใหม่ตามข้อมูลล่าสุด (ไม่นับห้องเดิมของการจองนี้ = ย้ายวันทับตัวเองได้)
                        var plan = new List<(int accomId, int adults, double amt)>();
                        var chosen = new HashSet<int>();
                        foreach (var r in rooms)
                        {
                            var map = MappedAccommodations(con, tx, r.ChannelName, r.RoomType);
                            if (map.Count == 0)
                            {
                                tx.Rollback();
                                return (false, $"ไม่มี mapping ห้อง '{r.RoomType}' ของ {r.ChannelName} — แก้ไขไม่สำเร็จ");
                            }
                            var reserved = OverlappingAccomIds(con, tx, r.CheckIn, r.CheckOut, resId);
                            var avail = map.Where(id => !reserved.Contains(id) && !chosen.Contains(id))
                                           .OrderByDescending(id => currentRooms.Contains(id))   // ห้องเดิมมาก่อน
                                           .Take(r.NoOfRooms).ToList();
                            if (avail.Count < r.NoOfRooms)
                            {
                                tx.Rollback();
                                return (false, $"ห้องไม่ว่างตามวันที่ใหม่ ({r.RoomType} ต้องการ {r.NoOfRooms} ว่าง {avail.Count}) — ต้องจัดห้องเอง");
                            }
                            foreach (var id in avail) { chosen.Add(id); plan.Add((id, r.Adults, r.NetAmount)); }
                        }

                        // แจ้งเตือนถ้าถูกย้ายห้องจริง (ห้องเดิมไม่ว่างในวันใหม่ / เปลี่ยนประเภทห้อง)
                        var newRooms = new HashSet<int>(plan.Select(x => x.accomId));
                        if (currentRooms.Count > 0 && !newRooms.SetEquals(currentRooms))
                        {
                            string moved = $"ℹ️ การจอง #{resId} ({head.BookingId}) ถูกย้ายห้องจากการแก้ไข " +
                                           $"(ห้องเดิมไม่ว่างตามวันที่ใหม่ หรือเปลี่ยนประเภทห้อง) — ตรวจสอบการจัดห้องอีกครั้ง";
                            _code.Logs(_conn, "EmailReservation", moved, "SYSTEM");
                            if (_notifyTelegram) Notify(moved);
                        }

                        double grossTotal = head.GrossTotal > 0 ? head.GrossTotal
                            : rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);
                        double netTotal = rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);
                        bool channelCollect = (head.PaymentType ?? "").IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0
                                              || string.IsNullOrEmpty(head.PaymentType);

                        using (var cmd = new SqlCommand(@"UPDATE [dbo].[Reservation] SET
                                [CheckinDate] = @In, [CheckoutDate] = @Out, [StayDays] = @Days,
                                [TotalPrice] = @Total, [Deposit] = @Dep,
                                OTA_Gross_Amount = @Gross, OTA_Net_Amount = @Net,
                                OTA_Payment_Type = @Pay, OTA_Guest_Name = @Guest,
                                [Remark] = @Remark, Modified_Date = GETDATE()
                              WHERE ID = @id", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", resId);
                            cmd.Parameters.AddWithValue("@In", head.CheckIn);
                            cmd.Parameters.AddWithValue("@Out", head.CheckOut);
                            cmd.Parameters.AddWithValue("@Days", stayDays);
                            cmd.Parameters.AddWithValue("@Total", (decimal)grossTotal);
                            cmd.Parameters.AddWithValue("@Dep", channelCollect ? (decimal)grossTotal : 0m);
                            cmd.Parameters.AddWithValue("@Gross", (decimal)grossTotal);
                            cmd.Parameters.AddWithValue("@Net", (decimal)netTotal);
                            cmd.Parameters.AddWithValue("@Pay", (object)(head.PaymentType ?? "") ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Guest", (object)head.GuestName ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@Remark",
                                $"จองผ่าน {head.ChannelName}\r\nBooking ID:{head.BookingId}\r\n(แก้ไขจากอีเมล {DateTime.Now:dd/MM/yyyy HH:mm})");
                            cmd.ExecuteNonQuery();
                        }

                        // จัดห้องใหม่ทั้งชุด
                        using (var del = new SqlCommand(
                            "DELETE FROM [dbo].[Reservation_Accommodation] WHERE Reservation_ID = @id", con, tx))
                        {
                            del.Parameters.AddWithValue("@id", resId);
                            del.ExecuteNonQuery();
                        }
                        foreach (var pl in plan)
                            using (var ins = new SqlCommand(@"INSERT INTO [dbo].[Reservation_Accommodation]
                                    ([Reservation_ID],[Accommodation_ID],[Amount],[Price],Use_Coupon)
                                    VALUES (@R,@A,@Adults,@Price,'0')", con, tx))
                            {
                                ins.Parameters.AddWithValue("@R", resId);
                                ins.Parameters.AddWithValue("@A", pl.accomId);
                                ins.Parameters.AddWithValue("@Adults", pl.adults);
                                ins.Parameters.AddWithValue("@Price", (int)Math.Floor(pl.amt));
                                ins.ExecuteNonQuery();
                            }

                        tx.Commit();
                        return (true, "ok");
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        _code.Logs(_conn, "EmailReservation", $"update failed res={resId}: {ex.Message}", "SYSTEM");
                        return (false, "อัปเดตการจองไม่สำเร็จ: " + ex.Message);
                    }
                }
            }
        }

        // ── Cancellation ─────────────────────────────────────────────────────────
        private (bool, bool, bool, string) ProcessCancellation(List<RoomBooking> rooms)
        {
            string bookingId = rooms.Count > 0 ? rooms[0].BookingId : null;
            if (string.IsNullOrWhiteSpace(bookingId)) return (false, false, true, "ไม่พบ Booking ID ในอีเมลยกเลิก");

            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID, Status FROM Reservation WHERE OTA_Booking_ID = @b OR Remark LIKE @p ORDER BY ID DESC",
                new Dictionary<string, object> { { "@b", bookingId }, { "@p", "%" + bookingId + "%" } });
            if (dt == null || dt.Rows.Count == 0) return (false, false, true, $"ไม่พบการจอง {bookingId} ในระบบ");
            int resId = Convert.ToInt32(dt.Rows[0]["ID"]);
            string status = dt.Rows[0]["Status"]?.ToString();
            if (status == "ยกเลิก" || status == "ยกเลิกคืนเงิน")
                return (false, true, true, $"การจอง #{resId} ถูกยกเลิกไปแล้ว");

            _code.DatabaseInsertSafe(_conn,
                "UPDATE Reservation SET Status = N'ยกเลิก', Modified_Date = GETDATE() WHERE ID = @id",
                new Dictionary<string, object> { { "@id", resId } });
            _code.DatabaseInsertSafe(_conn,
                "DELETE FROM Reservation_Accommodation WHERE Reservation_ID = @id",
                new Dictionary<string, object> { { "@id", resId } });
            _code.Logs(_conn, "EmailReservation", $"cancelled reservation {resId} booking={bookingId}", "SYSTEM");
            return (true, false, true, $"ยกเลิกการจอง #{resId} ({bookingId})");
        }

        // ── สร้างเอกสาร (toggle) ──────────────────────────────────────────────────
        private void TryEnqueueDocument(int resId, RoomBooking head)
        {
            // OTA settlement เปิด → ลูกหนี้ OTA (ยังไม่ implement processor — วางไว้ตาม OTA_Settlement_Design.md).
            // ตอนนี้ log เจตนาไว้ กันเงียบ; เมื่อ processor พร้อมจะ enqueue OTA_AR_INVOICE ที่นี่.
            _code.Logs(_conn, "EmailReservation",
                $"createDocument=on: reservation {resId} booking={head.BookingId} — OTA AR invoice จะ enqueue เมื่อ processor พร้อม (ดู OTA_Settlement_Design.md)", "SYSTEM");
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private IMailFolder GetOrCreateFolder(ImapClient client, string name)
        {
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            try { var f = personal.GetSubfolder(name); return f; }
            catch (FolderNotFoundException) { return personal.Create(name, true); }
        }

        private void Notify(string text)
        {
            try
            {
                string token = System.Configuration.ConfigurationManager.AppSettings["TelegramTokenTakeTime"];
                if (string.IsNullOrEmpty(token)) return;
                var bot = new TelegramBot2(token);
                bot.SendMessageAsync("-4969611371", text).GetAwaiter().GetResult();
            }
            catch { }
        }

        private static string Rx(string s, string pattern)
        {
            var m = Regex.Match(s ?? "", pattern, RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.Trim() : "";
        }
        private static double ParseMoney(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            s = s.Replace(",", "").Replace("THB", "").Trim();
            return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;
        }
        private static string CleanEntities(string s) =>
            string.IsNullOrEmpty(s) ? s : s.Replace("&amp;", "&").Replace("&#39;", "'").Replace("&nbsp;", " ").Trim();
        private static string SanitizePhone(string p)
        {
            if (string.IsNullOrWhiteSpace(p)) return "";
            p = p.Replace("66 66", "0").Replace("+66", "0");
            p = Regex.Replace(p, @"[^\d]", "");
            if (p.StartsWith("66") && p.Length > 9) p = "0" + p.Substring(2);
            if (p.Length > 0 && !p.StartsWith("0")) p = "0" + p;
            return p;
        }
        private static string PhoneFromBookingId(string bookingId)
        {
            var m = Regex.Match(bookingId ?? "", @"\((\d{9,10})\)");
            return m.Success ? m.Groups[1].Value : "";
        }
    }
}
