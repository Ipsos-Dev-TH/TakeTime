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
        private readonly int _imapPort, _maxStayDays, _maxDaysFuture, _retryHours, _retryMax;
        private readonly bool _moveFailed, _notifyTelegram, _createDocument, _retryFailed, _mapAnyChannel;
        private readonly List<int> _roomPriority;   // ลำดับห้องที่จัดให้ก่อน (โปรแกรมเดิม hard-code ไว้)
        private readonly string _cancelStatus;      // สถานะเมื่อยกเลิกจากอีเมล (ยกเลิก/ยกเลิกคืนเงิน/ยกเลิกไม่คืนเงิน)

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
            _retryFailed = Cfg("Email_Rsv_RetryFailed", "1") == "1";
            _retryHours = int.TryParse(Cfg("Email_Rsv_RetryHours", "72"), out var rh) ? rh : 72;
            _retryMax = int.TryParse(Cfg("Email_Rsv_RetryMaxPerRun", "20"), out var rm) ? rm : 20;
            _mapAnyChannel = Cfg("Email_Rsv_MapAnyChannel", "1") == "1";
            _roomPriority = (Cfg("Email_Rsv_RoomPriority", "") ?? "")
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => int.TryParse(x.Trim(), out var v) ? v : -1)
                .Where(v => v > 0).ToList();
            // สถานะยกเลิก — รับเฉพาะค่าที่ระบบรู้จัก กันพิมพ์ผิดแล้วหลุดสถานะแปลก
            string cs = Cfg("Email_Rsv_CancelStatus", "ยกเลิก");
            _cancelStatus = (cs == "ยกเลิกคืนเงิน" || cs == "ยกเลิกไม่คืนเงิน") ? cs : "ยกเลิก";
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
            public int Fetched, Created, Duplicate, Cancelled, Failed, Retried, RetrySucceeded, Manual;
            public List<string> Messages = new List<string>();
            public string Error;
            public override string ToString() =>
                Error != null ? "ผิดพลาด: " + Error
                : $"ดึง {Fetched} อีเมล → สร้าง {Created}, ซ้ำ {Duplicate}, ยกเลิก {Cancelled}, ล้มเหลว {Failed}"
                  + (Manual > 0 ? $", ต้องตรวจเอง {Manual}" : "")
                  + (Retried > 0 ? $" | ลองใหม่ {Retried} ฉบับ สำเร็จ {RetrySucceeded}" : "");
        }

        /// <summary>
        /// ผลประมวลอีเมล 1 ฉบับ. Park = "จบเคสด้วยมนุษย์" — แจ้งเตือนแล้ว ไม่ลองซ้ำอีก
        /// (ย้ายเข้า Processed ไม่ใช่ Failed เพื่อไม่ให้ retry loop วนเคสที่คนต้องตัดสินใจ)
        /// </summary>
        private class Outcome
        {
            public bool Ok, Dup, Cancelled, Park;
            public string Msg;
            public Outcome(bool ok, bool dup, bool cancelled, string msg, bool park = false)
            { Ok = ok; Dup = dup; Cancelled = cancelled; Msg = msg; Park = park; }
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
                        HandleOne(inbox, uid, processed, failed, res, false);

                    // ── ลองใหม่อัตโนมัติ: อีเมลที่เคยล้มเหลว (mapping/ห้องไม่ว่าง) ─────────────
                    // เดิมอีเมลที่ล้มเหลวถูกย้ายเข้า folder Failed แล้ว "จบ" — รอบถัดไปค้นหาเฉพาะ
                    // INBOX + ยังไม่อ่าน จึงไม่มีวันถูกหยิบมาทำอีก ต่อให้แก้ mapping/ปล่อยห้องแล้ว
                    // ตอนนี้จะวนกลับมาลองใหม่ให้เองภายใน Email_Rsv_RetryHours ชั่วโมง
                    if (_retryFailed && failed != null)
                        RetryFailedFolder(failed, processed, res);

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

        /// <summary>ประมวลผลอีเมล 1 ฉบับ + ย้าย folder ตามผล. isRetry = อยู่ใน folder Failed อยู่แล้ว.</summary>
        private void HandleOne(IMailFolder source, UniqueId uid, IMailFolder processed, IMailFolder failed,
            IntakeResult res, bool isRetry)
        {
            string subject = "";
            bool done = false;   // ok/dup/park = จบเคสแล้ว ย้ายออกจากคิว
            _inRetry = isRetry;      // กัน Notify ซ้ำจากชั้นใน (ProcessModification ฯลฯ) ตอนวนลองใหม่
            try
            {
                var msg = source.GetMessage(uid);
                subject = msg.Subject ?? "";
                string kind;
                var o = ProcessOne(msg, subject, out kind);
                done = o.Ok || o.Dup || o.Park;
                if (isRetry)
                {
                    res.Retried++;
                    if (done) res.RetrySucceeded++;
                }
                if (o.Ok && o.Cancelled) res.Cancelled++;
                else if (o.Ok) res.Created++;
                else if (o.Dup) res.Duplicate++;
                else if (o.Park) res.Manual++;
                else if (!isRetry) res.Failed++;
                res.Messages.Add($"[{(isRetry ? "ลองใหม่/" : "")}{kind}] {o.Msg}");

                // รอบลองใหม่: แจ้งเฉพาะตอนสำเร็จ (ไม่งั้นจะเตือนซ้ำทุก 5 นาทีจนกว่าจะแก้)
                // เคส Park แจ้งจากข้างใน Process* ไปแล้ว (ข้อความละเอียดกว่า) — ไม่แจ้งซ้ำ
                if (_notifyTelegram && !o.Park && (o.Ok || (!o.Dup && !isRetry)))
                    Notify($"{(o.Ok ? "✅" : "⚠️")} STAAH {kind}{(isRetry ? " (ลองใหม่)" : "")}: {o.Msg}", true);
            }
            catch (Exception ex)
            {
                if (!isRetry) res.Failed++;
                res.Messages.Add($"[error] {subject}: {ex.Message}");
                _code.Logs(_conn, "EmailReservation", $"process email failed: {ex.Message}", "SYSTEM");
            }
            finally { _inRetry = false; }

            try
            {
                if (done) source.MoveTo(uid, processed);
                else if (isRetry) { /* คาไว้ใน Failed รอรอบหน้า */ }
                else if (_moveFailed && failed != null) source.MoveTo(uid, failed);
                else source.AddFlags(uid, MessageFlags.Seen, true);
            }
            catch { }
        }

        /// <summary>
        /// วนอ่าน folder Failed แล้วลองลงจองใหม่ — ทำให้ระบบ "หายเอง" หลังผู้ใช้แก้ mapping
        /// หรือปล่อยห้องว่างแล้ว โดยไม่ต้องลากอีเมลกลับ INBOX เอง.
        /// จำกัดอายุ (Email_Rsv_RetryHours) + จำนวนต่อรอบ (Email_Rsv_RetryMaxPerRun) กันวนไม่รู้จบ.
        /// </summary>
        private void RetryFailedFolder(IMailFolder failed, IMailFolder processed, IntakeResult res)
        {
            try
            {
                failed.Open(FolderAccess.ReadWrite);
                if (failed.Count == 0) return;

                var cutoff = DateTime.Now.AddHours(-Math.Max(1, _retryHours));
                IList<UniqueId> uids;
                try { uids = failed.Search(SearchQuery.DeliveredAfter(cutoff.Date)); }
                catch { uids = failed.Search(SearchQuery.All); }

                int done = 0;
                foreach (var uid in uids)
                {
                    if (done >= _retryMax) break;
                    done++;
                    HandleOne(failed, uid, processed, null, res, true);
                }
                if (res.Retried > 0)
                    _code.Logs(_conn, "EmailReservation",
                        $"retry failed folder: ลองใหม่ {res.Retried} ฉบับ สำเร็จ {res.RetrySucceeded}", "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "EmailReservation", $"retry failed folder error: {ex.Message}", "SYSTEM");
            }
        }

        private Outcome ProcessOne(MimeMessage msg, string subject, out string kind)
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
            string phone = ResolvePhone(SanitizePhone(mobileNode?.InnerText?.Trim() ?? ""), bookingId);

            var roomRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'ROOM TYPE')]]");
            var dateRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'CHECK-IN')]]");
            var adultRows = doc.DocumentNode.SelectNodes(
                "//table[@border='0' and @cellpadding='5' and @width='100%' and @style='border-collapse:collapse;']//tr[.//span[contains(text(), 'ADULTS')]]");
            if (roomRows == null || roomRows.Count == 0)
            {
                // อีเมลบางแบบ (โดยเฉพาะใบยกเลิก) อาจไม่มีตารางห้อง/format เพี้ยน — ถ้า header
                // ยังอ่านได้ ให้คืนข้อมูลหัวอย่างเดียว เส้นทางยกเลิกใช้แค่ Booking Id ก็ทำงานต่อได้
                // (เส้นจองใหม่/แก้ไขจะติด validate วันเช็คอินเองตามปกติ)
                if (!string.IsNullOrWhiteSpace(bookingId))
                    list.Add(new RoomBooking
                    {
                        ChannelName = channel, BookingsStatus = status, BookingId = bookingId,
                        GuestName = guest, MobilePhone = phone, PaymentType = paymentType, GrossTotal = gross
                    });
                return list;
            }

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
        private Outcome ProcessNewReservation(List<RoomBooking> rooms)
        {
            if (rooms.Count == 0) return new Outcome(false, false, false, "แยกข้อมูลจากอีเมลไม่ได้ (format เปลี่ยน?)");
            var head = rooms[0];
            if (string.IsNullOrWhiteSpace(head.BookingId)) return new Outcome(false, false, false, "ไม่พบ Booking ID");

            // dedup — ข้ามการจองที่ถูกยกเลิกแล้ว (ทุกแบบ) เพื่อให้จองใหม่ด้วยเลขเดิมสร้างได้
            var existing = _code.DatabaseQuerySafe(_conn,
                @"SELECT TOP 1 ID FROM Reservation
                  WHERE (OTA_Booking_ID = @b OR Remark LIKE @p)
                    AND Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')",
                new Dictionary<string, object> { { "@b", head.BookingId }, { "@p", "%" + head.BookingId + "%" } });
            if (existing?.Rows.Count > 0) return new Outcome(false, true, false, $"Booking {head.BookingId} มีในระบบแล้ว (#{existing.Rows[0][0]})");

            // สถานะในเนื้ออีเมลต้องเป็น Confirmed (โปรแกรมเดิมเช็คไว้ — พอร์ตมาแล้วหายไป)
            // ถ้าไม่เช็ค อีเมลสถานะ Pending / Tentative / On Request จะถูกลงจองเป็นของจริง
            string bodyStatus = (head.BookingsStatus ?? "").Trim();
            if (bodyStatus.Length > 0 && !Has(bodyStatus, "Confirm") && !Has(bodyStatus, "Modif"))
                return new Outcome(false, false, false, $"สถานะการจองในอีเมลเป็น '{bodyStatus}' ไม่ใช่ Confirmed — ไม่ลงจองอัตโนมัติ");

            // validate
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return new Outcome(false, false, false, "วันเช็คอิน/เอาท์ไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays) return new Outcome(false, false, false, $"จำนวนคืน {stayDays} เกิน {_maxStayDays}");
            if ((head.CheckIn - DateTime.Today).TotalDays > _maxDaysFuture) return new Outcome(false, false, false, "จองล่วงหน้าเกินกำหนด");

            var (resId, reason) = SaveReservation(rooms, stayDays);
            if (resId <= 0) return new Outcome(false, false, false, "บันทึกการจองไม่สำเร็จ — " + reason);

            if (_createDocument) TryEnqueueDocument(resId, head);
            return new Outcome(true, false, false, $"จอง #{resId} {head.GuestName} {head.CheckIn:dd/MM} ({head.ChannelName}) gross={head.GrossTotal:N2}");
        }

        // ── Save (SERIALIZABLE txn, ตรงตาม external app) ──────────────────────────
        /// <summary>คืน (reservationId, reason). reservationId ≤ 0 = ไม่สำเร็จ, reason = สาเหตุจริงแบบอ่านออก.</summary>
        private (int Id, string Reason) SaveReservation(List<RoomBooking> rooms, int stayDays)
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
                            if (map.Rooms.Count == 0)
                            {
                                tx.Rollback();
                                string why = map.Describe(r.ChannelName, r.RoomType);
                                _code.Logs(_conn, "EmailReservation", "no mapping — " + why, "SYSTEM");
                                return (0, why);
                            }
                            LoadOccupancy(con, tx, map.Rooms, r.CheckIn, r.CheckOut);
                            var avail = map.Rooms.Where(x => !chosen.Contains(x.Id) && IsRoomFree(x, r.Adults))
                                                 .Take(r.NoOfRooms).ToList();
                            if (avail.Count < r.NoOfRooms)
                            {
                                string why = DescribeUnavailable(map, r, chosen);
                                tx.Rollback();
                                _code.Logs(_conn, "EmailReservation", "not enough rooms — " + why, "SYSTEM");
                                return (0, why);
                            }
                            foreach (var x in avail) { chosen.Add(x.Id); plan.Add((x.Id, r.Adults, r.NetAmount)); }
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
                            // ⚠️ "??" ไม่ทำงานกับสตริงว่าง — เคยหลุดเป็น Customer_MobilePhone = '' ที่ไม่มีลูกค้าผูก
                            string phone = string.IsNullOrWhiteSpace(head.MobilePhone)
                                ? "OTA_" + head.BookingId : head.MobilePhone;
                            EnsureCustomer(con, tx, phone, head.GuestName, head.PaymentType);
                            cmd.Parameters.AddWithValue("@Phone", phone);
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
                        return (resId, "ok");
                    }
                    catch (Exception ex)
                    {
                        try { tx.Rollback(); } catch { }
                        _code.Logs(_conn, "EmailReservation", $"save failed booking={head.BookingId}: {ex.Message}", "SYSTEM");
                        return (0, "เกิดข้อผิดพลาดขณะบันทึก: " + ex.Message);
                    }
                }
            }
        }

        // ── Mapping ห้อง OTA → ห้องจริง ────────────────────────────────────────────
        private class RoomInfo
        {
            public int Id;
            public string Name = "";
            public bool LimitWithPeople;
            public int Capacity;
            public int OrderId;
            // เติมตอนเช็คห้องว่าง
            public int Bookings;        // จำนวนใบจองที่ทับช่วงวันนี้
            public int PeakOccupied;    // จำนวนคนสูงสุดต่อคืน (เฉพาะห้องแบบจำกัดจำนวนคน)
            public string Blocker;      // ข้อความอธิบายว่าติดใบจองไหน
        }

        private class MapResult
        {
            public readonly List<RoomInfo> Rooms = new List<RoomInfo>();   // ห้องที่เปิดใช้งาน
            public string MatchedBy = "";                                  // ชั้นที่ match (ไว้ log)
            public int DisabledCount;                                      // mapping ชี้ห้องที่ปิดใช้งาน
            public List<string> KnownRoomTypes = new List<string>();
            public List<string> KnownChannels = new List<string>();

            public List<int> Ids => Rooms.Select(r => r.Id).ToList();
            public RoomInfo Find(int id) => Rooms.FirstOrDefault(r => r.Id == id);

            public string Describe(string channel, string roomType)
            {
                if (DisabledCount > 0 && Rooms.Count == 0)
                    return $"mapping ห้อง '{roomType}' ({channel}) ชี้ไปห้องที่ปิดใช้งานอยู่ {DisabledCount} ห้อง — เปิดใช้งานห้องในเมนู ที่พัก หรือแก้ MapDataWithSTAAH";
                string hint = KnownRoomTypes.Count > 0
                    ? " | ROOM_TYPE ที่มีของ channel นี้: " + string.Join(", ", KnownRoomTypes.Take(12))
                    : (KnownChannels.Count > 0
                        ? $" | ไม่พบ Agency '{channel}' — Agency ที่มีในตาราง: " + string.Join(", ", KnownChannels.Take(12))
                        : " | ตาราง MapDataWithSTAAH ยังว่าง");
                return $"ไม่มี mapping ห้องพัก: Agency='{channel}' ROOM_TYPE='{roomType}' (ตาราง MapDataWithSTAAH)" + hint;
            }
        }

        /// <summary>
        /// ชื่อห้องจากอีเมลมาได้หลายทรง — InnerText ของ HtmlAgilityPack **ไม่ถอด HTML entity ให้**
        /// (โปรแกรมเดิมจึงมี CleanHtmlEntities ที่ "ลบ" &amp;nbsp;/&amp;#160; ทิ้งไปเลย ไม่ใช่แทนด้วยช่องว่าง)
        /// แถวใน MapDataWithSTAAH ถูกสร้างให้เข้ากับผลลัพธ์แบบเดิม ⟹ ต้องลองทั้งสองแบบ
        /// บวกกับตัดชื่อ rate plan ท้าย " - " และวงเล็บ
        /// </summary>
        private static List<string> RoomTypeCandidates(string raw)
        {
            var list = new List<string>();
            Action<string> add = v =>
            {
                v = Norm(v);
                if (!string.IsNullOrWhiteSpace(v) && !list.Contains(v)) list.Add(v);
            };

            string deent = raw;
            try { deent = HtmlEntity.DeEntitize(raw) ?? raw; } catch { }

            foreach (var b in new[] { deent, CleanEntities(raw), StripEntities(raw) })
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                add(b);
                int dash = b.IndexOf(" - ", StringComparison.Ordinal);
                if (dash > 0) add(b.Substring(0, dash));
                int paren = b.IndexOf('(');
                if (paren > 0) add(b.Substring(0, paren));
                var inner = Regex.Match(b, @"\(([^)]+)\)");
                if (inner.Success) add(inner.Groups[1].Value);
            }
            return list;
        }

        private MapResult MappedAccommodations(SqlConnection con, SqlTransaction tx, string channel, string roomType)
        {
            var res = new MapResult();
            string ch = Norm(CleanEntities(channel));
            var variants = RoomTypeCandidates(roomType);
            // ชื่อห้องว่าง → ห้าม match อะไรเลย (LIKE '%%' จะจับทุกแถวของ Agency = คว้าห้องมั่ว)
            if (variants.Count == 0) return res;

            // ⚠️ เงื่อนไขที่ใช้ ROOM_TYPE เป็น "ส่วนหนึ่ง" ต้องกันแถวที่ ROOM_TYPE ว่าง/สั้นเกินไป
            // ไม่งั้น '%' + '' + '%' จะ match ทุกแถว = คว้าห้องมั่ว
            const string RtSub = "(LEN(LTRIM(RTRIM(m.ROOM_TYPE))) > 2 AND @r LIKE '%' + LTRIM(RTRIM(m.ROOM_TYPE)) + '%')";
            // @r15 = 15 ตัวแรกของชื่อห้อง + '%' — เงื่อนไขเดียวกับโปรแกรมเดิม (GetMappedAccommodations)
            // ที่ทำให้ mapping ที่มีอยู่แล้วในระบบใช้งานได้ ต้องคงไว้ ไม่งั้นใบจองที่เคยลงได้จะตกหมด
            var tiers = new List<(string Where, string How)>
            {
                ("m.Agency = @c AND m.ROOM_TYPE = @r",              "ตรงทั้ง Agency+ROOM_TYPE"),
                ("m.Agency = @c AND m.ROOM_TYPE LIKE @r15",         "ROOM_TYPE ขึ้นต้นเหมือนกัน 15 ตัวแรก (แบบโปรแกรมเดิม)"),
                ("m.Agency = @c AND m.ROOM_TYPE LIKE @rp",          "ROOM_TYPE คล้ายกัน"),
                ("m.Agency = @c AND " + RtSub,                      "ROOM_TYPE เป็นส่วนหนึ่งของชื่อในอีเมล"),
                ("@c <> '' AND m.Agency LIKE @cp AND (m.ROOM_TYPE = @r OR m.ROOM_TYPE LIKE @r15 OR m.ROOM_TYPE LIKE @rp OR " + RtSub + ")",
                                                                    "Agency คล้ายกัน"),
            };
            if (_mapAnyChannel)
                tiers.Add(("(m.ROOM_TYPE = @r OR m.ROOM_TYPE LIKE @r15 OR " + RtSub + ")", "ROOM_TYPE ตรง (ข้าม Agency)"));

            foreach (var rt in variants)
            {
                foreach (var tier in tiers)
                {
                    LoadMapping(con, tx, res, tier.Where, ch, rt);
                    if (res.Rooms.Count > 0)
                    {
                        res.MatchedBy = tier.How + (rt == variants[0] ? "" : $" (ใช้ชื่อ '{rt}')");
                        if (tier.How.StartsWith("Agency คล้าย") || tier.How.StartsWith("ROOM_TYPE ตรง (ข้าม"))
                            _code.Logs(_conn, "EmailReservation",
                                $"mapping fallback: Agency='{ch}' ROOM_TYPE='{roomType}' → {res.MatchedBy} ({res.Rooms.Count} ห้อง)", "SYSTEM");
                        return res;
                    }
                }
            }

            // ไม่เจอ → เก็บข้อมูลไว้บอกผู้ใช้ว่าตารางมีอะไรอยู่บ้าง
            res.KnownRoomTypes = ScalarList(con, tx,
                "SELECT DISTINCT ROOM_TYPE FROM MapDataWithSTAAH WHERE Agency = @c OR Agency LIKE @cp ORDER BY ROOM_TYPE", ch);
            if (res.KnownRoomTypes.Count == 0)
                res.KnownChannels = ScalarList(con, tx,
                    "SELECT DISTINCT Agency FROM MapDataWithSTAAH ORDER BY Agency", ch);
            return res;
        }

        private void LoadMapping(SqlConnection con, SqlTransaction tx, MapResult res, string where,
            string channel, string roomType)
        {
            res.Rooms.Clear(); res.DisabledCount = 0;
            string sql = @"SELECT DISTINCT m.Accommodation_ID, ISNULL(a.AccomName, N''),
                                  ISNULL(CONVERT(nvarchar(10), a.Status), '0'),
                                  ISNULL(CONVERT(nvarchar(10), a.LimitWithPeople), 'False'),
                                  ISNULL(TRY_CONVERT(int, a.People), 0),
                                  ISNULL(TRY_CONVERT(int, a.OrderID), 0)
                             FROM MapDataWithSTAAH m
                             LEFT JOIN Accommodation a ON a.ID = m.Accommodation_ID
                            WHERE " + where;
            string like = EscapeLike(roomType ?? "");
            using (var cmd = new SqlCommand(sql, con, tx))
            {
                cmd.Parameters.AddWithValue("@c", channel ?? "");
                cmd.Parameters.AddWithValue("@cp", "%" + EscapeLike(channel ?? "") + "%");
                cmd.Parameters.AddWithValue("@r", roomType ?? "");
                cmd.Parameters.AddWithValue("@rp", "%" + like + "%");
                cmd.Parameters.AddWithValue("@r15", (like.Length >= 15 ? like.Substring(0, 15) : like) + "%");
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        if (rd[0] == DBNull.Value) continue;
                        int id = Convert.ToInt32(rd[0]);
                        string st = rd[2]?.ToString() ?? "0";
                        bool active = st == "1" || st.Equals("True", StringComparison.OrdinalIgnoreCase);
                        if (!active) { res.DisabledCount++; continue; }
                        if (res.Rooms.Any(x => x.Id == id)) continue;
                        string lim = rd[3]?.ToString() ?? "False";
                        res.Rooms.Add(new RoomInfo
                        {
                            Id = id,
                            Name = string.IsNullOrEmpty(rd[1]?.ToString()) ? "#" + id : rd[1].ToString(),
                            LimitWithPeople = lim == "1" || lim.Equals("True", StringComparison.OrdinalIgnoreCase),
                            Capacity = Convert.ToInt32(rd[4]),
                            OrderId = Convert.ToInt32(rd[5])
                        });
                    }
            }
            OrderRooms(res.Rooms);
        }

        /// <summary>
        /// ลำดับการเลือกห้อง — โปรแกรมเดิม hard-code ไว้ {16,15,3,1,2,4,5} (SelectAccommodations)
        /// ย้ายมาเป็นค่าตั้งค่า Email_Rsv_RoomPriority (คั่นจุลภาค) ที่เหลือเรียงตาม OrderID
        /// </summary>
        private void OrderRooms(List<RoomInfo> rooms)
        {
            var priority = _roomPriority;
            var ordered = rooms
                .OrderBy(r => { int ix = priority.IndexOf(r.Id); return ix < 0 ? int.MaxValue : ix; })
                .ThenBy(r => r.OrderId).ThenBy(r => r.Id).ToList();
            rooms.Clear(); rooms.AddRange(ordered);
        }

        private List<string> ScalarList(SqlConnection con, SqlTransaction tx, string sql, string channel)
        {
            var list = new List<string>();
            try
            {
                using (var cmd = new SqlCommand(sql, con, tx))
                {
                    cmd.Parameters.AddWithValue("@c", channel ?? "");
                    cmd.Parameters.AddWithValue("@cp", "%" + EscapeLike(channel ?? "") + "%");
                    using (var rd = cmd.ExecuteReader())
                        while (rd.Read()) if (rd[0] != DBNull.Value) list.Add(rd[0].ToString());
                }
            }
            catch { }
            return list;
        }

        // ── ห้องว่าง ────────────────────────────────────────────────────────────
        // ⚠️ ต้องให้ผลตรงกับที่หน้าจอโชว์ (Default.aspx / AccommodationAvailabilityService /
        //    CheckReservationAvailability ของโปรแกรมเดิม) ไม่งั้นจะเกิดเคส
        //    "หน้าจอบอกว่าง แต่ระบบอ่านอีเมลบอกไม่ว่าง":
        //   1) ห้องแบบ LimitWithPeople (คิดตามจำนวนคน เช่น เต็นท์รวม) ห้ามถือว่าเต็มทันทีที่มีคนจอง
        //      ต้องนับหัวรายคืน แล้วเทียบกับ People — โปรแกรมเดิมทำแบบนี้ พอร์ตมาแล้วหายไป
        //   2) เทียบ "เฉพาะวัน" (CAST AS date) — ถ้า CheckinDate/CheckoutDate มีเวลาติดมา (เช่น 14:00)
        //      การเทียบแบบ datetime จะทำให้การจองที่ออกวันนั้น/เข้าช่วงบ่ายวันนั้นกลายเป็นทับกัน
        //   3) ตัดสถานะที่ระบบถือว่าห้องคืนแล้วออกให้ครบชุดเดียวกับ AccommodationAvailabilityService
        private const string FreeStatusFilter =
            @"r.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เสร็จสิ้น', N'ไม่มาเช็คอิน')";

        /// <summary>
        /// เติม Bookings/PeakOccupied/Blocker ให้ห้องที่ map ไว้ ตามช่วงวันที่ขอ (นับหัวรายคืน)
        /// </summary>
        private void LoadOccupancy(SqlConnection con, SqlTransaction tx, List<RoomInfo> rooms,
            DateTime ci, DateTime co, int excludeReservationId = 0)
        {
            foreach (var r in rooms) { r.Bookings = 0; r.PeakOccupied = 0; r.Blocker = null; }
            if (rooms.Count == 0) return;

            // (accomId, people, resId, status, in, out)
            var rows = new List<(int Accom, int Ppl, int ResId, string Status, DateTime In, DateTime Out)>();
            using (var cmd = new SqlCommand(@"SELECT ra.Accommodation_ID, ISNULL(TRY_CONVERT(int, ra.Amount), 1),
                                                     r.ID, ISNULL(r.Status, N''),
                                                     CAST(r.CheckinDate AS date), CAST(r.CheckoutDate AS date)
                FROM Reservation r WITH (HOLDLOCK)
                INNER JOIN Reservation_Accommodation ra ON r.ID = ra.Reservation_ID
                WHERE CAST(r.CheckinDate AS date) < @co AND CAST(r.CheckoutDate AS date) > @ci
                  AND r.ID <> @exclude
                  AND " + FreeStatusFilter, con, tx))
            {
                cmd.Parameters.AddWithValue("@exclude", excludeReservationId);
                cmd.Parameters.Add("@ci", SqlDbType.Date).Value = ci.Date;
                cmd.Parameters.Add("@co", SqlDbType.Date).Value = co.Date;
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        if (rd[0] == DBNull.Value) continue;
                        rows.Add((Convert.ToInt32(rd[0]), Convert.ToInt32(rd[1]), Convert.ToInt32(rd[2]),
                                  rd[3]?.ToString() ?? "", Convert.ToDateTime(rd[4]), Convert.ToDateTime(rd[5])));
                    }
            }

            foreach (var room in rooms)
            {
                var mine = rows.Where(x => x.Accom == room.Id).ToList();
                room.Bookings = mine.Count;
                if (mine.Count == 0) continue;

                // นับหัวสูงสุดต่อคืน (ห้องรวมแบบคิดตามคนอาจมีหลายใบจองคนละช่วง)
                for (var d = ci.Date; d < co.Date; d = d.AddDays(1))
                {
                    int ppl = mine.Where(x => x.In <= d && x.Out > d).Sum(x => Math.Max(1, x.Ppl));
                    if (ppl > room.PeakOccupied) room.PeakOccupied = ppl;
                }
                var first = mine.OrderBy(x => x.ResId).First();
                room.Blocker = $"จอง #{first.ResId} ({first.Status}) {first.In:dd/MM}-{first.Out:dd/MM}"
                             + (mine.Count > 1 ? $" +อีก {mine.Count - 1}" : "");
            }
        }

        /// <summary>ห้องนี้รับผู้เข้าพักเพิ่มอีก adults คนได้ไหม (ตรรกะเดียวกับ CheckReservationAvailability เดิม)</summary>
        private static bool IsRoomFree(RoomInfo r, int adults)
        {
            if (r.LimitWithPeople && r.Capacity > 0)
                return r.PeakOccupied + Math.Max(1, adults) <= r.Capacity;
            return r.Bookings == 0;
        }

        /// <summary>อธิบายว่า "ไม่ว่าง" เพราะห้องไหนติดการจองใด — ให้ผู้ใช้ตรวจได้ทันทีจาก Telegram/log.</summary>
        private static string DescribeUnavailable(MapResult map, RoomBooking r, HashSet<int> chosen)
        {
            var parts = new List<string>();
            int free = 0;
            foreach (var room in map.Rooms)
            {
                if (chosen.Contains(room.Id)) { parts.Add($"{room.Name}=ใช้ในใบจองนี้แล้ว"); continue; }
                if (IsRoomFree(room, r.Adults)) { free++; parts.Add($"{room.Name}=ว่าง"); continue; }
                parts.Add(room.LimitWithPeople && room.Capacity > 0
                    ? $"{room.Name}=เต็ม ({room.PeakOccupied}/{room.Capacity} คน, {room.Blocker})"
                    : $"{room.Name}=ติด{room.Blocker}");
            }
            return $"ห้องไม่ว่าง: '{r.RoomType}' ({r.ChannelName}) วันที่ {r.CheckIn:dd/MM/yyyy}-{r.CheckOut:dd/MM/yyyy} " +
                   $"ต้องการ {r.NoOfRooms} ห้อง ({r.Adults} คน) ว่าง {free} จาก {map.Rooms.Count} ห้องที่ map ไว้"
                   + (string.IsNullOrEmpty(map.MatchedBy) ? "" : $" [{map.MatchedBy}]")
                   + " | " + string.Join(", ", parts);
        }

        /// <summary>
        /// ตรวจ mapping + ห้องว่างแบบไม่บันทึกอะไร — ใช้จากปุ่ม "ตรวจสอบ mapping/ห้องว่าง" หน้า Admin
        /// เพื่อตอบคำถาม "ทำไมหน้าจอบอกว่าง แต่อีเมลลงจองไม่ได้"
        /// </summary>
        public string Diagnose(string channel, string roomType, DateTime checkIn, DateTime checkOut,
            int noOfRooms, int adults = 1)
        {
            if (checkOut <= checkIn) checkOut = checkIn.AddDays(1);
            if (noOfRooms <= 0) noOfRooms = 1;
            if (adults <= 0) adults = 1;
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    var map = MappedAccommodations(con, null, channel, roomType);
                    if (map.Rooms.Count == 0) return "❌ " + map.Describe(channel, roomType);

                    LoadOccupancy(con, null, map.Rooms, checkIn, checkOut);
                    var r = new RoomBooking
                    {
                        ChannelName = channel, RoomType = roomType, Adults = adults,
                        CheckIn = checkIn, CheckOut = checkOut, NoOfRooms = noOfRooms
                    };
                    int free = map.Rooms.Count(x => IsRoomFree(x, adults));
                    string head = $"map เจอ {map.Rooms.Count} ห้อง [{map.MatchedBy}]: " +
                                  string.Join(", ", map.Rooms.Select(x =>
                                      x.Name + (x.LimitWithPeople ? $"(รวม {x.Capacity} คน)" : "")));
                    if (free >= noOfRooms)
                        return $"✅ ลงจองได้ — {head}\nว่าง {free} ห้อง (ต้องการ {noOfRooms})";
                    return "❌ " + DescribeUnavailable(map, r, new HashSet<int>()) + "\n" + head;
                }
            }
            catch (Exception ex) { return "ตรวจสอบไม่สำเร็จ: " + ex.Message; }
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
        private Outcome ProcessModification(List<RoomBooking> rooms)
        {
            if (rooms.Count == 0) return new Outcome(false, false, false, "แยกข้อมูลจากอีเมลแก้ไขไม่ได้ (format เปลี่ยน?)");
            var head = rooms[0];
            if (string.IsNullOrWhiteSpace(head.BookingId)) return new Outcome(false, false, false, "ไม่พบ Booking ID ในอีเมลแก้ไข");

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
            // เคสเหล่านี้ "จบด้วยคน" (Park) — แจ้ง Telegram แล้วย้ายอีเมลเข้า Processed เลย
            // ไม่ส่งเข้า Failed เพราะ retry loop จะวนลองซ้ำทั้งที่ระบบตั้งใจไม่แก้อัตโนมัติ
            if (curStatus.IndexOf("เช็คอิน", StringComparison.Ordinal) >= 0
                || curStatus.IndexOf("เช็คเอาท์", StringComparison.Ordinal) >= 0)
            {
                string warn = $"⚠️ Booking {head.BookingId} (จอง #{resId}) มีอีเมลแก้ไข แต่สถานะเป็น \"{curStatus}\" แล้ว — ไม่แก้อัตโนมัติ กรุณาตรวจสอบเอง " +
                              $"(ข้อมูลใหม่: {head.CheckIn:dd/MM/yyyy}-{head.CheckOut:dd/MM/yyyy} ยอด {head.GrossTotal:N2})";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn, true);
                return new Outcome(false, false, false, warn, park: true);
            }

            var rcpt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID FROM Account_Receipt WHERE Reservation_ID = @id AND Status = 'Normal'",
                new Dictionary<string, object> { { "@id", resId } });
            if (rcpt?.Rows.Count > 0)
            {
                string warn = $"⚠️ Booking {head.BookingId} (จอง #{resId}) มีอีเมลแก้ไข แต่ออกใบเสร็จไปแล้ว ({rcpt.Rows[0][0]}) — ไม่แก้อัตโนมัติ " +
                              $"กรุณาปรับเอกสาร/ยอดเงินเอง (ข้อมูลใหม่: {head.CheckIn:dd/MM/yyyy}-{head.CheckOut:dd/MM/yyyy} ยอด {head.GrossTotal:N2})";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn, true);
                return new Outcome(false, false, false, warn, park: true);
            }

            // ── validate ข้อมูลใหม่ ────────────────────────────────────────────
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return new Outcome(false, false, false, "วันเช็คอิน/เอาท์ในอีเมลแก้ไขไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays) return new Outcome(false, false, false, $"จำนวนคืน {stayDays} เกิน {_maxStayDays}");

            DateTime oldIn = Convert.ToDateTime(dt.Rows[0]["CheckinDate"]);
            DateTime oldOut = Convert.ToDateTime(dt.Rows[0]["CheckoutDate"]);
            decimal oldTotal = dt.Rows[0]["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(dt.Rows[0]["TotalPrice"]) : 0m;

            var (ok, msg) = UpdateReservation(resId, rooms, stayDays);
            if (!ok) return new Outcome(false, false, false, msg);

            string changes = BuildChangeSummary(oldIn, oldOut, oldTotal, head, stayDays);
            string done = $"แก้ไขการจอง #{resId} ({head.BookingId}) เรียบร้อย — {changes}";
            _code.Logs(_conn, "EmailReservation", done, "SYSTEM");
            return new Outcome(true, false, false, done);
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
                            if (map.Rooms.Count == 0)
                            {
                                tx.Rollback();
                                return (false, "แก้ไขไม่สำเร็จ — " + map.Describe(r.ChannelName, r.RoomType));
                            }
                            LoadOccupancy(con, tx, map.Rooms, r.CheckIn, r.CheckOut, resId);
                            var avail = map.Rooms.Where(x => !chosen.Contains(x.Id) && IsRoomFree(x, r.Adults))
                                           .OrderByDescending(x => currentRooms.Contains(x.Id))   // ห้องเดิมมาก่อน
                                           .Take(r.NoOfRooms).ToList();
                            if (avail.Count < r.NoOfRooms)
                            {
                                string why = DescribeUnavailable(map, r, chosen);
                                tx.Rollback();
                                return (false, "ห้องไม่ว่างตามวันที่ใหม่ — ต้องจัดห้องเอง | " + why);
                            }
                            foreach (var x in avail) { chosen.Add(x.Id); plan.Add((x.Id, r.Adults, r.NetAmount)); }
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

                        // เบอร์ผู้จองเปลี่ยนในอีเมลแก้ไข → ย้ายการจองไปผูกลูกค้าเบอร์ใหม่
                        // (เฉพาะเบอร์จริง — ไม่ใช่ค่า fallback OTA_xxx และไม่ใช่ค่าว่าง)
                        if (!string.IsNullOrWhiteSpace(head.MobilePhone) && head.MobilePhone.Length >= 9
                            && !head.MobilePhone.StartsWith("OTA_"))
                        {
                            string curPhone = "";
                            using (var ph = new SqlCommand(
                                "SELECT ISNULL(Customer_MobilePhone, N'') FROM Reservation WHERE ID = @id", con, tx))
                            {
                                ph.Parameters.AddWithValue("@id", resId);
                                curPhone = ph.ExecuteScalar()?.ToString() ?? "";
                            }
                            if (curPhone != head.MobilePhone)
                            {
                                EnsureCustomer(con, tx, head.MobilePhone, head.GuestName, head.PaymentType);
                                using (var up = new SqlCommand(
                                    "UPDATE Reservation SET Customer_MobilePhone = @p WHERE ID = @id", con, tx))
                                {
                                    up.Parameters.AddWithValue("@p", head.MobilePhone);
                                    up.Parameters.AddWithValue("@id", resId);
                                    up.ExecuteNonQuery();
                                }
                                _code.Logs(_conn, "EmailReservation",
                                    $"modification: จอง #{resId} เปลี่ยนเบอร์ผู้จอง {curPhone} → {head.MobilePhone}", "SYSTEM");
                            }
                        }

                        using (var cmd = new SqlCommand(@"UPDATE [dbo].[Reservation] SET
                                [CheckinDate] = @In, [CheckoutDate] = @Out, [StayDays] = @Days,
                                [TotalPrice] = @Total, [Deposit] = @Dep,
                                OTA_Gross_Amount = @Gross, OTA_Net_Amount = @Net,
                                OTA_Payment_Type = @Pay, OTA_Guest_Name = @Guest,
                                [Remark] = @Remark" + ModifiedDateSql() + @"
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
        /// <summary>
        /// ยกเลิกจากอีเมล OTA — มี guard ชุดเดียวกับฝั่งแก้ไข:
        /// เช็คอิน/เช็คเอาท์แล้ว หรือออกใบเสร็จแล้ว → ไม่ยกเลิกอัตโนมัติ (เงิน/เอกสารจะขัดกัน)
        /// แจ้งคนแล้วจบเคส (Park). สถานะที่ตั้งเมื่อยกเลิกเลือกได้ผ่าน Email_Rsv_CancelStatus
        /// (โปรแกรมเดิมใช้ 'ยกเลิกคืนเงิน' — channel collect เงินอยู่ฝั่ง OTA)
        /// </summary>
        private Outcome ProcessCancellation(List<RoomBooking> rooms)
        {
            string bookingId = rooms.Count > 0 ? rooms[0].BookingId : null;
            if (string.IsNullOrWhiteSpace(bookingId)) return new Outcome(false, false, true, "ไม่พบ Booking ID ในอีเมลยกเลิก");

            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID, Status FROM Reservation WHERE OTA_Booking_ID = @b OR Remark LIKE @p ORDER BY ID DESC",
                new Dictionary<string, object> { { "@b", bookingId }, { "@p", "%" + bookingId + "%" } });
            // ไม่พบ = อาจเป็นอีเมลมาก่อนใบจอง (out-of-order) — ปล่อยเป็น fail ให้ retry loop
            // วนกลับมา: พอใบจองถูกสร้างจาก folder Failed แล้ว รอบถัดไปใบยกเลิกจะเจอและยกเลิกเอง
            if (dt == null || dt.Rows.Count == 0) return new Outcome(false, false, true, $"ไม่พบการจอง {bookingId} ในระบบ");
            int resId = Convert.ToInt32(dt.Rows[0]["ID"]);
            string status = dt.Rows[0]["Status"]?.ToString() ?? "";
            if (status == "ยกเลิก" || status == "ยกเลิกคืนเงิน" || status == "ยกเลิกไม่คืนเงิน")
                return new Outcome(false, true, true, $"การจอง #{resId} ถูกยกเลิกไปแล้ว");

            // ── guard: เช็คอิน/เช็คเอาท์แล้ว → ห้ามยกเลิกเอง (แขกอยู่ในห้อง/เข้าพักจบแล้ว) ──
            if (status.IndexOf("เช็คอิน", StringComparison.Ordinal) >= 0
                || status.IndexOf("เช็คเอาท์", StringComparison.Ordinal) >= 0)
            {
                string warn = $"⚠️ Booking {bookingId} (จอง #{resId}) มีอีเมลยกเลิก แต่สถานะเป็น \"{status}\" แล้ว — " +
                              "ไม่ยกเลิกอัตโนมัติ กรุณาตรวจสอบกับ OTA แล้วจัดการเอง";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn, true);
                return new Outcome(false, false, true, warn, park: true);
            }

            // ── guard: ออกใบเสร็จแล้ว → ยกเลิกเฉย ๆ จะทิ้งใบเสร็จ/เอกสารบัญชีค้างสถานะปกติ ──
            var rcpt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID FROM Account_Receipt WHERE Reservation_ID = @id AND Status = 'Normal'",
                new Dictionary<string, object> { { "@id", resId } });
            if (rcpt?.Rows.Count > 0)
            {
                string warn = $"⚠️ Booking {bookingId} (จอง #{resId}) มีอีเมลยกเลิก แต่ออกใบเสร็จไปแล้ว ({rcpt.Rows[0][0]}) — " +
                              "ไม่ยกเลิกอัตโนมัติ กรุณายกเลิกใบเสร็จ/เอกสารบัญชีก่อน แล้วยกเลิกการจองเอง";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn, true);
                return new Outcome(false, false, true, warn, park: true);
            }

            _code.DatabaseInsertSafe(_conn,
                @"UPDATE Reservation SET Status = @st,
                         Remark = ISNULL(Remark, N'') + @note" + ModifiedDateSql() + " WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@id", resId },
                    { "@st", _cancelStatus },
                    { "@note", $"\r\n(ยกเลิกจากอีเมล OTA {DateTime.Now:dd/MM/yyyy HH:mm})" }
                });
            _code.DatabaseInsertSafe(_conn,
                "DELETE FROM Reservation_Accommodation WHERE Reservation_ID = @id",
                new Dictionary<string, object> { { "@id", resId } });
            _code.Logs(_conn, "EmailReservation", $"cancelled reservation {resId} booking={bookingId} status={_cancelStatus}", "SYSTEM");
            return new Outcome(true, false, true, $"ยกเลิกการจอง #{resId} ({bookingId}) → สถานะ \"{_cancelStatus}\"");
        }

        // ── สร้างเอกสาร (toggle) ──────────────────────────────────────────────────
        private void TryEnqueueDocument(int resId, RoomBooking head)
        {
            // OTA settlement เปิด → ลูกหนี้ OTA (ยังไม่ implement processor — วางไว้ตาม OTA_Settlement_Design.md).
            // ตอนนี้ log เจตนาไว้ กันเงียบ; เมื่อ processor พร้อมจะ enqueue OTA_AR_INVOICE ที่นี่.
            _code.Logs(_conn, "EmailReservation",
                $"createDocument=on: reservation {resId} booking={head.BookingId} — OTA AR invoice จะ enqueue เมื่อ processor พร้อม (ดู OTA_Settlement_Design.md)", "SYSTEM");
        }

        // คอลัมน์ Modified_Date มาจาก PHASE18_27 — ฐานที่ยังไม่รันจะขึ้น
        // "Invalid column name 'Modified_Date'" ตอนอีเมลแก้ไข/ยกเลิกเข้ามา → ตรวจก่อนใช้
        private static int _hasModifiedDate;   // 0 = ยังไม่ตรวจ, 1 = มี, -1 = ไม่มี
        private string ModifiedDateSql()
        {
            if (_hasModifiedDate == 0)
            {
                bool exists = false;
                try
                {
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 1 FROM INFORMATION_SCHEMA.COLUMNS
                           WHERE TABLE_NAME = 'Reservation' AND COLUMN_NAME = 'Modified_Date'", null);
                    exists = dt != null && dt.Rows.Count > 0;
                }
                catch { }
                _hasModifiedDate = exists ? 1 : -1;
            }
            return _hasModifiedDate == 1 ? ", Modified_Date = GETDATE()" : "";
        }

        // ── helpers ──────────────────────────────────────────────────────────────
        private IMailFolder GetOrCreateFolder(ImapClient client, string name)
        {
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            try { var f = personal.GetSubfolder(name); return f; }
            catch (FolderNotFoundException) { return personal.Create(name, true); }
        }

        private bool _inRetry;   // อยู่ในรอบ "ลองใหม่" — งดแจ้งเตือนซ้ำ (แจ้งเฉพาะตอนสำเร็จ)
        private void Notify(string text, bool always = false)
        {
            if (_inRetry && !always) return;
            try
            {
                string token = AppCfg.Get("TelegramTokenTakeTime");
                if (string.IsNullOrEmpty(token)) return;
                var bot = new TelegramBot2(token);
                bot.SendMessageAsync(AppCfg.Get("TelegramChatId", "-4969611371"), text).GetAwaiter().GetResult();
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
        /// <summary>แบบเดียวกับ CleanHtmlEntities ของโปรแกรมเดิม — ลบ &amp;nbsp;/&amp;#160; ทิ้ง (ไม่แทนด้วยช่องว่าง)</summary>
        private static string StripEntities(string s) =>
            string.IsNullOrEmpty(s) ? s : s.Replace("&amp;", "&").Replace("&#39;", "'")
                                           .Replace("&#160;", "").Replace("&nbsp;", "").Trim();
        /// <summary>escape อักขระพิเศษของ LIKE — ชื่อห้องที่มี % หรือ _ จะได้ไม่กลายเป็น wildcard</summary>
        private static string EscapeLike(string s) =>
            string.IsNullOrEmpty(s) ? s : s.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
        /// <summary>ยุบช่องว่างซ้ำ/ตัดหัวท้าย — ชื่อห้องจาก HTML มักติด \r\n และเว้นวรรคหลายตัว จนเทียบกับตาราง map ไม่ตรง</summary>
        private static string Norm(string s) =>
            string.IsNullOrEmpty(s) ? "" : Regex.Replace(s.Replace('\u00A0', ' '), @"\s+", " ").Trim();
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
            if (string.IsNullOrWhiteSpace(bookingId)) return "";
            // Booking.com ใส่ PIN ในวงเล็บ เช่น "1114600000001674 (6502832396)"
            var m = Regex.Match(bookingId, @"\((\d{9,10})\)");
            if (m.Success)
            {
                string v = m.Groups[1].Value;
                if (v.Length == 9 && !v.StartsWith("0")) v = "0" + v;
                return v;
            }
            // สุดท้าย: ตัวเลขจากท่อนแรกของ Booking Id (ถ้ายาวพอจะเป็นเบอร์)
            string first = Regex.Replace(bookingId.Split(' ')[0] ?? "", @"[^\d]", "");
            return first.Length >= 9 && first.Length <= 20 ? first : "";
        }

        /// <summary>
        /// เบอร์โทรคือ key ของตาราง Customer — ห้ามปล่อยว่าง ไม่งั้นได้ใบจองที่ไม่มีลูกค้าผูกอยู่
        /// (ตรรกะเดียวกับ ValidateAndFixPhoneNumber ของโปรแกรมเดิม: ข้ามเบอร์บ้าน 02 →
        ///  ลองดึงจาก Booking Id → สุดท้ายใช้ค่า default เพื่อให้การจองผ่าน ไม่ตกทั้งใบ)
        /// </summary>
        private string ResolvePhone(string phone, string bookingId)
        {
            if (!string.IsNullOrWhiteSpace(phone) && phone.Length >= 9 && phone.Length <= 20
                && !phone.StartsWith("02"))
                return phone;

            string fromBooking = PhoneFromBookingId(bookingId);
            if (!string.IsNullOrWhiteSpace(fromBooking)) return fromBooking;
            if (!string.IsNullOrWhiteSpace(phone)) return phone;   // เบอร์บ้านยังดีกว่าไม่มี

            string def = Cfg("Email_Rsv_DefaultPhone", "");
            if (!string.IsNullOrWhiteSpace(def)) return def;
            return "OTA_" + (bookingId ?? "UNKNOWN");
        }
    }
}
