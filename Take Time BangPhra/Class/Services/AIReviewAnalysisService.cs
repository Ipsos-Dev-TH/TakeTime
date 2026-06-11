// ===========================================================================
// AIReviewAnalysisService.cs
// AI-Powered Review Analysis Service
// Handles online review fetching, sentiment analysis, dashboard analytics,
// response management, and summary generation using DeepSeek AI
// ===========================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services
{
    /// <summary>
    /// Service for AI-powered review analysis including online review fetching,
    /// sentiment analysis, dashboard analytics, response management, and summary generation
    /// </summary>
    public class AIReviewAnalysisService
    {
        private readonly string _connectionString;
        private readonly code _code;
        private readonly JavaScriptSerializer _serializer;
        private static bool _schemaEnsured = false;
        private static readonly object _schemaLock = new object();

        public AIReviewAnalysisService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            EnsureSchema();
        }

        #region Schema

        /// <summary>
        /// Auto-creates review analysis tables + view if they don't exist (covers PHASE16 Migration 01).
        /// Runs once per app domain. Safe to call repeatedly.
        /// </summary>
        private void EnsureSchema()
        {
            if (_schemaEnsured) return;
            lock (_schemaLock)
            {
                if (_schemaEnsured) return;
                try
                {
                    _code.DatabaseInsertSafe(_connectionString, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Review_Sources')
BEGIN
    CREATE TABLE AI_Review_Sources (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        SourceCode NVARCHAR(30) NOT NULL,
        SourceName NVARCHAR(100) NOT NULL,
        IconClass NVARCHAR(50),
        BrandColor NVARCHAR(20),
        IsEnabled BIT DEFAULT 0,
        ApiConfig NVARCHAR(MAX),
        LastFetchDate DATETIME,
        TotalReviews INT DEFAULT 0,
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_ReviewSource UNIQUE (SourceCode)
    );

    INSERT INTO AI_Review_Sources (SourceCode, SourceName, IconClass, BrandColor, IsEnabled) VALUES
    ('INTERNAL',    N'รีวิวจากระบบ',  'fas fa-home',        '#5D4037', 1),
    ('GOOGLE',      N'Google Reviews', 'fab fa-google',      '#4285F4', 0),
    ('FACEBOOK',    N'Facebook Reviews','fab fa-facebook',   '#1877F2', 0),
    ('AGODA',       N'Agoda',          'fas fa-bed',         '#5542F6', 0),
    ('BOOKING',     N'Booking.com',    'fas fa-suitcase',    '#003580', 0),
    ('TRIPADVISOR', N'TripAdvisor',    'fab fa-tripadvisor', '#34E0A1', 0),
    ('EXPEDIA',     N'Expedia',        'fas fa-globe',       '#FFCC00', 0),
    ('TRAVELOKA',   N'Traveloka',      'fas fa-plane',       '#0194F3', 0),
    ('PANTIP',      N'Pantip',         'fas fa-comments',    '#7A2D8F', 0),
    ('TIKTOK',      N'TikTok',         'fab fa-tiktok',      '#000000', 0),
    ('LEMON8',      N'Lemon8',         'fas fa-lemon',       '#FFE135', 0),
    ('WONGNAI',     N'Wongnai',        'fas fa-utensils',    '#ED1C24', 0),
    ('TWITTER',     N'X (Twitter)',    'fab fa-x-twitter',   '#000000', 0),
    ('INSTAGRAM',   N'Instagram',      'fab fa-instagram',   '#E4405F', 0),
    ('YOUTUBE',     N'YouTube',        'fab fa-youtube',     '#FF0000', 0);
END", null);

                    _code.DatabaseInsertSafe(_connectionString, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Online_Reviews')
BEGIN
    CREATE TABLE AI_Online_Reviews (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        SourceCode NVARCHAR(30) NOT NULL,
        PlatformReviewId NVARCHAR(200),
        ReviewerName NVARCHAR(200),
        ReviewerAvatar NVARCHAR(500),
        Rating FLOAT,
        ReviewTitle NVARCHAR(300),
        ReviewText NVARCHAR(MAX),
        ReviewDate DATETIME,
        Language NVARCHAR(10) DEFAULT 'TH',
        Sentiment NVARCHAR(20),
        SentimentScore FLOAT,
        Topics NVARCHAR(500),
        TopicScores NVARCHAR(MAX),
        AISummary NVARCHAR(500),
        SuggestedResponse NVARCHAR(MAX),
        ResponseStatus NVARCHAR(20) DEFAULT 'PENDING',
        ActualResponse NVARCHAR(MAX),
        RespondedDate DATETIME,
        RespondedBy NVARCHAR(100),
        IsAnalyzed BIT DEFAULT 0,
        IsFlagged BIT DEFAULT 0,
        FlagReason NVARCHAR(200),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AIReview_Source (SourceCode),
        INDEX IX_AIReview_Sentiment (Sentiment),
        INDEX IX_AIReview_Rating (Rating),
        INDEX IX_AIReview_Date (ReviewDate DESC),
        INDEX IX_AIReview_PlatformId (SourceCode, PlatformReviewId)
    );
END", null);

                    _code.DatabaseInsertSafe(_connectionString, @"
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Reviews')
   AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Guest_Reviews' AND COLUMN_NAME = 'Sentiment')
BEGIN
    ALTER TABLE Guest_Reviews ADD Sentiment NVARCHAR(20) NULL;
    ALTER TABLE Guest_Reviews ADD SentimentScore FLOAT NULL;
    ALTER TABLE Guest_Reviews ADD AITopics NVARCHAR(500) NULL;
    ALTER TABLE Guest_Reviews ADD AISummary NVARCHAR(500) NULL;
    ALTER TABLE Guest_Reviews ADD AISuggestedResponse NVARCHAR(MAX) NULL;
    ALTER TABLE Guest_Reviews ADD AITopicScores NVARCHAR(MAX) NULL;
    ALTER TABLE Guest_Reviews ADD IsAIAnalyzed BIT DEFAULT 0;
    ALTER TABLE Guest_Reviews ADD IsFlagged BIT DEFAULT 0;
    ALTER TABLE Guest_Reviews ADD FlagReason NVARCHAR(200) NULL;
END", null);

                    _code.DatabaseInsertSafe(_connectionString, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Report_Summaries')
BEGIN
    CREATE TABLE AI_Report_Summaries (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ReportType NVARCHAR(30) NOT NULL,
        PeriodType NVARCHAR(20) NOT NULL,
        PeriodStart DATE NOT NULL,
        PeriodEnd DATE NOT NULL,
        DataSnapshot NVARCHAR(MAX),
        AISummary NVARCHAR(MAX) NOT NULL,
        KeyInsights NVARCHAR(MAX),
        Recommendations NVARCHAR(MAX),
        TokensUsed INT DEFAULT 0,
        GeneratedBy NVARCHAR(100),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AIReport_Type (ReportType, PeriodType),
        INDEX IX_AIReport_Period (PeriodStart, PeriodEnd)
    );
END", null);

                    // CREATE VIEW must be the only statement in its batch → EXEC with doubled quotes
                    _code.DatabaseInsertSafe(_connectionString, @"
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Guest_Reviews')
   AND EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Guest_Reviews' AND COLUMN_NAME = 'Sentiment')
   AND NOT EXISTS (SELECT * FROM sys.views WHERE name = 'vw_AI_Review_AllSources')
BEGIN
    EXEC('CREATE VIEW vw_AI_Review_AllSources AS
    SELECT
        ''INTERNAL'' AS SourceCode,
        gr.ID,
        c.Name AS ReviewerName,
        NULL AS ReviewerAvatar,
        CAST(gr.OverallRating AS FLOAT) AS Rating,
        gr.ReviewTitle,
        ISNULL(gr.ReviewText, '''') + CASE WHEN gr.Pros IS NOT NULL THEN N'' ข้อดี: '' + gr.Pros ELSE '''' END + CASE WHEN gr.Cons IS NOT NULL THEN N'' ข้อเสีย: '' + gr.Cons ELSE '''' END AS ReviewText,
        gr.SubmittedDate AS ReviewDate,
        gr.Sentiment,
        gr.SentimentScore,
        gr.AITopics AS Topics,
        gr.AISummary,
        gr.AISuggestedResponse AS SuggestedResponse,
        gr.Status AS ResponseStatus,
        gr.ResponseText AS ActualResponse,
        CASE WHEN gr.IsAIAnalyzed = 1 THEN 1 ELSE 0 END AS IsAnalyzed,
        ISNULL(gr.IsFlagged, 0) AS IsFlagged,
        gr.FlagReason
    FROM Guest_Reviews gr
    LEFT JOIN Customer c ON c.MobilePhone = gr.Customer_MobilePhone
    WHERE gr.Status IN (''APPROVED'', ''PENDING'')
    UNION ALL
    SELECT
        aor.SourceCode,
        aor.ID,
        aor.ReviewerName,
        aor.ReviewerAvatar,
        aor.Rating,
        aor.ReviewTitle,
        aor.ReviewText,
        aor.ReviewDate,
        aor.Sentiment,
        aor.SentimentScore,
        aor.Topics,
        aor.AISummary,
        aor.SuggestedResponse,
        aor.ResponseStatus,
        aor.ActualResponse,
        CAST(aor.IsAnalyzed AS INT) AS IsAnalyzed,
        ISNULL(aor.IsFlagged, 0) AS IsFlagged,
        aor.FlagReason
    FROM AI_Online_Reviews aor')
END", null);

                    _schemaEnsured = true;
                }
                catch { /* keep _schemaEnsured false so a later call retries */ }
            }
        }

        #endregion

        #region 1. Online Review Fetching

        public DataTable GetReviewSources()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM AI_Review_Sources ORDER BY SourceCode", null);
        }

        public void UpdateSourceConfig(string sourceCode, bool enabled, string apiConfig)
        {
            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE AI_Review_Sources
                  SET IsEnabled = @Enabled, ApiConfig = @Config, Updated_Date = GETDATE()
                  WHERE SourceCode = @Code",
                new Dictionary<string, object>
                {
                    { "@Code", sourceCode },
                    { "@Enabled", enabled },
                    { "@Config", apiConfig ?? (object)DBNull.Value }
                });
        }

        // ── Dedup helper: check if a review already exists ──
        private bool ReviewExists(string sourceCode, string reviewerName, DateTime reviewDate, double rating)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT COUNT(*) AS Cnt FROM AI_Online_Reviews
                  WHERE SourceCode = @Source
                    AND ReviewerName = @Name
                    AND CAST(ReviewDate AS DATE) = CAST(@ReviewDate AS DATE)
                    AND Rating = @Rating",
                new Dictionary<string, object>
                {
                    { "@Source", sourceCode },
                    { "@Name", reviewerName },
                    { "@ReviewDate", reviewDate },
                    { "@Rating", rating }
                });
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Cnt"]) > 0;
        }

        private bool ReviewExistsByPlatformId(string platformReviewId)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT COUNT(*) AS Cnt FROM AI_Online_Reviews WHERE PlatformReviewId = @PlatformId",
                new Dictionary<string, object> { { "@PlatformId", platformReviewId } });
            return dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0]["Cnt"]) > 0;
        }

        private int InsertReview(string sourceCode, string platformReviewId, string reviewerName,
            string reviewerAvatar, double rating, string reviewTitle, string reviewText, DateTime reviewDate)
        {
            // Double-check: PlatformReviewId dedup first, then content dedup
            if (!string.IsNullOrEmpty(platformReviewId) && ReviewExistsByPlatformId(platformReviewId))
                return 0;
            if (ReviewExists(sourceCode, reviewerName, reviewDate, rating))
                return 0;

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO AI_Online_Reviews
                  (SourceCode, PlatformReviewId, ReviewerName, ReviewerAvatar, Rating, ReviewTitle, ReviewText,
                   ReviewDate, IsAnalyzed, IsFlagged, Created_Date)
                  VALUES
                  (@Source, @PlatformId, @Author, @Photo, @Rating, @Title, @Text,
                   @ReviewDate, 0, 0, GETDATE())",
                new Dictionary<string, object>
                {
                    { "@Source", sourceCode },
                    { "@PlatformId", platformReviewId ?? (object)DBNull.Value },
                    { "@Author", reviewerName },
                    { "@Photo", reviewerAvatar ?? (object)DBNull.Value },
                    { "@Rating", rating },
                    { "@Title", reviewTitle ?? (object)DBNull.Value },
                    { "@Text", reviewText ?? (object)DBNull.Value },
                    { "@ReviewDate", reviewDate }
                });
            return 1;
        }

        private void UpdateSourceMetadata(string sourceCode)
        {
            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE AI_Review_Sources
                  SET LastFetchDate = GETDATE(),
                      TotalReviews = (SELECT COUNT(*) FROM AI_Online_Reviews WHERE SourceCode = @Source),
                      Updated_Date = GETDATE()
                  WHERE SourceCode = @Source",
                new Dictionary<string, object> { { "@Source", sourceCode } });
        }

        // ── Google Reviews ──
        public Dictionary<string, object> FetchGoogleReviews()
        {
            int newCount = 0;
            int skipCount = 0;
            var result = new Dictionary<string, object> { { "source", "GOOGLE" } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'GOOGLE'", null);

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = "Google Reviews ไม่ได้เปิดใช้งาน หรือยังไม่ได้ตั้งค่า";
                    return result;
                }

                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                if (string.IsNullOrEmpty(apiConfigJson))
                {
                    result["success"] = false;
                    result["message"] = "ยังไม่ได้ตั้งค่า API Config (ต้องมี placeId, apiKey)";
                    return result;
                }

                var apiConfig = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                string placeId = apiConfig.ContainsKey("placeId") ? apiConfig["placeId"]?.ToString() : null;
                string apiKey = apiConfig.ContainsKey("apiKey") ? apiConfig["apiKey"]?.ToString() : null;

                if (string.IsNullOrEmpty(placeId) || string.IsNullOrEmpty(apiKey))
                {
                    result["success"] = false;
                    result["message"] = "ต้องระบุทั้ง placeId และ apiKey ในการตั้งค่า";
                    return result;
                }

                // Google Places API (returns max 5 most relevant reviews)
                // Use reviews_sort=newest to try get newer ones; also request in both TH and EN
                string[] languages = { "th", "en" };
                foreach (string lang in languages)
                {
                    string url = string.Format(
                        "https://maps.googleapis.com/maps/api/place/details/json?place_id={0}&fields=reviews&reviews_sort=newest&language={1}&key={2}",
                        Uri.EscapeDataString(placeId), lang, Uri.EscapeDataString(apiKey));

                    string responseText = MakeHttpGetRequest(url);
                    if (string.IsNullOrEmpty(responseText)) continue;

                    var response = _serializer.Deserialize<Dictionary<string, object>>(responseText);
                    if (response == null || !response.ContainsKey("result")) continue;

                    var placeResult = response["result"] as Dictionary<string, object>;
                    if (placeResult == null || !placeResult.ContainsKey("reviews")) continue;

                    var reviews = placeResult["reviews"] as ArrayList;
                    if (reviews == null) continue;

                    foreach (var reviewObj in reviews)
                    {
                        var review = reviewObj as Dictionary<string, object>;
                        if (review == null) continue;

                        string authorName = review.ContainsKey("author_name") ? review["author_name"]?.ToString() : "Unknown";
                        double rating = review.ContainsKey("rating") ? Convert.ToDouble(review["rating"]) : 0;
                        string text = review.ContainsKey("text") ? review["text"]?.ToString() : "";
                        long timeEpoch = review.ContainsKey("time") ? Convert.ToInt64(review["time"]) : 0;
                        string profilePhoto = review.ContainsKey("profile_photo_url") ? review["profile_photo_url"]?.ToString() : null;
                        string authorUrl = review.ContainsKey("author_url") ? review["author_url"]?.ToString() : null;

                        DateTime reviewDate = timeEpoch > 0
                            ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timeEpoch).ToLocalTime()
                            : DateTime.Now;

                        // Stable platform ID using epoch timestamp (unique per review)
                        string platformReviewId = "GOOGLE_" + timeEpoch + "_" + (authorUrl != null ? authorUrl.GetHashCode().ToString() : authorName.GetHashCode().ToString());

                        int added = InsertReview("GOOGLE", platformReviewId, authorName, profilePhoto,
                            rating, null, text, reviewDate);
                        if (added > 0) newCount++; else skipCount++;
                    }
                }

                UpdateSourceMetadata("GOOGLE");
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("Google Reviews: เพิ่มใหม่ {0} รายการ, ข้ามซ้ำ {1} รายการ", newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return result;
        }

        // ── Facebook Reviews (with pagination) ──
        public Dictionary<string, object> FetchFacebookReviews()
        {
            int newCount = 0;
            int skipCount = 0;
            var result = new Dictionary<string, object> { { "source", "FACEBOOK" } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'FACEBOOK'", null);

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = "Facebook Reviews ไม่ได้เปิดใช้งาน หรือยังไม่ได้ตั้งค่า";
                    return result;
                }

                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                if (string.IsNullOrEmpty(apiConfigJson))
                {
                    result["success"] = false;
                    result["message"] = "ยังไม่ได้ตั้งค่า API Config (ต้องมี pageId, pageAccessToken)";
                    return result;
                }

                var apiConfig = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                string pageId = apiConfig.ContainsKey("pageId") ? apiConfig["pageId"]?.ToString() : null;
                string pageAccessToken = apiConfig.ContainsKey("pageAccessToken") ? apiConfig["pageAccessToken"]?.ToString() : null;

                if (string.IsNullOrEmpty(pageId) || string.IsNullOrEmpty(pageAccessToken))
                {
                    result["success"] = false;
                    result["message"] = "ต้องระบุทั้ง pageId และ pageAccessToken ในการตั้งค่า";
                    return result;
                }

                // Facebook Graph API with pagination (fetch all pages, max 10 pages = ~250 reviews)
                string url = string.Format(
                    "https://graph.facebook.com/v18.0/{0}/ratings?fields=reviewer{{name,picture}},rating,review_text,created_time&limit=25&access_token={1}",
                    Uri.EscapeDataString(pageId), Uri.EscapeDataString(pageAccessToken));

                int pageCount = 0;
                while (!string.IsNullOrEmpty(url) && pageCount < 10)
                {
                    pageCount++;
                    string responseText = MakeHttpGetRequest(url);
                    if (string.IsNullOrEmpty(responseText)) break;

                    var response = _serializer.Deserialize<Dictionary<string, object>>(responseText);
                    if (response == null || !response.ContainsKey("data")) break;

                    var dataArray = response["data"] as ArrayList;
                    if (dataArray == null || dataArray.Count == 0) break;

                    foreach (var itemObj in dataArray)
                    {
                        var item = itemObj as Dictionary<string, object>;
                        if (item == null) continue;

                        string reviewerName = "Unknown";
                        string reviewerPhoto = null;
                        string reviewerId = null;
                        if (item.ContainsKey("reviewer"))
                        {
                            var reviewer = item["reviewer"] as Dictionary<string, object>;
                            if (reviewer != null)
                            {
                                reviewerName = reviewer.ContainsKey("name") ? reviewer["name"]?.ToString() : "Unknown";
                                reviewerId = reviewer.ContainsKey("id") ? reviewer["id"]?.ToString() : null;
                                if (reviewer.ContainsKey("picture"))
                                {
                                    var picture = reviewer["picture"] as Dictionary<string, object>;
                                    if (picture != null && picture.ContainsKey("data"))
                                    {
                                        var picData = picture["data"] as Dictionary<string, object>;
                                        if (picData != null && picData.ContainsKey("url"))
                                            reviewerPhoto = picData["url"]?.ToString();
                                    }
                                }
                            }
                        }

                        double rating = item.ContainsKey("rating") ? Convert.ToDouble(item["rating"]) : 0;
                        string reviewText = item.ContainsKey("review_text") ? item["review_text"]?.ToString() : "";
                        string createdTime = item.ContainsKey("created_time") ? item["created_time"]?.ToString() : null;

                        DateTime reviewDate = DateTime.Now;
                        if (!string.IsNullOrEmpty(createdTime))
                        {
                            DateTime parsed;
                            if (DateTime.TryParse(createdTime, out parsed))
                                reviewDate = parsed;
                        }

                        // Stable platform ID: use reviewer ID + created_time ISO string
                        string stableKey = (reviewerId ?? reviewerName) + "_" + (createdTime ?? reviewDate.ToString("yyyyMMddHHmmss"));
                        string platformReviewId = "FB_" + stableKey.GetHashCode();

                        int added = InsertReview("FACEBOOK", platformReviewId, reviewerName, reviewerPhoto,
                            rating, null, reviewText, reviewDate);
                        if (added > 0) newCount++; else skipCount++;
                    }

                    // Check for next page
                    url = null;
                    if (response.ContainsKey("paging"))
                    {
                        var paging = response["paging"] as Dictionary<string, object>;
                        if (paging != null && paging.ContainsKey("next"))
                            url = paging["next"]?.ToString();
                    }
                }

                UpdateSourceMetadata("FACEBOOK");
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("Facebook Reviews: เพิ่มใหม่ {0} รายการ, ข้ามซ้ำ {1} รายการ (ดึง {2} หน้า)", newCount, skipCount, pageCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return result;
        }

        // ── Agoda / Booking / TripAdvisor / Expedia / Traveloka ──
        // OTA platforms don't provide public review APIs
        // Use web scraping or manual import

        /// <summary>
        /// Fetch reviews by scraping a URL (generic HTML scraper for OTA platforms).
        /// Requires API config with: { "scrapeUrl": "...", "platform": "AGODA|BOOKING|TRIPADVISOR|..." }
        /// Sends the HTML to DeepSeek to extract structured review data.
        /// </summary>
        public Dictionary<string, object> FetchOTAReviews(string sourceCode)
        {
            int newCount = 0;
            int skipCount = 0;
            var result = new Dictionary<string, object> { { "source", sourceCode } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = @Source",
                    new Dictionary<string, object> { { "@Source", sourceCode } });

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = sourceCode + " ไม่ได้เปิดใช้งาน หรือยังไม่ได้ตั้งค่า";
                    return result;
                }

                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                if (string.IsNullOrEmpty(apiConfigJson))
                {
                    result["success"] = false;
                    result["message"] = "ยังไม่ได้ตั้งค่า API Config (ต้องมี scrapeUrl)";
                    return result;
                }

                var apiConfig = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                string scrapeUrl = apiConfig.ContainsKey("scrapeUrl") ? apiConfig["scrapeUrl"]?.ToString() : null;

                if (string.IsNullOrEmpty(scrapeUrl))
                {
                    result["success"] = false;
                    result["message"] = "ต้องระบุ scrapeUrl ของหน้ารีวิว " + sourceCode + " ในการตั้งค่า";
                    return result;
                }

                // Fetch the page HTML
                string html = MakeHttpGetRequest(scrapeUrl);
                if (string.IsNullOrEmpty(html) || html.Length < 100)
                {
                    result["success"] = false;
                    result["message"] = "ไม่สามารถดึงข้อมูลจาก URL ได้";
                    return result;
                }

                // Truncate HTML to fit DeepSeek context (keep first 15000 chars of body content)
                if (html.Length > 15000)
                    html = html.Substring(0, 15000);

                // Use DeepSeek to extract structured review data from the HTML
                string prompt = string.Format(
                    @"จากเนื้อหา HTML ของหน้ารีวิวโรงแรมจาก {0} ด้านล่าง ให้ดึงข้อมูลรีวิวทั้งหมดที่พบ
ตอบเป็น JSON array เท่านั้น แต่ละรายการมีรูปแบบ:
[
  {{
    ""reviewerName"": ""ชื่อผู้รีวิว"",
    ""rating"": 8.5,
    ""reviewText"": ""เนื้อหารีวิว"",
    ""reviewDate"": ""2024-01-15"",
    ""reviewTitle"": ""หัวข้อรีวิว หรือ null""
  }}
]

หมายเหตุ:
- rating ให้แปลงเป็นมาตรฐาน 1-5 (ถ้าแพลตฟอร์มใช้ 1-10 ให้หาร 2)
- reviewDate ให้แปลงเป็นรูปแบบ yyyy-MM-dd
- ถ้าไม่มีวันที่ชัดเจน ให้ประมาณจากข้อความเช่น ""2 เดือนที่แล้ว"" หรือ ""มกราคม 2024""
- ถ้าไม่พบรีวิวเลย ให้ตอบ []

HTML:
{1}", sourceCode, html);

                var deepSeek = new DeepSeekService(_connectionString);
                string sessionKey = "ota_scrape_" + sourceCode + "_" + DateTime.Now.Ticks;
                var aiResponse = deepSeek.SendMessage(prompt, sessionKey, null);

                if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.Message))
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่สามารถวิเคราะห์หน้ารีวิวได้: " + (aiResponse.Message ?? "unknown");
                    return result;
                }

                // Extract JSON array from AI response
                string jsonArray = ExtractJsonArrayFromResponse(aiResponse.Message);
                if (string.IsNullOrEmpty(jsonArray))
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่พบรีวิวในหน้าเว็บ";
                    return result;
                }

                var reviews = _serializer.Deserialize<ArrayList>(jsonArray);
                if (reviews == null || reviews.Count == 0)
                {
                    result["success"] = false;
                    result["message"] = "ไม่พบรีวิวในข้อมูลที่ AI วิเคราะห์ได้";
                    return result;
                }

                foreach (var reviewObj in reviews)
                {
                    var review = reviewObj as Dictionary<string, object>;
                    if (review == null) continue;

                    string reviewerName = review.ContainsKey("reviewerName") ? review["reviewerName"]?.ToString() : "Unknown";
                    double rating = review.ContainsKey("rating") ? Convert.ToDouble(review["rating"]) : 0;
                    string reviewText = review.ContainsKey("reviewText") ? review["reviewText"]?.ToString() : "";
                    string reviewTitle = review.ContainsKey("reviewTitle") ? review["reviewTitle"]?.ToString() : null;

                    DateTime reviewDate = DateTime.Now;
                    if (review.ContainsKey("reviewDate") && review["reviewDate"] != null)
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(review["reviewDate"].ToString(), out parsed))
                            reviewDate = parsed;
                    }

                    if (string.IsNullOrEmpty(reviewText) && string.IsNullOrEmpty(reviewTitle))
                        continue;

                    string platformReviewId = sourceCode + "_" + reviewerName.GetHashCode() + "_" + reviewDate.ToString("yyyyMMdd") + "_" + ((int)rating);

                    int added = InsertReview(sourceCode, platformReviewId, reviewerName, null,
                        rating, reviewTitle, reviewText, reviewDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata(sourceCode);
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("{0}: เพิ่มใหม่ {1} รายการ, ข้ามซ้ำ {2} รายการ", sourceCode, newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Fetch reviews from all enabled sources
        /// </summary>
        public List<Dictionary<string, object>> FetchAllEnabledSources()
        {
            var results = new List<Dictionary<string, object>>();

            DataTable dtSources = _code.DatabaseQuerySafe(_connectionString,
                "SELECT SourceCode FROM AI_Review_Sources WHERE IsEnabled = 1 AND SourceCode != 'INTERNAL'",
                null);

            foreach (DataRow row in dtSources.Rows)
            {
                string sourceCode = row["SourceCode"].ToString();
                try
                {
                    Dictionary<string, object> fetchResult;
                    switch (sourceCode)
                    {
                        case "GOOGLE":
                            fetchResult = FetchGoogleReviews();
                            break;
                        case "FACEBOOK":
                            fetchResult = FetchFacebookReviews();
                            break;
                        case "PANTIP":
                            fetchResult = FetchPantipReviews();
                            break;
                        case "WONGNAI":
                            fetchResult = FetchWongnaiReviews();
                            break;
                        case "TIKTOK":
                        case "LEMON8":
                        case "TWITTER":
                        case "INSTAGRAM":
                        case "YOUTUBE":
                            fetchResult = FetchSocialMentions(sourceCode);
                            break;
                        default:
                            fetchResult = FetchOTAReviews(sourceCode);
                            break;
                    }
                    results.Add(fetchResult);
                }
                catch (Exception ex)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "source", sourceCode },
                        { "success", false },
                        { "message", ex.Message }
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Import reviews manually — with robust dedup by reviewer+date+rating+source
        /// </summary>
        public Dictionary<string, object> ImportReviewsManual(string sourceCode, string jsonContent)
        {
            int newCount = 0;
            int skipCount = 0;
            var result = new Dictionary<string, object> { { "source", sourceCode } };

            try
            {
                if (string.IsNullOrEmpty(sourceCode))
                    throw new ArgumentException("ต้องระบุแหล่งที่มา (sourceCode)");
                if (string.IsNullOrEmpty(jsonContent))
                    throw new ArgumentException("ต้องมีข้อมูลรีวิว");

                var reviews = _serializer.Deserialize<ArrayList>(jsonContent);
                if (reviews == null || reviews.Count == 0)
                {
                    result["success"] = false;
                    result["message"] = "ไม่พบข้อมูลรีวิวใน JSON";
                    return result;
                }

                foreach (var reviewObj in reviews)
                {
                    var review = reviewObj as Dictionary<string, object>;
                    if (review == null) continue;

                    string reviewerName = review.ContainsKey("reviewerName") ? review["reviewerName"]?.ToString() : "Unknown";
                    double rating = review.ContainsKey("rating") ? Convert.ToDouble(review["rating"]) : 0;
                    string reviewText = review.ContainsKey("reviewText") ? review["reviewText"]?.ToString() : "";
                    string reviewTitle = review.ContainsKey("reviewTitle") ? review["reviewTitle"]?.ToString() : null;

                    // Date is required — if not provided, still import but with today's date
                    DateTime reviewDate = DateTime.Now;
                    if (review.ContainsKey("reviewDate") && review["reviewDate"] != null)
                    {
                        string dateStr = review["reviewDate"].ToString();
                        DateTime parsed;
                        // Support multiple formats
                        if (DateTime.TryParse(dateStr, out parsed))
                            reviewDate = parsed;
                        else if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out parsed))
                            reviewDate = parsed;
                    }

                    if (string.IsNullOrEmpty(reviewText) && string.IsNullOrEmpty(reviewTitle))
                        continue;

                    // Stable dedup ID based on source + reviewer + date + rating
                    string platformReviewId = sourceCode + "_IMPORT_" + reviewerName.GetHashCode()
                        + "_" + reviewDate.ToString("yyyyMMdd") + "_" + ((int)(rating * 10));

                    int added = InsertReview(sourceCode, platformReviewId, reviewerName, null,
                        rating, reviewTitle, reviewText, reviewDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata(sourceCode);
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("นำเข้า {0}: เพิ่มใหม่ {1} รายการ, ข้ามซ้ำ {2} รายการ", sourceCode, newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Use AI to parse unstructured review text (e.g. copy-paste from OTA website)
        /// into structured JSON, then import
        /// </summary>
        public Dictionary<string, object> ImportReviewsFromText(string sourceCode, string rawText)
        {
            var result = new Dictionary<string, object> { { "source", sourceCode } };

            try
            {
                if (string.IsNullOrEmpty(rawText) || rawText.Length < 10)
                {
                    result["success"] = false;
                    result["message"] = "ข้อมูลสั้นเกินไป";
                    return result;
                }

                // Truncate if too long
                if (rawText.Length > 10000)
                    rawText = rawText.Substring(0, 10000);

                string prompt = string.Format(
                    @"จากข้อความด้านล่างซึ่ง copy มาจากหน้ารีวิวโรงแรมบน {0} ให้ดึงข้อมูลรีวิวทั้งหมด
ตอบเป็น JSON array เท่านั้น:
[
  {{
    ""reviewerName"": ""ชื่อผู้รีวิว"",
    ""rating"": 4.5,
    ""reviewText"": ""เนื้อหารีวิว"",
    ""reviewDate"": ""2024-06-15"",
    ""reviewTitle"": ""หัวข้อ หรือ null""
  }}
]

หมายเหตุ:
- rating แปลงเป็นมาตรฐาน 1-5 (ถ้าแพลตฟอร์มใช้ 1-10 ให้หาร 2)
- reviewDate แปลงเป็น yyyy-MM-dd ถ้าเห็น ""2 เดือนที่แล้ว"" ให้คำนวณจากวันที่ {1}
- ถ้าไม่พบรีวิวให้ตอบ []

ข้อความ:
{2}", sourceCode, DateTime.Now.ToString("yyyy-MM-dd"), rawText);

                var deepSeek = new DeepSeekService(_connectionString);
                string sessionKey = "import_text_" + sourceCode + "_" + DateTime.Now.Ticks;
                var aiResponse = deepSeek.SendMessage(prompt, sessionKey, null);

                if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.Message))
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่สามารถวิเคราะห์ข้อความได้";
                    return result;
                }

                string jsonArray = ExtractJsonArrayFromResponse(aiResponse.Message);
                if (string.IsNullOrEmpty(jsonArray))
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่พบรีวิวในข้อความ";
                    return result;
                }

                // Use the existing manual import with dedup
                return ImportReviewsManual(sourceCode, jsonArray);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "เกิดข้อผิดพลาด: " + ex.Message;
                return result;
            }
        }

        // ── Social Media Scraping ──

        /// <summary>
        /// Get the hotel search keyword from AI_Review_Sources config or default
        /// ApiConfig: { "searchKeyword": "TakeTime BangPhra", "hotelName": "เทคไทม์ บางพระ" }
        /// </summary>
        private string GetSearchKeyword(string sourceCode)
        {
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT ApiConfig FROM AI_Review_Sources WHERE SourceCode = @Source",
                new Dictionary<string, object> { { "@Source", sourceCode } });

            if (dt.Rows.Count > 0 && dt.Rows[0]["ApiConfig"] != DBNull.Value)
            {
                string json = dt.Rows[0]["ApiConfig"]?.ToString();
                if (!string.IsNullOrEmpty(json))
                {
                    var config = _serializer.Deserialize<Dictionary<string, object>>(json);
                    if (config != null && config.ContainsKey("searchKeyword"))
                        return config["searchKeyword"]?.ToString();
                    if (config != null && config.ContainsKey("hotelName"))
                        return config["hotelName"]?.ToString();
                }
            }
            return "TakeTime BangPhra";
        }

        /// <summary>
        /// Scrape Pantip threads mentioning the hotel.
        /// Uses Pantip search URL, fetches HTML, AI parses thread content.
        /// Config: { "searchKeyword": "TakeTime บางพระ", "searchUrl": "https://pantip.com/search?q=..." }
        /// </summary>
        public Dictionary<string, object> FetchPantipReviews()
        {
            int newCount = 0, skipCount = 0;
            var result = new Dictionary<string, object> { { "source", "PANTIP" } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'PANTIP'", null);

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = "Pantip ไม่ได้เปิดใช้งาน";
                    return result;
                }

                string keyword = GetSearchKeyword("PANTIP");
                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                string searchUrl = null;

                if (!string.IsNullOrEmpty(apiConfigJson))
                {
                    var config = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                    if (config != null && config.ContainsKey("searchUrl"))
                        searchUrl = config["searchUrl"]?.ToString();
                }

                // Default Pantip search URL
                if (string.IsNullOrEmpty(searchUrl))
                    searchUrl = "https://pantip.com/search?q=" + Uri.EscapeDataString(keyword);

                string html = MakeHttpGetRequest(searchUrl);
                if (string.IsNullOrEmpty(html) || html.Length < 200)
                {
                    result["success"] = false;
                    result["message"] = "ไม่สามารถดึงข้อมูลจาก Pantip ได้ (อาจถูก block)";
                    return result;
                }

                string prompt = BuildSocialScrapingPrompt("Pantip", keyword, html,
                    "แต่ละกระทู้/ความคิดเห็นนับเป็น 1 รีวิว ใช้ชื่อผู้โพสต์เป็น reviewerName " +
                    "ถ้าไม่มี rating ให้ประเมินจากน้ำเสียง (บวก=4-5, กลาง=3, ลบ=1-2) " +
                    "reviewDate ใช้วันที่โพสต์");

                var parsed = CallAIAndParseReviews(prompt, "PANTIP");
                if (parsed == null)
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่พบรีวิวในหน้า Pantip";
                    return result;
                }

                foreach (var r in parsed)
                {
                    int added = InsertReview("PANTIP", r.PlatformId, r.ReviewerName, null,
                        r.Rating, r.ReviewTitle, r.ReviewText, r.ReviewDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata("PANTIP");
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("Pantip: เพิ่มใหม่ {0} รายการ, ข้ามซ้ำ {1} รายการ", newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "Pantip error: " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Scrape Wongnai reviews for the hotel.
        /// Config: { "searchKeyword": "TakeTime", "searchUrl": "https://www.wongnai.com/..." }
        /// </summary>
        public Dictionary<string, object> FetchWongnaiReviews()
        {
            int newCount = 0, skipCount = 0;
            var result = new Dictionary<string, object> { { "source", "WONGNAI" } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'WONGNAI'", null);

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = "Wongnai ไม่ได้เปิดใช้งาน";
                    return result;
                }

                string keyword = GetSearchKeyword("WONGNAI");
                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                string searchUrl = null;

                if (!string.IsNullOrEmpty(apiConfigJson))
                {
                    var config = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                    if (config != null && config.ContainsKey("searchUrl"))
                        searchUrl = config["searchUrl"]?.ToString();
                }

                if (string.IsNullOrEmpty(searchUrl))
                    searchUrl = "https://www.wongnai.com/search?q=" + Uri.EscapeDataString(keyword) + "&type=hotel";

                string html = MakeHttpGetRequest(searchUrl);
                if (string.IsNullOrEmpty(html) || html.Length < 200)
                {
                    result["success"] = false;
                    result["message"] = "ไม่สามารถดึงข้อมูลจาก Wongnai ได้";
                    return result;
                }

                string prompt = BuildSocialScrapingPrompt("Wongnai", keyword, html,
                    "Wongnai ใช้ rating 1-5 ดาว ดึง reviewerName, rating, reviewText, reviewDate");

                var parsed = CallAIAndParseReviews(prompt, "WONGNAI");
                if (parsed == null)
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่พบรีวิวใน Wongnai";
                    return result;
                }

                foreach (var r in parsed)
                {
                    int added = InsertReview("WONGNAI", r.PlatformId, r.ReviewerName, null,
                        r.Rating, r.ReviewTitle, r.ReviewText, r.ReviewDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata("WONGNAI");
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("Wongnai: เพิ่มใหม่ {0} รายการ, ข้ามซ้ำ {1} รายการ", newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "Wongnai error: " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Scrape TikTok, Lemon8, Twitter/X, Instagram, or YouTube mentions.
        /// These platforms don't have structured reviews, so we extract mentions/comments.
        /// Config: { "searchKeyword": "TakeTime BangPhra", "searchUrl": "https://..." }
        /// </summary>
        public Dictionary<string, object> FetchSocialMentions(string sourceCode)
        {
            int newCount = 0, skipCount = 0;
            var result = new Dictionary<string, object> { { "source", sourceCode } };

            try
            {
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = @Source",
                    new Dictionary<string, object> { { "@Source", sourceCode } });

                if (dtSource.Rows.Count == 0 || !Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                {
                    result["success"] = false;
                    result["message"] = sourceCode + " ไม่ได้เปิดใช้งาน";
                    return result;
                }

                string keyword = GetSearchKeyword(sourceCode);
                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                string searchUrl = null;
                string apiKey = null;

                if (!string.IsNullOrEmpty(apiConfigJson))
                {
                    var config = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                    if (config != null)
                    {
                        if (config.ContainsKey("searchUrl"))
                            searchUrl = config["searchUrl"]?.ToString();
                        if (config.ContainsKey("apiKey"))
                            apiKey = config["apiKey"]?.ToString();
                    }
                }

                // Platform-specific default search URLs
                if (string.IsNullOrEmpty(searchUrl))
                {
                    string encoded = Uri.EscapeDataString(keyword);
                    switch (sourceCode)
                    {
                        case "TIKTOK":
                            searchUrl = "https://www.tiktok.com/search?q=" + encoded;
                            break;
                        case "LEMON8":
                            searchUrl = "https://www.lemon8-app.com/search?q=" + encoded;
                            break;
                        case "TWITTER":
                            searchUrl = "https://x.com/search?q=" + encoded + "&f=live";
                            break;
                        case "INSTAGRAM":
                            searchUrl = "https://www.instagram.com/explore/tags/" + keyword.Replace(" ", "").ToLower() + "/";
                            break;
                        case "YOUTUBE":
                            // YouTube Data API if apiKey provided, otherwise search page
                            if (!string.IsNullOrEmpty(apiKey))
                                searchUrl = string.Format(
                                    "https://www.googleapis.com/youtube/v3/search?part=snippet&q={0}&type=video&maxResults=20&key={1}",
                                    encoded, Uri.EscapeDataString(apiKey));
                            else
                                searchUrl = "https://www.youtube.com/results?search_query=" + encoded;
                            break;
                        default:
                            result["success"] = false;
                            result["message"] = "ไม่รู้จักแพลตฟอร์ม: " + sourceCode;
                            return result;
                    }
                }

                // YouTube Data API has JSON response
                if (sourceCode == "YOUTUBE" && !string.IsNullOrEmpty(apiKey))
                {
                    return FetchYouTubeViaAPI(apiKey, keyword);
                }

                // For all other social platforms: fetch HTML, AI parses
                string html = MakeHttpGetRequest(searchUrl);
                if (string.IsNullOrEmpty(html) || html.Length < 200)
                {
                    result["success"] = false;
                    result["message"] = "ไม่สามารถดึงข้อมูลจาก " + sourceCode + " ได้ (อาจถูก block หรือต้องตั้ง searchUrl)";
                    return result;
                }

                string platformHint = GetSocialPlatformHint(sourceCode);
                string prompt = BuildSocialScrapingPrompt(sourceCode, keyword, html, platformHint);

                var parsed = CallAIAndParseReviews(prompt, sourceCode);
                if (parsed == null)
                {
                    result["success"] = false;
                    result["message"] = "AI ไม่พบโพสต์/รีวิวเกี่ยวกับโรงแรมใน " + sourceCode;
                    return result;
                }

                foreach (var r in parsed)
                {
                    int added = InsertReview(sourceCode, r.PlatformId, r.ReviewerName, null,
                        r.Rating, r.ReviewTitle, r.ReviewText, r.ReviewDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata(sourceCode);
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("{0}: เพิ่มใหม่ {1} รายการ, ข้ามซ้ำ {2} รายการ", sourceCode, newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = sourceCode + " error: " + ex.Message;
            }
            return result;
        }

        /// <summary>
        /// Fetch YouTube video comments mentioning the hotel via YouTube Data API v3
        /// </summary>
        private Dictionary<string, object> FetchYouTubeViaAPI(string apiKey, string keyword)
        {
            int newCount = 0, skipCount = 0;
            var result = new Dictionary<string, object> { { "source", "YOUTUBE" } };

            try
            {
                // Search for videos about the hotel
                string searchUrl = string.Format(
                    "https://www.googleapis.com/youtube/v3/search?part=snippet&q={0}+รีวิว+โรงแรม&type=video&maxResults=10&key={1}",
                    Uri.EscapeDataString(keyword), Uri.EscapeDataString(apiKey));

                string searchResponse = MakeHttpGetRequest(searchUrl);
                if (string.IsNullOrEmpty(searchResponse))
                {
                    result["success"] = false;
                    result["message"] = "YouTube API search ไม่สำเร็จ";
                    return result;
                }

                var searchData = _serializer.Deserialize<Dictionary<string, object>>(searchResponse);
                if (searchData == null || !searchData.ContainsKey("items"))
                {
                    result["success"] = false;
                    result["message"] = "YouTube: ไม่พบวิดีโอ";
                    return result;
                }

                var items = searchData["items"] as ArrayList;
                if (items == null || items.Count == 0)
                {
                    result["success"] = false;
                    result["message"] = "YouTube: ไม่พบวิดีโอเกี่ยวกับโรงแรม";
                    return result;
                }

                foreach (var itemObj in items)
                {
                    var item = itemObj as Dictionary<string, object>;
                    if (item == null) continue;

                    var idObj = item.ContainsKey("id") ? item["id"] as Dictionary<string, object> : null;
                    var snippet = item.ContainsKey("snippet") ? item["snippet"] as Dictionary<string, object> : null;
                    if (idObj == null || snippet == null) continue;

                    string videoId = idObj.ContainsKey("videoId") ? idObj["videoId"]?.ToString() : null;
                    if (string.IsNullOrEmpty(videoId)) continue;

                    string channelTitle = snippet.ContainsKey("channelTitle") ? snippet["channelTitle"]?.ToString() : "Unknown";
                    string videoTitle = snippet.ContainsKey("title") ? snippet["title"]?.ToString() : "";
                    string description = snippet.ContainsKey("description") ? snippet["description"]?.ToString() : "";
                    string publishedAt = snippet.ContainsKey("publishedAt") ? snippet["publishedAt"]?.ToString() : null;

                    DateTime videoDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(publishedAt))
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(publishedAt, out parsed))
                            videoDate = parsed;
                    }

                    string platformReviewId = "YT_VIDEO_" + videoId;
                    string reviewText = videoTitle + "\n" + description;

                    int added = InsertReview("YOUTUBE", platformReviewId, channelTitle, null,
                        0, videoTitle, reviewText, videoDate);
                    if (added > 0) newCount++; else skipCount++;
                }

                UpdateSourceMetadata("YOUTUBE");
                result["success"] = true;
                result["newCount"] = newCount;
                result["skipCount"] = skipCount;
                result["message"] = string.Format("YouTube: เพิ่มใหม่ {0} วิดีโอ, ข้ามซ้ำ {1}", newCount, skipCount);
            }
            catch (Exception ex)
            {
                result["success"] = false;
                result["message"] = "YouTube API error: " + ex.Message;
            }
            return result;
        }

        // ── Social scraping helpers ──

        private string BuildSocialScrapingPrompt(string platform, string keyword, string html, string platformHint)
        {
            if (html.Length > 15000)
                html = html.Substring(0, 15000);

            return string.Format(
                @"จาก HTML/เนื้อหาของหน้าค้นหา ""{0}"" บนแพลตฟอร์ม {1}
ให้ดึงโพสต์/ความคิดเห็น/รีวิวทั้งหมดที่เกี่ยวกับโรงแรมนี้

{2}

ตอบเป็น JSON array เท่านั้น:
[
  {{
    ""reviewerName"": ""ชื่อผู้โพสต์/username"",
    ""rating"": 4.0,
    ""reviewText"": ""เนื้อหาโพสต์/ความคิดเห็น"",
    ""reviewDate"": ""2024-06-15"",
    ""reviewTitle"": ""หัวข้อ หรือ null""
  }}
]

กฎ:
- rating 1-5 (ถ้าไม่มีคะแนน ให้ประเมินจากน้ำเสียง: ชม/แนะนำ=4-5, กลางๆ=3, บ่น/ด่า=1-2, ไม่ระบุ=0)
- reviewDate ใช้ yyyy-MM-dd ถ้าเห็น ""3 วันที่แล้ว"" ให้คำนวณจาก {3}
- เฉพาะโพสต์ที่เกี่ยวกับโรงแรม ""{0}"" เท่านั้น ข้ามโพสต์ไม่เกี่ยวข้อง
- ถ้าไม่พบเลย ตอบ []

HTML:
{4}", keyword, platform, platformHint, DateTime.Now.ToString("yyyy-MM-dd"), html);
        }

        private string GetSocialPlatformHint(string sourceCode)
        {
            switch (sourceCode)
            {
                case "TIKTOK":
                    return "TikTok: ดึงจาก caption ของวิดีโอ และ comments ถ้ามี ใช้ username เป็น reviewerName";
                case "LEMON8":
                    return "Lemon8: ดึงจากโพสต์รีวิว ใช้ username เป็น reviewerName มักมี rating ในเนื้อหา";
                case "TWITTER":
                    return "X/Twitter: ดึง tweets ที่กล่าวถึงโรงแรม ใช้ @username เป็น reviewerName tweet text เป็น reviewText";
                case "INSTAGRAM":
                    return "Instagram: ดึงจาก caption ของโพสต์ ใช้ username เป็น reviewerName";
                case "YOUTUBE":
                    return "YouTube: ดึง title + description ของวิดีโอรีวิว ใช้ชื่อ channel เป็น reviewerName";
                default:
                    return "";
            }
        }

        private class ParsedReview
        {
            public string PlatformId;
            public string ReviewerName;
            public double Rating;
            public string ReviewTitle;
            public string ReviewText;
            public DateTime ReviewDate;
        }

        private List<ParsedReview> CallAIAndParseReviews(string prompt, string sourceCode)
        {
            var deepSeek = new DeepSeekService(_connectionString);
            string sessionKey = "social_scrape_" + sourceCode + "_" + DateTime.Now.Ticks;
            var aiResponse = deepSeek.SendMessage(prompt, sessionKey, null);

            if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.Message))
                return null;

            string jsonArray = ExtractJsonArrayFromResponse(aiResponse.Message);
            if (string.IsNullOrEmpty(jsonArray))
                return null;

            var reviews = _serializer.Deserialize<ArrayList>(jsonArray);
            if (reviews == null || reviews.Count == 0)
                return null;

            var results = new List<ParsedReview>();
            foreach (var reviewObj in reviews)
            {
                var review = reviewObj as Dictionary<string, object>;
                if (review == null) continue;

                string reviewerName = review.ContainsKey("reviewerName") ? review["reviewerName"]?.ToString() : "Unknown";
                double rating = review.ContainsKey("rating") ? Convert.ToDouble(review["rating"]) : 0;
                string reviewText = review.ContainsKey("reviewText") ? review["reviewText"]?.ToString() : "";
                string reviewTitle = review.ContainsKey("reviewTitle") ? review["reviewTitle"]?.ToString() : null;

                DateTime reviewDate = DateTime.Now;
                if (review.ContainsKey("reviewDate") && review["reviewDate"] != null)
                {
                    DateTime parsed;
                    if (DateTime.TryParse(review["reviewDate"].ToString(), out parsed))
                        reviewDate = parsed;
                }

                if (string.IsNullOrEmpty(reviewText) && string.IsNullOrEmpty(reviewTitle))
                    continue;

                string platformId = sourceCode + "_" + reviewerName.GetHashCode()
                    + "_" + reviewDate.ToString("yyyyMMdd") + "_" + ((int)(rating * 10));

                results.Add(new ParsedReview
                {
                    PlatformId = platformId,
                    ReviewerName = reviewerName,
                    Rating = rating,
                    ReviewTitle = reviewTitle,
                    ReviewText = reviewText,
                    ReviewDate = reviewDate
                });
            }

            return results.Count > 0 ? results : null;
        }

        private string MakeHttpGetRequest(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Timeout = 30000;
                request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36";

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        return null;
                }
                return null;
            }
        }

        #endregion

        #region 2. Sentiment Analysis (DeepSeek)

        /// <summary>
        /// Analyze sentiment for a single review using DeepSeek AI
        /// </summary>
        public bool AnalyzeReviewSentiment(long reviewId, string sourceType)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceType))
                    throw new ArgumentNullException(nameof(sourceType));

                string reviewText = null;

                // Fetch review text based on source type
                if (sourceType == "INTERNAL")
                {
                    DataTable dtReview = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT ReviewText FROM Guest_Reviews WHERE ID = @Id",
                        new Dictionary<string, object> { { "@Id", reviewId } });

                    if (dtReview.Rows.Count == 0)
                        return false;

                    reviewText = dtReview.Rows[0]["ReviewText"]?.ToString();
                }
                else if (sourceType == "ONLINE")
                {
                    DataTable dtReview = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT ReviewText FROM AI_Online_Reviews WHERE ID = @Id",
                        new Dictionary<string, object> { { "@Id", reviewId } });

                    if (dtReview.Rows.Count == 0)
                        return false;

                    reviewText = dtReview.Rows[0]["ReviewText"]?.ToString();
                }
                else
                {
                    throw new ArgumentException("Invalid sourceType. Must be 'INTERNAL' or 'ONLINE'.");
                }

                if (string.IsNullOrEmpty(reviewText))
                    return false;

                // Build the analysis prompt
                string prompt = string.Format(
                    @"วิเคราะห์รีวิวของโรงแรมนี้:
""{0}""

ตอบในรูปแบบ JSON เท่านั้น:
{{
  ""sentiment"": ""POSITIVE"" หรือ ""NEGATIVE"" หรือ ""NEUTRAL"" หรือ ""MIXED"",
  ""score"": 0.0 ถึง 1.0 (1.0 = บวกมาก),
  ""topics"": [""cleanliness"",""service"",""location"",""value"",""food"",""facilities"",""wifi"",""parking"",""noise"",""staff""],
  ""topicScores"": {{""cleanliness"": 0.8, ""service"": 0.9}},
  ""summary"": ""สรุปสั้นๆ 1-2 ประโยค"",
  ""suggestedResponse"": ""ข้อความตอบกลับที่เหมาะสม สุภาพ เป็นภาษาไทย""
}}", reviewText);

                // Call DeepSeek
                var deepSeek = new DeepSeekService(_connectionString);
                string sessionKey = "review_analysis_" + sourceType + "_" + reviewId + "_" + DateTime.Now.Ticks;
                var aiResponse = deepSeek.SendMessage(prompt, sessionKey, null);

                if (!aiResponse.Success || string.IsNullOrEmpty(aiResponse.Message))
                    return false;

                // Parse the JSON response from AI
                string jsonResponse = ExtractJsonFromResponse(aiResponse.Message);
                if (string.IsNullOrEmpty(jsonResponse))
                    return false;

                var analysisResult = _serializer.Deserialize<Dictionary<string, object>>(jsonResponse);
                if (analysisResult == null)
                    return false;

                string sentiment = analysisResult.ContainsKey("sentiment") ? analysisResult["sentiment"]?.ToString() : "NEUTRAL";
                double score = analysisResult.ContainsKey("score") ? Convert.ToDouble(analysisResult["score"]) : 0.5;
                string summary = analysisResult.ContainsKey("summary") ? analysisResult["summary"]?.ToString() : "";
                string suggestedResponse = analysisResult.ContainsKey("suggestedResponse") ? analysisResult["suggestedResponse"]?.ToString() : "";

                // Serialize topics
                string topicsJson = "";
                if (analysisResult.ContainsKey("topics"))
                {
                    topicsJson = _serializer.Serialize(analysisResult["topics"]);
                }

                string topicScoresJson = "";
                if (analysisResult.ContainsKey("topicScores"))
                {
                    topicScoresJson = _serializer.Serialize(analysisResult["topicScores"]);
                }

                // Update the appropriate table
                if (sourceType == "INTERNAL")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Reviews
                          SET Sentiment = @Sentiment, SentimentScore = @Score, AITopics = @Topics,
                              AITopicScores = @TopicScores, AISummary = @Summary,
                              AISuggestedResponse = @SuggestedResp, IsAIAnalyzed = 1
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Sentiment", sentiment },
                            { "@Score", score },
                            { "@Topics", topicsJson ?? (object)DBNull.Value },
                            { "@TopicScores", topicScoresJson ?? (object)DBNull.Value },
                            { "@Summary", summary ?? (object)DBNull.Value },
                            { "@SuggestedResp", suggestedResponse ?? (object)DBNull.Value }
                        });
                }
                else // ONLINE
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE AI_Online_Reviews
                          SET Sentiment = @Sentiment, SentimentScore = @Score, Topics = @Topics,
                              TopicScores = @TopicScores, AISummary = @Summary,
                              SuggestedResponse = @SuggestedResp, IsAnalyzed = 1
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Sentiment", sentiment },
                            { "@Score", score },
                            { "@Topics", topicsJson ?? (object)DBNull.Value },
                            { "@TopicScores", topicScoresJson ?? (object)DBNull.Value },
                            { "@Summary", summary ?? (object)DBNull.Value },
                            { "@SuggestedResp", suggestedResponse ?? (object)DBNull.Value }
                        });
                }

                return true;
            }
            catch (Exception ex)
            {
                // Log the error but don't throw - allows batch processing to continue
                try
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO AI_Usage_Log (RequestDate, SessionKey, Model, PromptTokens, CompletionTokens, TotalTokens, ResponseTimeMs, Success, ErrorMessage)
                          VALUES (GETDATE(), @Key, 'review-analysis', 0, 0, 0, 0, 0, @Error)",
                        new Dictionary<string, object>
                        {
                            { "@Key", "review_error_" + reviewId },
                            { "@Error", ex.Message }
                        });
                }
                catch { }
                return false;
            }
        }

        /// <summary>
        /// Analyze all pending reviews from both internal and online sources (limit 20)
        /// </summary>
        public int AnalyzeAllPending()
        {
            int analyzed = 0;
            try
            {
                // Fetch pending internal reviews
                DataTable dtInternal = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 20 ID FROM Guest_Reviews
                      WHERE (IsAIAnalyzed = 0 OR IsAIAnalyzed IS NULL) AND ReviewText IS NOT NULL
                      ORDER BY SubmittedDate DESC",
                    null);

                foreach (DataRow row in dtInternal.Rows)
                {
                    long id = Convert.ToInt64(row["ID"]);
                    if (AnalyzeReviewSentiment(id, "INTERNAL"))
                        analyzed++;

                    if (analyzed >= 20) return analyzed;
                }

                // Fetch pending online reviews
                int remaining = 20 - analyzed;
                if (remaining <= 0) return analyzed;

                DataTable dtOnline = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@Limit) ID FROM AI_Online_Reviews
                      WHERE IsAnalyzed = 0 AND ReviewText IS NOT NULL
                      ORDER BY ReviewDate DESC",
                    new Dictionary<string, object> { { "@Limit", remaining } });

                foreach (DataRow row in dtOnline.Rows)
                {
                    long id = Convert.ToInt64(row["ID"]);
                    if (AnalyzeReviewSentiment(id, "ONLINE"))
                        analyzed++;

                    if (analyzed >= 20) break;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error analyzing pending reviews: " + ex.Message);
            }

            return analyzed;
        }

        /// <summary>
        /// Batch analyze internal (Guest_Reviews) reviews that haven't been AI analyzed
        /// </summary>
        public int BatchAnalyzeInternalReviews()
        {
            int analyzed = 0;
            try
            {
                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 20 ID FROM Guest_Reviews
                      WHERE (IsAIAnalyzed = 0 OR IsAIAnalyzed IS NULL) AND ReviewText IS NOT NULL
                      ORDER BY SubmittedDate DESC",
                    null);

                foreach (DataRow row in dtReviews.Rows)
                {
                    long id = Convert.ToInt64(row["ID"]);
                    if (AnalyzeReviewSentiment(id, "INTERNAL"))
                        analyzed++;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error batch analyzing internal reviews: " + ex.Message);
            }

            return analyzed;
        }

        /// <summary>
        /// Extract JSON content from an AI response that may contain extra text
        /// </summary>
        private string ExtractJsonFromResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            int startIdx = response.IndexOf('{');
            int endIdx = response.LastIndexOf('}');

            if (startIdx >= 0 && endIdx > startIdx)
                return response.Substring(startIdx, endIdx - startIdx + 1);

            return null;
        }

        private string ExtractJsonArrayFromResponse(string response)
        {
            if (string.IsNullOrEmpty(response))
                return null;

            int startIdx = response.IndexOf('[');
            int endIdx = response.LastIndexOf(']');

            if (startIdx >= 0 && endIdx > startIdx)
                return response.Substring(startIdx, endIdx - startIdx + 1);

            return null;
        }

        #endregion

        #region 3. Dashboard Analytics

        /// <summary>
        /// Get comprehensive review dashboard data
        /// </summary>
        public Dictionary<string, object> GetReviewDashboardData()
        {
            var dashboard = new Dictionary<string, object>();
            try
            {
                // Total reviews and average rating from the unified view
                DataTable dtTotal = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        COUNT(*) AS TotalReviews,
                        ISNULL(AVG(CAST(Rating AS FLOAT)), 0) AS AvgRating
                      FROM vw_AI_Review_AllSources",
                    null);

                if (dtTotal.Rows.Count > 0)
                {
                    dashboard["totalReviews"] = Convert.ToInt32(dtTotal.Rows[0]["TotalReviews"]);
                    dashboard["avgRating"] = Math.Round(Convert.ToDouble(dtTotal.Rows[0]["AvgRating"]), 2);
                }
                else
                {
                    dashboard["totalReviews"] = 0;
                    dashboard["avgRating"] = 0.0;
                }

                // Sentiment breakdown
                DataTable dtSentiment = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        SUM(CASE WHEN Sentiment = 'POSITIVE' THEN 1 ELSE 0 END) AS Positive,
                        SUM(CASE WHEN Sentiment = 'NEGATIVE' THEN 1 ELSE 0 END) AS Negative,
                        SUM(CASE WHEN Sentiment = 'NEUTRAL' THEN 1 ELSE 0 END) AS Neutral,
                        SUM(CASE WHEN Sentiment = 'MIXED' THEN 1 ELSE 0 END) AS Mixed
                      FROM vw_AI_Review_AllSources
                      WHERE Sentiment IS NOT NULL",
                    null);

                if (dtSentiment.Rows.Count > 0)
                {
                    var sentimentBreakdown = new Dictionary<string, object>
                    {
                        { "positive", Convert.ToInt32(dtSentiment.Rows[0]["Positive"]) },
                        { "negative", Convert.ToInt32(dtSentiment.Rows[0]["Negative"]) },
                        { "neutral", Convert.ToInt32(dtSentiment.Rows[0]["Neutral"]) },
                        { "mixed", Convert.ToInt32(dtSentiment.Rows[0]["Mixed"]) }
                    };
                    dashboard["sentimentBreakdown"] = sentimentBreakdown;
                }
                else
                {
                    dashboard["sentimentBreakdown"] = new Dictionary<string, object>
                    {
                        { "positive", 0 }, { "negative", 0 }, { "neutral", 0 }, { "mixed", 0 }
                    };
                }

                // Source breakdown
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        SourceCode AS Source,
                        COUNT(*) AS ReviewCount,
                        ISNULL(AVG(CAST(Rating AS FLOAT)), 0) AS AvgRating
                      FROM vw_AI_Review_AllSources
                      GROUP BY SourceCode
                      ORDER BY ReviewCount DESC",
                    null);

                var sourceBreakdown = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtSource.Rows)
                {
                    sourceBreakdown.Add(new Dictionary<string, object>
                    {
                        { "source", row["Source"].ToString() },
                        { "count", Convert.ToInt32(row["ReviewCount"]) },
                        { "avgRating", Math.Round(Convert.ToDouble(row["AvgRating"]), 2) }
                    });
                }
                dashboard["sourceBreakdown"] = sourceBreakdown;

                // Topic breakdown - aggregate Topics fields from all analyzed reviews
                dashboard["topicBreakdown"] = GetTopicAnalysis();

                // Recent 20 reviews with sentiment
                DataTable dtRecent = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 20
                        ID, SourceCode, ReviewerName, Rating, ReviewText, Sentiment,
                        SentimentScore, AISummary, ReviewDate
                      FROM vw_AI_Review_AllSources
                      ORDER BY ReviewDate DESC",
                    null);

                var recentReviews = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtRecent.Rows)
                {
                    recentReviews.Add(new Dictionary<string, object>
                    {
                        { "id", Convert.ToInt64(row["ID"]) },
                        { "source", row["SourceCode"].ToString() },
                        { "reviewerName", row["ReviewerName"]?.ToString() ?? "" },
                        { "rating", row["Rating"] != DBNull.Value ? Math.Round(Convert.ToDouble(row["Rating"]), 1) : 0.0 },
                        { "reviewText", row["ReviewText"]?.ToString() ?? "" },
                        { "sentiment", row["Sentiment"]?.ToString() ?? "" },
                        { "sentimentScore", row["SentimentScore"] != DBNull.Value ? Convert.ToDouble(row["SentimentScore"]) : 0.0 },
                        { "summary", row["AISummary"]?.ToString() ?? "" },
                        { "reviewDate", row["ReviewDate"] != DBNull.Value ? Convert.ToDateTime(row["ReviewDate"]).ToString("dd/MM/yyyy") : "" }
                    });
                }
                dashboard["recentReviews"] = recentReviews;

                // Rating distribution
                DataTable dtRating = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        SUM(CASE WHEN Rating = 5 THEN 1 ELSE 0 END) AS Star5,
                        SUM(CASE WHEN Rating = 4 THEN 1 ELSE 0 END) AS Star4,
                        SUM(CASE WHEN Rating = 3 THEN 1 ELSE 0 END) AS Star3,
                        SUM(CASE WHEN Rating = 2 THEN 1 ELSE 0 END) AS Star2,
                        SUM(CASE WHEN Rating = 1 THEN 1 ELSE 0 END) AS Star1
                      FROM vw_AI_Review_AllSources",
                    null);

                if (dtRating.Rows.Count > 0)
                {
                    dashboard["ratingDistribution"] = new Dictionary<string, object>
                    {
                        { "5", Convert.ToInt32(dtRating.Rows[0]["Star5"]) },
                        { "4", Convert.ToInt32(dtRating.Rows[0]["Star4"]) },
                        { "3", Convert.ToInt32(dtRating.Rows[0]["Star3"]) },
                        { "2", Convert.ToInt32(dtRating.Rows[0]["Star2"]) },
                        { "1", Convert.ToInt32(dtRating.Rows[0]["Star1"]) }
                    };
                }
                else
                {
                    dashboard["ratingDistribution"] = new Dictionary<string, object>
                    {
                        { "5", 0 }, { "4", 0 }, { "3", 0 }, { "2", 0 }, { "1", 0 }
                    };
                }

                // Monthly trend - last 12 months
                DataTable dtMonthly = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        FORMAT(ReviewDate, 'yyyy-MM') AS Month,
                        COUNT(*) AS ReviewCount,
                        ISNULL(AVG(CAST(Rating AS FLOAT)), 0) AS AvgRating
                      FROM vw_AI_Review_AllSources
                      WHERE ReviewDate >= DATEADD(MONTH, -12, GETDATE())
                      GROUP BY FORMAT(ReviewDate, 'yyyy-MM')
                      ORDER BY Month ASC",
                    null);

                var monthlyTrend = new List<Dictionary<string, object>>();
                foreach (DataRow row in dtMonthly.Rows)
                {
                    monthlyTrend.Add(new Dictionary<string, object>
                    {
                        { "month", row["Month"].ToString() },
                        { "count", Convert.ToInt32(row["ReviewCount"]) },
                        { "avgRating", Math.Round(Convert.ToDouble(row["AvgRating"]), 2) }
                    });
                }
                dashboard["monthlyTrend"] = monthlyTrend;

                // Flagged count
                DataTable dtFlagged = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS FlaggedCount
                      FROM vw_AI_Review_AllSources
                      WHERE IsFlagged = 1 OR Sentiment = 'NEGATIVE'",
                    null);

                dashboard["flaggedCount"] = dtFlagged.Rows.Count > 0
                    ? Convert.ToInt32(dtFlagged.Rows[0]["FlaggedCount"])
                    : 0;

                // Pending response count
                DataTable dtPending = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT COUNT(*) AS PendingCount
                      FROM vw_AI_Review_AllSources
                      WHERE ResponseStatus = 'PENDING'",
                    null);

                dashboard["pendingResponseCount"] = dtPending.Rows.Count > 0
                    ? Convert.ToInt32(dtPending.Rows[0]["PendingCount"])
                    : 0;
            }
            catch (Exception ex)
            {
                dashboard["error"] = ex.Message;
            }

            return dashboard;
        }

        /// <summary>
        /// Get reviews filtered by various criteria
        /// </summary>
        public List<Dictionary<string, object>> GetReviewsByFilter(string source, string sentiment, int? minRating, int? maxRating, string search, int limit)
        {
            var reviews = new List<Dictionary<string, object>>();
            try
            {
                if (limit <= 0) limit = 50;
                if (limit > 500) limit = 500;

                string sql = @"SELECT TOP (@Limit)
                    ID, SourceCode, ReviewerName, Rating, ReviewText, ReviewTitle, Sentiment,
                    SentimentScore, AISummary, SuggestedResponse, ActualResponse, ResponseStatus,
                    IsFlagged, FlagReason, ReviewDate
                  FROM vw_AI_Review_AllSources
                  WHERE 1=1";

                var parameters = new Dictionary<string, object>
                {
                    { "@Limit", limit }
                };

                if (!string.IsNullOrEmpty(source))
                {
                    sql += " AND SourceCode = @Source";
                    parameters["@Source"] = source;
                }

                if (!string.IsNullOrEmpty(sentiment))
                {
                    sql += " AND Sentiment = @Sentiment";
                    parameters["@Sentiment"] = sentiment;
                }

                if (minRating.HasValue)
                {
                    sql += " AND Rating >= @MinRating";
                    parameters["@MinRating"] = minRating.Value;
                }

                if (maxRating.HasValue)
                {
                    sql += " AND Rating <= @MaxRating";
                    parameters["@MaxRating"] = maxRating.Value;
                }

                if (!string.IsNullOrEmpty(search))
                {
                    sql += " AND (ReviewText LIKE @Search OR ReviewerName LIKE @Search OR AISummary LIKE @Search)";
                    parameters["@Search"] = "%" + search + "%";
                }

                sql += " ORDER BY ReviewDate DESC";

                DataTable dt = _code.DatabaseQuerySafe(_connectionString, sql, parameters);

                foreach (DataRow row in dt.Rows)
                {
                    reviews.Add(new Dictionary<string, object>
                    {
                        { "id", Convert.ToInt64(row["ID"]) },
                        { "source", row["SourceCode"]?.ToString() ?? "" },
                        { "reviewerName", row["ReviewerName"]?.ToString() ?? "" },
                        { "rating", row["Rating"] != DBNull.Value ? Math.Round(Convert.ToDouble(row["Rating"]), 1) : 0.0 },
                        { "reviewText", row["ReviewText"]?.ToString() ?? "" },
                        { "reviewTitle", row["ReviewTitle"]?.ToString() ?? "" },
                        { "sentiment", row["Sentiment"]?.ToString() ?? "" },
                        { "sentimentScore", row["SentimentScore"] != DBNull.Value ? Convert.ToDouble(row["SentimentScore"]) : 0.0 },
                        { "summary", row["AISummary"]?.ToString() ?? "" },
                        { "suggestedResponse", row["SuggestedResponse"]?.ToString() ?? "" },
                        { "actualResponse", row["ActualResponse"]?.ToString() ?? "" },
                        { "responseStatus", row["ResponseStatus"]?.ToString() ?? "" },
                        { "isFlagged", row["IsFlagged"] != DBNull.Value && Convert.ToBoolean(row["IsFlagged"]) },
                        { "flagReason", row["FlagReason"]?.ToString() ?? "" },
                        { "reviewDate", row["ReviewDate"] != DBNull.Value ? Convert.ToDateTime(row["ReviewDate"]).ToString("dd/MM/yyyy") : "" }
                    });
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error filtering reviews: " + ex.Message);
            }

            return reviews;
        }

        /// <summary>
        /// Aggregate all Topics fields and count occurrences of each topic, sorted by frequency
        /// </summary>
        public List<Dictionary<string, object>> GetTopicAnalysis()
        {
            var topicCounts = new Dictionary<string, int>();
            try
            {
                // Gather topics from internal reviews
                DataTable dtInternal = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT AITopics FROM Guest_Reviews WHERE IsAIAnalyzed = 1 AND AITopics IS NOT NULL",
                    null);

                foreach (DataRow row in dtInternal.Rows)
                {
                    ParseAndCountTopics(row["AITopics"]?.ToString(), topicCounts);
                }

                // Gather topics from online reviews
                DataTable dtOnline = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Topics FROM AI_Online_Reviews WHERE IsAnalyzed = 1 AND Topics IS NOT NULL",
                    null);

                foreach (DataRow row in dtOnline.Rows)
                {
                    ParseAndCountTopics(row["Topics"]?.ToString(), topicCounts);
                }
            }
            catch { }

            // Sort by frequency descending
            var result = new List<Dictionary<string, object>>();
            foreach (var kvp in topicCounts.OrderByDescending(k => k.Value))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "topic", kvp.Key },
                    { "count", kvp.Value }
                });
            }

            return result;
        }

        /// <summary>
        /// Parse a JSON array of topics and add to the counts dictionary
        /// </summary>
        private void ParseAndCountTopics(string topicsJson, Dictionary<string, int> topicCounts)
        {
            if (string.IsNullOrEmpty(topicsJson)) return;

            try
            {
                var topics = _serializer.Deserialize<ArrayList>(topicsJson);
                if (topics == null) return;

                foreach (var topic in topics)
                {
                    string topicStr = topic?.ToString()?.Trim().ToLower();
                    if (string.IsNullOrEmpty(topicStr)) continue;

                    if (topicCounts.ContainsKey(topicStr))
                        topicCounts[topicStr]++;
                    else
                        topicCounts[topicStr] = 1;
                }
            }
            catch { }
        }

        #endregion

        #region 4. Response Management

        /// <summary>
        /// Apply AI-suggested response to a review
        /// </summary>
        public bool ApplyAISuggestedResponse(long reviewId, string sourceType)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceType))
                    throw new ArgumentNullException(nameof(sourceType));

                if (sourceType == "INTERNAL")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Reviews
                          SET ResponseText = AISuggestedResponse,
                              ResponseDate = GETDATE()
                          WHERE ID = @Id AND AISuggestedResponse IS NOT NULL",
                        new Dictionary<string, object> { { "@Id", reviewId } });
                }
                else if (sourceType == "ONLINE")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE AI_Online_Reviews
                          SET ActualResponse = SuggestedResponse,
                              ResponseStatus = 'RESPONDED',
                              RespondedDate = GETDATE()
                          WHERE ID = @Id AND SuggestedResponse IS NOT NULL",
                        new Dictionary<string, object> { { "@Id", reviewId } });
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Save a custom response to a review
        /// </summary>
        public bool SaveCustomResponse(long reviewId, string sourceType, string response, string respondedBy)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceType))
                    throw new ArgumentNullException(nameof(sourceType));
                if (string.IsNullOrEmpty(response))
                    throw new ArgumentNullException(nameof(response));

                if (sourceType == "INTERNAL")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Reviews
                          SET ResponseText = @Response,
                              ResponseDate = GETDATE()
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Response", response }
                        });
                }
                else if (sourceType == "ONLINE")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE AI_Online_Reviews
                          SET ActualResponse = @Response,
                              ResponseStatus = 'RESPONDED',
                              RespondedDate = GETDATE(),
                              RespondedBy = @RespondedBy
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Response", response },
                            { "@RespondedBy", respondedBy ?? (object)DBNull.Value }
                        });
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Flag a review for attention
        /// </summary>
        public bool FlagReview(long reviewId, string sourceType, string reason)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceType))
                    throw new ArgumentNullException(nameof(sourceType));

                if (sourceType == "INTERNAL")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE Guest_Reviews
                          SET IsFlagged = 1, FlagReason = @Reason
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Reason", reason ?? (object)DBNull.Value }
                        });
                }
                else if (sourceType == "ONLINE")
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"UPDATE AI_Online_Reviews
                          SET IsFlagged = 1, FlagReason = @Reason
                          WHERE ID = @Id",
                        new Dictionary<string, object>
                        {
                            { "@Id", reviewId },
                            { "@Reason", reason ?? (object)DBNull.Value }
                        });
                }
                else
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region 5. Summary Generation

        /// <summary>
        /// Generate an AI-powered review summary for a given period
        /// </summary>
        public string GenerateReviewSummary(string period)
        {
            try
            {
                if (string.IsNullOrEmpty(period))
                    throw new ArgumentNullException(nameof(period));

                // Determine date range based on period
                DateTime startDate;
                string periodLabel;

                switch (period.ToUpper())
                {
                    case "WEEK":
                        startDate = DateTime.Now.AddDays(-7);
                        periodLabel = "สัปดาห์ที่ผ่านมา";
                        break;
                    case "MONTH":
                        startDate = DateTime.Now.AddMonths(-1);
                        periodLabel = "เดือนที่ผ่านมา";
                        break;
                    case "QUARTER":
                        startDate = DateTime.Now.AddMonths(-3);
                        periodLabel = "ไตรมาสที่ผ่านมา";
                        break;
                    default:
                        throw new ArgumentException("Invalid period. Must be 'WEEK', 'MONTH', or 'QUARTER'.");
                }

                // Gather all reviews in the period from the unified view
                DataTable dtReviews = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 100
                        SourceCode, ReviewerName, Rating, ReviewText, Sentiment, SentimentScore,
                        AISummary, ReviewDate
                      FROM vw_AI_Review_AllSources
                      WHERE ReviewDate >= @StartDate
                      ORDER BY ReviewDate DESC",
                    new Dictionary<string, object> { { "@StartDate", startDate } });

                if (dtReviews.Rows.Count == 0)
                    return "ไม่พบรีวิวในช่วง" + periodLabel;

                // Build review data string for the AI prompt
                var reviewData = new StringBuilder();
                int totalReviews = dtReviews.Rows.Count;
                double totalRating = 0;
                int ratingCount = 0;
                int positiveCount = 0, negativeCount = 0, neutralCount = 0;

                foreach (DataRow row in dtReviews.Rows)
                {
                    string source = row["SourceCode"]?.ToString() ?? "";
                    string reviewer = row["ReviewerName"]?.ToString() ?? "Anonymous";
                    int rating = row["Rating"] != DBNull.Value ? Convert.ToInt32(row["Rating"]) : 0;
                    string text = row["ReviewText"]?.ToString() ?? "";
                    string sentiment = row["Sentiment"]?.ToString() ?? "";

                    if (rating > 0) { totalRating += rating; ratingCount++; }
                    if (sentiment == "POSITIVE") positiveCount++;
                    else if (sentiment == "NEGATIVE") negativeCount++;
                    else neutralCount++;

                    // Truncate very long reviews for the prompt
                    if (text.Length > 200)
                        text = text.Substring(0, 200) + "...";

                    reviewData.AppendLine(string.Format("- [{0}] {1} ({2}/5): {3}", source, reviewer, rating, text));
                }

                double avgRating = ratingCount > 0 ? totalRating / ratingCount : 0;

                string prompt = string.Format(
                    @"สรุปภาพรวมรีวิวของโรงแรม TakeTime BangPhra ในช่วง{0}:

สถิติ:
- จำนวนรีวิวทั้งหมด: {1}
- คะแนนเฉลี่ย: {2:F1}/5
- รีวิวเชิงบวก: {3}, เชิงลบ: {4}, กลาง: {5}

ข้อมูลรีวิว:
{6}

กรุณาสรุปประเด็นหลัก จุดแข็ง จุดอ่อน และข้อเสนอแนะเพื่อปรับปรุงบริการ เขียนเป็นภาษาไทย",
                    periodLabel, totalReviews, avgRating, positiveCount, negativeCount, neutralCount,
                    reviewData.ToString());

                // Call DeepSeek for summary
                var deepSeek = new DeepSeekService(_connectionString);
                string sessionKey = "review_summary_" + period + "_" + DateTime.Now.Ticks;
                var aiResponse = deepSeek.SendMessage(prompt, sessionKey, null);

                if (aiResponse.Success && !string.IsNullOrEmpty(aiResponse.Message))
                {
                    return aiResponse.Message;
                }

                return "ไม่สามารถสร้างสรุปรีวิวได้ในขณะนี้: " + (aiResponse.Message ?? "Unknown error");
            }
            catch (Exception ex)
            {
                return "เกิดข้อผิดพลาดในการสร้างสรุปรีวิว: " + ex.Message;
            }
        }

        #endregion
    }
}
