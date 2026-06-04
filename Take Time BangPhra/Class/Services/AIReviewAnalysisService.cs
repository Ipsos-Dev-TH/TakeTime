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

        public AIReviewAnalysisService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
            _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        }

        #region 1. Online Review Fetching

        /// <summary>
        /// Get all configured review sources
        /// </summary>
        public DataTable GetReviewSources()
        {
            try
            {
                return _code.DatabaseQuerySafe(_connectionString,
                    "SELECT * FROM AI_Review_Sources ORDER BY SourceCode", null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error getting review sources: " + ex.Message);
            }
        }

        /// <summary>
        /// Update a review source configuration
        /// </summary>
        public void UpdateSourceConfig(string sourceCode, bool enabled, string apiConfig)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceCode))
                    throw new ArgumentNullException(nameof(sourceCode));

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
            catch (Exception ex)
            {
                throw new Exception("Error updating source config: " + ex.Message);
            }
        }

        /// <summary>
        /// Fetch reviews from Google Places API
        /// </summary>
        public int FetchGoogleReviews()
        {
            int newCount = 0;
            try
            {
                // Read config from AI_Review_Sources
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'GOOGLE'",
                    null);

                if (dtSource.Rows.Count == 0)
                    throw new Exception("Google review source not configured");

                if (!Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                    return 0;

                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                if (string.IsNullOrEmpty(apiConfigJson))
                    throw new Exception("Google API config is empty");

                var apiConfig = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                string placeId = apiConfig.ContainsKey("placeId") ? apiConfig["placeId"]?.ToString() : null;
                string apiKey = apiConfig.ContainsKey("apiKey") ? apiConfig["apiKey"]?.ToString() : null;

                if (string.IsNullOrEmpty(placeId) || string.IsNullOrEmpty(apiKey))
                    throw new Exception("Google placeId or apiKey is missing");

                // Call Google Places API
                string url = string.Format(
                    "https://maps.googleapis.com/maps/api/place/details/json?place_id={0}&fields=reviews&language=th&key={1}",
                    Uri.EscapeDataString(placeId), Uri.EscapeDataString(apiKey));

                string responseText = MakeHttpGetRequest(url);
                if (string.IsNullOrEmpty(responseText))
                    return 0;

                var response = _serializer.Deserialize<Dictionary<string, object>>(responseText);
                if (response == null || !response.ContainsKey("result"))
                    return 0;

                var result = response["result"] as Dictionary<string, object>;
                if (result == null || !result.ContainsKey("reviews"))
                    return 0;

                var reviews = result["reviews"] as ArrayList;
                if (reviews == null || reviews.Count == 0)
                    return 0;

                foreach (var reviewObj in reviews)
                {
                    var review = reviewObj as Dictionary<string, object>;
                    if (review == null) continue;

                    string authorName = review.ContainsKey("author_name") ? review["author_name"]?.ToString() : "Unknown";
                    int rating = review.ContainsKey("rating") ? Convert.ToInt32(review["rating"]) : 0;
                    string text = review.ContainsKey("text") ? review["text"]?.ToString() : "";
                    long timeEpoch = review.ContainsKey("time") ? Convert.ToInt64(review["time"]) : 0;
                    string profilePhoto = review.ContainsKey("profile_photo_url") ? review["profile_photo_url"]?.ToString() : null;

                    DateTime reviewDate = timeEpoch > 0
                        ? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(timeEpoch).ToLocalTime()
                        : DateTime.Now;

                    // Generate a platform-specific review ID
                    string platformReviewId = "GOOGLE_" + authorName.GetHashCode() + "_" + timeEpoch;

                    // Check if already exists
                    DataTable dtExisting = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT COUNT(*) AS Cnt FROM AI_Online_Reviews WHERE PlatformReviewId = @PlatformId",
                        new Dictionary<string, object> { { "@PlatformId", platformReviewId } });

                    if (dtExisting.Rows.Count > 0 && Convert.ToInt32(dtExisting.Rows[0]["Cnt"]) > 0)
                        continue;

                    // Insert new review
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO AI_Online_Reviews
                          (SourceCode, PlatformReviewId, ReviewerName, ReviewerAvatar, Rating, ReviewText,
                           ReviewDate, IsAnalyzed, IsFlagged, Created_Date)
                          VALUES
                          ('GOOGLE', @PlatformId, @Author, @Photo, @Rating, @Text,
                           @ReviewDate, 0, 0, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@PlatformId", platformReviewId },
                            { "@Author", authorName },
                            { "@Photo", profilePhoto ?? (object)DBNull.Value },
                            { "@Rating", rating },
                            { "@Text", text ?? (object)DBNull.Value },
                            { "@ReviewDate", reviewDate }
                        });

                    newCount++;
                }

                // Update source metadata
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE AI_Review_Sources
                      SET LastFetchDate = GETDATE(),
                          TotalReviews = (SELECT COUNT(*) FROM AI_Online_Reviews WHERE SourceCode = 'GOOGLE')
                      WHERE SourceCode = 'GOOGLE'",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching Google reviews: " + ex.Message);
            }

            return newCount;
        }

        /// <summary>
        /// Fetch reviews from Facebook Graph API
        /// </summary>
        public int FetchFacebookReviews()
        {
            int newCount = 0;
            try
            {
                // Read config from AI_Review_Sources
                DataTable dtSource = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ApiConfig, IsEnabled FROM AI_Review_Sources WHERE SourceCode = 'FACEBOOK'",
                    null);

                if (dtSource.Rows.Count == 0)
                    throw new Exception("Facebook review source not configured");

                if (!Convert.ToBoolean(dtSource.Rows[0]["IsEnabled"]))
                    return 0;

                string apiConfigJson = dtSource.Rows[0]["ApiConfig"]?.ToString();
                if (string.IsNullOrEmpty(apiConfigJson))
                    throw new Exception("Facebook API config is empty");

                var apiConfig = _serializer.Deserialize<Dictionary<string, object>>(apiConfigJson);
                string pageId = apiConfig.ContainsKey("pageId") ? apiConfig["pageId"]?.ToString() : null;
                string pageAccessToken = apiConfig.ContainsKey("pageAccessToken") ? apiConfig["pageAccessToken"]?.ToString() : null;

                if (string.IsNullOrEmpty(pageId) || string.IsNullOrEmpty(pageAccessToken))
                    throw new Exception("Facebook pageId or pageAccessToken is missing");

                // Call Facebook Graph API
                string url = string.Format(
                    "https://graph.facebook.com/v18.0/{0}/ratings?fields=reviewer{{name,picture}},rating,review_text,created_time&access_token={1}",
                    Uri.EscapeDataString(pageId), Uri.EscapeDataString(pageAccessToken));

                string responseText = MakeHttpGetRequest(url);
                if (string.IsNullOrEmpty(responseText))
                    return 0;

                var response = _serializer.Deserialize<Dictionary<string, object>>(responseText);
                if (response == null || !response.ContainsKey("data"))
                    return 0;

                var dataArray = response["data"] as ArrayList;
                if (dataArray == null || dataArray.Count == 0)
                    return 0;

                foreach (var itemObj in dataArray)
                {
                    var item = itemObj as Dictionary<string, object>;
                    if (item == null) continue;

                    // Extract reviewer info
                    string reviewerName = "Unknown";
                    string reviewerPhoto = null;
                    if (item.ContainsKey("reviewer"))
                    {
                        var reviewer = item["reviewer"] as Dictionary<string, object>;
                        if (reviewer != null)
                        {
                            reviewerName = reviewer.ContainsKey("name") ? reviewer["name"]?.ToString() : "Unknown";
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

                    int rating = item.ContainsKey("rating") ? Convert.ToInt32(item["rating"]) : 0;
                    string reviewText = item.ContainsKey("review_text") ? item["review_text"]?.ToString() : "";
                    string createdTime = item.ContainsKey("created_time") ? item["created_time"]?.ToString() : null;

                    DateTime reviewDate = DateTime.Now;
                    if (!string.IsNullOrEmpty(createdTime))
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(createdTime, out parsed))
                            reviewDate = parsed;
                    }

                    // Generate a platform-specific review ID
                    string platformReviewId = "FB_" + reviewerName.GetHashCode() + "_" + reviewDate.Ticks;

                    // Check if already exists
                    DataTable dtExisting = _code.DatabaseQuerySafe(_connectionString,
                        "SELECT COUNT(*) AS Cnt FROM AI_Online_Reviews WHERE PlatformReviewId = @PlatformId",
                        new Dictionary<string, object> { { "@PlatformId", platformReviewId } });

                    if (dtExisting.Rows.Count > 0 && Convert.ToInt32(dtExisting.Rows[0]["Cnt"]) > 0)
                        continue;

                    // Insert new review
                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO AI_Online_Reviews
                          (SourceCode, PlatformReviewId, ReviewerName, ReviewerAvatar, Rating, ReviewText,
                           ReviewDate, IsAnalyzed, IsFlagged, Created_Date)
                          VALUES
                          ('FACEBOOK', @PlatformId, @Author, @Photo, @Rating, @Text,
                           @ReviewDate, 0, 0, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@PlatformId", platformReviewId },
                            { "@Author", reviewerName },
                            { "@Photo", reviewerPhoto ?? (object)DBNull.Value },
                            { "@Rating", rating },
                            { "@Text", reviewText ?? (object)DBNull.Value },
                            { "@ReviewDate", reviewDate }
                        });

                    newCount++;
                }

                // Update source metadata
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE AI_Review_Sources
                      SET LastFetchDate = GETDATE(),
                          TotalReviews = (SELECT COUNT(*) FROM AI_Online_Reviews WHERE SourceCode = 'FACEBOOK')
                      WHERE SourceCode = 'FACEBOOK'",
                    null);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching Facebook reviews: " + ex.Message);
            }

            return newCount;
        }

        /// <summary>
        /// Import reviews manually from JSON content
        /// </summary>
        public int ImportReviewsManual(string sourceCode, string csvOrJsonContent)
        {
            int count = 0;
            try
            {
                if (string.IsNullOrEmpty(sourceCode))
                    throw new ArgumentNullException(nameof(sourceCode));
                if (string.IsNullOrEmpty(csvOrJsonContent))
                    throw new ArgumentNullException(nameof(csvOrJsonContent));

                // Parse JSON array: [{ reviewerName, rating, reviewText, reviewDate, reviewTitle }]
                var reviews = _serializer.Deserialize<ArrayList>(csvOrJsonContent);
                if (reviews == null || reviews.Count == 0)
                    return 0;

                foreach (var reviewObj in reviews)
                {
                    var review = reviewObj as Dictionary<string, object>;
                    if (review == null) continue;

                    string reviewerName = review.ContainsKey("reviewerName") ? review["reviewerName"]?.ToString() : "Unknown";
                    int rating = review.ContainsKey("rating") ? Convert.ToInt32(review["rating"]) : 0;
                    string reviewText = review.ContainsKey("reviewText") ? review["reviewText"]?.ToString() : "";
                    string reviewTitle = review.ContainsKey("reviewTitle") ? review["reviewTitle"]?.ToString() : null;

                    DateTime reviewDate = DateTime.Now;
                    if (review.ContainsKey("reviewDate") && review["reviewDate"] != null)
                    {
                        DateTime parsed;
                        if (DateTime.TryParse(review["reviewDate"].ToString(), out parsed))
                            reviewDate = parsed;
                    }

                    string platformReviewId = "MANUAL_" + sourceCode + "_" + DateTime.Now.Ticks + "_" + count;

                    _code.DatabaseInsertSafe(_connectionString,
                        @"INSERT INTO AI_Online_Reviews
                          (SourceCode, PlatformReviewId, ReviewerName, Rating, ReviewText, ReviewTitle,
                           ReviewDate, IsAnalyzed, IsFlagged, Created_Date)
                          VALUES
                          (@Source, @PlatformId, @Author, @Rating, @Text, @Title,
                           @ReviewDate, 0, 0, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@Source", sourceCode },
                            { "@PlatformId", platformReviewId },
                            { "@Author", reviewerName },
                            { "@Rating", rating },
                            { "@Text", reviewText ?? (object)DBNull.Value },
                            { "@Title", reviewTitle ?? (object)DBNull.Value },
                            { "@ReviewDate", reviewDate }
                        });

                    count++;
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error importing reviews: " + ex.Message);
            }

            return count;
        }

        /// <summary>
        /// Make an HTTP GET request and return the response body
        /// </summary>
        private string MakeHttpGetRequest(string url)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.ContentType = "application/json";
                request.Timeout = 30000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                {
                    return reader.ReadToEnd();
                }
            }
            catch (WebException wex)
            {
                string errorMsg = "HTTP request failed";
                if (wex.Response != null)
                {
                    using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        errorMsg = reader.ReadToEnd();
                }
                throw new Exception(errorMsg);
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

            // Try to find JSON between curly braces
            int startIdx = response.IndexOf('{');
            int endIdx = response.LastIndexOf('}');

            if (startIdx >= 0 && endIdx > startIdx)
            {
                return response.Substring(startIdx, endIdx - startIdx + 1);
            }

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
