// ===========================================================================
// AIReviewDashboard.aspx.cs
// AI Review Dashboard - Sentiment analysis, review management, response tools
// ===========================================================================

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.CRM
{
    public partial class AIReviewDashboard : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);
            ViewStateUserKey = Session.SessionID;
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "Reviews", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            // Auth check
            try
            {
                if (Session["permission"]?.ToString() != "True" ||
                    (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
                {
                    Response.Redirect("~/Admin/Login");
                    return;
                }
            }
            catch
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            // Handle API calls
            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePost();
                return;
            }
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var data = string.IsNullOrEmpty(body) ? new Dictionary<string, object>() : serializer.Deserialize<Dictionary<string, object>>(body);
            Dictionary<string, object> result;

            switch (action)
            {
                case "getDashboard":
                    result = HandleGetDashboard();
                    break;
                case "getReviews":
                    result = HandleGetReviews(data);
                    break;
                case "fetchGoogle":
                    result = HandleFetchGoogle();
                    break;
                case "fetchFacebook":
                    result = HandleFetchFacebook();
                    break;
                case "importReviews":
                    result = HandleImportReviews(data);
                    break;
                case "importText":
                    result = HandleImportText(data);
                    break;
                case "fetchOTA":
                    result = HandleFetchOTA(data);
                    break;
                case "fetchAll":
                    result = HandleFetchAll();
                    break;
                case "aiDiscover":
                    result = HandleAIDiscover(data);
                    break;
                case "analyzePending":
                    result = HandleAnalyzePending();
                    break;
                case "analyzeOne":
                    result = HandleAnalyzeOne(data);
                    break;
                case "applyResponse":
                    result = HandleApplyResponse(data);
                    break;
                case "saveResponse":
                    result = HandleSaveResponse(data);
                    break;
                case "flagReview":
                    result = HandleFlagReview(data);
                    break;
                case "generateSummary":
                    result = HandleGenerateSummary(data);
                    break;
                case "getSources":
                    result = HandleGetSources();
                    break;
                case "updateSource":
                    result = HandleUpdateSource(data);
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action: " + action } };
                    break;
            }

            WriteJson(result);
        }

        #region Action Handlers

        private Dictionary<string, object> HandleGetDashboard()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                var data = svc.GetReviewDashboardData();
                return new Dictionary<string, object> { { "success", true }, { "data", data } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "โหลดข้อมูลแดชบอร์ดไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleGetReviews(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);

                string source = GetString(data, "source", "");
                string sentiment = GetString(data, "sentiment", "");
                int? minRating = GetNullableInt(data, "minRating");
                int? maxRating = GetNullableInt(data, "maxRating");
                string search = GetString(data, "search", "");
                int limit = GetInt(data, "limit", 50);

                var reviews = svc.GetReviewsByFilter(source, sentiment, minRating, maxRating, search, limit);
                return new Dictionary<string, object> { { "success", true }, { "data", reviews } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "โหลดรีวิวไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleFetchGoogle()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                return svc.FetchGoogleReviews();
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "ดึงรีวิว Google ไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleFetchFacebook()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                return svc.FetchFacebookReviews();
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "ดึงรีวิว Facebook ไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleFetchOTA(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string sourceCode = GetString(data, "sourceCode", "");
                if (string.IsNullOrEmpty(sourceCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องระบุ sourceCode" } };

                switch (sourceCode)
                {
                    case "PANTIP":
                        return svc.FetchPantipReviews();
                    case "WONGNAI":
                        return svc.FetchWongnaiReviews();
                    case "TIKTOK":
                    case "LEMON8":
                    case "TWITTER":
                    case "INSTAGRAM":
                    case "YOUTUBE":
                        return svc.FetchSocialMentions(sourceCode);
                    default:
                        return svc.FetchOTAReviews(sourceCode);
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "ดึงรีวิวไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleFetchAll()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                var results = svc.FetchAllEnabledSources();
                return new Dictionary<string, object> { { "success", true }, { "data", results } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "ดึงรีวิวไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleAIDiscover(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string sourceCode = GetString(data, "sourceCode", "");
                if (string.IsNullOrEmpty(sourceCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องระบุ sourceCode" } };

                return svc.AIDiscoverSource(sourceCode);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "AI ค้นหาไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleImportReviews(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string sourceCode = GetString(data, "sourceCode", "INTERNAL");
                string content = GetString(data, "content", "");

                if (string.IsNullOrWhiteSpace(content))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาใส่เนื้อหา JSON หรือข้อความรีวิว" } };

                return svc.ImportReviewsManual(sourceCode, content);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "นำเข้าไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleImportText(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string sourceCode = GetString(data, "sourceCode", "INTERNAL");
                string rawText = GetString(data, "text", "");

                if (string.IsNullOrWhiteSpace(rawText))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาใส่ข้อความรีวิวที่ copy มาจากเว็บ" } };

                return svc.ImportReviewsFromText(sourceCode, rawText);
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "นำเข้าไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleAnalyzePending()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                int count = svc.AnalyzeAllPending();
                return new Dictionary<string, object> { { "success", true }, { "count", count }, { "message", "วิเคราะห์สำเร็จ: " + count + " รีวิว" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "วิเคราะห์ไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleAnalyzeOne(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                long reviewId = GetLong(data, "reviewId", 0);
                string sourceType = GetString(data, "sourceType", "ONLINE");

                if (reviewId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบ ID รีวิว" } };

                bool ok = svc.AnalyzeReviewSentiment(reviewId, sourceType);
                return new Dictionary<string, object> { { "success", ok }, { "message", ok ? "วิเคราะห์ Sentiment สำเร็จ" : "วิเคราะห์ไม่สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "วิเคราะห์ไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleApplyResponse(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                long reviewId = GetLong(data, "reviewId", 0);
                string sourceType = GetString(data, "sourceType", "ONLINE");

                if (reviewId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบ ID รีวิว" } };

                bool ok = svc.ApplyAISuggestedResponse(reviewId, sourceType);
                return new Dictionary<string, object> { { "success", ok }, { "message", ok ? "ใช้คำตอบ AI สำเร็จ" : "ใช้คำตอบ AI ไม่สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "เกิดข้อผิดพลาด: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleSaveResponse(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                long reviewId = GetLong(data, "reviewId", 0);
                string sourceType = GetString(data, "sourceType", "ONLINE");
                string response = GetString(data, "response", "");

                if (reviewId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบ ID รีวิว" } };
                if (string.IsNullOrWhiteSpace(response))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาเขียนคำตอบ" } };

                string respondedBy = Session["UserName"]?.ToString() ?? Session["User"]?.ToString() ?? "Admin";
                bool ok = svc.SaveCustomResponse(reviewId, sourceType, response, respondedBy);
                return new Dictionary<string, object> { { "success", ok }, { "message", ok ? "บันทึกคำตอบสำเร็จ" : "บันทึกคำตอบไม่สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "เกิดข้อผิดพลาด: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleFlagReview(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                long reviewId = GetLong(data, "reviewId", 0);
                string sourceType = GetString(data, "sourceType", "ONLINE");
                string reason = GetString(data, "reason", "");

                if (reviewId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบ ID รีวิว" } };
                if (string.IsNullOrWhiteSpace(reason))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเหตุผล" } };

                bool ok = svc.FlagReview(reviewId, sourceType, reason);
                return new Dictionary<string, object> { { "success", ok }, { "message", ok ? "ตั้งค่าสถานะสำเร็จ" : "ตั้งค่าสถานะไม่สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "เกิดข้อผิดพลาด: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleGenerateSummary(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string period = GetString(data, "period", "MONTH");
                string summary = svc.GenerateReviewSummary(period);
                return new Dictionary<string, object> { { "success", true }, { "summary", summary } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "สร้างสรุปไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleGetSources()
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                DataTable dt = svc.GetReviewSources();
                var sources = new List<Dictionary<string, object>>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        var source = new Dictionary<string, object>();
                        foreach (DataColumn col in dt.Columns)
                        {
                            source[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
                        }
                        sources.Add(source);
                    }
                }

                return new Dictionary<string, object> { { "success", true }, { "data", sources } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "โหลดแหล่งที่มาไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, object> HandleUpdateSource(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIReviewAnalysisService(ConnStr);
                string sourceCode = GetString(data, "sourceCode", "");
                bool enabled = GetBool(data, "enabled", false);
                string apiConfig = GetString(data, "apiConfig", "");

                if (string.IsNullOrWhiteSpace(sourceCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่พบรหัสแหล่งที่มา" } };

                svc.UpdateSourceConfig(sourceCode, enabled, apiConfig);
                return new Dictionary<string, object> { { "success", true }, { "message", "อัปเดตแหล่งที่มาสำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "อัปเดตไม่สำเร็จ: " + ex.Message } };
            }
        }

        #endregion

        #region Helpers

        private void WriteJson(object data)
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(data));
            Response.End();
        }

        private static string GetString(Dictionary<string, object> data, string key, string fallback)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
                return data[key].ToString();
            return fallback;
        }

        private static int GetInt(Dictionary<string, object> data, string key, int fallback)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                int val;
                if (int.TryParse(data[key].ToString(), out val))
                    return val;
            }
            return fallback;
        }

        private static long GetLong(Dictionary<string, object> data, string key, long fallback)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                long val;
                if (long.TryParse(data[key].ToString(), out val))
                    return val;
            }
            return fallback;
        }

        private static int? GetNullableInt(Dictionary<string, object> data, string key)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                string s = data[key].ToString();
                int val;
                if (!string.IsNullOrEmpty(s) && int.TryParse(s, out val))
                    return val;
            }
            return null;
        }

        private static bool GetBool(Dictionary<string, object> data, string key, bool fallback)
        {
            if (data != null && data.ContainsKey(key) && data[key] != null)
            {
                if (data[key] is bool)
                    return (bool)data[key];
                bool val;
                if (bool.TryParse(data[key].ToString(), out val))
                    return val;
            }
            return fallback;
        }

        #endregion
    }
}
