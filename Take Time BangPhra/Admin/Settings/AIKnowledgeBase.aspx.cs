using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    public partial class AIKnowledgeBase : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (!Feature.Guard(this, "AI", "~/Default")) return;   // ฟีเจอร์ถูกปิด (ตั้งค่าระบบ → หมวดฟีเจอร์)
            if (Session["permission"]?.ToString() != "True" ||
                (Session["User"]?.ToString() != "Owner" && Session["User"]?.ToString() != "Admin"))
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

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
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body ?? "{}");
            Dictionary<string, object> result;

            switch (action)
            {
                case "getKB":
                    result = GetKBEntries(data);
                    break;
                case "getKBEntry":
                    result = GetKBEntry(data);
                    break;
                case "saveKB":
                    result = SaveKBEntry(data);
                    break;
                case "deleteKB":
                    result = DeleteKBEntry(data);
                    break;
                case "getPatterns":
                    result = GetPatterns();
                    break;
                case "deletePattern":
                    result = DeletePattern(data);
                    break;
                case "getFeatures":
                    result = GetFeatures();
                    break;
                case "setFeature":
                    result = SetFeature(data);
                    break;
                case "getStats":
                    result = GetStats();
                    break;
                case "importCsv":
                    result = ImportCsv(data);
                    break;
                case "importText":
                    result = ImportText(data);
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private Dictionary<string, object> GetKBEntries(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIKnowledgeService(ConnStr);
                string category = data.ContainsKey("category") ? data["category"]?.ToString() : null;
                string search = data.ContainsKey("search") ? data["search"]?.ToString() : null;
                if (string.IsNullOrEmpty(category)) category = null;
                if (string.IsNullOrEmpty(search)) search = null;

                DataTable dt = svc.GetKnowledgeBase(category, search);
                var entries = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    entries.Add(new
                    {
                        id = Convert.ToInt64(row["ID"]),
                        category = row["Category"]?.ToString(),
                        question = row["Question"]?.ToString(),
                        answer = row["Answer"]?.ToString(),
                        keywords = row["Keywords"]?.ToString(),
                        matchCount = Convert.ToInt32(row["MatchCount"]),
                        source = row["Source"]?.ToString(),
                        language = row["Language"]?.ToString()
                    });
                }

                return new Dictionary<string, object> { { "entries", entries } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "entries", new List<object>() }, { "error", ex.Message } };
            }
        }

        private Dictionary<string, object> GetKBEntry(Dictionary<string, object> data)
        {
            try
            {
                long id = Convert.ToInt64(data["id"]);
                var svc = new AIKnowledgeService(ConnStr);
                DataTable dt = svc.GetKnowledgeBase();

                foreach (DataRow row in dt.Rows)
                {
                    if (Convert.ToInt64(row["ID"]) == id)
                    {
                        return new Dictionary<string, object>
                        {
                            { "entry", new {
                                id = id,
                                category = row["Category"]?.ToString(),
                                question = row["Question"]?.ToString(),
                                answer = row["Answer"]?.ToString(),
                                keywords = row["Keywords"]?.ToString()
                            }}
                        };
                    }
                }
                return new Dictionary<string, object> { { "entry", null } };
            }
            catch
            {
                return new Dictionary<string, object> { { "entry", null } };
            }
        }

        private Dictionary<string, object> SaveKBEntry(Dictionary<string, object> data)
        {
            try
            {
                var svc = new AIKnowledgeService(ConnStr);
                long id = data.ContainsKey("id") ? Convert.ToInt64(data["id"]) : 0;
                string category = data["category"]?.ToString() ?? "GENERAL";
                string question = data["question"]?.ToString() ?? "";
                string answer = data["answer"]?.ToString() ?? "";
                string keywords = data.ContainsKey("keywords") ? data["keywords"]?.ToString() : "";

                if (string.IsNullOrEmpty(keywords))
                    keywords = svc.ExtractKeywords(question);

                if (id > 0)
                    svc.UpdateKnowledgeEntry(id, question, answer, keywords, category);
                else
                    svc.AddKnowledgeEntry(category, question, answer, keywords);

                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> DeleteKBEntry(Dictionary<string, object> data)
        {
            try
            {
                long id = Convert.ToInt64(data["id"]);
                new AIKnowledgeService(ConnStr).DeleteKnowledgeEntry(id);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> GetPatterns()
        {
            try
            {
                var svc = new AIKnowledgeService(ConnStr);
                DataTable dt = svc.GetLearnedPatterns(200);
                var patterns = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    patterns.Add(new
                    {
                        id = Convert.ToInt64(row["ID"]),
                        inputPattern = row["InputPattern"]?.ToString(),
                        keywords = row["InputKeywords"]?.ToString(),
                        response = row["Response"]?.ToString(),
                        category = row["Category"]?.ToString(),
                        confidence = row["Confidence"] != DBNull.Value ? Convert.ToDouble(row["Confidence"]) : 0,
                        successCount = Convert.ToInt32(row["SuccessCount"]),
                        failCount = Convert.ToInt32(row["FailCount"]),
                        learnedFrom = row["LearnedFrom"]?.ToString(),
                        source = row["Source"]?.ToString()
                    });
                }

                return new Dictionary<string, object> { { "patterns", patterns } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "patterns", new List<object>() }, { "error", ex.Message } };
            }
        }

        private Dictionary<string, object> DeletePattern(Dictionary<string, object> data)
        {
            try
            {
                long id = Convert.ToInt64(data["id"]);
                new AIKnowledgeService(ConnStr).DeleteLearnedPattern(id);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> GetFeatures()
        {
            try
            {
                var svc = new AIKnowledgeService(ConnStr);
                DataTable dt = svc.GetAllFeatures();
                var features = new List<object>();

                foreach (DataRow row in dt.Rows)
                {
                    features.Add(new
                    {
                        code = row["FeatureCode"]?.ToString(),
                        name = row["FeatureName"]?.ToString(),
                        description = row["Description"]?.ToString(),
                        enabled = Convert.ToBoolean(row["IsEnabled"])
                    });
                }

                return new Dictionary<string, object> { { "features", features } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "features", new List<object>() }, { "error", ex.Message } };
            }
        }

        private Dictionary<string, object> SetFeature(Dictionary<string, object> data)
        {
            try
            {
                string code = data["code"]?.ToString();
                bool enabled = Convert.ToBoolean(data["enabled"]);
                new AIKnowledgeService(ConnStr).SetFeatureEnabled(code, enabled);
                return new Dictionary<string, object> { { "success", true } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> GetStats()
        {
            try
            {
                return new AIKnowledgeService(ConnStr).GetLearningStats();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private Dictionary<string, object> ImportCsv(Dictionary<string, object> data)
        {
            try
            {
                string content = data["content"]?.ToString() ?? "";
                var svc = new AIKnowledgeService(ConnStr);
                int count = svc.ImportKnowledgeCsv(content);
                return new Dictionary<string, object> { { "success", true }, { "count", count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ImportText(Dictionary<string, object> data)
        {
            try
            {
                string content = data["content"]?.ToString() ?? "";
                var svc = new AIKnowledgeService(ConnStr);
                int count = svc.ImportKnowledgeFromText(content);
                return new Dictionary<string, object> { { "success", true }, { "count", count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private void WriteJson(object data)
        {
            Response.Clear();
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.Serialize(data));
            Response.End();
        }
    }
}
