using System;
using System.Collections.Generic;
using System.Data;
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
    /// อ่านอีเมลตอบกลับจาก "กรมสรรพากร / ผู้ให้บริการ e-Tax" แล้วมาร์คใบกำกับว่า
    /// **นำส่งสรรพากรสำเร็จ** (PHASE18_28)
    ///
    /// เคสจริง: ลูกค้าขอ e-Tax → ระบบสร้าง+นำส่ง → กรมฯ/ผู้ให้บริการตอบกลับทางอีเมลว่ารับเอกสารแล้ว
    /// เดิมต้องเปิดอีเมลอ่านเอง ไม่มีที่ไหนในระบบบอกว่าใบไหนผ่านจริง
    ///
    /// ทำงาน: ใช้กล่องอีเมลเดียวกับระบบอ่านอีเมลจอง (Email_Rsv_*) → ค้นอีเมลยังไม่อ่าน
    /// จากโดเมนที่ตั้งไว้ → หาเลขเอกสาร/เลขใบเสร็จในหัวเรื่อง+เนื้อความ → จับคู่กับ
    /// Accounting_ETax_Log → เขียน Rd_Confirmed_Date
    ///
    /// ปลอดภัย: มาร์คเฉพาะเมื่อ "เจอเลขเอกสารที่ตรงกับใบที่มี e-Tax จริง" และ
    /// "มีคำที่บ่งชี้ความสำเร็จ" — อีเมลแจ้งข้อผิดพลาดจะไม่ถูกนับเป็นสำเร็จ
    /// </summary>
    public class EtaxRdConfirmService
    {
        private readonly string _conn;
        private readonly code _code = new code();

        private readonly bool _enabled;
        private readonly string[] _fromContains;
        private readonly string[] _successWords;
        private readonly string _processedLabel;

        private readonly string _imapServer, _imapUser, _imapPassword;
        private readonly int _imapPort;

        public EtaxRdConfirmService(string connectionString)
        {
            _conn = connectionString;

            _enabled = Cfg("Etax_Rd_Watch_Enabled", "0") == "1";
            _fromContains = Split(Cfg("Etax_Rd_FromContains", "rd.go.th, etax, teda.th"));
            _successWords = Split(Cfg("Etax_Rd_SuccessWords",
                "สำเร็จ, สมบูรณ์, ได้รับเอกสาร, นำส่งเรียบร้อย, success, accepted, completed"));
            _processedLabel = Cfg("Etax_Rd_ProcessedLabel", "RD-Processed");

            _imapServer = Cfg("Email_Rsv_ImapServer", "imap.gmail.com");
            _imapPort = int.TryParse(Cfg("Email_Rsv_ImapPort", "993"), out var p) ? p : 993;
            _imapUser = Cfg("Email_Rsv_Username", "");
            _imapPassword = _code.Derypt(Cfg("Email_Rsv_Password_Encrypted", ""));
        }

        public static bool IsEnabled(string conn)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = 'Etax_Rd_Watch_Enabled'", null);
                return dt?.Rows.Count > 0 && dt.Rows[0][0]?.ToString() == "1";
            }
            catch { return false; }
        }

        // ── เรียกจาก background timer ───────────────────────────────────────────
        private static DateTime _lastPoll = DateTime.MinValue;
        private static readonly object _lock = new object();

        public static void PollIfDue(string conn, int everyMinutes = 10)
        {
            if (!IsEnabled(conn)) return;
            lock (_lock)
            {
                if ((DateTime.Now - _lastPoll).TotalMinutes < everyMinutes) return;
                _lastPoll = DateTime.Now;
            }
            try { new EtaxRdConfirmService(conn).Poll(); } catch { }
        }

        public class RdPollResult
        {
            public int Fetched, Confirmed, Unmatched, Failed;
            public string Error;
            public List<string> Messages = new List<string>();
        }

        /// <summary>อ่านอีเมลตอบกลับแล้วมาร์คใบที่นำส่งสำเร็จ — เรียกซ้ำได้ (idempotent)</summary>
        public RdPollResult Poll()
        {
            var res = new RdPollResult();
            if (!_enabled) { res.Error = "ยังไม่ได้เปิดการอ่านอีเมลตอบกลับ e-Tax"; return res; }
            if (string.IsNullOrWhiteSpace(_imapUser) || string.IsNullOrWhiteSpace(_imapPassword))
            { res.Error = "ยังไม่ได้ตั้งค่าอีเมล IMAP (ใช้ค่าเดียวกับระบบอ่านอีเมลจอง)"; return res; }
            if (_fromContains.Length == 0) { res.Error = "ยังไม่ได้ตั้งโดเมนผู้ส่ง (Etax_Rd_FromContains)"; return res; }

            try
            {
                using (var client = new ImapClient())
                {
                    client.Connect(_imapServer, _imapPort, true);
                    client.Authenticate(_imapUser, _imapPassword);
                    var inbox = client.Inbox;
                    inbox.Open(FolderAccess.ReadWrite);

                    IMailFolder processed = null;
                    try { processed = GetOrCreateFolder(client, _processedLabel); } catch { }

                    SearchQuery q = SearchQuery.FromContains(_fromContains[0]);
                    for (int i = 1; i < _fromContains.Length; i++)
                        q = SearchQuery.Or(q, SearchQuery.FromContains(_fromContains[i]));
                    var uids = inbox.Search(SearchQuery.And(q, SearchQuery.NotSeen));
                    res.Fetched = uids.Count;

                    foreach (var uid in uids)
                    {
                        bool handled = false;
                        try
                        {
                            var msg = inbox.GetMessage(uid);
                            int n = Ingest(msg, res);
                            handled = n >= 0;
                        }
                        catch (Exception ex)
                        {
                            res.Failed++;
                            res.Messages.Add("[error] " + ex.Message);
                            Log("อ่านอีเมลตอบกลับล้มเหลว: " + ex.Message);
                        }

                        try
                        {
                            if (handled && processed != null) inbox.MoveTo(uid, processed);
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
                Log("IMAP error: " + ex.Message);
            }
            return res;
        }

        /// <summary>คืนจำนวนใบที่มาร์คได้ (0 = ไม่เจอเลขที่ตรง, -1 = ไม่ใช่อีเมลแจ้งสำเร็จ)</summary>
        private int Ingest(MimeMessage msg, RdPollResult res)
        {
            string subject = msg.Subject ?? "";
            string body = PlainText(msg);
            string all = subject + "\n" + body;
            string msgId = (msg.MessageId ?? "").Trim();

            // ต้องมีคำที่บ่งชี้ "สำเร็จ" — อีเมลแจ้ง error/ปฏิเสธจะไม่ถูกนับ
            bool success = _successWords.Any(w =>
                all.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!success)
            {
                res.Messages.Add("[ข้าม] ไม่พบคำยืนยันความสำเร็จ: " + Truncate(subject, 60));
                return -1;
            }

            // ประมวลผลไปแล้ว (อีเมลเดิมถูกอ่านซ้ำ) → ไม่ทำซ้ำ
            if (!string.IsNullOrEmpty(msgId))
            {
                var dup = _code.DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 1 FROM Accounting_ETax_Log WHERE Rd_Confirm_MsgId = @m",
                    new Dictionary<string, object> { { "@m", msgId } });
                if (dup?.Rows.Count > 0) return 0;
            }

            // หาเลขเอกสารในอีเมล แล้วจับคู่กับใบที่มี e-Tax จริง
            int marked = 0;
            foreach (string token in ExtractDocTokens(all))
            {
                int n = _code.DatabaseInsertSafe(_conn,
                    @"UPDATE Accounting_ETax_Log
                         SET Rd_Confirmed_Date = GETDATE(),
                             Rd_Confirm_Ref = @ref,
                             Rd_Confirm_MsgId = @mid,
                             Status = CASE WHEN ISNULL(Status,'') IN ('','PENDING','SENT','SUBMITTED')
                                           THEN 'RD_CONFIRMED' ELSE Status END
                       WHERE Nexaacc_Etax_Id IS NOT NULL
                         AND Rd_Confirmed_Date IS NULL
                         AND (Document_Number = @t OR Receipt_Number = @t OR Etax_Ref_Number = @t)",
                    new Dictionary<string, object>
                    {
                        { "@ref", Truncate(subject, 200) },
                        { "@mid", string.IsNullOrEmpty(msgId) ? (object)DBNull.Value : msgId },
                        { "@t", token }
                    });
                if (n > 0)
                {
                    marked += n;
                    res.Confirmed += n;
                    Log($"สรรพากรยืนยันรับเอกสาร: {token} (จากอีเมล \"{Truncate(subject, 80)}\")");
                    NotifyStaff($"✅ กรมสรรพากรรับเอกสาร e-Tax แล้ว\nเลขที่: {token}\n{Truncate(subject, 120)}");
                }
            }

            if (marked == 0)
            {
                res.Unmatched++;
                res.Messages.Add("[ไม่พบใบที่ตรง] " + Truncate(subject, 70));
                Log("อีเมลแจ้งสำเร็จแต่จับคู่เลขเอกสารไม่ได้: " + Truncate(subject, 120));
            }
            return marked;
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        /// <summary>ดึงเลขเอกสารที่เป็นไปได้: REC-xxx / INV-xxx / เลขล้วน 6-20 หลัก</summary>
        private static List<string> ExtractDocTokens(string text)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(text)) return list;

            foreach (Match m in Regex.Matches(text, @"\b([A-Z]{2,6}-[A-Z0-9\-]{3,24})\b", RegexOptions.IgnoreCase))
                Add(list, m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text,
                @"(?:เลขที่|เอกสาร|ใบกำกับ|document|invoice|no\.?)\s*[:：]?\s*([A-Z0-9\-\/]{5,24})",
                RegexOptions.IgnoreCase))
                Add(list, m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"\b(\d{6,20})\b"))
                Add(list, m.Groups[1].Value);
            return list;
        }

        private static void Add(List<string> list, string v)
        {
            v = (v ?? "").Trim().Trim('.', ',', ')', '(');
            if (v.Length >= 5 && list.Count < 12 &&
                !list.Any(x => string.Equals(x, v, StringComparison.OrdinalIgnoreCase)))
                list.Add(v);
        }

        private static string PlainText(MimeMessage msg)
        {
            string t = msg.TextBody;
            if (string.IsNullOrWhiteSpace(t) && !string.IsNullOrWhiteSpace(msg.HtmlBody))
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(msg.HtmlBody);
                t = System.Net.WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? "");
            }
            return t ?? "";
        }

        private IMailFolder GetOrCreateFolder(ImapClient client, string name)
        {
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            try { return personal.GetSubfolder(name); }
            catch (FolderNotFoundException) { return personal.Create(name, true); }
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
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            catch { }
            return def;
        }

        private static string[] Split(string s) =>
            (s ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(x => x.Trim()).Where(x => x.Length > 1).ToArray();

        private static string Truncate(string s, int n) =>
            string.IsNullOrEmpty(s) ? "" : (s.Length <= n ? s : s.Substring(0, n) + "…");

        private void Log(string m)
        {
            try { _code.Logs(_conn, "EtaxRdConfirm", m, "SYSTEM"); } catch { }
        }

        private void NotifyStaff(string text)
        {
            try
            {
                string token = AppCfg.Get("TelegramTokenTakeTime");
                if (string.IsNullOrEmpty(token)) return;
                new TelegramBot2(token)
                    .SendMessageAsync(AppCfg.Get("TelegramChatId", "-4969611371"), text)
                    .GetAwaiter().GetResult();
            }
            catch { }
        }
    }
}
