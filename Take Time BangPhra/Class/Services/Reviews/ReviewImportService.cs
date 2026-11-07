// ===========================================================================
// ReviewImportService.cs — นำเข้ารีวิวจากไฟล์ CSV (export จาก extranet) และป้อนทีละรีวิว
//
// โปรไฟล์หัวคอลัมน์:
//   BOOKING  — ไฟล์ export "Guest reviews" ของ Booking.com extranet (สเกล 10, Positive/Negative review แยกช่อง)
//   AGODA    — export จาก YCS (สเกล 10)
//   GENERIC  — แม่แบบของระบบ (ดาวน์โหลดได้จากหน้า แหล่งรีวิว) — มีคอลัมน์ platform / rating_scale เอง
// จับหัวคอลัมน์แบบยืดหยุ่น (ไม่สนตัวพิมพ์/ช่องว่าง/ขีด) เพราะชื่อคอลัมน์จริงของแต่ละ extranet เปลี่ยนได้
// PDPA: ไม่เก็บเลขการจอง/อีเมล/เบอร์ — เลขการจองใช้ทำรหัสกันซ้ำแบบแฮชเท่านั้น (bkres:…)
// ===========================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class ReviewImportResult
    {
        public int Rows, Inserted, Updated, Unchanged, Skipped;
        public List<string> Errors = new List<string>();
        public List<long> InsertedIds = new List<long>();
        public string Message
        {
            get
            {
                return "อ่าน " + Rows + " แถว: เพิ่มใหม่ " + Inserted + ", อัปเดต " + Updated + ", ซ้ำ " + Unchanged
                     + (Skipped > 0 ? ", ข้าม " + Skipped : "")
                     + (Errors.Count > 0 ? " — ปัญหา: " + string.Join("; ", Errors.Take(5)) : "");
            }
        }
    }

    public class ReviewImportService
    {
        private readonly string _conn;
        public ReviewImportService(string connectionString) { _conn = connectionString; }

        public const string GenericTemplateHeader =
            "platform,review_id,review_date,reviewer_name,rating,rating_scale,title,text,positive,negative,owner_response,owner_response_date,stay_date,room_type,traveller_type,language,review_url";

        private static readonly Dictionary<string, string[]> Aliases = new Dictionary<string, string[]>
        {
            { "id",       new[] { "reviewid", "id", "reviewnumber", "reviewno" } },
            { "resno",    new[] { "reservationnumber", "bookingnumber", "bookingid", "reservationid", "confirmationnumber", "bookingreference" } },
            { "platform", new[] { "platform", "source", "channel", "site", "แพลตฟอร์ม" } },
            { "date",     new[] { "reviewdate", "date", "datesubmitted", "submitted", "createdat", "created", "publisheddate", "reviewedon", "วันที่รีวิว", "วันที่" } },
            { "name",     new[] { "guestname", "reviewername", "reviewer", "name", "author", "guest", "ชื่อผู้เข้าพัก", "ชื่อ" } },
            { "rating",   new[] { "reviewscore", "overallscore", "score", "rating", "overallrating", "overall", "totalscore", "คะแนน", "คะแนนรวม" } },
            { "scale",    new[] { "ratingscale", "scale", "maxscore", "outof" } },
            { "title",    new[] { "reviewtitle", "title", "headline", "หัวข้อ" } },
            { "text",     new[] { "reviewtext", "review", "comment", "comments", "text", "body", "content", "ความคิดเห็น", "รีวิว" } },
            { "positive", new[] { "positivereview", "positive", "liked", "pros", "whatdidyoulike", "ข้อดี" } },
            { "negative", new[] { "negativereview", "negative", "disliked", "cons", "whatdidntyoulike", "ข้อเสีย" } },
            { "reply",    new[] { "propertyreply", "propertyresponse", "ownerresponse", "hotelresponse", "reply", "response", "managementresponse", "คำตอบ", "คำตอบของโรงแรม" } },
            { "replydate",new[] { "ownerresponsedate", "replydate", "responsedate", "propertyreplydate" } },
            { "stay",     new[] { "staydate", "checkin", "checkindate", "arrival", "arrivaldate", "stayedon", "วันเข้าพัก" } },
            { "room",     new[] { "roomtype", "room", "roomname", "ประเภทห้อง" } },
            { "traveller",new[] { "travellertype", "travelertype", "triptype", "guesttype", "travelpurpose", "ประเภทผู้เข้าพัก" } },
            { "language", new[] { "language", "lang", "reviewlanguage", "ภาษา" } },
            { "url",      new[] { "reviewurl", "url", "link" } }
        };

        private static string NormHeader(string h)
        {
            if (h == null) return "";
            var sb = new StringBuilder();
            foreach (char c in h.Trim().ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>นำเข้า CSV — sourceCode = แหล่งที่จะผูก (เช่น BOOKING), profile = BOOKING|AGODA|GENERIC</summary>
        public ReviewImportResult ImportCsv(string sourceCode, string profile, Stream input, decimal? defaultScale, int maxRows = 5000)
        {
            var res = new ReviewImportResult();
            var src = ReviewSourceConfig.Load(_conn, sourceCode);
            if (src == null) throw new ArgumentException("ไม่พบแหล่งรีวิว " + sourceCode);
            profile = (profile ?? "GENERIC").ToUpperInvariant();

            string content;
            using (var rd = new StreamReader(input, Encoding.UTF8, true)) content = rd.ReadToEnd();
            var rows = ParseCsv(content);
            if (rows.Count < 2) throw new ArgumentException("ไฟล์ไม่มีข้อมูล (ต้องมีหัวคอลัมน์ + อย่างน้อย 1 แถว)");

            var header = rows[0].Select(NormHeader).ToList();
            var map = new Dictionary<string, int>();
            foreach (var kv in Aliases)
                foreach (var alias in kv.Value)
                {
                    int idx = header.IndexOf(alias);
                    if (idx >= 0) { map[kv.Key] = idx; break; }
                }
            if (!map.ContainsKey("rating") && !map.ContainsKey("text") && !map.ContainsKey("positive"))
                throw new ArgumentException("ไม่พบคอลัมน์คะแนนหรือข้อความรีวิว — หัวคอลัมน์ที่อ่านได้: " + string.Join(", ", rows[0].Take(20)));

            decimal profileScale = profile == "BOOKING" || profile == "AGODA" ? 10m
                : (defaultScale ?? ReviewPlatforms.DefaultScale(src.SourceCode));
            var store = new ReviewStore(_conn);

            for (int i = 1; i < rows.Count && res.Rows < maxRows; i++)
            {
                var row = rows[i];
                if (row.All(string.IsNullOrWhiteSpace)) continue;
                res.Rows++;
                try
                {
                    Func<string, string> col = k =>
                    {
                        int idx;
                        if (!map.TryGetValue(k, out idx) || idx >= row.Count) return null;
                        string v = row[idx];
                        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
                    };

                    string text = col("text");
                    string pos = col("positive"), neg = col("negative");
                    if (pos != null || neg != null)
                    {
                        var sb = new StringBuilder();
                        if (!string.IsNullOrEmpty(text)) sb.Append(text);
                        if (pos != null && !IsNothing(pos)) { if (sb.Length > 0) sb.Append('\n'); sb.Append("ข้อดี: ").Append(pos); }
                        if (neg != null && !IsNothing(neg)) { if (sb.Length > 0) sb.Append('\n'); sb.Append("ข้อเสีย: ").Append(neg); }
                        text = sb.ToString();
                    }

                    decimal? raw = ReviewRating.Parse(col("rating"));
                    decimal? scale = ReviewRating.Parse(col("scale")) ?? (raw.HasValue && raw.Value > profileScale ? ReviewRating.GuessScale(raw, src.SourceCode) : profileScale);

                    string origin = ReviewPlatforms.NormalizeOrigin(col("platform"));
                    string extId = col("id");
                    if (extId == null && col("resno") != null)
                        extId = ReviewText.StableId("bkres:", col("resno"));   // เลขจองไม่เก็บตรง ๆ (PDPA)

                    if (raw == null && string.IsNullOrWhiteSpace(text) && col("title") == null) { res.Skipped++; continue; }

                    var item = new ReviewItem
                    {
                        ExternalReviewId = extId,
                        OriginPlatform = origin,
                        ReviewerName = col("name"),
                        RatingRaw = raw,
                        RatingScale = raw.HasValue ? scale : null,
                        Title = col("title"),
                        Text = text,
                        ReviewDate = ReviewText.ParseDate(col("date")),
                        OwnerResponse = col("reply"),
                        OwnerResponseDate = ReviewText.ParseDate(col("replydate")),
                        StayDate = ReviewText.ParseDate(col("stay")),
                        RoomType = col("room"),
                        TravellerType = col("traveller"),
                        Language = col("language"),
                        ReviewUrl = col("url"),
                        IsProvisional = false
                    };
                    var u = store.Upsert(src, item, IngestMethods.Csv);
                    Count(res, u);
                }
                catch (Exception ex)
                {
                    res.Skipped++;
                    if (res.Errors.Count < 20) res.Errors.Add("แถว " + (i + 1) + ": " + ex.Message);
                }
            }
            store.RefreshTotals(src.SourceCode);
            return res;
        }

        private static bool IsNothing(string s)
        {
            string t = s.Trim().ToLowerInvariant();
            return t == "-" || t == "n/a" || t == "none" || t == "nothing" || t == "ไม่มี";
        }

        private static void Count(ReviewImportResult res, UpsertResult u)
        {
            switch (u.Outcome)
            {
                case UpsertOutcome.Inserted: res.Inserted++; res.InsertedIds.Add(u.ReviewId); break;
                case UpsertOutcome.Updated: res.Updated++; break;
                case UpsertOutcome.Unchanged: res.Unchanged++; break;
                default: res.Skipped++; break;
            }
        }

        /// <summary>ป้อนรีวิวเองทีละรายการ (วางจากหน้า extranet / Facebook / Airbnb)</summary>
        public UpsertResult AddManual(string sourceCode, ReviewItem item)
        {
            var src = ReviewSourceConfig.Load(_conn, sourceCode);
            if (src == null) throw new ArgumentException("ไม่พบแหล่งรีวิว " + sourceCode);
            if (item == null) throw new ArgumentException("ไม่มีข้อมูลรีวิว");
            if (!item.RatingRaw.HasValue && string.IsNullOrWhiteSpace(item.Text) && string.IsNullOrWhiteSpace(item.Title))
                throw new ArgumentException("ต้องมีคะแนนหรือข้อความรีวิวอย่างน้อยหนึ่งอย่าง");
            if (item.RatingRaw.HasValue && !item.RatingScale.HasValue)
                item.RatingScale = ReviewRating.GuessScale(item.RatingRaw, src.SourceCode);
            if (item.RatingRaw.HasValue && !ReviewRating.IsValid(item.RatingRaw, item.RatingScale))
                throw new ArgumentException("คะแนนต้องมากกว่า 0 และไม่เกินคะแนนเต็ม (" + item.RatingScale + ")");
            item.IsProvisional = false;
            var store = new ReviewStore(_conn);
            var u = store.Upsert(src, item, IngestMethods.Manual);
            store.RefreshTotals(src.SourceCode);
            return u;
        }

        // ── CSV parser (RFC 4180 + เดาตัวคั่น , ; tab) ──────────────────────────────

        public static List<List<string>> ParseCsv(string content)
        {
            var rows = new List<List<string>>();
            if (string.IsNullOrEmpty(content)) return rows;
            if (content[0] == '﻿') content = content.Substring(1);
            char delim = DetectDelimiter(content);

            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else field.Append(c);
                }
                else if (c == '"') inQuotes = true;
                else if (c == delim) { row.Add(field.ToString()); field.Clear(); }
                else if (c == '\r') { }
                else if (c == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
                else field.Append(c);
            }
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
            return rows;
        }

        private static char DetectDelimiter(string content)
        {
            int nl = content.IndexOf('\n');
            string first = nl > 0 ? content.Substring(0, nl) : content;
            int commas = first.Count(c => c == ','), semis = first.Count(c => c == ';'), tabs = first.Count(c => c == '\t');
            if (tabs > commas && tabs > semis) return '\t';
            if (semis > commas) return ';';
            return ',';
        }
    }
}
