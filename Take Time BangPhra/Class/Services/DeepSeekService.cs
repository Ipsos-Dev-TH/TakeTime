using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Services
{
    public class DeepSeekService
    {
        private readonly string _connectionString;
        private readonly code _code;
        private static bool _schemaEnsured = false;
        private static readonly object _schemaLock = new object();

        public DeepSeekService(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _code = new code();
            EnsureSchema();
        }

        #region Schema

        /// <summary>
        /// Auto-creates AI tables if they don't exist (covers PHASE14 Migration 01).
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
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Integration_Config')
BEGIN
    CREATE TABLE AI_Integration_Config (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        ConfigKey NVARCHAR(100) NOT NULL,
        ConfigValue NVARCHAR(MAX) NOT NULL,
        Description NVARCHAR(255),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_AI_Integration_Config_Key UNIQUE (ConfigKey)
    );

    INSERT INTO AI_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('AI_Provider',            'deepseek',                 'AI Provider (deepseek)'),
    ('AI_BaseUrl',             'https://api.deepseek.com', 'DeepSeek API base URL'),
    ('AI_ApiKey_Encrypted',    '',                         'Encrypted API Key for DeepSeek'),
    ('AI_Model',               'deepseek-chat',            'Model name (deepseek-chat, deepseek-reasoner)'),
    ('AI_Enabled',             'false',                    'Enable/disable AI features (true/false)'),
    ('AI_MaxTokens',           '2048',                     'Maximum tokens per response'),
    ('AI_Temperature',         '0.7',                      'Temperature (0.0 - 2.0)'),
    ('AI_TimeoutSec',          '30',                       'HTTP request timeout in seconds'),
    ('AI_SystemPrompt',        N'คุณเป็นผู้ช่วย AI ของ TakeTime BangPhra ที่พักริมทะเล ตอบคำถามเป็นภาษาไทย สุภาพ เป็นมิตร ให้ข้อมูลเกี่ยวกับที่พัก สิ่งอำนวยความสะดวก กิจกรรม สถานที่ใกล้เคียง และบริการต่างๆ หากไม่ทราบข้อมูลให้แนะนำติดต่อ Front Desk', 'System prompt for AI assistant'),
    ('AI_GuestChat_Enabled',   'false',                    'Enable AI assistant in guest chat'),
    ('AI_AdminSuggest_Enabled','false',                    'Enable AI reply suggestions for admin chat'),
    ('AI_MaxHistory',          '20',                       'Max conversation history messages to include');
END", null);

                    _code.DatabaseInsertSafe(_connectionString, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Chat_History')
BEGIN
    CREATE TABLE AI_Chat_History (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        SessionKey NVARCHAR(100) NOT NULL,
        Role NVARCHAR(20) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        TokensUsed INT NULL,
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_AI_Chat_SessionKey (SessionKey),
        INDEX IX_AI_Chat_Date (Created_Date)
    );
END", null);

                    _code.DatabaseInsertSafe(_connectionString, @"
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AI_Usage_Log')
BEGIN
    CREATE TABLE AI_Usage_Log (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        RequestDate DATETIME DEFAULT GETDATE(),
        SessionKey NVARCHAR(100),
        Model NVARCHAR(50),
        PromptTokens INT,
        CompletionTokens INT,
        TotalTokens INT,
        ResponseTimeMs INT,
        Success BIT DEFAULT 1,
        ErrorMessage NVARCHAR(500) NULL,
        INDEX IX_AI_Usage_Date (RequestDate)
    );
END", null);

                    _schemaEnsured = true;
                }
                catch { /* keep _schemaEnsured false so a later call retries */ }

                // Retention: เก็บประวัติแชท/usage log 90 วัน — กันตารางโตไม่จำกัด
                // (รันครั้งเดียวต่อ app domain ตอน schema ensure)
                try
                {
                    _code.DatabaseInsertSafe(_connectionString,
                        @"DELETE FROM AI_Chat_History WHERE Created_Date < DATEADD(DAY, -90, GETDATE());
                          DELETE FROM AI_Usage_Log WHERE RequestDate < DATEADD(DAY, -180, GETDATE());", null);
                }
                catch { }
            }
        }

        #endregion

        #region Configuration

        private string GetConfig(string key, string fallback = "")
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ConfigValue FROM AI_Integration_Config WHERE ConfigKey = @Key",
                    new Dictionary<string, object> { { "@Key", key } });
                return dt.Rows.Count > 0 ? dt.Rows[0]["ConfigValue"].ToString() : fallback;
            }
            catch { return fallback; }
        }

        public void SetConfig(string key, string value)
        {
            // UPDLOCK+HOLDLOCK: atomic upsert — กัน lost update เมื่อหลาย thread เซ็ต key เดียวกัน
            _code.DatabaseInsertSafe(_connectionString,
                @"BEGIN TRAN;
                  UPDATE AI_Integration_Config WITH (UPDLOCK, HOLDLOCK)
                  SET ConfigValue = @Value, Updated_Date = GETDATE() WHERE ConfigKey = @Key;
                  IF @@ROWCOUNT = 0
                    INSERT INTO AI_Integration_Config (ConfigKey, ConfigValue) VALUES (@Key, @Value);
                  COMMIT TRAN;",
                new Dictionary<string, object> { { "@Key", key }, { "@Value", value } });
        }

        public Dictionary<string, string> GetAllConfig()
        {
            var config = new Dictionary<string, string>();
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ConfigKey, ConfigValue FROM AI_Integration_Config", null);
                foreach (DataRow row in dt.Rows)
                    config[row["ConfigKey"].ToString()] = row["ConfigValue"].ToString();
            }
            catch { }
            return config;
        }

        public bool IsEnabled => GetConfig("AI_Enabled", "false") == "true";
        public bool IsGuestChatEnabled => IsEnabled && GetConfig("AI_GuestChat_Enabled", "false") == "true";
        public bool IsAdminSuggestEnabled => IsEnabled && GetConfig("AI_AdminSuggest_Enabled", "false") == "true";

        #endregion

        #region API Communication

        /// <param name="systemPromptOverride">ถ้าระบุ จะใช้แทน AI_SystemPrompt จาก config —
        /// ใช้ส่ง prompt เสริม (booking context ฯลฯ) แบบ per-request โดยไม่แตะ config กลาง</param>
        public ChatResponse SendMessage(string userMessage, string sessionKey, List<ChatMessage> history = null, string systemPromptOverride = null)
        {
            var sw = Stopwatch.StartNew();
            string model = GetConfig("AI_Model", "deepseek-chat");

            try
            {
                string baseUrl = GetConfig("AI_BaseUrl", "https://api.deepseek.com");
                string apiKey = GetConfig("AI_ApiKey_Encrypted");
                if (string.IsNullOrEmpty(apiKey))
                    return new ChatResponse { Success = false, Message = "API Key ยังไม่ได้ตั้งค่า" };

                apiKey = DecryptKey(apiKey);
                int maxTokens = int.Parse(GetConfig("AI_MaxTokens", "2048"));
                double temperature = double.Parse(GetConfig("AI_Temperature", "0.7"));
                int timeoutSec = int.Parse(GetConfig("AI_TimeoutSec", "30"));
                string systemPrompt = !string.IsNullOrEmpty(systemPromptOverride)
                    ? systemPromptOverride
                    : GetConfig("AI_SystemPrompt", "You are a helpful assistant.");
                int maxHistory = int.Parse(GetConfig("AI_MaxHistory", "20"));

                var messages = new List<object>();
                messages.Add(new { role = "system", content = systemPrompt });

                if (history != null)
                {
                    int startIdx = Math.Max(0, history.Count - maxHistory);
                    for (int i = startIdx; i < history.Count; i++)
                        messages.Add(new { role = history[i].Role, content = history[i].Content });
                }

                messages.Add(new { role = "user", content = userMessage });

                var requestBody = new
                {
                    model = model,
                    messages = messages,
                    max_tokens = maxTokens,
                    temperature = temperature,
                    stream = false
                };

                var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                string json = serializer.Serialize(requestBody);

                var request = (HttpWebRequest)WebRequest.Create(baseUrl.TrimEnd('/') + "/v1/chat/completions");
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Headers.Add("Authorization", "Bearer " + apiKey);
                request.Timeout = timeoutSec * 1000;

                byte[] data = Encoding.UTF8.GetBytes(json);
                request.ContentLength = data.Length;
                using (var stream = request.GetRequestStream())
                    stream.Write(data, 0, data.Length);

                string responseText;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                    responseText = reader.ReadToEnd();

                var result = serializer.Deserialize<Dictionary<string, object>>(responseText);

                string assistantMessage = "";
                int promptTokens = 0, completionTokens = 0, totalTokens = 0;

                if (result.ContainsKey("choices"))
                {
                    var choices = result["choices"] as System.Collections.ArrayList;
                    if (choices != null && choices.Count > 0)
                    {
                        var choice = choices[0] as Dictionary<string, object>;
                        var msg = choice?["message"] as Dictionary<string, object>;
                        assistantMessage = msg?["content"]?.ToString() ?? "";
                    }
                }

                if (result.ContainsKey("usage"))
                {
                    var usage = result["usage"] as Dictionary<string, object>;
                    if (usage != null)
                    {
                        promptTokens = Convert.ToInt32(usage.ContainsKey("prompt_tokens") ? usage["prompt_tokens"] : 0);
                        completionTokens = Convert.ToInt32(usage.ContainsKey("completion_tokens") ? usage["completion_tokens"] : 0);
                        totalTokens = Convert.ToInt32(usage.ContainsKey("total_tokens") ? usage["total_tokens"] : 0);
                    }
                }

                sw.Stop();

                SaveHistory(sessionKey, "user", userMessage, 0);
                SaveHistory(sessionKey, "assistant", assistantMessage, completionTokens);
                LogUsage(sessionKey, model, promptTokens, completionTokens, totalTokens, (int)sw.ElapsedMilliseconds, true, null);

                return new ChatResponse
                {
                    Success = true,
                    Message = assistantMessage,
                    PromptTokens = promptTokens,
                    CompletionTokens = completionTokens,
                    TotalTokens = totalTokens,
                    ResponseTimeMs = (int)sw.ElapsedMilliseconds
                };
            }
            catch (WebException wex)
            {
                sw.Stop();
                string errorMsg = "Connection error";
                if (wex.Response != null)
                {
                    using (var reader = new StreamReader(wex.Response.GetResponseStream()))
                        errorMsg = reader.ReadToEnd();
                }
                LogUsage(sessionKey, model, 0, 0, 0, (int)sw.ElapsedMilliseconds, false, errorMsg);
                return new ChatResponse { Success = false, Message = "เกิดข้อผิดพลาดในการเชื่อมต่อ AI: " + GetFriendlyError(errorMsg) };
            }
            catch (Exception ex)
            {
                sw.Stop();
                LogUsage(sessionKey, model, 0, 0, 0, (int)sw.ElapsedMilliseconds, false, ex.Message);
                return new ChatResponse { Success = false, Message = "เกิดข้อผิดพลาด: " + ex.Message };
            }
        }

        public ChatResponse TestConnection()
        {
            return SendMessage("Hello, respond with just: Connection successful.", "test_" + DateTime.Now.Ticks);
        }

        #endregion

        #region History

        public List<ChatMessage> GetHistory(string sessionKey)
        {
            var list = new List<ChatMessage>();
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Role, Content, Created_Date FROM AI_Chat_History WHERE SessionKey = @Key ORDER BY Created_Date ASC",
                    new Dictionary<string, object> { { "@Key", sessionKey } });
                foreach (DataRow row in dt.Rows)
                    list.Add(new ChatMessage
                    {
                        Role = row["Role"].ToString(),
                        Content = row["Content"].ToString(),
                        CreatedDate = Convert.ToDateTime(row["Created_Date"])
                    });
            }
            catch { }
            return list;
        }

        private void SaveHistory(string sessionKey, string role, string content, int tokens)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "INSERT INTO AI_Chat_History (SessionKey, Role, Content, TokensUsed, Created_Date) VALUES (@Key, @Role, @Content, @Tokens, GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@Key", sessionKey },
                        { "@Role", role },
                        { "@Content", content },
                        { "@Tokens", tokens }
                    });
            }
            catch { }
        }

        public void ClearHistory(string sessionKey)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "DELETE FROM AI_Chat_History WHERE SessionKey = @Key",
                    new Dictionary<string, object> { { "@Key", sessionKey } });
            }
            catch { }
        }

        #endregion

        #region Usage Stats

        private void LogUsage(string sessionKey, string model, int prompt, int completion, int total, int responseMs, bool success, string error)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO AI_Usage_Log (RequestDate, SessionKey, Model, PromptTokens, CompletionTokens, TotalTokens, ResponseTimeMs, Success, ErrorMessage)
                      VALUES (GETDATE(), @Key, @Model, @Prompt, @Completion, @Total, @ResponseMs, @Success, @Error)",
                    new Dictionary<string, object>
                    {
                        { "@Key", sessionKey },
                        { "@Model", model },
                        { "@Prompt", prompt },
                        { "@Completion", completion },
                        { "@Total", total },
                        { "@ResponseMs", responseMs },
                        { "@Success", success },
                        { "@Error", error != null ? (object)error : DBNull.Value }
                    });
            }
            catch { }
        }

        public Dictionary<string, object> GetUsageStats()
        {
            var stats = new Dictionary<string, object>();
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT
                        COUNT(*) AS TotalRequests,
                        SUM(CASE WHEN Success = 1 THEN 1 ELSE 0 END) AS SuccessCount,
                        SUM(CASE WHEN Success = 0 THEN 1 ELSE 0 END) AS FailCount,
                        ISNULL(SUM(TotalTokens), 0) AS TotalTokensUsed,
                        ISNULL(AVG(ResponseTimeMs), 0) AS AvgResponseMs,
                        ISNULL(SUM(CASE WHEN RequestDate >= CAST(GETDATE() AS DATE) THEN TotalTokens ELSE 0 END), 0) AS TodayTokens,
                        SUM(CASE WHEN RequestDate >= CAST(GETDATE() AS DATE) THEN 1 ELSE 0 END) AS TodayRequests
                      FROM AI_Usage_Log", null);

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    stats["totalRequests"] = Convert.ToInt32(row["TotalRequests"]);
                    stats["successCount"] = Convert.ToInt32(row["SuccessCount"]);
                    stats["failCount"] = Convert.ToInt32(row["FailCount"]);
                    stats["totalTokensUsed"] = Convert.ToInt64(row["TotalTokensUsed"]);
                    stats["avgResponseMs"] = Convert.ToInt32(row["AvgResponseMs"]);
                    stats["todayTokens"] = Convert.ToInt64(row["TodayTokens"]);
                    stats["todayRequests"] = Convert.ToInt32(row["TodayRequests"]);
                }
            }
            catch { }
            return stats;
        }

        #endregion

        #region Helpers

        // DPAPI (machine scope) — เข้ารหัสจริง ผูกกับเครื่อง server
        // prefix "dpapi:" แยกจากค่าเก่าที่เป็น base64 เปล่าๆ (อ่านย้อนหลังได้)
        private const string DpapiPrefix = "dpapi:";
        private static readonly byte[] _dpapiEntropy = Encoding.UTF8.GetBytes("TakeTime.AI.ApiKey.v1");

        private string EncryptKey(string plainKey)
        {
            try
            {
                byte[] protectedBytes = System.Security.Cryptography.ProtectedData.Protect(
                    Encoding.UTF8.GetBytes(plainKey), _dpapiEntropy,
                    System.Security.Cryptography.DataProtectionScope.LocalMachine);
                return DpapiPrefix + Convert.ToBase64String(protectedBytes);
            }
            catch
            {
                // DPAPI ใช้ไม่ได้ (เช่น non-Windows) → fallback base64 เดิม
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainKey));
            }
        }

        private string DecryptKey(string encryptedKey)
        {
            if (string.IsNullOrEmpty(encryptedKey)) return encryptedKey;
            try
            {
                if (encryptedKey.StartsWith(DpapiPrefix))
                {
                    byte[] protectedBytes = Convert.FromBase64String(encryptedKey.Substring(DpapiPrefix.Length));
                    byte[] plain = System.Security.Cryptography.ProtectedData.Unprotect(
                        protectedBytes, _dpapiEntropy,
                        System.Security.Cryptography.DataProtectionScope.LocalMachine);
                    return Encoding.UTF8.GetString(plain);
                }
                // ค่าเก่า: base64 เปล่า — อ่านได้ และจะถูก upgrade เป็น DPAPI ตอนบันทึกครั้งถัดไป
                return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedKey));
            }
            catch { return encryptedKey; }
        }

        public void SetApiKey(string plainKey)
        {
            SetConfig("AI_ApiKey_Encrypted", EncryptKey(plainKey));
        }

        public bool HasApiKey()
        {
            return !string.IsNullOrEmpty(GetConfig("AI_ApiKey_Encrypted"));
        }

        public string MaskApiKey()
        {
            string encrypted = GetConfig("AI_ApiKey_Encrypted");
            if (string.IsNullOrEmpty(encrypted)) return "";
            string plain = DecryptKey(encrypted);
            if (plain.Length <= 8) return "****";
            return plain.Substring(0, 4) + "****" + plain.Substring(plain.Length - 4);
        }

        private string GetFriendlyError(string raw)
        {
            if (raw.Contains("401") || raw.Contains("Unauthorized")) return "API Key ไม่ถูกต้อง";
            if (raw.Contains("429") || raw.Contains("rate")) return "เกินจำนวนคำขอที่อนุญาต กรุณารอสักครู่";
            if (raw.Contains("500")) return "เซิร์ฟเวอร์ AI มีปัญหา กรุณาลองใหม่";
            if (raw.Contains("timeout") || raw.Contains("Timeout")) return "หมดเวลาการเชื่อมต่อ";
            return raw.Length > 100 ? raw.Substring(0, 100) + "..." : raw;
        }

        #endregion
    }

    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
        public int ResponseTimeMs { get; set; }
    }
}
