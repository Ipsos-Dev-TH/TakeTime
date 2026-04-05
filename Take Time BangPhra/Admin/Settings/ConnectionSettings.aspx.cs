using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Settings
{
    public partial class ConnectionSettings : Page
    {
        private readonly code _code = new code();

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
            {
                Response.Redirect("~/Admin/Login");
                return;
            }

            // Handle POST with JSON body first (e.g., saveAccounting)
            if (Request.HttpMethod == "POST" && Request.ContentType?.Contains("application/json") == true)
            {
                HandlePostRequest();
                return;
            }

            // Handle AJAX GET requests (e.g., test connections)
            string action = Request.QueryString["action"];
            if (!string.IsNullOrEmpty(action))
            {
                HandleAjaxRequest(action);
                return;
            }

            if (!IsPostBack)
            {
                LoadConnectionData();
                LoadAccountingData();
            }
        }

        private void LoadConnectionData()
        {
            string connStr = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
            var connParts = ParseConnectionString(connStr);

            bool dbConnected = false;
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(connStr, "SELECT 1 AS Test", null);
                dbConnected = dt != null && dt.Rows.Count > 0;
            }
            catch { }

            string lineUrl = ConfigurationManager.AppSettings["lineurl"] ?? "";
            string lineToken = ConfigurationManager.AppSettings["linetoken"] ?? "";
            string lineChannelToken = ConfigurationManager.AppSettings["linechannelaccesstokentaketime"] ?? "";
            string telegramToken = ConfigurationManager.AppSettings["TelegramTokenTakeTime"] ?? "";
            string smtpServer = ConfigurationManager.AppSettings["SMTP"] ?? "";
            string smtpPort = ConfigurationManager.AppSettings["SMTP_Port"] ?? "";
            string emailFrom = ConfigurationManager.AppSettings["Email_From"] ?? "";
            string smtpSsl = ConfigurationManager.AppSettings["SMTP_EnableSsl"] ?? "";
            string googleApiKey = ConfigurationManager.AppSettings["GooglePlacesApiKey"] ?? "";
            string taxApiKey = ConfigurationManager.AppSettings["TaxInvoiceApiKey"] ?? "";

            var data = new Dictionary<string, object>
            {
                { "dbServer", connParts.ContainsKey("server") ? connParts["server"] : "" },
                { "dbName", connParts.ContainsKey("database") ? connParts["database"] : "" },
                { "dbUser", connParts.ContainsKey("user") ? connParts["user"] : "" },
                { "dbConnected", dbConnected },
                { "lineUrl", lineUrl },
                { "lineToken", MaskValue(lineToken) },
                { "lineChannelToken", MaskValue(lineChannelToken) },
                { "lineConfigured", !string.IsNullOrEmpty(lineToken) },
                { "telegramToken", MaskValue(telegramToken) },
                { "telegramConfigured", !string.IsNullOrEmpty(telegramToken) },
                { "smtpServer", smtpServer },
                { "smtpPort", smtpPort },
                { "emailFrom", emailFrom },
                { "smtpSsl", smtpSsl },
                { "emailConfigured", !string.IsNullOrEmpty(smtpServer) && !string.IsNullOrEmpty(emailFrom) },
                { "googleApiKey", MaskValue(googleApiKey) },
                { "googleConfigured", !string.IsNullOrEmpty(googleApiKey) },
                { "taxApiKey", MaskValue(taxApiKey) },
                { "taxConfigured", !string.IsNullOrEmpty(taxApiKey) }
            };

            hfConnectionData.Value = new JavaScriptSerializer().Serialize(data);
        }

        private void LoadAccountingData()
        {
            string connStr = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
            try
            {
                var config = new Integration.AccountingConfig(connStr);
                var data = new Dictionary<string, object>
                {
                    { "baseUrl", config.BaseUrl },
                    { "hasApiKey", !string.IsNullOrEmpty(config.ApiKey) },
                    { "companyId", config.CompanyId != System.Guid.Empty ? config.CompanyId.ToString() : "" },
                    { "enabled", config.Enabled },
                    { "isConfigured", config.IsConfigured }
                };
                hfAccountingData.Value = new JavaScriptSerializer().Serialize(data);
            }
            catch
            {
                hfAccountingData.Value = "{}";
            }
        }

        private void HandleAjaxRequest(string action)
        {
            if (action == "test")
            {
                string type = Request.QueryString["type"] ?? "";
                var result = TestConnection(type);
                WriteJsonResponse(result);
            }
        }

        private void HandlePostRequest()
        {
            string body;
            using (var reader = new StreamReader(Request.InputStream))
            {
                body = reader.ReadToEnd();
            }

            string action = Request.QueryString["action"] ?? "";
            if (action == "saveAccounting")
            {
                var result = SaveAccountingConfig(body);
                WriteJsonResponse(result);
            }
        }

        private Dictionary<string, object> TestConnection(string type)
        {
            string connStr = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

            switch (type)
            {
                case "database":
                    return TestDatabaseConnection(connStr);
                case "line":
                    return TestLineConnection();
                case "telegram":
                    return TestTelegramConnection();
                case "email":
                    return TestEmailConnection();
                case "accounting":
                    return TestAccountingConnection(connStr);
                case "google":
                    return TestGoogleConnection();
                case "taxinvoice":
                    return TestTaxInvoiceConnection();
                default:
                    return new Dictionary<string, object> { { "success", false }, { "message", "Unknown connection type" } };
            }
        }

        private Dictionary<string, object> TestDatabaseConnection(string connStr)
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                DataTable dt = _code.DatabaseQuerySafe(connStr, "SELECT @@VERSION AS Version, DB_NAME() AS DatabaseName, GETDATE() AS ServerTime", null);
                sw.Stop();

                if (dt != null && dt.Rows.Count > 0)
                {
                    string version = dt.Rows[0]["Version"]?.ToString()?.Split('\n')[0] ?? "Unknown";
                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "message", $"เชื่อมต่อสำเร็จ ({sw.ElapsedMilliseconds}ms) - {version}" }
                    };
                }
                return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถ query ได้" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "เชื่อมต่อไม่ได้: " + ex.Message } };
            }
        }

        private Dictionary<string, object> TestLineConnection()
        {
            try
            {
                string token = ConfigurationManager.AppSettings["linetoken"] ?? "";
                if (string.IsNullOrEmpty(token))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า LINE Token" } };

                string url = ConfigurationManager.AppSettings["lineurl"] ?? "https://notify-api.line.me/api/notify";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.Headers.Add("Authorization", "Bearer " + token);
                request.ContentType = "application/x-www-form-urlencoded";
                request.Timeout = 10000;

                byte[] data = Encoding.UTF8.GetBytes("message=[TakeTime Test] ทดสอบการเชื่อมต่อ LINE สำเร็จ");
                request.ContentLength = data.Length;
                using (var stream = request.GetRequestStream()) stream.Write(data, 0, data.Length);

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    return new Dictionary<string, object>
                    {
                        { "success", response.StatusCode == HttpStatusCode.OK },
                        { "message", response.StatusCode == HttpStatusCode.OK ? "ส่งข้อความทดสอบ LINE สำเร็จ" : "HTTP " + (int)response.StatusCode }
                    };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "LINE Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> TestTelegramConnection()
        {
            try
            {
                string token = ConfigurationManager.AppSettings["TelegramTokenTakeTime"] ?? "";
                if (string.IsNullOrEmpty(token))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Telegram Token" } };

                string url = $"https://api.telegram.org/bot{token}/getMe";
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Timeout = 10000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string body = reader.ReadToEnd();
                    bool ok = body.Contains("\"ok\":true");
                    return new Dictionary<string, object>
                    {
                        { "success", ok },
                        { "message", ok ? "Telegram Bot เชื่อมต่อสำเร็จ" : "Bot ไม่ตอบสนอง" }
                    };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Telegram Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> TestEmailConnection()
        {
            try
            {
                string smtp = ConfigurationManager.AppSettings["SMTP"] ?? "";
                string port = ConfigurationManager.AppSettings["SMTP_Port"] ?? "587";

                if (string.IsNullOrEmpty(smtp))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า SMTP Server" } };

                using (var client = new System.Net.Sockets.TcpClient())
                {
                    var result = client.BeginConnect(smtp, int.Parse(port), null, null);
                    bool connected = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                    if (connected && client.Connected)
                    {
                        client.Close();
                        return new Dictionary<string, object>
                        {
                            { "success", true },
                            { "message", $"SMTP Server {smtp}:{port} เชื่อมต่อสำเร็จ" }
                        };
                    }
                    return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่สามารถเชื่อมต่อ {smtp}:{port} ได้ (Timeout)" } };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "SMTP Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> TestAccountingConnection(string connStr)
        {
            try
            {
                var config = new Integration.AccountingConfig(connStr);
                if (!config.IsConfigured)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Nexaacc (URL, API Key, Company ID, หรือยังไม่เปิดใช้งาน)" } };

                var client = new Integration.AccountingApiClient(config, connStr);
                var task = client.TestConnectionAsync();
                task.Wait();
                var result = task.Result;
                return new Dictionary<string, object>
                {
                    { "success", result.Success },
                    { "message", result.Message }
                };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> TestGoogleConnection()
        {
            string apiKey = ConfigurationManager.AppSettings["GooglePlacesApiKey"] ?? "";
            if (string.IsNullOrEmpty(apiKey))
                return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Google API Key" } };

            return new Dictionary<string, object> { { "success", true }, { "message", "Google API Key ตั้งค่าแล้ว (ตรวจสอบ quota ที่ Google Console)" } };
        }

        private Dictionary<string, object> TestTaxInvoiceConnection()
        {
            string apiKey = ConfigurationManager.AppSettings["TaxInvoiceApiKey"] ?? "";
            if (string.IsNullOrEmpty(apiKey))
                return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Tax Invoice API Key" } };

            return new Dictionary<string, object> { { "success", true }, { "message", "Tax Invoice API Key ตั้งค่าแล้ว" } };
        }

        private Dictionary<string, object> SaveAccountingConfig(string jsonBody)
        {
            try
            {
                string connStr = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";
                var serializer = new JavaScriptSerializer();
                var data = serializer.Deserialize<Dictionary<string, object>>(jsonBody);

                var config = new Integration.AccountingConfig(connStr);
                if (data.ContainsKey("baseUrl")) config.SetConfig("Nexaacc_BaseUrl", data["baseUrl"]?.ToString() ?? "");
                if (data.ContainsKey("apiKey") && !string.IsNullOrEmpty(data["apiKey"]?.ToString()))
                {
                    config.SetConfig("Nexaacc_ApiKey_Encrypted", _code.Crypt(data["apiKey"].ToString()));
                }
                if (data.ContainsKey("companyId")) config.SetConfig("Nexaacc_CompanyId", data["companyId"]?.ToString() ?? "");
                if (data.ContainsKey("enabled")) config.SetConfig("Nexaacc_Enabled", Convert.ToBoolean(data["enabled"]) ? "true" : "false");

                // Reload to check
                var newConfig = new Integration.AccountingConfig(connStr);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", "บันทึกการตั้งค่า Nexaacc สำเร็จ" },
                    { "isConfigured", newConfig.IsConfigured }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "บันทึกไม่สำเร็จ: " + ex.Message } };
            }
        }

        private Dictionary<string, string> ParseConnectionString(string connStr)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(connStr)) return result;

            foreach (string part in connStr.Split(';'))
            {
                string trimmed = part.Trim();
                int eqIdx = trimmed.IndexOf('=');
                if (eqIdx > 0)
                {
                    string key = trimmed.Substring(0, eqIdx).Trim().ToLower();
                    string val = trimmed.Substring(eqIdx + 1).Trim();
                    if (key == "data source" || key == "server") result["server"] = val;
                    else if (key == "initial catalog" || key == "database") result["database"] = val;
                    else if (key == "user id" || key == "uid") result["user"] = val;
                }
            }
            return result;
        }

        private string MaskValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Length <= 8) return new string('*', value.Length);
            return value.Substring(0, 4) + new string('*', value.Length - 8) + value.Substring(value.Length - 4);
        }

        private void WriteJsonResponse(Dictionary<string, object> data)
        {
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer().Serialize(data));
            Response.End();
        }
    }
}
