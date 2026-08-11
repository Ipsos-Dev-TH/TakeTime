using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text.RegularExpressions;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using HtmlAgilityPack;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// สะพาน "อีเมล ↔ แชท" สำหรับคุยกับลูกค้าที่จองผ่าน OTA โดยไม่ต้องขอ API จาก OTA:
    ///
    ///   ลูกค้า OTA ──(อีเมลผ่าน relay ของ OTA เช่น xxx@agoda-messaging.com)──▶ กล่องเมลที่พัก
    ///        ▲                                                                    │ (IMAP poll)
    ///        │                                                                    ▼
    ///   OTA ส่งต่อให้ลูกค้า ◀──(SMTP ตอบกลับไปที่ alias เดิม)── พนักงานพิมพ์ในหน้าแชท OmniChannel
    ///
    /// Agoda / Booking.com ให้ "อีเมลแฝง" (masked alias) ของลูกค้าต่อการจอง — ส่งเมลไปที่
    /// alias นั้น OTA จะ relay ต่อให้ลูกค้าเห็นเป็นข้อความในแอปของเขาเอง จึงคุยสองทางได้
    /// โดยไม่ต้องมี partner API
    ///
    /// การตั้งค่าอยู่ใน Config ของ channel EMAIL (Admin → Chat → ChannelSettings):
    ///   fromDomains, pollMinutes, processedLabel, notifyTelegram, signature
    /// เปิด/ปิดด้วยสวิตช์ของ channel EMAIL. ใช้กล่อง IMAP เดียวกับระบบอ่านอีเมลจอง
    /// (Email_Rsv_* — ตั้งที่ Admin → Accounting Integration) และส่งออกด้วย SMTP เดิม
    /// ของระบบ (SMTP / Email_From / Email_Password_From)
    /// </summary>
    public class EmailChatService
    {
        private readonly string _conn;
        private readonly code _code = new code();
        private readonly OmniChannelService _omni;

        // ── config (channel EMAIL) ──
        private readonly bool _enabled;
        private readonly string[] _fromDomains;
        private readonly int _pollMinutes;
        private readonly string _processedLabel;
        private readonly string[] _extraFolders;
        private readonly bool _notifyTelegram;
        private readonly string _signature;

        // ── IMAP (ใช้ร่วมกับระบบอ่านอีเมลจอง — กล่องเดียวกัน) ──
        private readonly string _imapServer, _imapUser, _imapPassword;
        private readonly int _imapPort;

        private const string Channel = "EMAIL";
        private const int MaxBodyChars = 4000;
        private const int MaxAttachments = 5;
        private const long MaxAttachmentBytes = 10 * 1024 * 1024;

        public EmailChatService(string connectionString)
        {
            _conn = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _omni = new OmniChannelService(_conn);

            var cfg = _omni.GetChannelConfig(Channel) ?? new Dictionary<string, object>();
            string Get(string k, string def) =>
                cfg.ContainsKey(k) && !string.IsNullOrWhiteSpace(cfg[k]?.ToString()) ? cfg[k].ToString().Trim() : def;

            _enabled = IsEnabled(_conn);
            _fromDomains = Get("fromDomains", "agoda-messaging.com, mchat.booking.com, guest.booking.com")
                .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(d => d.Trim().ToLowerInvariant()).Where(d => d.Length > 3).Distinct().ToArray();
            _pollMinutes = int.TryParse(Get("pollMinutes", "3"), out var pm) && pm >= 1 ? pm : 3;
            _processedLabel = Get("processedLabel", "Chat-Processed");
            _extraFolders = Get("extraFolders", "")
                .Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => f.Trim()).Where(f => f.Length > 0 && !f.Equals("INBOX", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            _notifyTelegram = Get("notifyTelegram", "1") != "0";
            _signature = Get("signature", "");

            _imapServer = RsvCfg("Email_Rsv_ImapServer", "imap.gmail.com");
            _imapPort = int.TryParse(RsvCfg("Email_Rsv_ImapPort", "993"), out var p) ? p : 993;
            _imapUser = RsvCfg("Email_Rsv_Username", "");
            _imapPassword = _code.Derypt(RsvCfg("Email_Rsv_Password_Encrypted", ""));
        }

        private string RsvCfg(string key, string def)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string v = dt.Rows[0][0].ToString();
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }
            return def;
        }

        /// <summary>เปิดใช้เมื่อ channel EMAIL ใน OmniChannel ถูกเปิด (Admin → Chat → ตั้งค่าช่องทาง)</summary>
        public static bool IsEnabled(string conn)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 IsEnabled FROM OmniChannel_Channels WHERE ChannelCode = 'EMAIL'", null);
                return dt?.Rows.Count > 0 && Convert.ToBoolean(dt.Rows[0][0]);
            }
            catch { return false; }
        }

        // ── ขาเข้า: ดึงอีเมลลูกค้า → ลงเป็นข้อความแชท ─────────────────────────────

        private static DateTime _lastPoll = DateTime.MinValue;
        private static readonly object _pollLock = new object();

        /// <summary>เรียกจาก timer หลัก (Global.asax) — คุม enabled + รอบเวลาเอง, no-op ถ้ายังไม่ถึงรอบ</summary>
        public static void PollIfDue(string conn)
        {
            if (!IsEnabled(conn)) return;
            EmailChatService svc;
            try { svc = new EmailChatService(conn); } catch { return; }
            lock (_pollLock)
            {
                if ((DateTime.Now - _lastPoll).TotalMinutes < svc._pollMinutes) return;
                _lastPoll = DateTime.Now;
            }
            svc.PollInbox();
        }

        public class ChatPollResult
        {
            public int Fetched, Received, Duplicate, Failed;
            public string Error;
            public List<string> Messages = new List<string>();
        }

        /// <summary>
        /// อ่านอีเมลที่ยังไม่อ่านจากโดเมน relay ของ OTA แล้วลงเป็นข้อความแชทใน OmniChannel.
        /// ค้นเฉพาะโดเมนที่ตั้งไว้ — ไม่แตะอีเมลจอง STAAH (ระบบ intake ค้นแยกด้วย from ของตัวเอง)
        /// </summary>
        public ChatPollResult PollInbox()
        {
            var res = new ChatPollResult();
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
            { res.Error = "ยังไม่ได้ตั้งค่าอีเมล IMAP (ใช้ค่าเดียวกับระบบอ่านอีเมลจอง — Admin → Accounting Integration)"; return res; }
            if (_fromDomains.Length == 0) { res.Error = "ยังไม่ได้ตั้งโดเมนอีเมลลูกค้า (fromDomains)"; return res; }

            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect(_imapServer, _imapPort, true);
                    client.Authenticate(_imapUser, _imapPassword);

                    IMailFolder processed = null;
                    try { processed = GetOrCreateFolder(client, _processedLabel); } catch { }

                    // โฟลเดอร์ที่ไล่อ่าน: INBOX + โฟลเดอร์/label เพิ่มเติมที่ตั้งไว้ (extraFolders)
                    // — รองรับเคส Gmail ตั้ง filter ติด label แล้วย้ายข้าม Inbox ไป
                    var folders = new List<IMailFolder> { client.Inbox };
                    foreach (string name in _extraFolders)
                    {
                        if (string.Equals(name, _processedLabel, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            var f = client.GetFolder(name);   // full path เช่น "OTA-Chat" หรือ "งาน/Agoda"
                            if (f != null) folders.Add(f);
                        }
                        catch (FolderNotFoundException)
                        {
                            res.Messages.Add($"[warn] ไม่พบโฟลเดอร์ '{name}' ในกล่องเมล");
                        }
                    }

                    // OR ของทุกโดเมน AND ยังไม่อ่าน
                    SearchQuery domainQuery = SearchQuery.FromContains(_fromDomains[0]);
                    for (int i = 1; i < _fromDomains.Length; i++)
                        domainQuery = SearchQuery.Or(domainQuery, SearchQuery.FromContains(_fromDomains[i]));
                    var query = SearchQuery.And(domainQuery, SearchQuery.NotSeen);

                    foreach (var folder in folders)
                    {
                        try
                        {
                            folder.Open(FolderAccess.ReadWrite);
                            var uids = folder.Search(query);
                            res.Fetched += uids.Count;

                            foreach (var uid in uids)
                            {
                                bool ok = false;
                                try
                                {
                                    var msg = folder.GetMessage(uid);
                                    // ข้อความเดียวกันอาจโผล่หลายโฟลเดอร์ (Gmail label ซ้อน) —
                                    // dedup ด้วย Message-Id ใน IngestMessage กันลงแชทซ้ำอยู่แล้ว
                                    var r = IngestMessage(msg);
                                    if (r == IngestOutcome.Received) { res.Received++; ok = true; }
                                    else if (r == IngestOutcome.Duplicate) { res.Duplicate++; ok = true; }
                                    else res.Failed++;
                                }
                                catch (Exception ex)
                                {
                                    res.Failed++;
                                    res.Messages.Add("[error] " + ex.Message);
                                    _code.Logs(_conn, "EmailChat", $"ingest failed: {ex.Message}", "SYSTEM");
                                }

                                try
                                {
                                    if (ok && processed != null) folder.MoveTo(uid, processed);
                                    else folder.AddFlags(uid, MessageFlags.Seen, true);   // กันวนซ้ำแม้ ingest พลาด (มี log แล้ว)
                                }
                                catch { }
                            }
                        }
                        catch (Exception fex)
                        {
                            res.Messages.Add($"[warn] อ่านโฟลเดอร์ '{folder.FullName}' ไม่สำเร็จ: {fex.Message}");
                            _code.Logs(_conn, "EmailChat", $"folder '{folder.FullName}' error: {fex.Message}", "SYSTEM");
                        }
                    }
                    client.Disconnect(true);
                }
            }
            catch (Exception ex)
            {
                res.Error = ex.Message;
                _code.Logs(_conn, "EmailChat", $"IMAP error: {ex.Message}", "SYSTEM");
            }
            return res;
        }

        private enum IngestOutcome { Received, Duplicate, Failed }

        private IngestOutcome IngestMessage(MimeMessage msg)
        {
            // ที่อยู่สำหรับตอบกลับ = Reply-To ก่อน (Agoda ใส่ alias ของลูกค้าไว้ตรงนี้ — From เป็น
            // notifications@agoda-messaging.com ที่ตอบกลับไม่ถึงลูกค้า) แล้วค่อย fallback From
            var replyBox = msg.ReplyTo?.Mailboxes?.FirstOrDefault();
            var fromBox = msg.From?.Mailboxes?.FirstOrDefault();
            var mbox = replyBox ?? fromBox;
            string alias = mbox?.Address?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(alias)) return IngestOutcome.Failed;

            // ชื่อลูกค้า: เอาจาก From ก่อน (Agoda ใส่ชื่อจริง เช่น "PHENRATCHANEE PHENSUPA")
            // ส่วนชื่อบน Reply-To เป็น "Reply to XXX (do not edit)" — ตัด wrapper ทิ้งถ้าจำเป็น
            string guestName = (fromBox?.Name ?? mbox.Name ?? "").Trim();
            guestName = Regex.Replace(guestName, @"^\s*Reply\s+to\s+", "", RegexOptions.IgnoreCase);
            guestName = Regex.Replace(guestName, @"\s*\(do not edit\)\s*$", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(guestName)) guestName = alias.Split('@')[0];

            // dedup ด้วย Message-Id (ถ้าย้ายโฟลเดอร์ไม่สำเร็จ รอบหน้าอ่านซ้ำจะไม่ลงข้อความซ้ำ)
            string messageId = (msg.MessageId ?? "").Trim();
            if (messageId.Length > 0)
            {
                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ID FROM OmniChannel_Messages WHERE PlatformMessageId = @m",
                    new Dictionary<string, object> { { "@m", messageId } });
                if (dup?.Rows.Count > 0) return IngestOutcome.Duplicate;
            }

            string subject = (msg.Subject ?? "").Trim();
            string text = ExtractText(msg);
            var files = CollectAttachments(msg);
            if (string.IsNullOrWhiteSpace(text) && files.Count == 0) return IngestOutcome.Duplicate; // เมลเปล่า — ข้าม

            long reservationId = FindReservationId(subject + "\n" + text);

            var meta = new JavaScriptSerializer().Serialize(new Dictionary<string, object>
            {
                { "subject", subject },
                { "messageId", messageId },
                { "from", alias },
                { "reservationId", reservationId }
            });

            var result = _omni.ReceiveMessage(Channel, alias, guestName,
                string.IsNullOrWhiteSpace(text) ? "(ไฟล์แนบ)" : text,
                "TEXT", platformMessageId: messageId.Length > 0 ? messageId : null,
                metadata: meta, displayName: guestName);
            if (!result.Success) return IngestOutcome.Failed;

            // เติมข้อมูล contact/conversation: อีเมล + การจองที่จับคู่ได้ + หัวเรื่องแรก
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE OmniChannel_Contacts SET Email = @E, Updated_Date = GETDATE(),
                        Reservation_ID = COALESCE(Reservation_ID, NULLIF(@R, 0))
                      WHERE ID = @C",
                    new Dictionary<string, object> { { "@E", alias }, { "@R", reservationId }, { "@C", result.ContactID } });
                if (!string.IsNullOrWhiteSpace(subject))
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE OmniChannel_Conversations SET Subject = @S WHERE ID = @Id AND (Subject IS NULL OR Subject = '')",
                        new Dictionary<string, object> { { "@S", Truncate(subject, 300) }, { "@Id", result.ConversationID } });
                if (reservationId > 0)
                    _code.DatabaseInsertSafe(_conn,
                        "UPDATE OmniChannel_Conversations SET Tags = @T WHERE ID = @Id AND (Tags IS NULL OR Tags = '')",
                        new Dictionary<string, object> { { "@T", "จอง #" + reservationId }, { "@Id", result.ConversationID } });
            }
            catch { }

            // ไฟล์แนบ (รูป/เอกสาร) → ข้อความแยกในแชท
            foreach (var f in files)
            {
                try
                {
                    string url = SaveAttachment(result.ConversationID, f.Item1, f.Item2);
                    if (url != null)
                        _omni.ReceiveMessage(Channel, alias, guestName, f.Item1,
                            f.Item3 ? "IMAGE" : "FILE", mediaUrl: url, displayName: guestName);
                }
                catch (Exception ex) { _code.Logs(_conn, "EmailChat", $"attachment save failed: {ex.Message}", "SYSTEM"); }
            }

            _code.Logs(_conn, "EmailChat",
                $"รับข้อความจาก {guestName} <{alias}> conv={result.ConversationID}" +
                (reservationId > 0 ? $" จอง #{reservationId}" : "") + $" ({Truncate(text, 80)})", "SYSTEM");

            if (_notifyTelegram)
                Notify($"💬 ข้อความใหม่จากลูกค้า OTA\nจาก: {guestName}" +
                       (reservationId > 0 ? $" (จอง #{reservationId})" : "") +
                       $"\n{Truncate(text, 300)}\n\nตอบกลับที่: {BaseUrl}/Admin/Chat/OmniChannelInbox");

            return IngestOutcome.Received;
        }

        // ── ขาออก: พนักงานพิมพ์ในแชท → ส่งอีเมลไปที่ alias ของลูกค้า ────────────────

        /// <summary>
        /// ส่งข้อความแชทขาออกเป็นอีเมลถึง alias ของลูกค้า (เรียกจาก OmniChannelService.DeliverOutbound).
        /// ทำ thread ต่อจากอีเมลล่าสุดของลูกค้า (Re: หัวเรื่องเดิม + In-Reply-To) เพื่อให้ relay ของ
        /// OTA จับคู่การสนทนาถูกใบจอง. ผิดพลาด → mark ข้อความ FAILED + log (ไม่โยนต่อ)
        /// </summary>
        public void DeliverToEmail(long conversationId, string content)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_conn,
                    @"SELECT ci.PlatformUserId
                      FROM OmniChannel_Conversations c
                      JOIN OmniChannel_Contact_Identifiers ci ON ci.ContactID = c.ContactID AND ci.ChannelCode = c.ChannelCode
                      WHERE c.ID = @Id",
                    new Dictionary<string, object> { { "@Id", conversationId } });
                string alias = dt?.Rows.Count > 0 ? dt.Rows[0][0]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(alias) || !alias.Contains("@"))
                { MarkLastOutbound(conversationId, "FAILED"); _code.Logs(_conn, "EmailChat", $"conv {conversationId}: ไม่พบอีเมลลูกค้า", "SYSTEM"); return; }

                // หัวเรื่อง + Message-Id ล่าสุดฝั่งลูกค้า สำหรับต่อ thread
                string subject = null, inReplyTo = null;
                var last = _code.DatabaseQuerySafe(_conn,
                    @"SELECT TOP 1 PlatformMessageId, Metadata FROM OmniChannel_Messages
                      WHERE ConversationID = @Id AND Direction = 'IN' ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@Id", conversationId } });
                if (last?.Rows.Count > 0)
                {
                    inReplyTo = last.Rows[0]["PlatformMessageId"]?.ToString();
                    try
                    {
                        var meta = new JavaScriptSerializer()
                            .Deserialize<Dictionary<string, object>>(last.Rows[0]["Metadata"]?.ToString() ?? "{}");
                        if (meta != null && meta.ContainsKey("subject")) subject = meta["subject"]?.ToString();
                    }
                    catch { }
                }
                if (string.IsNullOrWhiteSpace(subject))
                {
                    var conv = _code.DatabaseQuerySafe(_conn,
                        "SELECT Subject FROM OmniChannel_Conversations WHERE ID = @Id",
                        new Dictionary<string, object> { { "@Id", conversationId } });
                    subject = conv?.Rows.Count > 0 ? conv.Rows[0][0]?.ToString() : null;
                }
                if (string.IsNullOrWhiteSpace(subject)) subject = "Message from the property";
                if (!subject.TrimStart().StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
                    subject = "Re: " + subject;

                string fromEmail = AppCfg.Get("Email_From");
                string password = AppCfg.Get("Email_Password_From");
                string smtpServer = AppCfg.Get("SMTP");
                if (string.IsNullOrWhiteSpace(fromEmail) || string.IsNullOrWhiteSpace(smtpServer))
                { MarkLastOutbound(conversationId, "FAILED"); _code.Logs(_conn, "EmailChat", "ยังไม่ได้ตั้งค่า SMTP/Email_From (ศูนย์ตั้งค่าระบบ)", "SYSTEM"); return; }
                int smtpPort = int.TryParse(AppCfg.Get("SMTP_Port"), out var sp) ? sp : 587;

                string body = content ?? "";
                if (!string.IsNullOrWhiteSpace(_signature)) body += "\r\n\r\n--\r\n" + _signature;

                string ourMessageId = $"<chat-{conversationId}-{Guid.NewGuid():N}@{fromEmail.Split('@').Last()}>";
                using (var mail = new MailMessage(fromEmail, alias))
                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = AppCfg.GetBool("SMTP_EnableSsl", true);
                    client.UseDefaultCredentials = false;
                    client.Credentials = new NetworkCredential(fromEmail, password);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = false;   // ข้อความล้วน — relay ของ OTA แสดงผลชัวร์สุด
                    mail.SubjectEncoding = System.Text.Encoding.UTF8;
                    mail.BodyEncoding = System.Text.Encoding.UTF8;
                    mail.Headers.Add("Message-ID", ourMessageId);
                    if (!string.IsNullOrWhiteSpace(inReplyTo))
                    {
                        string reply = inReplyTo.StartsWith("<") ? inReplyTo : "<" + inReplyTo + ">";
                        mail.Headers.Add("In-Reply-To", reply);
                        mail.Headers.Add("References", reply);
                    }
                    client.Send(mail);
                }

                // เก็บ Message-Id ของเราไว้ที่ข้อความขาออกล่าสุด — เผื่อ trace thread ภายหลัง
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE OmniChannel_Messages SET PlatformMessageId = @Mid, DeliveryStatus = 'SENT'
                      WHERE ID = (SELECT MAX(ID) FROM OmniChannel_Messages WHERE ConversationID = @Id AND Direction = 'OUT')",
                    new Dictionary<string, object> { { "@Mid", ourMessageId }, { "@Id", conversationId } });
            }
            catch (Exception ex)
            {
                MarkLastOutbound(conversationId, "FAILED");
                _code.Logs(_conn, "EmailChat", $"ส่งอีเมลตอบลูกค้าไม่สำเร็จ (conv {conversationId}): {ex.Message}", "SYSTEM");
            }
        }

        private void MarkLastOutbound(long conversationId, string status)
        {
            try
            {
                _code.DatabaseInsertSafe(_conn,
                    @"UPDATE OmniChannel_Messages SET DeliveryStatus = @S
                      WHERE ID = (SELECT MAX(ID) FROM OmniChannel_Messages WHERE ConversationID = @Id AND Direction = 'OUT')",
                    new Dictionary<string, object> { { "@S", status }, { "@Id", conversationId } });
            }
            catch { }
        }

        // ── helpers ──────────────────────────────────────────────────────────────

        /// <summary>ดึงเนื้อความจากอีเมล ตัดส่วน quote ของข้อความเก่า (On ... wrote:, > ..., ฯลฯ)</summary>
        private static string ExtractText(MimeMessage msg)
        {
            string text = msg.TextBody;
            if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(msg.HtmlBody))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(msg.HtmlBody);
                foreach (var n in doc.DocumentNode.SelectNodes("//style|//script")?.ToList() ?? new List<HtmlNode>())
                    n.Remove();
                text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? "");
            }
            if (string.IsNullOrWhiteSpace(text)) return "";

            var lines = text.Replace("\r\n", "\n").Split('\n');
            var keep = new List<string>();
            var cutPatterns = new[]
            {
                new Regex(@"^\s*-{2,}\s*Original Message", RegexOptions.IgnoreCase),
                new Regex(@"^\s*_{10,}\s*$"),
                new Regex(@"^\s*On .{0,120}wrote:\s*$", RegexOptions.IgnoreCase),
                new Regex(@"^\s*เมื่อ .{0,120}เขียนว่า:?\s*$"),
                new Regex(@"^\s*(From|จาก)\s*:\s*.+@.+$", RegexOptions.IgnoreCase),
                new Regex(@"^\s*>{1,}")
            };
            foreach (var raw in lines)
            {
                if (cutPatterns.Any(p => p.IsMatch(raw))) break;
                keep.Add(raw.TrimEnd());
            }
            string result = string.Join("\n", keep);
            result = Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
            if (result.Length > MaxBodyChars) result = result.Substring(0, MaxBodyChars) + " …";
            return result;
        }

        /// <summary>(ชื่อไฟล์, bytes, เป็นรูปไหม) — เฉพาะรูป/PDF ขนาดไม่เกินลิมิต</summary>
        private static List<Tuple<string, byte[], bool>> CollectAttachments(MimeMessage msg)
        {
            var list = new List<Tuple<string, byte[], bool>>();
            foreach (var att in msg.Attachments.OfType<MimePart>())
            {
                if (list.Count >= MaxAttachments) break;
                string name = att.FileName ?? "file";
                string mime = att.ContentType?.MimeType?.ToLowerInvariant() ?? "";
                bool isImage = mime.StartsWith("image/");
                if (!isImage && mime != "application/pdf") continue;
                using (var ms = new MemoryStream())
                {
                    att.Content.DecodeTo(ms);
                    if (ms.Length == 0 || ms.Length > MaxAttachmentBytes) continue;
                    list.Add(Tuple.Create(name, ms.ToArray(), isImage));
                }
            }
            return list;
        }

        private static string SaveAttachment(long conversationId, string fileName, byte[] bytes)
        {
            string root = HostingEnvironment.MapPath("~/Images/ChatFiles");
            if (root == null) return null;
            string dir = Path.Combine(root, conversationId.ToString(CultureInfo.InvariantCulture));
            Directory.CreateDirectory(dir);
            string safe = Regex.Replace(fileName ?? "file", @"[^\w\.\-ก-๙]", "_");
            if (safe.Length > 80) safe = safe.Substring(safe.Length - 80);
            string final = DateTime.Now.ToString("yyyyMMddHHmmssfff") + "_" + safe;
            File.WriteAllBytes(Path.Combine(dir, final), bytes);
            return $"/Images/ChatFiles/{conversationId}/{final}";
        }

        /// <summary>เดา Booking Id จากหัวเรื่อง/เนื้อความ แล้วจับคู่กับ Reservation.OTA_Booking_ID</summary>
        private long FindReservationId(string text)
        {
            try
            {
                // ตัวเลขที่มีป้ายกำกับชัดเจนมาก่อน (format จริงของ Agoda: "หมายเลขการจอง: 2038656748")
                var candidates = new List<string>();
                foreach (Match m in Regex.Matches(text ?? "",
                    @"(?:หมายเลขการจอง|Booking\s*(?:Id|Number)|การจอง)\s*[#:：]?\s*(\d{6,14})", RegexOptions.IgnoreCase))
                    candidates.Add(m.Groups[1].Value);
                foreach (Match m in Regex.Matches(text ?? "", @"\b(\d{7,12})\b"))
                    candidates.Add(m.Groups[1].Value);

                var seen = new HashSet<string>();
                foreach (string cand in candidates)
                {
                    if (!seen.Add(cand)) continue;
                    if (seen.Count > 8) break;
                    var dt = _code.DatabaseQuerySafe(_conn,
                        @"SELECT TOP 1 ID FROM [dbo].[Reservation]
                          WHERE OTA_Booking_ID LIKE @b ORDER BY ID DESC",
                        new Dictionary<string, object> { { "@b", "%" + cand + "%" } });
                    if (dt?.Rows.Count > 0) return Convert.ToInt64(dt.Rows[0][0]);
                }
            }
            catch { } // คอลัมน์ OTA ยังไม่มี (migration ยังไม่รัน) → ข้ามการจับคู่
            return 0;
        }

        private IMailFolder GetOrCreateFolder(ImapClient client, string name)
        {
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            try { return personal.GetSubfolder(name); }
            catch (FolderNotFoundException) { return personal.Create(name, true); }
        }

        private string BaseUrl
        {
            get
            {
                try
                {
                    var req = System.Web.HttpContext.Current?.Request;
                    if (req != null) return $"{req.Url.Scheme}://{req.Url.Authority}";
                }
                catch { }
                return "https://taketimebangphra.com";
            }
        }

        private void Notify(string text)
        {
            try
            {
                string token = AppCfg.Get("TelegramTokenTakeTime");
                if (string.IsNullOrEmpty(token)) return;
                var bot = new TelegramBot2(token);
                bot.SendMessageAsync(AppCfg.Get("TelegramChatId", "-4969611371"), text).GetAwaiter().GetResult();
            }
            catch { }
        }

        private static string Truncate(string s, int len) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= len ? s : s.Substring(0, len) + "…");
    }
}
