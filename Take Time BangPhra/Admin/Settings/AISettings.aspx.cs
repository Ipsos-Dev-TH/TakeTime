using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Web.Script.Serialization;
using System.Web.UI;
using Take_Time_BangPhra.Services;

namespace Take_Time_BangPhra.Admin.Settings
{
    public partial class AISettings : Page
    {
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
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

            string action = Request.QueryString["action"];
            if (!string.IsNullOrEmpty(action))
            {
                HandleAction(action);
                return;
            }

            if (!IsPostBack)
                LoadConfig();
        }

        private void LoadConfig()
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                var cfg = svc.GetAllConfig();

                var data = new Dictionary<string, object>
                {
                    { "baseUrl", GetVal(cfg, "AI_BaseUrl", "https://api.deepseek.com") },
                    { "hasApiKey", svc.HasApiKey() },
                    { "maskedApiKey", svc.MaskApiKey() },
                    { "model", GetVal(cfg, "AI_Model", "deepseek-chat") },
                    { "enabled", GetVal(cfg, "AI_Enabled", "false") == "true" },
                    { "guestChatEnabled", GetVal(cfg, "AI_GuestChat_Enabled", "false") == "true" },
                    { "adminSuggestEnabled", GetVal(cfg, "AI_AdminSuggest_Enabled", "false") == "true" },
                    { "maxTokens", int.Parse(GetVal(cfg, "AI_MaxTokens", "2048")) },
                    { "temperature", double.Parse(GetVal(cfg, "AI_Temperature", "0.7")) },
                    { "timeout", int.Parse(GetVal(cfg, "AI_TimeoutSec", "30")) },
                    { "maxHistory", int.Parse(GetVal(cfg, "AI_MaxHistory", "20")) },
                    { "systemPrompt", GetVal(cfg, "AI_SystemPrompt", "") }
                };

                hfConfigData.Value = new JavaScriptSerializer().Serialize(data);
            }
            catch
            {
                hfConfigData.Value = "{}";
            }
        }

        private void HandleAction(string action)
        {
            Dictionary<string, object> result;

            switch (action)
            {
                case "testConnection":
                    result = TestConnection();
                    break;
                case "stats":
                    result = GetStats();
                    break;
                case "aiFeatures":
                    result = GetAIFeatures();
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private void HandlePost()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
                body = reader.ReadToEnd();

            string action = Request.QueryString["action"] ?? "";
            var serializer = new JavaScriptSerializer();
            var data = serializer.Deserialize<Dictionary<string, object>>(body);
            Dictionary<string, object> result;

            switch (action)
            {
                case "saveApi":
                    result = SaveApiConfig(data);
                    break;
                case "saveFeatures":
                    result = SaveFeatures(data);
                    break;
                case "saveParams":
                    result = SaveParams(data);
                    break;
                case "saveSystemPrompt":
                    result = SaveSystemPrompt(data);
                    break;
                case "chat":
                    result = HandleChat(data);
                    break;
                case "setAIFeature":
                    result = SetAIFeature(data);
                    break;
                default:
                    result = new Dictionary<string, object> { { "success", false }, { "message", "Unknown action" } };
                    break;
            }

            WriteJson(result);
        }

        private Dictionary<string, object> SaveApiConfig(Dictionary<string, object> data)
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);

                string baseUrl = data.ContainsKey("baseUrl") ? data["baseUrl"]?.ToString() : "";
                string apiKey = data.ContainsKey("apiKey") ? data["apiKey"]?.ToString() : "";
                string model = data.ContainsKey("model") ? data["model"]?.ToString() : "deepseek-chat";

                if (!string.IsNullOrEmpty(baseUrl))
                    svc.SetConfig("AI_BaseUrl", baseUrl.TrimEnd('/'));

                if (!string.IsNullOrEmpty(apiKey))
                    svc.SetApiKey(apiKey);

                svc.SetConfig("AI_Model", model);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "บันทึกการตั้งค่า API สำเร็จ" },
                    { "maskedKey", svc.MaskApiKey() }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveFeatures(Dictionary<string, object> data)
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                svc.SetConfig("AI_Enabled", data.ContainsKey("enabled") && (bool)data["enabled"] ? "true" : "false");
                svc.SetConfig("AI_GuestChat_Enabled", data.ContainsKey("guestChat") && (bool)data["guestChat"] ? "true" : "false");
                svc.SetConfig("AI_AdminSuggest_Enabled", data.ContainsKey("adminSuggest") && (bool)data["adminSuggest"] ? "true" : "false");
                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกสำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveParams(Dictionary<string, object> data)
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                if (data.ContainsKey("maxTokens"))
                    svc.SetConfig("AI_MaxTokens", Convert.ToInt32(data["maxTokens"]).ToString());
                if (data.ContainsKey("temperature"))
                    svc.SetConfig("AI_Temperature", Convert.ToDouble(data["temperature"]).ToString("0.0"));
                if (data.ContainsKey("timeout"))
                    svc.SetConfig("AI_TimeoutSec", Convert.ToInt32(data["timeout"]).ToString());
                if (data.ContainsKey("maxHistory"))
                    svc.SetConfig("AI_MaxHistory", Convert.ToInt32(data["maxHistory"]).ToString());
                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึกพารามิเตอร์สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveSystemPrompt(Dictionary<string, object> data)
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                string prompt = data.ContainsKey("systemPrompt") ? data["systemPrompt"]?.ToString() : "";
                if (string.IsNullOrWhiteSpace(prompt))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณากรอก System Prompt" } };

                svc.SetConfig("AI_SystemPrompt", prompt);
                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึก System Prompt สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> TestConnection()
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                if (!svc.HasApiKey())
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาตั้งค่า API Key ก่อน" } };

                var result = svc.TestConnection();
                return new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "message", result.Success
                        ? "✅ เชื่อมต่อสำเร็จ (ตอบกลับใน " + result.ResponseTimeMs + "ms, ใช้ " + result.TotalTokens + " tokens)"
                        : "❌ " + result.Message }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> HandleChat(Dictionary<string, object> data)
        {
            try
            {
                var svc = new DeepSeekService(ConnStr);
                string message = data.ContainsKey("message") ? data["message"]?.ToString() : "";
                string sessionKey = data.ContainsKey("sessionKey") ? data["sessionKey"]?.ToString() : "test";

                if (string.IsNullOrWhiteSpace(message))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาพิมพ์ข้อความ" } };

                var history = svc.GetHistory(sessionKey);
                var result = svc.SendMessage(message, sessionKey, history);

                return new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "message", result.Message },
                    { "tokens", result.TotalTokens },
                    { "responseMs", result.ResponseTimeMs }
                };
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
                var svc = new DeepSeekService(ConnStr);
                return svc.GetUsageStats();
            }
            catch
            {
                return new Dictionary<string, object>();
            }
        }

        private Dictionary<string, object> GetAIFeatures()
        {
            try
            {
                var svc = new AIKnowledgeService(ConnStr);
                var dt = svc.GetAllFeatures();
                var features = new System.Collections.Generic.List<object>();
                foreach (System.Data.DataRow row in dt.Rows)
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
            catch
            {
                return new Dictionary<string, object> { { "features", new System.Collections.Generic.List<object>() } };
            }
        }

        private Dictionary<string, object> SetAIFeature(Dictionary<string, object> data)
        {
            try
            {
                string featureCode = data["code"]?.ToString();
                bool enabled = Convert.ToBoolean(data["enabled"]);
                new AIKnowledgeService(ConnStr).SetFeatureEnabled(featureCode, enabled);
                return new Dictionary<string, object> { { "success", true } };
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
            Response.Write(new JavaScriptSerializer().Serialize(data));
            Response.End();
        }

        private string GetVal(Dictionary<string, string> cfg, string key, string fallback)
        {
            return cfg.ContainsKey(key) ? cfg[key] : fallback;
        }
    }
}
