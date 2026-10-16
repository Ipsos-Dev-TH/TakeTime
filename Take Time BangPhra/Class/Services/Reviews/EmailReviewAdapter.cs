// ===========================================================================
// EmailReviewAdapter.cs — อ่านอีเมลแจ้ง "มีรีวิวใหม่" จาก OTA (Booking.com / Agoda / Expedia / Trip.com / ...)
//
// ใช้กล่องเมลเดียวกับการอ่านอีเมลจอง (Email_Rsv_* ใน Accounting_Integration_Config) ผ่าน MailKit IMAP
// รูปแบบเดียวกับ EmailReservationService: ค้นหา (ผู้ส่ง + หัวเรื่อง) → แกะ → ย้ายเข้า label ของตัวเอง
//   • ไม่แตะอีเมล STAAH (ผู้ส่งไม่ตรง) และไม่แตะอีเมลแชทลูกค้า (excludeSenders: guest.booking.com ฯลฯ)
//   • อีเมลที่ตัวอ่านแชทย้ายเข้า Chat-Processed ไปก่อน — อ่านแบบ read-only จาก extraFolders (ไม่ย้าย)
//   • แกะแบบระมัดระวัง: เอาเท่าที่เจอชัด ๆ (คะแนน x/10, ชื่อ, ข้อความ, ลิงก์) — ไม่เจอ = NULL ไม่เดา
//   • ทุกแถวเป็น Is_Provisional = 1 (สัญญาณ "มีรีวิวใหม่") — เมื่อได้ข้อมูลจริงจาก CSV/API จะถูกเลื่อนขั้นแทนแถวใหม่
// config (ApiConfig ของแหล่ง): senders, excludeSenders, subjectContains, excludeSubjects, extraFolders,
//         processedLabel (Review-Processed), lookbackDays (14), maxPerRun (50), moveProcessed (true),
//         scoreRegex (กลุ่ม 1 = คะแนน, กลุ่ม 2 = สเกล ถ้ามี)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class EmailReviewAdapter : IReviewSourceAdapter
    {
        private readonly string _conn;
        private const int ImapTimeoutMs = 120000;

        public EmailReviewAdapter(string connectionString)
        {
            _conn = connectionString;
        }

        public string Key { get { return IngestMethods.Email; } }
        public bool SupportsAutomatic { get { return true; } }

        /// <summary>ค่าตั้งต้นต่อแพลตฟอร์ม: ผู้ส่ง | ผู้ส่งที่ต้องข้าม | คำในหัวเรื่อง</summary>
        public static readonly Dictionary<string, string[]> Defaults = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "BOOKING",     new[] { "booking.com", "mchat.booking.com,guest.booking.com", "review,รีวิว,ความคิดเห็น" } },
            { "AGODA",       new[] { "agoda.com", "agoda-messaging.com", "review,รีวิว" } },
            { "EXPEDIA",     new[] { "expediagroup.com,expedia.com", "", "review,รีวิว" } },
            { "TRIPCOM",     new[] { "trip.com,ctrip.com", "", "review,点评,รีวิว" } },
            { "TRAVELOKA",   new[] { "traveloka.com", "", "review,รีวิว,ulasan" } },
            { "TRIPADVISOR", new[] { "tripadvisor.com", "", "review,รีวิว" } },
            { "GOOGLE",      new[] { "google.com", "", "review,รีวิว" } },
            { "AIRBNB",      new[] { "airbnb.com", "", "review,รีวิว" } }
        };

        /// <summary>หัวเรื่องที่มีคำว่า review แต่ไม่ใช่รีวิวจากแขก (เช่น ขอให้ที่พักรีวิวแขก)</summary>
        private const string DefaultExcludeSubjects =
            "review your guest,rate your guest,write a review,leave a review,ให้คะแนนผู้เข้าพัก,รีวิวผู้เข้าพัก,review reminder";

        private static string[] Split(string s)
        {
            return (s ?? "").Split(new[] { ',', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private string RsvCfg(string key, string def)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT TOP 1 ConfigValue FROM Accounting_Integration_Config WHERE ConfigKey = @k",
                    new Dictionary<string, object> { { "@k", key } });
                if (dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value && dt.Rows[0][0].ToString().Length > 0)
                    return dt.Rows[0][0].ToString();
            }
            catch { }
            return def;
        }

        public ReviewFetchResult Fetch(ReviewSourceRow src)
        {
            string[] d;
            Defaults.TryGetValue(src.SourceCode ?? "", out d);
            string[] senders = Split(src.Cfg("senders", d != null ? d[0] : ""));
            string[] excludeSenders = Split(src.Cfg("excludeSenders", d != null ? d[1] : ""));
            string[] subjects = Split(src.Cfg("subjectContains", d != null ? d[2] : "review,รีวิว"));
            string[] excludeSubjects = Split(src.Cfg("excludeSubjects", DefaultExcludeSubjects));
            string[] extraFolders = Split(src.Cfg("extraFolders", "Chat-Processed"));
            string processedLabel = src.Cfg("processedLabel", "Review-Processed");
            int lookback = Math.Max(1, Math.Min(90, src.CfgInt("lookbackDays", 14)));
            int maxPerRun = Math.Max(1, Math.Min(500, src.CfgInt("maxPerRun", 50)));
            bool move = src.CfgBool("moveProcessed", true);
            if (senders.Length == 0)
                throw new ReviewSourceSkippedException("ยังไม่ได้ตั้งผู้ส่งอีเมล (senders) ของแหล่ง " + src.SourceCode);

            string server = RsvCfg("Email_Rsv_ImapServer", "imap.gmail.com");
            int port;
            if (!int.TryParse(RsvCfg("Email_Rsv_ImapPort", "993"), out port)) port = 993;
            string user = RsvCfg("Email_Rsv_Username", "");
            string pass = "";
            try { pass = new code().Derypt(RsvCfg("Email_Rsv_Password_Encrypted", "")); } catch { }
            if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
                throw new ReviewSourceSkippedException("ยังไม่ได้ตั้งกล่องเมล (Email_Rsv_Username / รหัสผ่าน) ที่หน้า อ่านอีเมลจอง");

            var result = new ReviewFetchResult();
            int processed = 0, matched = 0;

            using (var client = new ImapClient())
            {
                client.Timeout = ImapTimeoutMs;
                client.Connect(server, port, true);
                client.Authenticate(user, pass);

                IMailFolder doneFolder = null;
                if (move)
                {
                    try { doneFolder = GetOrCreateFolder(client, processedLabel); }
                    catch (Exception ex) { result.Warnings.Add("สร้าง label " + processedLabel + " ไม่ได้: " + ex.Message); }
                }

                SearchQuery fromQ = SearchQuery.FromContains(senders[0]);
                for (int i = 1; i < senders.Length; i++) fromQ = SearchQuery.Or(fromQ, SearchQuery.FromContains(senders[i]));
                var query = SearchQuery.And(fromQ, SearchQuery.DeliveredAfter(DateTime.Now.AddDays(-lookback).Date));

                var folders = new List<KeyValuePair<IMailFolder, bool>> { new KeyValuePair<IMailFolder, bool>(client.Inbox, true) };
                foreach (var name in extraFolders)
                {
                    if (string.Equals(name, processedLabel, StringComparison.OrdinalIgnoreCase)) continue;
                    try
                    {
                        var f = client.GetFolder(name);
                        if (f != null) folders.Add(new KeyValuePair<IMailFolder, bool>(f, false));
                    }
                    catch (FolderNotFoundException) { }
                    catch (Exception ex) { result.Warnings.Add("เปิดโฟลเดอร์ " + name + " ไม่ได้: " + ex.Message); }
                }

                foreach (var pair in folders)
                {
                    if (processed >= maxPerRun) break;
                    var folder = pair.Key;
                    bool canMove = pair.Value && doneFolder != null;
                    try { folder.Open(canMove ? FolderAccess.ReadWrite : FolderAccess.ReadOnly); }
                    catch (Exception ex) { result.Warnings.Add("เปิด " + folder.FullName + " ไม่ได้: " + ex.Message); continue; }

                    IList<UniqueId> uids = folder.Search(query);
                    if (uids.Count == 0) continue;
                    var toMove = new List<UniqueId>();
                    foreach (var sum in folder.Fetch(uids, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId))
                    {
                        if (processed >= maxPerRun) break;
                        var env = sum.Envelope;
                        if (env == null) continue;
                        string subject = env.Subject ?? "";
                        string from = string.Join(",", env.From.Mailboxes.Select(m => (m.Address ?? "").ToLowerInvariant()));
                        if (excludeSenders.Any(x => from.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                        if (!subjects.Any(x => subject.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                        if (excludeSubjects.Any(x => subject.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                        matched++;
                        try
                        {
                            var msg = folder.GetMessage(sum.UniqueId);
                            var item = Parse(src, msg, env.MessageId);
                            if (item != null) result.Items.Add(item);
                            processed++;
                            if (canMove) toMove.Add(sum.UniqueId);
                        }
                        catch (Exception ex)
                        {
                            result.Warnings.Add("แกะอีเมล '" + ReviewText.Clip(subject, 60) + "' ไม่ได้: " + ex.Message);
                        }
                    }
                    if (toMove.Count > 0)
                    {
                        try { folder.MoveTo(toMove, doneFolder); }
                        catch (Exception ex) { result.Warnings.Add("ย้ายอีเมลเข้า " + processedLabel + " ไม่ได้: " + ex.Message); }
                    }
                }
                client.Disconnect(true);
            }
            result.Note = "อีเมลที่ตรงเงื่อนไข " + matched + " ฉบับ, แกะแล้ว " + processed;
            return result;
        }

        private static IMailFolder GetOrCreateFolder(ImapClient client, string name)
        {
            var personal = client.GetFolder(client.PersonalNamespaces[0]);
            try { return personal.GetSubfolder(name); }
            catch (FolderNotFoundException) { return personal.Create(name, true); }
        }

        // ── แกะเนื้ออีเมล ──────────────────────────────────────────────────────

        private static readonly Regex LabelScore = new Regex(
            @"(?i)(?:review score|overall score|guest score|guest rating|overall rating|score|rating|คะแนนรีวิว|คะแนนรวม|คะแนน|评分|总评)\s*[:：]?\s*(\d{1,2}(?:[.,]\d{1,2})?)(?:\s*(?:/|out of|จาก|เต็ม)\s*(10|5|100)\b)?",
            RegexOptions.Compiled);
        private static readonly Regex SlashScore = new Regex(@"(?<![\d.,])(\d{1,2}(?:[.,]\d{1,2})?)\s*/\s*(10|5)\b", RegexOptions.Compiled);
        private static readonly Regex StarScore = new Regex(@"(?i)\b([1-5])\s*-?\s*(?:star|stars|ดาว)\b", RegexOptions.Compiled);
        private static readonly Regex NameRx = new Regex(
            @"(?im)^\s*(?:guest name|guest|reviewer|reviewed by|written by|review by|ชื่อผู้เข้าพัก|ชื่อแขก|ผู้รีวิว)\s*[:：]\s*(.{2,60}?)\s*$",
            RegexOptions.Compiled);
        private static readonly Regex TitleRx = new Regex(@"(?im)^\s*(?:review title|title|หัวข้อรีวิว|หัวข้อ)\s*[:：]\s*(.{2,200}?)\s*$", RegexOptions.Compiled);
        private static readonly Regex TextRx = new Regex(
            @"(?is)(?:^|\n)\s*(?:guest review|guest comment|comments?|review text|review|what the guest said|ความคิดเห็น|ข้อความรีวิว|รีวิว)\s*[:：]\s*(.{5,3000}?)(?:\n\s*\n|$)",
            RegexOptions.Compiled);
        private static readonly Regex LikedRx = new Regex(
            @"(?is)(?:^|\n)\s*(?:liked|positive|what they liked|ชอบ|ข้อดี)\s*[:：]\s*(.{2,2000}?)(?=\n\s*\n|\n\s*(?:disliked|negative|what they didn.t like|ไม่ชอบ|ข้อเสีย)\s*[:：]|$)",
            RegexOptions.Compiled);
        private static readonly Regex DislikedRx = new Regex(
            @"(?is)(?:^|\n)\s*(?:disliked|negative|what they didn.t like|ไม่ชอบ|ข้อเสีย)\s*[:：]\s*(.{2,2000}?)(?=\n\s*\n|$)",
            RegexOptions.Compiled);
        private static readonly Regex HrefRx = new Regex(@"href\s*=\s*[""']([^""']+)[""']", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex ReviewIdRx = new Regex(@"(?i)review[_-]?id[=:/\s#]*([A-Za-z0-9_-]{4,64})", RegexOptions.Compiled);

        public static ReviewItem Parse(ReviewSourceRow src, MimeMessage msg, string messageId)
        {
            string html = msg.HtmlBody;
            string text = msg.TextBody;
            if (string.IsNullOrWhiteSpace(text)) text = ReviewText.HtmlToText(html);
            text = (text ?? "").Replace("\r\n", "\n");
            if (text.Length > 30000) text = text.Substring(0, 30000);

            var item = new ReviewItem
            {
                IsProvisional = true,
                ReviewDate = msg.Date != DateTimeOffset.MinValue ? msg.Date.LocalDateTime : DateTime.Now,
                OriginPlatform = src.OriginPlatform
            };

            // คะแนน
            decimal? raw = null, scale = null;
            string custom = src.Cfg("scoreRegex");
            Match m = null;
            if (!string.IsNullOrEmpty(custom))
            {
                try { m = Regex.Match(text, custom, RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2)); } catch { m = null; }
                if (m != null && !m.Success) m = null;
            }
            if (m == null) { var mm = LabelScore.Match(text); if (mm.Success) m = mm; }
            if (m == null) { var mm = SlashScore.Match(text); if (mm.Success) m = mm; }
            if (m != null)
            {
                raw = ReviewRating.Parse(m.Groups[1].Value);
                if (m.Groups.Count > 2 && m.Groups[2].Success) scale = ReviewRating.Parse(m.Groups[2].Value);
            }
            else
            {
                var sm = StarScore.Match(text);
                if (sm.Success) { raw = ReviewRating.Parse(sm.Groups[1].Value); scale = 5m; }
            }
            if (raw.HasValue && !scale.HasValue) scale = ReviewRating.GuessScale(raw, src.OriginPlatform);
            if (ReviewRating.IsValid(raw, scale)) { item.RatingRaw = raw; item.RatingScale = scale; }

            var nm = NameRx.Match(text);
            if (nm.Success) item.ReviewerName = nm.Groups[1].Value.Trim();
            var tm = TitleRx.Match(text);
            if (tm.Success) item.Title = tm.Groups[1].Value.Trim();

            var liked = LikedRx.Match(text);
            var disliked = DislikedRx.Match(text);
            if (liked.Success || disliked.Success)
            {
                var sb = new StringBuilder();
                if (liked.Success) sb.Append("ข้อดี: ").Append(liked.Groups[1].Value.Trim());
                if (disliked.Success) { if (sb.Length > 0) sb.Append('\n'); sb.Append("ข้อเสีย: ").Append(disliked.Groups[1].Value.Trim()); }
                item.Text = sb.ToString();
            }
            else
            {
                var xm = TextRx.Match(text);
                if (xm.Success) item.Text = xm.Groups[1].Value.Trim();
            }

            // ลิงก์รีวิว (ตัด query ที่อาจมี session/token ของ extranet ออก)
            if (!string.IsNullOrEmpty(html))
                foreach (Match h in HrefRx.Matches(html))
                {
                    string u = System.Net.WebUtility.HtmlDecode(h.Groups[1].Value);
                    if (u.IndexOf("review", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string clean = CleanUrl(u);
                    if (clean == null) continue;
                    item.ReviewUrl = clean;
                    var rid = ReviewIdRx.Match(u);
                    if (rid.Success) item.ExternalReviewId = "rv:" + rid.Groups[1].Value;
                    break;
                }

            if (string.IsNullOrEmpty(item.ExternalReviewId))
                item.ExternalReviewId = ReviewText.StableId("email:", messageId ?? (msg.Subject + "|" + item.ReviewDate));

            if (string.IsNullOrEmpty(item.Text) && string.IsNullOrEmpty(item.Title))
                item.Title = ReviewText.Clip("[แจ้งเตือนรีวิวใหม่] " + (msg.Subject ?? ""), 300);
            return item;
        }

        private static string CleanUrl(string u)
        {
            Uri uri;
            if (!Uri.TryCreate(u, UriKind.Absolute, out uri)) return null;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return null;
            var keep = new List<string>();
            string q = uri.Query.TrimStart('?');
            foreach (var part in q.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string k = part.Split('=')[0].ToLowerInvariant();
                if (k.Contains("review") || k == "hotel_id" || k == "hotelid" || k == "property_id" || k == "lang")
                    keep.Add(part);
            }
            string s = uri.GetLeftPart(UriPartial.Path) + (keep.Count > 0 ? "?" + string.Join("&", keep) : "");
            return s.Length > 1000 ? s.Substring(0, 1000) : s;
        }
    }
}
