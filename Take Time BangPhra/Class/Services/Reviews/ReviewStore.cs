// ===========================================================================
// ReviewStore.cs — บันทึกรีวิวลง AI_Online_Reviews แบบ upsert + สถานะแหล่ง + Review_Fetch_Log
//
// ลำดับการหาแถวเดิม:
//   1) (Origin_Platform, External_Review_Id)            — รหัสจริงของแพลตฟอร์ม (unique index)
//   2) Content_Hash                                      — แพลตฟอร์ม|ชื่อ|วันที่|ข้อความ (ไม่มีรหัส)
//   3) แถวชั่วคราว (Is_Provisional = 1, เช่นจากอีเมล) ที่วันใกล้กัน ±2 วัน + คะแนน/ชื่อตรงกัน
//      — เมื่อได้ข้อมูลจริง (API/CSV) จะ "เลื่อนขั้น" แถวเดิมแทนการเพิ่มแถวใหม่
// ไม่แก้คอลัมน์ผล AI (Sentiment/Topics/...) — ถ้าข้อความเปลี่ยนจะตั้ง IsAnalyzed = 0 ให้วิเคราะห์ใหม่
// PDPA: ไม่เก็บรูป/ลิงก์โปรไฟล์ผู้รีวิว (ReviewerAvatar = NULL เสมอ), ชื่อตามที่แสดงเท่านั้น
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace Take_Time_BangPhra.Services.Reviews
{
    public class ReviewStore
    {
        private readonly string _conn;

        public ReviewStore(string connectionString)
        {
            _conn = connectionString;
        }

        // ── Upsert ────────────────────────────────────────────────────────────

        private class Existing
        {
            public long Id;
            public string ExternalId;
            public bool Provisional;
            public decimal? RatingRaw, RatingScale;
            public string Name, Title, Text, OwnerResponse, Url, RoomType, Traveller, Language;
            public DateTime? OwnerResponseDate, StayDate;
        }

        private const string SelectCols = @"ID, External_Review_Id, Is_Provisional, Rating_Raw, Rating_Scale, ReviewerName,
            ReviewTitle, ReviewText, Owner_Response, Owner_Response_Date, Review_Url, Stay_Date, Room_Type, Traveller_Type, Language";

        public UpsertResult Upsert(ReviewSourceRow src, ReviewItem it, string ingestMethod)
        {
            if (src == null) throw new ArgumentNullException("src");
            if (it == null) return new UpsertResult { Outcome = UpsertOutcome.Skipped };

            string origin = !string.IsNullOrWhiteSpace(it.OriginPlatform)
                ? ReviewPlatforms.NormalizeOrigin(it.OriginPlatform) : src.OriginPlatform;
            origin = ReviewPlatforms.Truncate(origin, 50);

            decimal? scale = it.RatingScale;
            if (!scale.HasValue && it.RatingRaw.HasValue) scale = ReviewRating.GuessScale(it.RatingRaw, origin);
            decimal? raw = ReviewRating.IsValid(it.RatingRaw, scale) ? it.RatingRaw : null;
            if (!raw.HasValue) scale = null;
            decimal? r10 = ReviewRating.To10(raw, scale);
            decimal? r5 = ReviewRating.To5(raw, scale);

            string name = ReviewText.CleanName(it.ReviewerName);
            string title = ReviewText.Clip(it.Title, 300);
            string text = ReviewText.Clip(it.Text, 20000);
            if (string.IsNullOrWhiteSpace(title)) title = null;
            if (string.IsNullOrWhiteSpace(text)) text = null;
            DateTime? date = ReviewText.SaneDate(it.ReviewDate);
            string extId = ReviewText.Clip(it.ExternalReviewId, 200);
            if (string.IsNullOrWhiteSpace(extId)) extId = null;
            string url = ReviewText.Clip(it.ReviewUrl, 1000);
            string resp = ReviewText.Clip(it.OwnerResponse, 20000);
            if (string.IsNullOrWhiteSpace(resp)) resp = null;
            string room = ReviewText.Clip(it.RoomType, 200);
            string trav = ReviewText.Clip(it.TravellerType, 100);
            string lang = ReviewText.Clip(it.Language, 10);
            DateTime? stay = it.StayDate.HasValue ? (DateTime?)it.StayDate.Value.Date : null;
            DateTime? respDate = ReviewText.SaneDate(it.OwnerResponseDate);
            // วันที่โดยประมาณ ("4 เดือนที่แล้ว") เลื่อนทุกวัน ⇒ ไม่ใช้ในแฮชกันซ้ำ
            string hash = ReviewText.ContentHash(origin, name, it.ApproxDate ? null : date, title, text, raw);

            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                Existing ex = null;
                if (extId != null)
                    ex = FindOne(con, "SELECT TOP 1 " + SelectCols + " FROM AI_Online_Reviews WHERE Origin_Platform = @o AND External_Review_Id = @e",
                        P("@o", origin), P("@e", extId));
                if (ex == null)
                    ex = FindOne(con, "SELECT TOP 1 " + SelectCols + " FROM AI_Online_Reviews WHERE Content_Hash = @h AND Origin_Platform = @o ORDER BY ID",
                        P("@h", hash), P("@o", origin));
                // วันที่โดยประมาณ: ลองจับแถวเดิมของรีวิวเดียวกันด้วยชื่อ + ข้อความตรงกัน (เช่นแถวจาก Places API ที่แฮชรวมวันที่จริง)
                if (ex == null && it.ApproxDate && name != null && text != null && text.Length <= 4000)
                    ex = FindOne(con, "SELECT TOP 1 " + SelectCols + @" FROM AI_Online_Reviews
                          WHERE Origin_Platform = @o AND ReviewerName = @n AND ReviewText = @x ORDER BY ID",
                        P("@o", origin), P("@n", name), P("@x", text));
                if (ex == null && !it.IsProvisional && !it.ApproxDate && date.HasValue && (r5.HasValue || name != null))
                    ex = FindOne(con, "SELECT TOP 1 " + SelectCols + @" FROM AI_Online_Reviews
                          WHERE Origin_Platform = @o AND Is_Provisional = 1 AND Is_Excluded = 0
                            AND ReviewDate >= DATEADD(DAY, -2, @d) AND ReviewDate < DATEADD(DAY, 3, @d)
                            AND ( (@r5 IS NOT NULL AND Rating_5 IS NOT NULL AND ABS(Rating_5 - @r5) < 0.06)
                               OR (@n IS NOT NULL AND ReviewerName IS NOT NULL AND ReviewerName = @n) )
                            AND (@r5 IS NULL OR Rating_5 IS NULL OR ABS(Rating_5 - @r5) < 0.06)
                            AND (@n IS NULL OR ReviewerName IS NULL OR ReviewerName = @n)
                          ORDER BY ABS(DATEDIFF(MINUTE, ReviewDate, @d))",
                        P("@o", origin), P("@d", date.Value.Date), P("@r5", r5), P("@n", name));

                if (ex == null)
                {
                    try
                    {
                        long id = Insert(con, src, ingestMethod, origin, extId, name, raw, scale, r10, r5, title, text,
                            date ?? DateTime.Now, lang, url, resp, respDate, stay, room, trav, hash, it.IsProvisional, it.MachineTranslated, it.Meta);
                        return new UpsertResult { Outcome = UpsertOutcome.Inserted, ReviewId = id, Rating5 = r5 };
                    }
                    catch (SqlException sx) when (sx.Number == 2601 || sx.Number == 2627)
                    {
                        // ชนกับรอบอื่นที่เพิ่งเพิ่มแถวเดียวกัน (unique index) — ถือว่ามีอยู่แล้ว
                        return new UpsertResult { Outcome = UpsertOutcome.Unchanged };
                    }
                }

                // เพิ่มอย่างเดียว (AGODA_LIGHT): แถวเดิมที่ไม่ใช่ชั่วคราวคงไว้ตามเดิม
                // ข้อยกเว้นครั้งเดียว: แถวเดิมเก็บคำแปล (เช่น en) แล้วรอบนี้ได้ต้นฉบับ (เช่น th) ของรีวิวรหัสเดียวกัน ⇒ แทนข้อความ/หัวข้อ/ภาษา
                if (it.InsertOnly && !ex.Provisional)
                {
                    string exLang = !string.IsNullOrWhiteSpace(ex.Language) ? ex.Language.Trim().ToLowerInvariant() : AgodaPageParser.DetectLang(ex.Text);
                    bool upgrade = it.IsOriginalText && it.MachineTranslated != true && lang != null && text != null
                                && extId != null && string.Equals(ex.ExternalId, extId, StringComparison.Ordinal)
                                && !string.Equals(exLang, lang, StringComparison.OrdinalIgnoreCase)
                                && !string.Equals(text, ex.Text, StringComparison.Ordinal);
                    if (!upgrade)
                    {
                        // ข้อยกเว้น: คำตอบของเจ้าของบนแพลตฟอร์มเพิ่งมี/เปลี่ยน (GOOGLE_TRAVEL_LIGHT) — วันที่คำตอบตั้งเมื่อข้อความเปลี่ยนเท่านั้น
                        bool respUpdate = it.AllowOwnerResponseUpdate && resp != null && !string.Equals(resp, ex.OwnerResponse, StringComparison.Ordinal);
                        if (!respUpdate) return new UpsertResult { Outcome = UpsertOutcome.Unchanged, ReviewId = ex.Id };
                        using (var cmd = new SqlCommand(@"
UPDATE AI_Online_Reviews SET
    Owner_Response = @resp, Owner_Response_Date = COALESCE(@respDate, GETDATE()), ResponseStatus = 'RESPONDED',
    ActualResponse = CASE WHEN ActualResponse IS NULL THEN @resp ELSE ActualResponse END,
    RespondedDate = CASE WHEN RespondedDate IS NULL THEN COALESCE(@respDate, GETDATE()) ELSE RespondedDate END,
    Fetched_At = GETDATE(), Updated_At = GETDATE()
WHERE ID = @id", con))
                        {
                            cmd.CommandTimeout = 30;
                            Add(cmd, "@resp", resp); Add(cmd, "@respDate", respDate); Add(cmd, "@id", ex.Id);
                            cmd.ExecuteNonQuery();
                        }
                        return new UpsertResult { Outcome = UpsertOutcome.Updated, ReviewId = ex.Id, Rating5 = r5 };
                    }
                    using (var cmd = new SqlCommand(@"
UPDATE AI_Online_Reviews SET
    ReviewTitle = COALESCE(@t, ReviewTitle), ReviewText = @x, Language = @lang, IsAnalyzed = 0,
    Content_Hash = @h, Fetched_At = GETDATE(), Updated_At = GETDATE()" + (HasMachineTranslatedColumn() ? ", Is_Machine_Translated = 0" : "") + @"
WHERE ID = @id", con))
                    {
                        cmd.CommandTimeout = 30;
                        Add(cmd, "@t", title); Add(cmd, "@x", text); Add(cmd, "@lang", lang); Add(cmd, "@h", hash); Add(cmd, "@id", ex.Id);
                        cmd.ExecuteNonQuery();
                    }
                    return new UpsertResult { Outcome = UpsertOutcome.Updated, ReviewId = ex.Id, Rating5 = r5 };
                }

                // ── อัปเดตเฉพาะเมื่อมีอะไรใหม่จริง ──
                bool adoptExt = extId != null && (ex.ExternalId == null || (ex.Provisional && ex.ExternalId != extId));
                bool promote = ex.Provisional && !it.IsProvisional;
                bool ratingChanged = raw.HasValue && (ex.RatingRaw != raw || ex.RatingScale != scale);
                bool textChanged = text != null && !string.Equals(text, ex.Text, StringComparison.Ordinal);
                bool titleChanged = title != null && !string.Equals(title, ex.Title, StringComparison.Ordinal);
                bool respChanged = resp != null && !string.Equals(resp, ex.OwnerResponse, StringComparison.Ordinal);
                bool other = (url != null && ex.Url == null) || (stay.HasValue && !ex.StayDate.HasValue)
                          || (room != null && ex.RoomType == null) || (trav != null && ex.Traveller == null)
                          || (lang != null && ex.Language == null) || (name != null && ex.Name == null)
                          || (respDate.HasValue && ex.OwnerResponseDate != respDate);

                if (!(adoptExt || promote || ratingChanged || textChanged || titleChanged || respChanged || other))
                    return new UpsertResult { Outcome = UpsertOutcome.Unchanged, ReviewId = ex.Id };

                using (var cmd = new SqlCommand(@"
UPDATE AI_Online_Reviews SET
    External_Review_Id = CASE WHEN @adopt = 1 THEN @e ELSE External_Review_Id END,
    PlatformReviewId   = CASE WHEN @adopt = 1 THEN @e ELSE PlatformReviewId END,
    Is_Provisional     = CASE WHEN @prov = 0 THEN 0 ELSE Is_Provisional END,
    Ingest_Method      = CASE WHEN @promote = 1 THEN @m ELSE Ingest_Method END,
    ReviewerName       = COALESCE(@n, ReviewerName),
    Rating_Raw         = COALESCE(@raw, Rating_Raw),
    Rating_Scale       = CASE WHEN @raw IS NOT NULL THEN @scale ELSE Rating_Scale END,
    Rating_10          = CASE WHEN @raw IS NOT NULL THEN @r10 ELSE Rating_10 END,
    Rating_5           = CASE WHEN @raw IS NOT NULL THEN @r5 ELSE Rating_5 END,
    Rating             = CASE WHEN @raw IS NOT NULL THEN CAST(@r5 AS FLOAT) ELSE Rating END,
    ReviewTitle        = COALESCE(@t, ReviewTitle),
    ReviewText         = COALESCE(@x, ReviewText),
    IsAnalyzed         = CASE WHEN @textChanged = 1 THEN 0 ELSE IsAnalyzed END,
    Review_Url         = COALESCE(Review_Url, @u),
    Owner_Response     = COALESCE(@resp, Owner_Response),
    Owner_Response_Date= COALESCE(@respDate, Owner_Response_Date),
    ResponseStatus     = CASE WHEN @resp IS NOT NULL THEN 'RESPONDED' ELSE ResponseStatus END,
    ActualResponse     = CASE WHEN @resp IS NOT NULL AND ActualResponse IS NULL THEN @resp ELSE ActualResponse END,
    RespondedDate      = CASE WHEN @resp IS NOT NULL AND RespondedDate IS NULL THEN COALESCE(@respDate, GETDATE()) ELSE RespondedDate END,
    Stay_Date          = COALESCE(Stay_Date, @stay),
    Room_Type          = COALESCE(Room_Type, @room),
    Traveller_Type     = COALESCE(Traveller_Type, @trav),
    Language           = COALESCE(Language, @lang),
    Content_Hash       = CASE WHEN @x IS NOT NULL OR Content_Hash IS NULL THEN @h ELSE Content_Hash END,
    Fetched_At         = GETDATE(),
    Updated_At         = GETDATE()
WHERE ID = @id", con))
                {
                    cmd.CommandTimeout = 30;
                    Add(cmd, "@adopt", adoptExt ? 1 : 0); Add(cmd, "@e", extId);
                    Add(cmd, "@prov", it.IsProvisional ? 1 : 0); Add(cmd, "@promote", promote ? 1 : 0);
                    Add(cmd, "@m", ingestMethod); Add(cmd, "@n", name);
                    Add(cmd, "@raw", raw); Add(cmd, "@scale", scale); Add(cmd, "@r10", r10); Add(cmd, "@r5", r5);
                    Add(cmd, "@t", title); Add(cmd, "@x", text); Add(cmd, "@textChanged", textChanged ? 1 : 0);
                    Add(cmd, "@u", url); Add(cmd, "@resp", resp); Add(cmd, "@respDate", respDate);
                    Add(cmd, "@stay", stay); Add(cmd, "@room", room); Add(cmd, "@trav", trav); Add(cmd, "@lang", lang);
                    Add(cmd, "@h", hash); Add(cmd, "@id", ex.Id);
                    try { cmd.ExecuteNonQuery(); }
                    catch (SqlException sx) when (sx.Number == 2601 || sx.Number == 2627)
                    {
                        return new UpsertResult { Outcome = UpsertOutcome.Unchanged, ReviewId = ex.Id };
                    }
                }
                return new UpsertResult { Outcome = UpsertOutcome.Updated, ReviewId = ex.Id, Rating5 = r5 };
            }
        }

        private long Insert(SqlConnection con, ReviewSourceRow src, string method, string origin, string extId, string name,
            decimal? raw, decimal? scale, decimal? r10, decimal? r5, string title, string text, DateTime reviewDate,
            string lang, string url, string resp, DateTime? respDate, DateTime? stay, string room, string trav,
            string hash, bool provisional, bool? machineTranslated = null, Dictionary<string, object> meta = null)
        {
            bool mtCol = machineTranslated.HasValue && HasMachineTranslatedColumn();
            bool metaCol = meta != null && meta.Count > 0 && HasColumn("Platform_Meta");
            string metaJson = null;
            if (metaCol)
            {
                try { metaJson = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(meta); }
                catch { metaCol = false; }
                if (metaJson != null && metaJson.Length > 8000) metaJson = null;
                if (metaJson == null) metaCol = false;
            }
            using (var cmd = new SqlCommand(@"
INSERT INTO AI_Online_Reviews
    (SourceCode, PlatformReviewId, ReviewerName, ReviewerAvatar, Rating, ReviewTitle, ReviewText, ReviewDate, Language,
     ResponseStatus, ActualResponse, RespondedDate, IsAnalyzed, IsFlagged, Created_Date,
     Origin_Platform, External_Review_Id, Rating_Raw, Rating_Scale, Rating_10, Rating_5, Review_Url,
     Owner_Response, Owner_Response_Date, Stay_Date, Room_Type, Traveller_Type, Ingest_Method, Content_Hash,
     Is_Provisional, Is_Excluded, Fetched_At, Updated_At" + (mtCol ? ", Is_Machine_Translated" : "") + (metaCol ? ", Platform_Meta" : "") + @")
VALUES
    (@src, @e, @n, NULL, @rating, @t, @x, @d, @lang,
     CASE WHEN @resp IS NULL THEN 'PENDING' ELSE 'RESPONDED' END, @resp,
     CASE WHEN @resp IS NULL THEN NULL ELSE COALESCE(@respDate, GETDATE()) END, 0, 0, GETDATE(),
     @o, @e, @raw, @scale, @r10, @r5, @u,
     @resp, @respDate, @stay, @room, @trav, @m, @h,
     @prov, 0, GETDATE(), GETDATE()" + (mtCol ? ", @mt" : "") + (metaCol ? ", @meta" : "") + @");
SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", con))
            {
                if (mtCol) Add(cmd, "@mt", machineTranslated.Value ? 1 : 0);
                if (metaCol) Add(cmd, "@meta", metaJson);
                cmd.CommandTimeout = 30;
                Add(cmd, "@src", src.SourceCode); Add(cmd, "@e", extId); Add(cmd, "@n", name);
                Add(cmd, "@rating", r5.HasValue ? (object)(double)r5.Value : null);
                Add(cmd, "@t", title); Add(cmd, "@x", text); Add(cmd, "@d", reviewDate); Add(cmd, "@lang", lang);
                Add(cmd, "@resp", resp); Add(cmd, "@respDate", respDate);
                Add(cmd, "@o", origin); Add(cmd, "@raw", raw); Add(cmd, "@scale", scale); Add(cmd, "@r10", r10); Add(cmd, "@r5", r5);
                Add(cmd, "@u", url); Add(cmd, "@stay", stay); Add(cmd, "@room", room); Add(cmd, "@trav", trav);
                Add(cmd, "@m", method); Add(cmd, "@h", hash); Add(cmd, "@prov", provisional ? 1 : 0);
                object o = cmd.ExecuteScalar();
                return o == null || o == DBNull.Value ? 0 : Convert.ToInt64(o);
            }
        }

        // ── คอลัมน์เสริมของ AI_Online_Reviews (ยังไม่รัน migration = ข้าม) cache 5 นาที ──
        //   Is_Machine_Translated (PHASE19_27), Platform_Meta (PHASE19_28)
        private static readonly Dictionary<string, KeyValuePair<bool, DateTime>> _cols = new Dictionary<string, KeyValuePair<bool, DateTime>>();
        private static readonly object _colsLock = new object();

        private bool HasMachineTranslatedColumn() { return HasColumn("Is_Machine_Translated"); }

        private bool HasColumn(string column)
        {
            lock (_colsLock)
            {
                KeyValuePair<bool, DateTime> c;
                if (_cols.TryGetValue(column, out c) && (DateTime.Now - c.Value).TotalMinutes < 5) return c.Key;
            }
            bool ok = false;
            try
            {
                var dt = new code().DatabaseQuerySafe(_conn,
                    "SELECT CASE WHEN COL_LENGTH('dbo.AI_Online_Reviews', @c) IS NULL THEN 0 ELSE 1 END",
                    new Dictionary<string, object> { { "@c", column } });
                ok = dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 1;
            }
            catch { ok = false; }
            lock (_colsLock) { _cols[column] = new KeyValuePair<bool, DateTime>(ok, DateTime.Now); }
            return ok;
        }

        private static Existing FindOne(SqlConnection con, string sql, params SqlParameter[] ps)
        {
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.CommandTimeout = 30;
                foreach (var p in ps) cmd.Parameters.Add(p);
                using (var rd = cmd.ExecuteReader())
                {
                    if (!rd.Read()) return null;
                    return new Existing
                    {
                        Id = Convert.ToInt64(rd["ID"]),
                        ExternalId = S(rd["External_Review_Id"]),
                        Provisional = rd["Is_Provisional"] != DBNull.Value && Convert.ToBoolean(rd["Is_Provisional"]),
                        RatingRaw = D(rd["Rating_Raw"]),
                        RatingScale = D(rd["Rating_Scale"]),
                        Name = S(rd["ReviewerName"]),
                        Title = S(rd["ReviewTitle"]),
                        Text = S(rd["ReviewText"]),
                        OwnerResponse = S(rd["Owner_Response"]),
                        OwnerResponseDate = rd["Owner_Response_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["Owner_Response_Date"]),
                        Url = S(rd["Review_Url"]),
                        StayDate = rd["Stay_Date"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(rd["Stay_Date"]),
                        RoomType = S(rd["Room_Type"]),
                        Traveller = S(rd["Traveller_Type"]),
                        Language = S(rd["Language"])
                    };
                }
            }
        }

        private static string S(object o) { return o == null || o == DBNull.Value ? null : o.ToString(); }
        private static decimal? D(object o) { return o == null || o == DBNull.Value ? (decimal?)null : Convert.ToDecimal(o); }

        private static SqlParameter P(string name, object value)
        {
            return new SqlParameter(name, value ?? DBNull.Value);
        }

        private static void Add(SqlCommand cmd, string name, object value)
        {
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        // ── lease ต่อแหล่ง (กันสอง worker ดึงแหล่งเดียวกันพร้อมกัน) ─────────────────

        /// <summary>
        /// ขอ lease ของแหล่ง: UPDATE…WHERE Lease_Until IS NULL OR &lt; GETDATE() แล้วอ่าน @@ROWCOUNT ทันที
        /// </summary>
        public bool TryLease(int sourceId, int minutes, string owner)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
SET NOCOUNT ON;
DECLARE @got INT = 0;
UPDATE AI_Review_Sources
   SET Lease_Until = DATEADD(MINUTE, @m, GETDATE()), Lease_Owner = @o
 WHERE ID = @id AND (Lease_Until IS NULL OR Lease_Until < GETDATE());
SET @got = @@ROWCOUNT;
SELECT @got;", con))
                {
                    cmd.CommandTimeout = 15;
                    cmd.Parameters.AddWithValue("@m", Math.Max(1, minutes));
                    cmd.Parameters.AddWithValue("@o", ReviewPlatforms.Truncate(owner ?? "", 100));
                    cmd.Parameters.AddWithValue("@id", sourceId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
        }

        public void ReleaseLease(int sourceId, string owner)
        {
            Exec(@"UPDATE AI_Review_Sources SET Lease_Until = NULL, Lease_Owner = NULL
                    WHERE ID = @id AND (Lease_Owner = @o OR Lease_Owner IS NULL)",
                new Dictionary<string, object> { { "@id", sourceId }, { "@o", ReviewPlatforms.Truncate(owner ?? "", 100) } });
        }

        // ── สถานะแหล่งหลังดึง ─────────────────────────────────────────────────

        public void MarkSuccess(ReviewSourceRow src, ReviewFetchResult fr, string owner, string warning)
        {
            Exec(@"
UPDATE AI_Review_Sources SET
    Last_Success_At = GETDATE(), LastFetchDate = GETDATE(),
    Last_Error = @warn, Consecutive_Failures = 0,
    Next_Fetch_At = DATEADD(MINUTE, @interval, GETDATE()),
    Platform_Rating = COALESCE(@pr, Platform_Rating),
    Platform_Rating_Scale = CASE WHEN @pr IS NOT NULL THEN COALESCE(@ps, Platform_Rating_Scale) ELSE Platform_Rating_Scale END,
    Platform_Review_Count = COALESCE(@pc, Platform_Review_Count),
    TotalReviews = (SELECT COUNT(*) FROM AI_Online_Reviews WHERE SourceCode = @code AND Is_Excluded = 0),
    Lease_Until = NULL, Lease_Owner = NULL, Updated_Date = GETDATE()
WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@warn", string.IsNullOrEmpty(warning) ? null : ReviewText.Clip(warning, 1000) },
                    { "@interval", Math.Max(15, src.FetchIntervalMin) },
                    { "@pr", fr != null ? fr.PlatformRating : null },
                    { "@ps", fr != null ? fr.PlatformRatingScale : null },
                    { "@pc", fr != null ? fr.PlatformReviewCount : null },
                    { "@code", src.SourceCode }, { "@id", src.Id }
                });
        }

        /// <summary>บันทึกความล้มเหลว + เลื่อนรอบถัดไปแบบ exponential backoff (15 นาที × 2^(n-1))</summary>
        /// <param name="minDelayMin">รอบถัดไปไม่เร็วกว่านี้ (นาที) — เช่น AGODA_LIGHT ห้าม retry ถี่</param>
        /// <returns>จำนวนครั้งที่ล้มเหลวติดกันหลังบันทึก</returns>
        public int MarkFailure(ReviewSourceRow src, string error, int minDelayMin = 0)
        {
            int n = src.ConsecutiveFailures + 1;
            int cap = Math.Max(360, src.FetchIntervalMin);
            double backoff = 15 * Math.Pow(2, Math.Min(n - 1, 10));
            int delay = Math.Max((int)Math.Min(backoff, cap), minDelayMin);
            Exec(@"
UPDATE AI_Review_Sources SET
    Last_Error = @err, Consecutive_Failures = Consecutive_Failures + 1, LastFetchDate = GETDATE(),
    Next_Fetch_At = DATEADD(MINUTE, @delay, GETDATE()),
    Lease_Until = NULL, Lease_Owner = NULL, Updated_Date = GETDATE()
WHERE ID = @id",
                new Dictionary<string, object> { { "@err", ReviewText.Clip(error, 1000) }, { "@delay", delay }, { "@id", src.Id } });
            return n;
        }

        /// <summary>
        /// จำนวนรอบล่าสุดที่ "โดนกันบอท" ติดกัน (อ่านจาก Review_Fetch_Log — ไม่ต้องมีคอลัมน์ใหม่;
        /// ข้ามแถว SKIPPED/RUNNING เพื่อไม่ให้การกดซ้ำที่ถูกพักไว้ตัดลำดับ)
        /// </summary>
        public int CountConsecutiveBlocked(int sourceId, int max)
        {
            try
            {
                var dt = new code().DatabaseQuerySafe(_conn,
                    @"SELECT TOP (@n) Status, Message FROM Review_Fetch_Log
                       WHERE Source_ID = @s AND Status NOT IN ('SKIPPED','RUNNING')
                       ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@n", Math.Max(1, max) * 4 }, { "@s", sourceId } });
                int n = 0;
                foreach (DataRow r in dt.Rows)
                {
                    string msg = r["Message"] == DBNull.Value ? "" : r["Message"].ToString();
                    if (msg.StartsWith(ParseMissPrefix, StringComparison.Ordinal)) continue;   // แกะไม่ได้ ≠ โดนกัน: ไม่นับ และไม่ตัดลำดับ
                    if (!msg.StartsWith(BlockedPrefix, StringComparison.Ordinal)) break;
                    n++;
                    if (n >= max) break;
                }
                return n;
            }
            catch { return 0; }
        }

        /// <summary>คำนำหน้าข้อความ log ของรอบที่โดนกันบอท (ใช้นับลำดับติดกัน)</summary>
        public const string BlockedPrefix = "[BLOCKED]";
        /// <summary>คำนำหน้าข้อความ log ของรอบที่หน้าโหลดได้แต่แกะรีวิวไม่ได้ (ไม่นับเป็นการโดนกัน)</summary>
        public const string ParseMissPrefix = LightFetch.ParseMissPrefix;

        /// <summary>ปิดดึงอัตโนมัติของแหล่ง — true เมื่อเพิ่งปิดจริงในครั้งนี้ (ใช้กันแจ้งเตือนซ้ำ)</summary>
        public bool DisableAuto(int sourceId, string reason)
        {
            using (var con = new SqlConnection(_conn))
            {
                con.Open();
                using (var cmd = new SqlCommand(@"
SET NOCOUNT ON;
DECLARE @n INT = 0;
UPDATE AI_Review_Sources SET Is_Enabled = 0, Last_Error = @r, Updated_Date = GETDATE()
 WHERE ID = @id AND Is_Enabled = 1;
SET @n = @@ROWCOUNT;
IF @n = 0 UPDATE AI_Review_Sources SET Last_Error = @r WHERE ID = @id;
SELECT @n;", con))
                {
                    cmd.CommandTimeout = 15;
                    cmd.Parameters.AddWithValue("@r", ReviewText.Clip(reason ?? "", 1000));
                    cmd.Parameters.AddWithValue("@id", sourceId);
                    return Convert.ToInt32(cmd.ExecuteScalar()) == 1;
                }
            }
        }

        public void MarkSkipped(ReviewSourceRow src, string message)
        {
            Exec(@"
UPDATE AI_Review_Sources SET
    Last_Error = @err, Next_Fetch_At = DATEADD(MINUTE, @interval, GETDATE()),
    Lease_Until = NULL, Lease_Owner = NULL, Updated_Date = GETDATE()
WHERE ID = @id",
                new Dictionary<string, object>
                {
                    { "@err", ReviewText.Clip("ข้าม: " + message, 1000) },
                    { "@interval", Math.Max(15, src.FetchIntervalMin) }, { "@id", src.Id }
                });
        }

        /// <summary>อัปเดตจำนวนรีวิวของแหล่ง (หลังนำเข้า CSV/ป้อนเอง)</summary>
        public void RefreshTotals(string sourceCode)
        {
            Exec(@"UPDATE AI_Review_Sources
                      SET TotalReviews = (SELECT COUNT(*) FROM AI_Online_Reviews WHERE SourceCode = @c AND Is_Excluded = 0),
                          LastFetchDate = GETDATE(), Updated_Date = GETDATE()
                    WHERE SourceCode = @c",
                new Dictionary<string, object> { { "@c", sourceCode } });
        }

        // ── Review_Fetch_Log ──────────────────────────────────────────────────

        public long StartLog(int sourceId)
        {
            try
            {
                using (var con = new SqlConnection(_conn))
                {
                    con.Open();
                    using (var cmd = new SqlCommand(
                        "INSERT INTO Review_Fetch_Log (Source_ID, Started_At, Status) VALUES (@s, GETDATE(), 'RUNNING'); SELECT CAST(SCOPE_IDENTITY() AS BIGINT);", con))
                    {
                        cmd.Parameters.AddWithValue("@s", sourceId);
                        return Convert.ToInt64(cmd.ExecuteScalar());
                    }
                }
            }
            catch { return 0; }
        }

        public void FinishLog(long logId, int sourceId, SourceRunResult r)
        {
            try
            {
                if (logId > 0)
                    Exec(@"UPDATE Review_Fetch_Log SET Finished_At = GETDATE(), Status = @st, Fetched = @f, Inserted = @i,
                                  Updated = @u, Message = @msg WHERE ID = @id",
                        new Dictionary<string, object>
                        {
                            { "@st", r.Status }, { "@f", r.Fetched }, { "@i", r.Inserted }, { "@u", r.Updated },
                            { "@msg", ReviewText.Clip(r.Message, 2000) }, { "@id", logId }
                        });
                else
                    Exec(@"INSERT INTO Review_Fetch_Log (Source_ID, Started_At, Finished_At, Status, Fetched, Inserted, Updated, Message)
                           VALUES (@s, GETDATE(), GETDATE(), @st, @f, @i, @u, @msg)",
                        new Dictionary<string, object>
                        {
                            { "@s", sourceId }, { "@st", r.Status }, { "@f", r.Fetched }, { "@i", r.Inserted },
                            { "@u", r.Updated }, { "@msg", ReviewText.Clip(r.Message, 2000) }
                        });
            }
            catch { }
        }

        /// <summary>ลบ log เก่ากว่า 180 วัน (เรียกวันละครั้งจากตัวตั้งเวลา)</summary>
        public void PurgeOldLogs()
        {
            try { Exec("DELETE FROM Review_Fetch_Log WHERE Started_At < DATEADD(DAY, -180, GETDATE())", null); }
            catch { }
        }

        /// <summary>คำนวณ Content_Hash ให้แถวเดิม (LEGACY) ทีละชุด — สูตรเดียวกับ Upsert จึงจับซ้ำได้</summary>
        public int BackfillHashes(int max)
        {
            var dt = new code().DatabaseQuerySafe(_conn,
                @"SELECT TOP (@n) ID, Origin_Platform, SourceCode, ReviewerName, ReviewDate, ReviewTitle, ReviewText, Rating_Raw
                    FROM AI_Online_Reviews WHERE Content_Hash IS NULL ORDER BY ID",
                new Dictionary<string, object> { { "@n", max } });
            int done = 0;
            foreach (DataRow r in dt.Rows)
            {
                string origin = r["Origin_Platform"] != DBNull.Value ? r["Origin_Platform"].ToString()
                    : ReviewPlatforms.OriginFor(r["SourceCode"].ToString());
                DateTime? d = r["ReviewDate"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["ReviewDate"]);
                string h = ReviewText.ContentHash(origin, ReviewText.CleanName(S(r["ReviewerName"])), d,
                    S(r["ReviewTitle"]), S(r["ReviewText"]), D(r["Rating_Raw"]));
                Exec("UPDATE AI_Online_Reviews SET Content_Hash = @h, Origin_Platform = COALESCE(Origin_Platform, @o) WHERE ID = @id",
                    new Dictionary<string, object> { { "@h", h }, { "@o", origin }, { "@id", Convert.ToInt64(r["ID"]) } });
                done++;
            }
            return done;
        }

        public DataTable GetLog(int? sourceId, int limit)
        {
            if (limit <= 0 || limit > 500) limit = 50;
            return new code().DatabaseQuerySafe(_conn,
                @"SELECT TOP (@n) l.ID, l.Source_ID, s.SourceCode, s.SourceName, l.Started_At, l.Finished_At, l.Status,
                         l.Fetched, l.Inserted, l.Updated, l.Message
                    FROM Review_Fetch_Log l LEFT JOIN AI_Review_Sources s ON s.ID = l.Source_ID
                   WHERE (@sid IS NULL OR l.Source_ID = @sid)
                   ORDER BY l.ID DESC",
                new Dictionary<string, object> { { "@n", limit }, { "@sid", sourceId.HasValue ? (object)sourceId.Value : null } });
        }

        private void Exec(string sql, Dictionary<string, object> ps)
        {
            new code().DatabaseInsertSafe(_conn, sql, ps);
        }
    }
}
