using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;
using System.Web.UI;

namespace Take_Time_BangPhra.Admin.Settings
{
    public partial class AccountingIntegration : Page
    {
        private readonly code _code = new code();
        private string ConnStr => ConfigurationManager.ConnectionStrings["TaketimeConnectionString"]?.ConnectionString ?? "";

        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["permission"]?.ToString() != "True")
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
            {
                LoadConfig();
                LoadQueue();
            }
        }

        private void LoadConfig()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                var data = new Dictionary<string, object>
                {
                    { "baseUrl", config.BaseUrl },
                    { "hasApiKey", !string.IsNullOrEmpty(config.ApiKey) },
                    { "companyId", config.CompanyId != Guid.Empty ? config.CompanyId.ToString() : "" },
                    { "enabled", config.Enabled },
                    { "syncInterval", config.SyncIntervalSeconds },
                    { "maxRetries", config.MaxRetries },
                    { "timeout", config.TimeoutSeconds },
                    { "isConfigured", config.IsConfigured }
                };
                hfConfigData.Value = new JavaScriptSerializer().Serialize(data);
            }
            catch
            {
                hfConfigData.Value = "{}";
            }
        }

        private void LoadQueue()
        {
            // Queue data loaded via AJAX
        }

        private void HandleAction(string action)
        {
            Dictionary<string, object> result;

            switch (action)
            {
                case "testApi":
                    result = TestApiLogin();
                    break;
                case "fetchAccounts":
                    result = FetchChartOfAccounts();
                    break;
                case "processQueue":
                    result = ProcessQueueNow();
                    break;
                case "queueData":
                    result = GetQueueData();
                    break;
                case "retryItem":
                    result = RetryQueueItem();
                    break;
                case "retryAllFailed":
                    result = RetryAllFailed();
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
            var data = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(body);
            Dictionary<string, object> result;

            switch (action)
            {
                case "saveApi":
                    result = SaveApiConfig(data);
                    break;
                case "saveSyncSettings":
                    result = SaveSyncSettings(data);
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
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("baseUrl")) config.SetConfig("Nexaacc_BaseUrl", data["baseUrl"]?.ToString() ?? "");
                if (data.ContainsKey("apiKey") && !string.IsNullOrEmpty(data["apiKey"]?.ToString()))
                    config.SetConfig("Nexaacc_ApiKey_Encrypted", _code.Crypt(data["apiKey"].ToString()));
                if (data.ContainsKey("companyId")) config.SetConfig("Nexaacc_CompanyId", data["companyId"]?.ToString() ?? "");

                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึก API Config สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> SaveSyncSettings(Dictionary<string, object> data)
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (data.ContainsKey("enabled")) config.SetConfig("Nexaacc_Enabled", data["enabled"]?.ToString() ?? "false");
                if (data.ContainsKey("syncInterval")) config.SetConfig("Nexaacc_SyncInterval_Sec", data["syncInterval"]?.ToString() ?? "30");
                if (data.ContainsKey("maxRetries")) config.SetConfig("Nexaacc_MaxRetries", data["maxRetries"]?.ToString() ?? "5");
                if (data.ContainsKey("timeout")) config.SetConfig("Nexaacc_TimeoutSec", data["timeout"]?.ToString() ?? "30");

                return new Dictionary<string, object> { { "success", true }, { "message", "บันทึก Sync Settings สำเร็จ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> TestApiLogin()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (string.IsNullOrEmpty(config.BaseUrl))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Base URL" } };
                if (string.IsNullOrEmpty(config.ApiKey))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า API Key" } };

                // Test API Key by fetching Chart of Accounts
                var testUrl = config.BaseUrl.TrimEnd('/') + $"/api/companies/{config.CompanyId}/accounting/accounts";
                var request = (HttpWebRequest)WebRequest.Create(testUrl);
                request.Method = "GET";
                request.Headers.Add("X-Api-Key", config.ApiKey);
                request.Timeout = 15000;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    sw.Stop();
                    string responseBody = reader.ReadToEnd();
                    bool isSuccess = response.StatusCode == HttpStatusCode.OK;
                    return new Dictionary<string, object>
                    {
                        { "success", isSuccess },
                        { "message", isSuccess ? $"API Key ใช้งานได้ ({sw.ElapsedMilliseconds}ms)" : "API Key ไม่สามารถใช้งานได้" }
                    };
                }
            }
            catch (WebException wex)
            {
                string detail = "";
                if (wex.Response != null)
                    using (var r = new StreamReader(wex.Response.GetResponseStream())) detail = r.ReadToEnd();
                return new Dictionary<string, object> { { "success", false }, { "message", "API Error: " + (detail.Length > 0 ? detail : wex.Message) } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> FetchChartOfAccounts()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (!config.IsConfigured)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Nexaacc ครบถ้วน (Base URL, API Key, Company ID)" } };

                var testUrl = config.BaseUrl.TrimEnd('/') + $"/api/companies/{config.CompanyId}/accounting/accounts";
                var request = (HttpWebRequest)WebRequest.Create(testUrl);
                request.Method = "GET";
                request.Headers.Add("X-Api-Key", config.ApiKey);
                request.Timeout = 15000;

                using (var response = (HttpWebResponse)request.GetResponse())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string responseBody = reader.ReadToEnd();
                    var result = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(responseBody);
                    bool success = result != null && result.ContainsKey("data");
                    return new Dictionary<string, object>
                    {
                        { "success", success },
                        { "message", success ? "ดึง Chart of Accounts สำเร็จ" : "ไม่สามารถดึงข้อมูลได้" }
                    };
                }
            }
            catch (WebException wex)
            {
                string detail = "";
                if (wex.Response != null)
                    using (var r = new StreamReader(wex.Response.GetResponseStream())) detail = r.ReadToEnd();
                return new Dictionary<string, object> { { "success", false }, { "message", "API Error: " + (detail.Length > 0 ? detail : wex.Message) } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ProcessQueueNow()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                var task = sync.ProcessQueueAsync(50);
                task.Wait();
                int processed = task.Result;

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ประมวลผลสำเร็จ {processed} รายการ" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", "Process Error: " + ex.Message } };
            }
        }

        private Dictionary<string, object> GetQueueData()
        {
            try
            {
                // Get summary counts
                DataTable summary = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT Status, COUNT(*) as Cnt
                      FROM Accounting_Sync_Queue
                      WHERE Created_Date >= DATEADD(DAY, -7, GETDATE())
                      GROUP BY Status", null);

                int pending = 0, processing = 0, completed = 0, failed = 0;
                if (summary != null)
                {
                    foreach (DataRow row in summary.Rows)
                    {
                        string status = row["Status"]?.ToString() ?? "";
                        int cnt = Convert.ToInt32(row["Cnt"]);
                        switch (status)
                        {
                            case "PENDING": pending = cnt; break;
                            case "PROCESSING": processing = cnt; break;
                            case "COMPLETED": completed = cnt; break;
                            case "FAILED": failed = cnt; break;
                        }
                    }
                }

                // Get recent items
                DataTable items = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TOP 50 ID, Entity_Type, Entity_ID, Action_Type, Status,
                             Retry_Count, Max_Retries, Error_Message, Created_Date
                      FROM Accounting_Sync_Queue
                      ORDER BY Created_Date DESC", null);

                var itemList = new List<Dictionary<string, object>>();
                if (items != null)
                {
                    foreach (DataRow row in items.Rows)
                    {
                        itemList.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt64(row["ID"]) },
                            { "entityType", row["Entity_Type"]?.ToString() },
                            { "entityId", Convert.ToInt32(row["Entity_ID"]) },
                            { "actionType", row["Action_Type"]?.ToString() },
                            { "status", row["Status"]?.ToString() },
                            { "retryCount", Convert.ToInt32(row["Retry_Count"]) },
                            { "maxRetries", Convert.ToInt32(row["Max_Retries"]) },
                            { "error", row["Error_Message"]?.ToString() ?? "" },
                            { "created", Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM HH:mm") }
                        });
                    }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "pending", pending },
                    { "processing", processing },
                    { "completed", completed },
                    { "failed", failed },
                    { "items", itemList }
                };
            }
            catch
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "pending", 0 }, { "processing", 0 }, { "completed", 0 }, { "failed", 0 },
                    { "items", new List<object>() }
                };
            }
        }

        private Dictionary<string, object> RetryQueueItem()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                var sync = new Integration.AccountingSyncService(ConnStr);
                sync.RetryItem(queueId);
                return new Dictionary<string, object> { { "success", true }, { "message", $"Reset queue item #{queueId} to PENDING" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> RetryAllFailed()
        {
            try
            {
                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL, Error_Message = NULL
                      WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries", null);

                return new Dictionary<string, object> { { "success", true }, { "message", "Reset failed items ทั้งหมดเป็น PENDING แล้ว" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private void WriteJson(Dictionary<string, object> data)
        {
            Response.ContentType = "application/json";
            Response.Write(new JavaScriptSerializer().Serialize(data));
            Response.End();
        }
    }
}
