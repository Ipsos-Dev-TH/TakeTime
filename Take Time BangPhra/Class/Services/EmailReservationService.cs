using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Text;
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
    /// config อยู่ใน Accounting_Integration_Config (แก้ผ่านหน้า Admin), เก็บ refsell_amt (ระดับ booking) /
    /// AMOUNT (เรตต่อคืน) / paymentType แยกกันไว้ต่อกับ OTA settlement — อีเมลไม่ได้บอกค่าคอม.
    /// เรียกจาก timer (Global.asax) หรือปุ่ม "ดึงตอนนี้" ในหน้า Admin. dedup ด้วย OTA_Booking_ID/Remark.
    /// </summary>
    public class EmailReservationService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        // config (โหลดครั้งเดียวตอนสร้าง)
        private readonly string _imapServer, _imapUser, _imapPassword, _processedLabel, _failedLabel, _fromContains;
        private readonly string _ignoredLabel;      // folder เก็บอีเมล STAAH ที่ไม่เกี่ยวกับการจอง
        private readonly int _imapPort, _maxStayDays, _maxDaysFuture, _retryHours, _retryMax;
        private readonly bool _moveFailed, _notifyTelegram, _createDocument, _retryFailed, _mapAnyChannel;
        private readonly List<int> _roomPriority;   // ลำดับห้องที่จัดให้ก่อน (โปรแกรมเดิม hard-code ไว้)
        private readonly string _cancelStatus;      // สถานะเมื่อยกเลิกจากอีเมล (ยกเลิก/ยกเลิกคืนเงิน/ยกเลิกไม่คืนเงิน)
        private readonly int _customerTypeId;       // Customer_Type_ID ของลูกค้าที่สร้างอัตโนมัติ (2 = บุคคลธรรมดา)

        public EmailReservationService(string connectionString)
        {
            _conn = connectionString;
            _imapServer = Cfg("Email_Rsv_ImapServer", "imap.gmail.com");
            _imapPort = int.TryParse(Cfg("Email_Rsv_ImapPort", "993"), out var p) ? p : 993;
            _imapUser = Cfg("Email_Rsv_Username", "");
            _imapPassword = _code.Derypt(Cfg("Email_Rsv_Password_Encrypted", ""));
            _processedLabel = Cfg("Email_Rsv_ProcessedLabel", "STAAH-Processed");
            _failedLabel = Cfg("Email_Rsv_FailedLabel", "STAAH-Failed");
            _ignoredLabel = Cfg("Email_Rsv_IgnoredLabel", "STAAH-Other");
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
            _customerTypeId = int.TryParse(Cfg("Email_Rsv_CustomerTypeId", "2"), out var ct) ? ct : 2;
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
            public int Fetched, Created, Duplicate, Cancelled, Failed, Retried, RetrySucceeded, Manual, Ignored;
            public List<string> Messages = new List<string>();
            /// <summary>เลขจองในโฟลเดอร์ Failed ที่ระบบนี้ "ไม่เคยประมวลผล" — หลักฐานว่ามี
            /// ตัวอ่านเมลตัวอื่น (โปรแกรมเก่า GetReservationfromGmail / เซิร์ฟเวอร์เก่า) แย่งอ่านอยู่</summary>
            public List<string> ForeignBookings = new List<string>();
            public string Error;
            public override string ToString() =>
                Error != null ? "ผิดพลาด: " + Error
                : $"ดึง {Fetched} อีเมล → สร้าง {Created}, ซ้ำ {Duplicate}, ยกเลิก {Cancelled}, ล้มเหลว {Failed}"
                  + (Manual > 0 ? $", ต้องตรวจเอง {Manual}" : "")
                  + (Ignored > 0 ? $", ไม่ใช่ใบจอง {Ignored}" : "")
                  + (Retried > 0 ? $" | ลองใหม่ {Retried} ฉบับ สำเร็จ {RetrySucceeded}" : "");
        }

        /// <summary>
        /// ผลประมวลอีเมล 1 ฉบับ. Park = "จบเคสด้วยมนุษย์" — แจ้งเตือนแล้ว ไม่ลองซ้ำอีก
        /// (ย้ายเข้า Processed ไม่ใช่ Failed เพื่อไม่ให้ retry loop วนเคสที่คนต้องตัดสินใจ)
        /// </summary>
        private class Outcome
        {
            public bool Ok, Dup, Cancelled, Park;
            public bool Ignored;               // ไม่ใช่อีเมลการจอง — ข้ามเงียบ ๆ ไม่นับล้มเหลว ไม่ลองซ้ำ
            public string Msg;                 // ข้อความสั้น (log / หน้า Admin)
            public Outcome(bool ok, bool dup, bool cancelled, string msg, bool park = false)
            { Ok = ok; Dup = dup; Cancelled = cancelled; Msg = msg; Park = park; }

            // ── ข้อมูลประกอบสำหรับข้อความ Telegram แบบละเอียด ──
            public string Reason;              // หัวข้อสาเหตุตอนล้มเหลว (สั้น)
            public int ResId;
            public RoomBooking Head;
            public List<RoomBooking> Rooms;
            public string Changes;             // สรุปสิ่งที่เปลี่ยน (เส้นแก้ไข)
            public string OldDates;            // ช่วงวันเดิมก่อนแก้/ก่อนยกเลิก

            public Outcome Detail(RoomBooking head, List<RoomBooking> rooms = null, int resId = 0)
            { Head = head; Rooms = rooms; ResId = resId; return this; }
            public Outcome Because(string reason) { Reason = reason; return this; }
        }

        /// <summary>
        /// ส่งข้อความตัวอย่างเข้า Telegram ด้วย layout จริง — ใช้ดูหน้าตาก่อนมีอีเมลจริงเข้ามา
        /// </summary>
        public (bool, string) TestTelegram()
        {
            if (string.IsNullOrEmpty(AppCfg.Get("TelegramTokenTakeTime")))
                return (false, "ยังไม่ได้ตั้งค่า Telegram token ในระบบ");

            var demo = new RoomBooking
            {
                ChannelName = "Booking.com", BookingId = "1114600000001674", GuestName = "ตัวอย่าง ทดสอบระบบ",
                MobilePhone = "0812345678", RoomType = "Nordic Tent", NoOfRooms = 1, Adults = 2,
                NetAmount = 950, GrossTotal = 1080, PaymentType = "ChannelCollect",
                CheckIn = DateTime.Today.AddDays(1), CheckOut = DateTime.Today.AddDays(2),
                AssignedRooms = new List<string> { "Nordic Tent 3" }
            };
            var rooms = new List<RoomBooking> { demo };
            var ok = new Outcome(true, false, false, "ตัวอย่างข้อความ").Detail(demo, rooms, 9999);
            Notify("🧪 <i>ข้อความทดสอบ — ไม่ใช่การจองจริง</i>\n\n"
                   + BuildNotification("จองใหม่", ok, "New Reservation - Booking.com", false), true);

            var bad = new Outcome(false, false, false,
                "ห้องไม่ว่าง: 'Nordic Tent' (Booking.com) วันที่ … ต้องการ 1 ห้อง ว่าง 0 จาก 6 ห้องที่ map ไว้ | Nordic Tent 1=ติดจอง #123 …")
                .Detail(demo, rooms).Because("ห้องไม่ว่าง");
            Notify("🧪 <i>ข้อความทดสอบ — ไม่ใช่การจองจริง</i>\n\n"
                   + BuildNotification("จองใหม่", bad, "New Reservation - Booking.com", false), true);

            return (true, "ส่งตัวอย่าง 2 ข้อความเข้า Telegram แล้ว (สำเร็จ + ล้มเหลว) — เปิดดูในกลุ่มได้เลย");
        }

        /// <summary>
        /// อ่านอีเมล STAAH ล่าสุด N ฉบับแบบ read-only แล้วรายงานว่า "แยกอะไรออกมาได้บ้าง"
        /// — ตอบคำถามอย่าง "ในอีเมลมีบอกยอดที่ OTA จะโอนไหม" ด้วยข้อมูลจริง ไม่ใช่การเดา
        /// เปิด folder แบบ ReadOnly + ไม่ move/ไม่ mark seen → ไม่กระทบคิวประมวลผลปกติ
        /// </summary>
        public string PreviewLatest(int count = 3)
        {
            if (count <= 0 || count > 10) count = 3;
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
                return "ยังไม่ได้ตั้งค่าอีเมล/รหัสผ่าน";
            var sb = new StringBuilder();
            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect(_imapServer, _imapPort, true);
                    client.Authenticate(_imapUser, _imapPassword);

                    // ดูทั้ง INBOX และ folder ที่ประมวลผลไปแล้ว (อีเมลส่วนใหญ่ถูกย้ายไปแล้ว)
                    var folders = new List<IMailFolder> { client.Inbox };
                    foreach (var name in new[] { _processedLabel, _failedLabel, _ignoredLabel })
                    {
                        try { folders.Add(client.GetFolder(client.PersonalNamespaces[0]).GetSubfolder(name)); }
                        catch { }
                    }

                    int shown = 0;
                    foreach (var folder in folders)
                    {
                        if (shown >= count) break;
                        try { folder.Open(FolderAccess.ReadOnly); } catch { continue; }
                        var uids = folder.Search(SearchQuery.FromContains(_fromContains));
                        for (int i = uids.Count - 1; i >= 0 && shown < count; i--)
                        {
                            var msg = folder.GetMessage(uids[i]);
                            shown++;
                            sb.AppendLine($"══ [{folder.Name}] {msg.Date:dd/MM/yyyy HH:mm} — {msg.Subject}");
                            sb.AppendLine(DescribeParse(msg.HtmlBody));
                            sb.AppendLine();
                        }
                    }
                    client.Disconnect(true);
                    if (shown == 0) sb.AppendLine("ไม่พบอีเมลจากผู้ส่งที่มีคำว่า '" + _fromContains + "'");
                }
            }
            catch (Exception ex) { return "อ่านอีเมลไม่สำเร็จ: " + ex.Message; }
            return sb.ToString();
        }

        /// <summary>รายงานฟิลด์ที่ parser หาเจอ/ไม่เจอ จาก HTML ของอีเมล 1 ฉบับ</summary>
        private string DescribeParse(string html)
        {
            if (string.IsNullOrEmpty(html)) return "  (อีเมลไม่มีเนื้อหา HTML)";
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            string text = doc.DocumentNode.InnerText;

            var sb = new StringBuilder();
            Func<string, string> f = v => string.IsNullOrWhiteSpace(v) ? "— ไม่พบ —" : v;
            sb.AppendLine($"  Channel Name   : {f(Rx(text, @"Channel Name:\s*(.+?)\s*(?:\n|Bookings Status)"))}");
            sb.AppendLine($"  Bookings Status: {f(Rx(text, @"Bookings Status:\s*(.+?)\s*(?:\n|Booking Id)"))}");
            sb.AppendLine($"  Booking Id     : {f(Rx(text, @"Booking Id#?:\s*(.+?)\s*(?:\n|$)"))}");
            sb.AppendLine($"  Payment Type   : {f(Rx(text, @"Payment Type:\s*(.+?)\s*(?:\n|$)"))}");

            string refsell = Rx(text, @"refsell_amt\s*:?\s*([\d,\.]+)");
            string allIncl = Rx(text, @"Total\s*\(All Inclusive\)\s*:?\s*THB\s*([\d,\.]+)");
            sb.AppendLine($"  refsell_amt    : {f(refsell)}");
            sb.AppendLine($"  Total (All Inclusive): {f(allIncl)}");

            var rooms = ExtractRoomBookings(html);
            sb.AppendLine($"  แยก room row ได้: {rooms.Count} แถว");
            foreach (var r in rooms)
                sb.AppendLine($"    • ROOM TYPE='{r.RoomType}' | NO OF ROOMS={r.NoOfRooms} | ADULTS={r.Adults} " +
                              $"| AMOUNT={r.NetAmount:N2} | {r.CheckIn:dd/MM/yyyy}-{r.CheckOut:dd/MM/yyyy}");
            if (rooms.Count > 0)
                sb.AppendLine($"  ผู้เข้าพัก='{rooms[0].GuestName}' เบอร์='{rooms[0].MobilePhone}'");

            // คำที่บ่งชี้ค่าคอม/ยอดโอน — ถ้าไม่เจอ แปลว่าอีเมลไม่ได้บอกมา
            var hints = new List<string>();
            foreach (var kw in new[] { "commission", "ค่าคอม", "net rate", "netrate", "payout", "remit", "transfer amount" })
                if (text.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) hints.Add(kw);
            sb.AppendLine(hints.Count > 0
                ? "  คำที่เกี่ยวกับค่าคอม/ยอดโอนที่พบ: " + string.Join(", ", hints)
                : "  ⚠️ ไม่พบคำที่เกี่ยวกับค่าคอมมิชชั่น/ยอดที่ OTA จะโอนในอีเมลนี้");
            return sb.ToString();
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

        // ── ลายเซ็นประจำ instance ─────────────────────────────────────────────────
        // ใส่ท้ายทุกข้อความ Telegram: เครื่อง/ชื่อไซต์/เวลา build ของ DLL
        // → เมื่อมี deployment เก่าค้างรันแข่งกัน (เคยเกิด: build เก่าตีอีเมลตกก่อน แล้ว build
        //   ใหม่ค่อยกู้จาก retry) จะดูออกทันทีว่าข้อความไหนมาจากตัวไหน โดยไม่ต้องเดา
        //   (ข้อความที่ "ไม่มีลายเซ็น" = build เก่าก่อนมีฟีเจอร์นี้)
        private static string _instanceStamp;
        private static string InstanceStamp()
        {
            if (_instanceStamp != null) return _instanceStamp;
            try
            {
                var asm = typeof(EmailReservationService).Assembly;
                DateTime built = DateTime.MinValue;
                try
                {
                    string appPath = System.Web.Hosting.HostingEnvironment.ApplicationPhysicalPath;
                    string dll = System.IO.Path.Combine(appPath ?? "", "bin", asm.GetName().Name + ".dll");
                    if (System.IO.File.Exists(dll)) built = System.IO.File.GetLastWriteTime(dll);
                }
                catch { }
                if (built == DateTime.MinValue && !string.IsNullOrEmpty(asm.Location))
                    built = System.IO.File.GetLastWriteTime(asm.Location);

                string site = "";
                try { site = System.Web.Hosting.HostingEnvironment.SiteName ?? ""; } catch { }
                _instanceStamp = Environment.MachineName
                    + (string.IsNullOrEmpty(site) ? "" : "/" + site)
                    + (built == DateTime.MinValue ? "" : $" · build {built:dd/MM HH:mm}");
            }
            catch { _instanceStamp = Environment.MachineName; }
            return _instanceStamp;
        }

        /// <summary>
        /// ล็อกระดับฐานข้อมูล (sp_getapplock, Session owner) — กันหลายตัวดึงกล่องเมลเดียวกันพร้อมกัน
        /// ครอบทั้ง timer ชนปุ่ม "ดึงตอนนี้", app pool ซ้อนตอน recycle, และ deployment ใหม่หลายชุด
        /// ที่ชี้ DB เดียวกัน (ตัวเก่าที่ไม่มีโค้ดนี้ล็อกไม่ได้ — ต้องปิดทิ้งสถานเดียว)
        /// ล็อกปล่อยเองเมื่อ connection ปิด
        /// </summary>
        private static bool TryAcquireIntakeLock(SqlConnection con)
        {
            try
            {
                using (var cmd = new SqlCommand("sp_getapplock", con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Resource", "TakeTime_EmailRsvIntake");
                    cmd.Parameters.AddWithValue("@LockMode", "Exclusive");
                    cmd.Parameters.AddWithValue("@LockOwner", "Session");
                    cmd.Parameters.AddWithValue("@LockTimeout", 0);
                    var ret = cmd.Parameters.Add("@Result", SqlDbType.Int);
                    ret.Direction = ParameterDirection.ReturnValue;
                    cmd.ExecuteNonQuery();
                    return Convert.ToInt32(ret.Value) >= 0;
                }
            }
            catch { return true; }   // ล็อกใช้ไม่ได้ (สิทธิ์/รุ่น SQL) → ทำงานต่อแบบไม่ล็อกดีกว่าหยุดทั้งระบบ
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

            using (var gate = new SqlConnection(_conn))
            {
                try { gate.Open(); } catch { /* DB ล่ม — ปล่อยไปตายที่ขั้นตอนปกติเพื่อได้ error เดิม */ }
                if (gate.State == ConnectionState.Open && !TryAcquireIntakeLock(gate))
                {
                    res.Error = "มีการดึงอีเมลรอบอื่นกำลังทำงานอยู่ — ข้ามรอบนี้ (กันประมวลผลซ้อน)";
                    _code.Logs(_conn, "EmailReservation", $"intake skipped: lock held elsewhere [{InstanceStamp()}]", "SYSTEM");
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
                    // แยก folder ให้อีเมลที่ไม่เกี่ยวกับการจอง — STAAH-Failed จะได้เหลือแต่ปัญหาจริง
                    IMailFolder ignored = null;
                    try { ignored = GetOrCreateFolder(client, _ignoredLabel); } catch { }

                    // กรองแค่ผู้ส่ง + ยังไม่อ่าน — ไม่กรองหัวเรื่อง เพราะอีเมล "แก้ไข" ของ STAAH
                    // ใช้หัวเรื่อง "New Reservation ..." เหมือนจองใหม่ (สถานะจริงอยู่ในเนื้อเมล)
                    // อีเมลที่ไม่ใช่ใบจองจะ parse ไม่ได้ แล้วถูกรายงานเป็นล้มเหลวตามปกติ
                    var query = SearchQuery.And(
                        SearchQuery.FromContains(_fromContains),
                        SearchQuery.NotSeen);
                    var uids = inbox.Search(query);
                    res.Fetched = uids.Count;

                    foreach (var uid in uids)
                        HandleOne(inbox, uid, processed, failed, ignored, res, false);

                    // ── ลองใหม่อัตโนมัติ: อีเมลที่เคยล้มเหลว (mapping/ห้องไม่ว่าง) ─────────────
                    // เดิมอีเมลที่ล้มเหลวถูกย้ายเข้า folder Failed แล้ว "จบ" — รอบถัดไปค้นหาเฉพาะ
                    // INBOX + ยังไม่อ่าน จึงไม่มีวันถูกหยิบมาทำอีก ต่อให้แก้ mapping/ปล่อยห้องแล้ว
                    // ตอนนี้จะวนกลับมาลองใหม่ให้เองภายใน Email_Rsv_RetryHours ชั่วโมง
                    if (_retryFailed && failed != null)
                        RetryFailedFolder(failed, processed, ignored, res);

                    client.Disconnect(true);
                }
                NotifySummary(res);
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
                _code.Logs(_conn, "EmailReservation", $"IMAP error: {ex.Message}", "SYSTEM");
                if (_notifyTelegram) Notify("❌ STAAH intake error: " + ex.Message + $"\n🖥 <i>{E(InstanceStamp())}</i>");
            }
            }   // gate — ปล่อย applock เมื่อ connection ปิด
            return res;
        }

        /// <summary>ประมวลผลอีเมล 1 ฉบับ + ย้าย folder ตามผล. isRetry = อยู่ใน folder Failed อยู่แล้ว.</summary>
        private void HandleOne(IMailFolder source, UniqueId uid, IMailFolder processed, IMailFolder failed,
            IMailFolder ignored, IntakeResult res, bool isRetry)
        {
            string subject = "";
            bool done = false;              // ok/dup/park/ignored = จบเคสแล้ว ย้ายออกจากคิว
            IMailFolder movedTo = processed;
            _inRetry = isRetry;      // กัน Notify ซ้ำจากชั้นใน (ProcessModification ฯลฯ) ตอนวนลองใหม่
            try
            {
                var msg = source.GetMessage(uid);
                subject = msg.Subject ?? "";

                // จดเลขจองของอีเมลนี้ลง Logs เสมอ — เป็น "ลายเซ็นว่าเราเป็นคนอ่าน"
                // ให้ตัวตรวจตัวอ่านแปลกปลอม (ด้านล่าง) แยกออกว่าอีเมลใน Failed ใบไหน
                // ถูกย้ายมาโดยเรา ใบไหนถูกโปรแกรมอื่นย้ายมา
                string traceBid = null;
                try
                {
                    var traceRooms = ExtractRoomBookings(msg.HtmlBody);
                    traceBid = traceRooms.Count > 0 ? (traceRooms[0].BookingId ?? "").Trim() : null;
                    if (!string.IsNullOrEmpty(traceBid) && !isRetry)
                        _code.Logs(_conn, "EmailReservation",
                            $"อ่านอีเมล booking={traceBid} [{InstanceStamp()}]", "SYSTEM");
                }
                catch { }

                // รอบลองใหม่: ถ้าเลขจองนี้ไม่เคยมีร่องรอยใน Logs เลย = เราไม่เคยอ่านฉบับนี้
                // → มีตัวอ่านตัวอื่นเป็นคนย้ายเข้า Failed (โปรแกรมเก่า/เซิร์ฟเวอร์เก่า)
                if (isRetry && !string.IsNullOrEmpty(traceBid))
                {
                    try
                    {
                        var seen = _code.DatabaseQuerySafe(_conn,
                            @"SELECT TOP 1 1 FROM Logs
                              WHERE LogAction = 'EmailReservation'
                                AND LogDateTime >= DATEADD(DAY, -7, GETDATE())
                                AND CAST(LogDetail AS NVARCHAR(MAX)) LIKE @p",
                            new Dictionary<string, object> { { "@p", "%" + traceBid + "%" } });
                        if (seen == null || seen.Rows.Count == 0)
                            res.ForeignBookings.Add(traceBid);
                    }
                    catch { }
                }

                string kind;
                var o = ProcessOne(msg, subject, out kind);
                done = o.Ok || o.Dup || o.Park || o.Ignored;
                movedTo = o.Ignored ? ignored : processed;
                if (isRetry)
                {
                    res.Retried++;
                    if (done && !o.Ignored) res.RetrySucceeded++;
                }
                if (o.Ignored) res.Ignored++;
                else if (o.Ok && o.Cancelled) res.Cancelled++;
                else if (o.Ok) res.Created++;
                else if (o.Dup) res.Duplicate++;
                else if (o.Park) res.Manual++;
                else if (!isRetry) res.Failed++;
                res.Messages.Add($"[{(isRetry ? "ลองใหม่/" : "")}{kind}] {o.Msg}");

                // รอบลองใหม่: แจ้งเฉพาะตอนสำเร็จ (ไม่งั้นจะเตือนซ้ำทุก 5 นาทีจนกว่าจะแก้)
                // เคส Park แจ้งจากข้างใน Process* ไปแล้ว (ข้อความละเอียดกว่า) — ไม่แจ้งซ้ำ
                if (_notifyTelegram && !o.Park && !o.Ignored && (o.Ok || (!o.Dup && !isRetry)))
                    Notify(BuildNotification(kind, o, subject, isRetry), true);
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
                if (done && movedTo != null && movedTo != source) source.MoveTo(uid, movedTo);
                else if (done) source.AddFlags(uid, MessageFlags.Seen, true);   // ไม่มี folder ปลายทาง
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
        private void RetryFailedFolder(IMailFolder failed, IMailFolder processed, IMailFolder ignored, IntakeResult res)
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
                    HandleOne(failed, uid, processed, null, ignored, res, true);
                }
                if (res.Retried > 0)
                    _code.Logs(_conn, "EmailReservation",
                        $"retry failed folder: ลองใหม่ {res.Retried} ฉบับ สำเร็จ {res.RetrySucceeded}", "SYSTEM");

                // 🚨 พบอีเมลที่ระบบนี้ไม่เคยอ่าน แต่โผล่ใน Failed = มีตัวอ่านเมลตัวอื่นทำงานอยู่
                //   (โปรแกรม GetReservationfromGmail ตัวเก่าใน Task Scheduler / เว็บอินสแตนซ์เก่า)
                //   ตัวเก่าใช้ตรรกะ mapping รุ่นเก่า → จองไม่ติดทั้งที่ระบบใหม่ลงได้ + แย่งอีเมลไปก่อน
                //   ระบบนี้กู้การจองคืนให้ผ่านรอบ retry แล้ว แต่ต้องปิดตัวเก่าถึงจะหายขาด
                if (res.ForeignBookings.Count > 0)
                {
                    string ids = string.Join(", ", res.ForeignBookings.Distinct().Take(5));
                    string warn = "🚨 <b>ตรวจพบโปรแกรมอ่านอีเมลจองตัวอื่น!</b>\n\n"
                        + $"พบอีเมลจอง ({ids}) ในโฟลเดอร์ Failed ที่ระบบนี้ไม่เคยอ่าน\n"
                        + "— แปลว่ามีตัวอ่านเมลตัวเก่ายังรันอยู่ (แย่งอ่านก่อนแล้วลงจองไม่สำเร็จ)\n\n"
                        + "🔧 วิธีปิดให้หายขาด:\n"
                        + "1. เปิด Task Scheduler บนเซิร์ฟเวอร์ → หา GetReservationfromGmail → Disable\n"
                        + "2. ตรวจว่าไม่มีเว็บ/เซิร์ฟเวอร์สำรองที่ deploy โค้ดรุ่นเก่าแล้วยังชี้เมลกล่องเดียวกัน\n"
                        + "3. สังเกตข้อความ Telegram: ของระบบใหม่มีบรรทัด 🖥 เครื่อง/เวลา build เสมอ — "
                        + "ข้อความที่ไม่มี = มาจากตัวเก่า\n\n"
                        + $"♻️ ระบบนี้ได้ลองลงจองคืนให้แล้วในรอบนี้\n🖥 <i>{E(InstanceStamp())}</i>";
                    _code.Logs(_conn, "EmailReservation",
                        $"foreign poller detected: bookings [{ids}] อยู่ใน Failed โดยไม่มีร่องรอยการอ่านของระบบนี้", "SYSTEM");
                    if (_notifyTelegram) Notify(warn, true);
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_conn, "EmailReservation", $"retry failed folder error: {ex.Message}", "SYSTEM");
            }
        }

        private Outcome ProcessOne(MimeMessage msg, string subject, out string kind)
        {
            var rooms = ExtractRoomBookings(msg.HtmlBody);

            // ── ด่านแรก: อีเมลนี้เกี่ยวกับการจองหรือเปล่า ───────────────────────────
            // ตัวกรองขาเข้าใช้แค่ "ผู้ส่งมีคำว่า staah" → จดหมายข่าว/รายงาน/แจ้งเตือนระบบ
            // ของ STAAH ก็เข้ามาด้วย พอ parse ไม่ได้เลยถูกนับเป็น "ล้มเหลว" กองใน STAAH-Failed
            // แล้ว retry ไล่ลองซ้ำทุกรอบ 72 ชม. โดยเปล่าประโยชน์
            if (!LooksLikeBookingEmail(subject, msg.HtmlBody, rooms))
            {
                kind = "ไม่ใช่ใบจอง";
                return new Outcome(false, false, false,
                    $"ข้าม — ไม่ใช่อีเมลการจอง: {Trunc(subject ?? "(ไม่มีหัวเรื่อง)", 120)}") { Ignored = true };
            }

            // ⚠️ สถานะจริงอยู่ใน "เนื้ออีเมล" (Bookings Status: Confirmed/Modified/Cancelled)
            // ไม่ใช่หัวเรื่อง — อีเมลแก้ไขของ STAAH ใช้หัวเรื่อง "New Reservation ..." เหมือนเดิม
            // ถ้าดูแต่หัวเรื่องจะถูกมองเป็นจองใหม่ แล้ว dedup ตัดทิ้ง = การแก้ไขหายเงียบ ๆ
            string bodyStatus = rooms.Count > 0 ? (rooms[0].BookingsStatus ?? "").Trim() : "";

            // สถานะในเนื้อเมลเชื่อถือได้ที่สุด — ใช้ก่อนเสมอ
            if (bodyStatus.Length > 0)
            {
                if (Has(bodyStatus, "Cancel")) { kind = "ยกเลิก"; return ProcessCancellation(rooms); }
                if (Has(bodyStatus, "Modif") || Has(bodyStatus, "Amend") || Has(bodyStatus, "Change"))
                { kind = "แก้ไข"; return ProcessModification(rooms); }
                kind = "จองใหม่";
                return ProcessNewReservation(rooms);
            }

            // อ่านสถานะจากเนื้อเมลไม่ได้ → ใช้หัวเรื่องเป็นตัวสำรอง
            // ใช้เฉพาะคำที่ STAAH ใช้จริง ไม่เอาคำกว้าง ๆ อย่าง "Update"/"Change"
            // (จดหมายข่าวหัวเรื่อง "... Update" เคยถูกตีเป็นอีเมลแก้ไขการจอง)
            string subj = subject ?? "";
            if (Has(subj, "Cancel")) { kind = "ยกเลิก"; return ProcessCancellation(rooms); }
            if (Has(subj, "Modif") || Has(subj, "Amend")) { kind = "แก้ไข"; return ProcessModification(rooms); }
            kind = "จองใหม่";
            return ProcessNewReservation(rooms);
        }

        /// <summary>
        /// อีเมลนี้เป็นใบจอง/แก้ไข/ยกเลิก หรือเป็นอีเมลอื่นของ STAAH (จดหมายข่าว/รายงาน/แจ้งเตือน)
        /// เกณฑ์ตั้งไว้ "หลวมเข้าไว้" ฝั่งที่บอกว่าใช่ — ถ้าเป็นใบจองจริงแต่ format เปลี่ยนจน parse
        /// ไม่ได้ ต้องยังถูกจับเป็นความล้มเหลวให้คนเห็น ไม่ใช่เงียบหายไป
        /// จะตัดทิ้งก็ต่อเมื่อ "ไม่มีร่องรอยของใบจองเลยสักอย่าง"
        /// </summary>
        private static bool LooksLikeBookingEmail(string subject, string html, List<RoomBooking> rooms)
        {
            if (rooms != null && rooms.Count > 0) return true;          // แยกข้อมูลได้ = ใบจองแน่นอน

            string body = html ?? "";
            foreach (var marker in new[] { "Booking Id", "Bookings Status", "ROOM TYPE", "CHECK-IN", "CHECK-OUT" })
                if (body.IndexOf(marker, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            // หัวเรื่องที่ STAAH ใช้จริงกับใบจอง (ตรงกับตัวกรองของโปรแกรมเดิม)
            string subj = subject ?? "";
            foreach (var kw in new[] { "New Reservation", "Reservation", "Cancelled", "Cancellation",
                                       "Modified", "Modification", "Amended" })
                if (subj.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0) return true;

            return false;
        }

        private static bool Has(string haystack, string needle) =>
            (haystack ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        // ── Parse ──────────────────────────────────────────────────────────────
        private class RoomBooking
        {
            public string ChannelName, BookingsStatus, BookingId, GuestName, MobilePhone, RoomType, PaymentType;
            public int NoOfRooms = 1, Adults = 1;
            // ⚠️ อีเมล STAAH ให้ตัวเลขมาแค่ 2 ตัว และ **ไม่มีค่าคอมมิชชั่น/ยอดที่ OTA จะโอนจริง**
            //    - AMOUNT      = เรตต่อคืนของ room row นั้น (ตัวที่โปรแกรมเดิมใช้คิดยอดรวมทั้งใบ)
            //    - refsell_amt = ราคาขายอ้างอิงระดับ booking (บางอีเมลไม่มี → fallback Total (All Inclusive))
            //    ยอดที่ OTA โอนจริงต้องรอ statement/settlement จาก OTA ห้ามเดาจากอีเมลนี้
            public double NetAmount;      // AMOUNT ต่อคืน (ต่อ room row)
            public double GrossTotal;     // refsell_amt / Total (All Inclusive) — ระดับ booking
            public DateTime CheckIn, CheckOut;
            public List<string> AssignedRooms;   // ชื่อห้องจริงที่ระบบจัดให้ (ไว้แจ้ง Telegram)
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
            if (existing?.Rows.Count > 0)
                return new Outcome(false, true, false, $"Booking {head.BookingId} มีในระบบแล้ว (#{existing.Rows[0][0]})")
                    .Detail(head, rooms, Convert.ToInt32(existing.Rows[0][0]));

            // สถานะในเนื้ออีเมลต้องเป็น Confirmed (โปรแกรมเดิมเช็คไว้ — พอร์ตมาแล้วหายไป)
            // ถ้าไม่เช็ค อีเมลสถานะ Pending / Tentative / On Request จะถูกลงจองเป็นของจริง
            string bodyStatus = (head.BookingsStatus ?? "").Trim();
            if (bodyStatus.Length > 0 && !Has(bodyStatus, "Confirm") && !Has(bodyStatus, "Modif"))
                return new Outcome(false, false, false, $"สถานะการจองในอีเมลเป็น '{bodyStatus}' ไม่ใช่ Confirmed — ไม่ลงจองอัตโนมัติ")
                    .Detail(head, rooms).Because("สถานะยังไม่ยืนยัน");

            // validate
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return new Outcome(false, false, false, "วันเช็คอิน/เอาท์ไม่ถูกต้อง").Detail(head, rooms).Because("ข้อมูลวันที่ไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays)
                return new Outcome(false, false, false, $"จำนวนคืน {stayDays} เกินที่ตั้งไว้ ({_maxStayDays})").Detail(head, rooms).Because("จำนวนคืนเกินกำหนด");
            if ((head.CheckIn - DateTime.Today).TotalDays > _maxDaysFuture)
                return new Outcome(false, false, false, $"จองล่วงหน้าเกิน {_maxDaysFuture} วัน").Detail(head, rooms).Because("จองล่วงหน้าเกินกำหนด");

            var (resId, reason) = SaveReservation(rooms, stayDays);
            if (resId <= 0)
                return new Outcome(false, false, false, reason).Detail(head, rooms)
                    .Because(reason.StartsWith("ไม่มี mapping") ? "ไม่มี mapping ห้องพัก"
                           : reason.StartsWith("ห้องไม่ว่าง") ? "ห้องไม่ว่าง" : "บันทึกลงฐานข้อมูลไม่สำเร็จ");

            if (_createDocument) TryEnqueueDocument(resId, head);
            return new Outcome(true, false, false,
                $"จอง #{resId} {head.GuestName} {head.CheckIn:dd/MM} ({head.ChannelName}) gross={head.GrossTotal:N2}")
                .Detail(head, rooms, resId);
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
                        var assignedNames = new List<string>();
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
                            foreach (var x in avail) { chosen.Add(x.Id); assignedNames.Add(x.Name); plan.Add((x.Id, r.Adults, r.NetAmount)); }
                        }

                        // gross รวม (ราคาขายจริง) → TotalPrice/Deposit; net รวม → OTA_Net_Amount
                        double grossTotal = head.GrossTotal > 0 ? head.GrossTotal
                            : rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);   // fallback net ถ้าไม่มี gross
                        double netTotal = rooms.Sum(r => r.NetAmount * r.NoOfRooms * stayDays);
                        bool channelCollect = (head.PaymentType ?? "").IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0
                                              || string.IsNullOrEmpty(head.PaymentType); // STAAH default = channel collect

                        int resId;
                        using (var cmd = new SqlCommand(ReservationInsertSql(), con, tx))
                        {
                            // ⚠️ "??" ไม่ทำงานกับสตริงว่าง — เคยหลุดเป็น Customer_MobilePhone = '' ที่ไม่มีลูกค้าผูก
                            string phone = string.IsNullOrWhiteSpace(head.MobilePhone)
                                ? "OTA_" + head.BookingId : head.MobilePhone;
                            EnsureCustomer(con, tx, phone, head.GuestName, head.ChannelName);
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
                        head.AssignedRooms = assignedNames;   // ไว้แจ้ง Telegram ว่าได้ห้องไหนจริง
                        _code.Logs(_conn, "EmailReservation",
                            $"created reservation {resId} ({plan.Count} rooms: {string.Join(", ", assignedNames)}) booking={head.BookingId}", "SYSTEM");
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

            // ⚠️ วน "ชั้นความแม่นยำ" เป็นวงนอก และ "รูปของชื่อห้อง" เป็นวงใน
            // ถ้าสลับกัน (วนชื่อเป็นวงนอก) การ match แบบหลวมของชื่อเต็มจะชนะการ match แบบตรงเป๊ะ
            // ของชื่อสั้น เช่น "Nordic Tent - RNB - Nordic Tent - Double - Room no breakfast"
            // จะไปเข้าเงื่อนไข "ROOM_TYPE เป็นส่วนหนึ่งของชื่อในอีเมล" ก่อนที่ "Nordic Tent" จะได้ลองแบบตรง
            foreach (var tier in tiers)
            {
                foreach (var rt in variants)
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

        /// <param name="comeFrom">ที่มาของลูกค้า (ชื่อ channel) — ลงคอลัมน์ ComeFrom ถ้าตารางมี</param>
        private void EnsureCustomer(SqlConnection con, SqlTransaction tx, string phone, string name, string comeFrom)
        {
            if (string.IsNullOrWhiteSpace(phone)) return;
            using (var chk = new SqlCommand("SELECT COUNT(*) FROM Customer WHERE MobilePhone = @p", con, tx))
            {
                chk.Parameters.AddWithValue("@p", phone);
                if (Convert.ToInt32(chk.ExecuteScalar()) > 0) return;
            }
            using (var ins = new SqlCommand(CustomerInsertSql(), con, tx))
            {
                ins.Parameters.AddWithValue("@p", phone);
                ins.Parameters.AddWithValue("@n", string.IsNullOrWhiteSpace(name) ? phone : name);
                ins.Parameters.AddWithValue("@from", (object)(comeFrom ?? "") ?? DBNull.Value);
                ins.Parameters.AddWithValue("@ctype", _customerTypeId);
                ins.ExecuteNonQuery();
            }
        }

        // ── สร้าง SQL ตามคอลัมน์ที่มีจริง ────────────────────────────────────────
        // schema ของแต่ละ deployment ไม่เท่ากัน (Customer ไม่มี Created_Date, Reservation อาจยัง
        // ไม่ได้รัน migration ที่เพิ่ม OTA_*) — hard-code ชื่อคอลัมน์แล้วพังทั้งใบจองเป็นเรื่องใหญ่
        // จึงอ่าน INFORMATION_SCHEMA ครั้งเดียว แล้วประกอบ statement เฉพาะคอลัมน์ที่มี
        private HashSet<string> TableColumns(string table)
        {
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t",
                    new Dictionary<string, object> { { "@t", table } });
                if (dt != null)
                    foreach (DataRow r in dt.Rows)
                        if (r[0] != DBNull.Value) cols.Add(r[0].ToString());
            }
            catch { }
            return cols;
        }

        private static string _reservationInsertSql;
        private string ReservationInsertSql()
        {
            if (_reservationInsertSql != null) return _reservationInsertSql;
            var cols = TableColumns("Reservation");

            var names = new List<string> { "[Customer_MobilePhone]", "[CheckinDate]", "[CheckoutDate]",
                                           "[StayDays]", "[Status]", "[TotalPrice]", "[Deposit]", "[Remark]" };
            var vals = new List<string> { "@Phone", "@In", "@Out", "@Days", "N'มัดจำแล้ว'", "@Total", "@Dep", "@Remark" };
            Action<string, string> opt = (col, val) =>
            {
                if (cols.Count == 0 || cols.Contains(col)) { names.Add("[" + col + "]"); vals.Add(val); }
            };
            opt("Reserve_By", "N'System(Email)'");
            opt("Created_Date", "GETDATE()");
            // ⚠️ คอลัมน์นี้เก็บเป็นข้อความ 'True'/'False' (โค้ดอื่นอ่านด้วย .ToString().ToLower())
            // ถ้าใส่ 1 จะได้ '1' ซึ่งไม่ตรงทั้ง "true" และ "false" → logic ใบเสร็จเพี้ยน
            opt("NoCreateReceipt", "N'True'");
            opt("NoNameinReceipt", "N'True'");
            opt("OTA_Channel", "@Ch");
            opt("OTA_Booking_ID", "@Bk");
            opt("OTA_Gross_Amount", "@Gross");
            opt("OTA_Net_Amount", "@Net");
            opt("OTA_Payment_Type", "@Pay");
            opt("OTA_Guest_Name", "@Guest");

            _reservationInsertSql = $"INSERT INTO [dbo].[Reservation] ({string.Join(",", names)}) " +
                                    $"VALUES ({string.Join(",", vals)}); SELECT SCOPE_IDENTITY();";
            _code.Logs(_conn, "EmailReservation", "reservation insert columns: " + string.Join(",", names), "SYSTEM");
            return _reservationInsertSql;
        }

        /// <summary>ชุด SET ของ UPDATE ตอนแก้ไข — ข้ามคอลัมน์ OTA_* ที่ยังไม่ได้ migrate</summary>
        private static string _reservationUpdateSet;
        private string ReservationUpdateSet()
        {
            if (_reservationUpdateSet != null) return _reservationUpdateSet;
            var cols = TableColumns("Reservation");
            var sets = new List<string>
            {
                "[CheckinDate] = @In", "[CheckoutDate] = @Out", "[StayDays] = @Days",
                "[TotalPrice] = @Total", "[Deposit] = @Dep", "[Remark] = @Remark"
            };
            Action<string, string> opt = (col, expr) =>
            {
                if (cols.Count == 0 || cols.Contains(col)) sets.Add(expr);
            };
            opt("OTA_Gross_Amount", "OTA_Gross_Amount = @Gross");
            opt("OTA_Net_Amount", "OTA_Net_Amount = @Net");
            opt("OTA_Payment_Type", "OTA_Payment_Type = @Pay");
            opt("OTA_Guest_Name", "OTA_Guest_Name = @Guest");
            opt("Modified_Date", "Modified_Date = GETDATE()");
            _reservationUpdateSet = string.Join(", ", sets);
            return _reservationUpdateSet;
        }

        private static string _customerInsertSql;
        private string CustomerInsertSql()
        {
            if (_customerInsertSql != null) return _customerInsertSql;
            var cols = TableColumns("Customer");

            var names = new List<string> { "MobilePhone", "Name" };
            var vals = new List<string> { "@p", "@n" };
            Action<string, string> opt = (col, val) =>
            {
                if (cols.Contains(col)) { names.Add("[" + col + "]"); vals.Add(val); }
            };
            opt("FullName", "@n");
            opt("ComeFrom", "@from");
            opt("NickName", "N''");
            opt("Remark", "N''");
            opt("Address", "N''");
            opt("Address1", "N''");
            opt("IDNumber", "N''");
            opt("Email", "N''");
            opt("Branch_Number", "N'00000'");
            opt("Address_ID", "0");
            opt("Customer_Type_ID", "@ctype");
            opt("Created_Date", "GETDATE()");

            _customerInsertSql = $"INSERT INTO Customer ({string.Join(", ", names)}) VALUES ({string.Join(", ", vals)})";
            _code.Logs(_conn, "EmailReservation", "customer insert columns: " + string.Join(",", names), "SYSTEM");
            return _customerInsertSql;
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
            if (rooms.Count == 0) return new Outcome(false, false, false, "แยกข้อมูลจากอีเมลแก้ไขไม่ได้ (format เปลี่ยน?)").Because("อ่านอีเมลไม่ออก");
            var head = rooms[0];
            if (string.IsNullOrWhiteSpace(head.BookingId))
                return new Outcome(false, false, false, "ไม่พบ Booking ID ในอีเมลแก้ไข").Detail(head, rooms).Because("อ่านอีเมลไม่ออก");

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
                return new Outcome(false, false, false, warn, park: true).Detail(head, rooms, resId);
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
                return new Outcome(false, false, false, warn, park: true).Detail(head, rooms, resId);
            }

            // ── validate ข้อมูลใหม่ ────────────────────────────────────────────
            if (head.CheckIn == default || head.CheckOut == default || head.CheckOut <= head.CheckIn)
                return new Outcome(false, false, false, "วันเช็คอิน/เอาท์ในอีเมลแก้ไขไม่ถูกต้อง").Detail(head, rooms, resId).Because("ข้อมูลวันที่ไม่ถูกต้อง");
            int stayDays = (int)(head.CheckOut - head.CheckIn).TotalDays;
            if (stayDays > _maxStayDays)
                return new Outcome(false, false, false, $"จำนวนคืน {stayDays} เกินที่ตั้งไว้ ({_maxStayDays})").Detail(head, rooms, resId).Because("จำนวนคืนเกินกำหนด");

            DateTime oldIn = Convert.ToDateTime(dt.Rows[0]["CheckinDate"]);
            DateTime oldOut = Convert.ToDateTime(dt.Rows[0]["CheckoutDate"]);
            decimal oldTotal = dt.Rows[0]["TotalPrice"] != DBNull.Value ? Convert.ToDecimal(dt.Rows[0]["TotalPrice"]) : 0m;

            var (ok, msg) = UpdateReservation(resId, rooms, stayDays);
            if (!ok)
                return new Outcome(false, false, false, msg).Detail(head, rooms, resId)
                    .Because(msg.Contains("ห้องไม่ว่าง") ? "ห้องไม่ว่างตามวันที่ใหม่" : "แก้ไขไม่สำเร็จ");

            string changes = BuildChangeSummary(oldIn, oldOut, oldTotal, head, stayDays);
            string done = $"แก้ไขการจอง #{resId} ({head.BookingId}) เรียบร้อย — {changes}";
            _code.Logs(_conn, "EmailReservation", done, "SYSTEM");
            var okOut = new Outcome(true, false, false, done).Detail(head, rooms, resId);
            okOut.Changes = changes;
            okOut.OldDates = $"{oldIn:dd/MM/yyyy}-{oldOut:dd/MM/yyyy}";
            return okOut;
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
                                EnsureCustomer(con, tx, head.MobilePhone, head.GuestName, head.ChannelName);
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

                        using (var cmd = new SqlCommand(
                            "UPDATE [dbo].[Reservation] SET " + ReservationUpdateSet() + " WHERE ID = @id", con, tx))
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
            var chead = rooms.Count > 0 ? rooms[0] : null;
            if (string.IsNullOrWhiteSpace(bookingId))
                return new Outcome(false, false, true, "ไม่พบ Booking ID ในอีเมลยกเลิก").Because("อ่านอีเมลไม่ออก");

            var dt = _code.DatabaseQuerySafe(_conn,
                "SELECT TOP 1 ID, Status FROM Reservation WHERE OTA_Booking_ID = @b OR Remark LIKE @p ORDER BY ID DESC",
                new Dictionary<string, object> { { "@b", bookingId }, { "@p", "%" + bookingId + "%" } });
            // ไม่พบ = อาจเป็นอีเมลมาก่อนใบจอง (out-of-order) — ปล่อยเป็น fail ให้ retry loop
            // วนกลับมา: พอใบจองถูกสร้างจาก folder Failed แล้ว รอบถัดไปใบยกเลิกจะเจอและยกเลิกเอง
            if (dt == null || dt.Rows.Count == 0)
                return new Outcome(false, false, true, $"ไม่พบการจอง {bookingId} ในระบบ (อาจยังลงจองไม่สำเร็จ — ระบบจะยกเลิกให้เองหลังใบจองถูกสร้าง)")
                    .Detail(chead).Because("ไม่พบการจองในระบบ");
            int resId = Convert.ToInt32(dt.Rows[0]["ID"]);
            string status = dt.Rows[0]["Status"]?.ToString() ?? "";
            if (status == "ยกเลิก" || status == "ยกเลิกคืนเงิน" || status == "ยกเลิกไม่คืนเงิน")
                return new Outcome(false, true, true, $"การจอง #{resId} ถูกยกเลิกไปแล้ว").Detail(chead, null, resId);

            // ── guard: เช็คอิน/เช็คเอาท์แล้ว → ห้ามยกเลิกเอง (แขกอยู่ในห้อง/เข้าพักจบแล้ว) ──
            if (status.IndexOf("เช็คอิน", StringComparison.Ordinal) >= 0
                || status.IndexOf("เช็คเอาท์", StringComparison.Ordinal) >= 0)
            {
                string warn = $"⚠️ Booking {bookingId} (จอง #{resId}) มีอีเมลยกเลิก แต่สถานะเป็น \"{status}\" แล้ว — " +
                              "ไม่ยกเลิกอัตโนมัติ กรุณาตรวจสอบกับ OTA แล้วจัดการเอง";
                _code.Logs(_conn, "EmailReservation", warn, "SYSTEM");
                if (_notifyTelegram) Notify(warn, true);
                return new Outcome(false, false, true, warn, park: true).Detail(chead, null, resId);
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
                return new Outcome(false, false, true, warn, park: true).Detail(chead, null, resId);
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
            return new Outcome(true, false, true, $"ยกเลิกการจอง #{resId} ({bookingId}) → สถานะ \"{_cancelStatus}\"")
                .Detail(chead, null, resId);
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

        // ── ข้อความแจ้งเตือน Telegram ────────────────────────────────────────────
        // TelegramBot2 ส่งด้วย parse_mode=HTML อยู่แล้ว → จัดรูปแบบให้อ่านง่ายบนมือถือ
        // (ข้อความบรรทัดเดียวแบบเดิมอ่านยาก ต้องเปิดระบบดูทุกครั้งว่าใบไหน ห้องอะไร)
        private string BuildNotification(string kind, Outcome o, string subject, bool isRetry)
        {
            var h = o.Head;
            var sb = new StringBuilder();
            string tag = isRetry ? "  <i>(ลองใหม่อัตโนมัติ)</i>" : "";

            if (o.Ok && o.Cancelled) sb.AppendLine("<b>🚫 ยกเลิกการจองแล้ว</b>" + tag);
            else if (o.Ok && kind == "แก้ไข") sb.AppendLine("<b>✏️ แก้ไขการจองแล้ว</b>" + tag);
            else if (o.Ok) sb.AppendLine("<b>✅ การจองสำเร็จ</b>" + tag);
            else if (o.Cancelled) sb.AppendLine("<b>❌ ยกเลิกไม่สำเร็จ</b>" + tag);
            else sb.AppendLine($"<b>❌ {E(kind)}ไม่สำเร็จ</b>" + tag);
            sb.AppendLine();

            if (!o.Ok)
            {
                sb.AppendLine($"⚠️ <b>สาเหตุ:</b> {E(o.Reason ?? "ไม่ทราบสาเหตุ")}");
                sb.AppendLine($"📝 <b>รายละเอียด:</b> {E(Trunc(o.Msg, 700))}");
                if (!string.IsNullOrWhiteSpace(subject))
                    sb.AppendLine($"✉️ <b>อีเมล:</b> {E(Trunc(subject, 120))}");
                // ตอนล้มเหลวต้องเห็น ROOM TYPE "ตัวเต็มดิบ ๆ" เพราะเป็นค่าที่ต้องเอาไปใส่ตาราง
                // MapDataWithSTAAH ให้ตรง — ย่อแล้วจะแก้ mapping ไม่ได้
                if (o.Rooms != null)
                    foreach (var r in o.Rooms.Where(x => !string.IsNullOrWhiteSpace(x.RoomType)))
                        sb.AppendLine($"🏷 <b>ROOM TYPE ในอีเมล:</b> <code>{E(r.RoomType)}</code>");
                sb.AppendLine();
            }

            if (o.ResId > 0) sb.AppendLine($"📋 <b>เลขที่จองในระบบ:</b> {o.ResId}");
            if (h != null)
            {
                if (!string.IsNullOrWhiteSpace(h.BookingId)) sb.AppendLine($"🔖 <b>Booking ID:</b> {E(h.BookingId)}");
                if (!string.IsNullOrWhiteSpace(h.ChannelName)) sb.AppendLine($"🌐 <b>ช่องทาง:</b> {E(h.ChannelName)}");
                if (!string.IsNullOrWhiteSpace(h.GuestName) || !string.IsNullOrWhiteSpace(h.MobilePhone))
                {
                    sb.AppendLine();
                    if (!string.IsNullOrWhiteSpace(h.GuestName)) sb.AppendLine($"👤 <b>ผู้เข้าพัก:</b> {E(h.GuestName)}");
                    if (!string.IsNullOrWhiteSpace(h.MobilePhone)) sb.AppendLine($"📞 <b>เบอร์โทร:</b> {E(h.MobilePhone)}");
                }
                if (h.CheckIn != default && h.CheckOut != default)
                {
                    int nights = (int)(h.CheckOut - h.CheckIn).TotalDays;
                    sb.AppendLine();
                    if (!string.IsNullOrEmpty(o.OldDates)) sb.AppendLine($"📅 <b>เดิม:</b> {E(o.OldDates)}");
                    sb.AppendLine($"📅 <b>เช็คอิน:</b> {h.CheckIn:dd/MM/yyyy}");
                    sb.AppendLine($"📅 <b>เช็คเอาท์:</b> {h.CheckOut:dd/MM/yyyy}");
                    if (nights > 0) sb.AppendLine($"🌙 <b>จำนวนคืน:</b> {nights} คืน");
                }
            }

            if (o.Rooms != null && o.Rooms.Count > 0)
            {
                int nights = Math.Max(1, (int)(h.CheckOut - h.CheckIn).TotalDays);
                sb.AppendLine();
                sb.AppendLine("<b>🏨 ห้องที่จอง:</b>");
                int totalRooms = 0;
                foreach (var r in o.Rooms)
                {
                    // ROOM TYPE จาก OTA ยาวมาก เช่น
                    // "Nordic Tent - RNB - Nordic Tent - Double - Room no breakfast"
                    // → แยกชื่อห้องกับแผนราคาคนละบรรทัด ไม่งั้นบนมือถืออ่านไม่ออก
                    var (baseName, ratePlan) = SplitRoomType(r.RoomType);
                    sb.AppendLine($"  • {E(baseName)} × {r.NoOfRooms} ({r.Adults} คน, {r.NetAmount:N0} THB/คืน)");
                    if (!string.IsNullOrEmpty(ratePlan))
                        sb.AppendLine($"     ↳ <i>แผนราคา: {E(Trunc(ratePlan, 80))}</i>");
                    totalRooms += r.NoOfRooms;
                }
                if (h.AssignedRooms != null && h.AssignedRooms.Count > 0)
                    sb.AppendLine($"🛏 <b>จัดให้ห้อง:</b> {E(string.Join(", ", h.AssignedRooms))}");
                else if (totalRooms > 0)
                    sb.AppendLine($"🛏 <b>จำนวนห้อง:</b> {totalRooms}");

                // ⚠️ ห้ามเรียกตัวไหนว่า "ยอดที่ OTA จะโอน" — อีเมลไม่ได้บอกค่าคอมมิชชั่นมาด้วย
                // มีแค่ refsell_amt (ระดับ booking) กับ AMOUNT (เรตต่อคืนต่อห้อง) ถ้าสองตัวไม่เท่ากัน
                // แปลว่าอาจมีภาษี/ส่วนลด/ค่าคอมรวมอยู่ — แจ้งให้คนดู ไม่สรุปแทน
                double refSell = h.GrossTotal;
                double sumRows = o.Rooms.Sum(r => r.NetAmount * r.NoOfRooms * nights);
                double booked = refSell > 0 ? refSell : sumRows;
                sb.AppendLine();
                sb.AppendLine($"💰 <b>ยอดรวมที่บันทึก:</b> {booked:N0} THB");
                if (refSell > 0 && sumRows > 0 && Math.Abs(refSell - sumRows) > 1)
                {
                    sb.AppendLine($"   ├ refsell_amt (ในอีเมล): {refSell:N0}");
                    sb.AppendLine($"   └ AMOUNT รวมรายห้อง: {sumRows:N0}  <i>ต่าง {Math.Abs(refSell - sumRows):N0} — อาจเป็นภาษี/ส่วนลด/ค่าคอม ตรวจกับ OTA</i>");
                }
                bool channelCollect = (h.PaymentType ?? "").IndexOf("Channel", StringComparison.OrdinalIgnoreCase) >= 0
                                      || string.IsNullOrEmpty(h.PaymentType);
                sb.AppendLine($"💳 <b>การชำระ:</b> {(channelCollect ? "OTA เก็บเงินแล้ว (Channel Collect)" : "เก็บเงินหน้างาน (Hotel Collect)")}");
            }

            if (!string.IsNullOrEmpty(o.Changes))
            {
                sb.AppendLine();
                sb.AppendLine($"🔄 <b>สิ่งที่เปลี่ยน:</b> {E(o.Changes)}");
            }

            if (o.Ok && o.Cancelled)
            {
                sb.AppendLine();
                sb.AppendLine($"<i>สถานะในระบบ: {E(_cancelStatus)} — ห้องถูกปล่อยคืนแล้ว</i>");
            }
            else if (!o.Ok)
            {
                sb.AppendLine();
                sb.AppendLine(_retryFailed && _moveFailed
                    ? $"<i>♻️ ระบบจะลองใหม่ให้เองทุกรอบภายใน {_retryHours} ชม. — แก้ mapping หรือปล่อยห้องแล้วไม่ต้องทำอะไรเพิ่ม</i>"
                    : "<i>⚠️ ระบบไม่ลองใหม่ให้ (ปิดไว้) — ต้องจัดการเอง</i>");
            }

            sb.Append($"\n🕐 <i>{DateTime.Now:dd/MM/yyyy HH:mm} · 🖥 {E(InstanceStamp())}</i>");
            return sb.ToString();
        }

        /// <summary>สรุปท้ายรอบ — ส่งเฉพาะเมื่อรอบนั้นมีอีเมลให้ทำจริง (กันสแปมทุก 5 นาที)</summary>
        private void NotifySummary(IntakeResult res)
        {
            if (!_notifyTelegram || Cfg("Email_Rsv_NotifySummary", "1") != "1") return;
            if (res.Fetched == 0 && res.Retried == 0) return;
            // อีเมลใบเดียวจบในตัวอยู่แล้ว (แจ้งไปแล้วละเอียดกว่า) — สรุปมีค่าเมื่อทำหลายฉบับ
            if (res.Fetched + res.Retried < 2 && res.Failed == 0 && res.Manual == 0) return;
            // รอบที่เจอแต่อีเมลที่ไม่เกี่ยวกับการจอง = ไม่มีอะไรให้รายงาน
            if (res.Created + res.Cancelled + res.Duplicate + res.Failed + res.Manual == 0) return;

            string emoji = (res.Failed > 0 || res.Manual > 0) ? "⚠️" : "✅";
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{emoji} สรุปการอ่านอีเมลจอง OTA</b>");
            sb.AppendLine();
            sb.AppendLine($"📧 <b>อีเมลที่ดึง:</b> {res.Fetched}");
            if (res.Created > 0) sb.AppendLine($"✅ <b>จองใหม่/แก้ไขสำเร็จ:</b> {res.Created}");
            if (res.Cancelled > 0) sb.AppendLine($"🚫 <b>ยกเลิก:</b> {res.Cancelled}");
            if (res.Duplicate > 0) sb.AppendLine($"🔁 <b>ซ้ำ (มีในระบบแล้ว):</b> {res.Duplicate}");
            if (res.Failed > 0) sb.AppendLine($"❌ <b>ล้มเหลว:</b> {res.Failed}");
            if (res.Manual > 0) sb.AppendLine($"🖐 <b>ต้องตรวจเอง:</b> {res.Manual}");
            if (res.Ignored > 0) sb.AppendLine($"📭 <b>ไม่ใช่อีเมลการจอง (ข้าม):</b> {res.Ignored}");
            if (res.Retried > 0) sb.AppendLine($"♻️ <b>ลองใหม่:</b> {res.Retried} ฉบับ (สำเร็จ {res.RetrySucceeded})");
            sb.Append($"\n🕐 <i>{DateTime.Now:dd/MM/yyyy HH:mm} · 🖥 {E(InstanceStamp())}</i>");
            Notify(sb.ToString(), true);
        }

        /// <summary>แยก "Nordic Tent - RNB - ... - Room no breakfast" → ("Nordic Tent", "RNB - ... - Room no breakfast")</summary>
        private static (string Name, string RatePlan) SplitRoomType(string roomType)
        {
            string rt = Norm(CleanEntities(roomType ?? ""));
            int i = rt.IndexOf(" - ", StringComparison.Ordinal);
            return i > 0 ? (rt.Substring(0, i).Trim(), rt.Substring(i + 3).Trim()) : (rt, "");
        }

        private static string E(string s) =>
            string.IsNullOrEmpty(s) ? "" : s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        private static string Trunc(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";

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
