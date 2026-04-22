using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
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
                    { "syncMode", config.SyncMode },
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
                case "resyncItem":
                    result = ResyncCompletedItem();
                    break;
                case "retryAllFailed":
                    result = RetryAllFailed();
                    break;
                case "mappings":
                    result = GetAccountMappings();
                    break;
                case "updateMapping":
                    result = UpdateAccountMapping();
                    break;
                case "cleanupAutoSync":
                    result = CleanupOldAutoSync();
                    break;
                case "previewCleanup":
                    result = PreviewAutoSyncCleanup();
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
                if (data.ContainsKey("syncMode")) config.SetConfig("Nexaacc_SyncMode", data["syncMode"]?.ToString() ?? "DOCUMENT");
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
                var client = new Integration.AccountingApiClient(new Integration.AccountingConfig(ConnStr), ConnStr);
                var result = System.Threading.Tasks.Task.Run(() => client.TestConnectionAsync()).Result;
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

                var client = new Integration.AccountingApiClient(config, ConnStr);
                var result = System.Threading.Tasks.Task.Run(() => client.GetAccountsAsync()).Result;
                bool success = result != null && result.data != null;

                if (!success)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถดึงข้อมูลได้" } };

                // Build lookup: AccountCode → AccountId from Nexaacc API response
                var nexaaccAccounts = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
                foreach (var acc in result.data)
                {
                    if (!string.IsNullOrEmpty(acc.AccountCode) && acc.Id != Guid.Empty)
                        nexaaccAccounts[acc.AccountCode.Trim()] = acc.Id;
                }

                // Load current mappings from Accounting_Account_Mapping
                DataTable mappings = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT ID, TakeTime_Code, Nexaacc_AccountCode FROM Accounting_Account_Mapping WHERE Is_Active = 1", null);

                int matched = 0;
                int unmatched = 0;
                var unmatchedCodes = new List<string>();

                if (mappings != null)
                {
                    foreach (DataRow row in mappings.Rows)
                    {
                        int mappingId = Convert.ToInt32(row["ID"]);
                        string ttCode = row["TakeTime_Code"]?.ToString() ?? "";
                        string accountCode = (row["Nexaacc_AccountCode"]?.ToString() ?? "").Trim();

                        if (string.IsNullOrEmpty(accountCode)) continue;

                        // Try exact match first, then prefix match (e.g., "1110" matches "111" or vice versa)
                        Guid? matchedId = null;

                        if (nexaaccAccounts.ContainsKey(accountCode))
                        {
                            matchedId = nexaaccAccounts[accountCode];
                        }
                        else
                        {
                            // Try: Nexaacc code starts with our code, or our code starts with Nexaacc code
                            foreach (var kvp in nexaaccAccounts)
                            {
                                if (kvp.Key.StartsWith(accountCode) || accountCode.StartsWith(kvp.Key))
                                {
                                    matchedId = kvp.Value;
                                    // Update the stored account code to match exactly what Nexaacc has
                                    _code.DatabaseInsertSafe(ConnStr,
                                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code WHERE ID = @id",
                                        new Dictionary<string, object> { { "@code", kvp.Key }, { "@id", mappingId } });
                                    break;
                                }
                            }
                        }

                        if (matchedId.HasValue)
                        {
                            _code.DatabaseInsertSafe(ConnStr,
                                "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountId = @accId WHERE ID = @id",
                                new Dictionary<string, object> { { "@accId", matchedId.Value }, { "@id", mappingId } });
                            matched++;
                        }
                        else
                        {
                            unmatched++;
                            unmatchedCodes.Add($"{ttCode}({accountCode})");
                        }
                    }
                }

                string msg = $"ดึง Chart of Accounts สำเร็จ — จับคู่ได้ {matched} รายการ";
                if (unmatched > 0)
                    msg += $", ไม่พบ {unmatched} รายการ: {string.Join(", ", unmatchedCodes)}";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", msg },
                    { "matched", matched },
                    { "unmatched", unmatched },
                    { "nexaaccTotal", nexaaccAccounts.Count }
                };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                if (inner is Integration.AccountingApiException apiEx)
                {
                    if (apiEx.StatusCode == 401)
                        return new Dictionary<string, object> { { "success", false }, { "message", "API Key ไม่ถูกต้องหรือหมดอายุ (401) — กรุณาตรวจสอบ API Key" } };
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Nexaacc API Error ({apiEx.StatusCode}): {apiEx.ResponseBody}" } };
                }
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
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
                int processed = System.Threading.Tasks.Task.Run(() => sync.ProcessQueueAsync(50)).Result;

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
                // Parse pagination & filter params
                int page = 1, pageSize = 20;
                int.TryParse(Request.QueryString["page"] ?? "1", out page);
                int.TryParse(Request.QueryString["pageSize"] ?? "20", out pageSize);
                string statusFilter = Request.QueryString["status"] ?? "";
                if (page < 1) page = 1;
                if (pageSize < 5) pageSize = 5;
                if (pageSize > 100) pageSize = 100;

                // Get summary counts (all time)
                DataTable summary = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT Status, COUNT(*) as Cnt
                      FROM Accounting_Sync_Queue
                      GROUP BY Status", null);

                int pending = 0, processing = 0, completed = 0, failed = 0;
                if (summary != null)
                {
                    foreach (DataRow row in summary.Rows)
                    {
                        string s = row["Status"]?.ToString() ?? "";
                        int cnt = Convert.ToInt32(row["Cnt"]);
                        switch (s)
                        {
                            case "PENDING": pending = cnt; break;
                            case "PROCESSING": processing = cnt; break;
                            case "COMPLETED": completed = cnt; break;
                            case "FAILED": failed = cnt; break;
                        }
                    }
                }

                // Build WHERE clause for status filter
                string whereClause = "";
                var queryParams = new Dictionary<string, object>();
                var validStatuses = new HashSet<string> { "PENDING", "PROCESSING", "COMPLETED", "FAILED" };
                if (!string.IsNullOrEmpty(statusFilter) && validStatuses.Contains(statusFilter.ToUpper()))
                {
                    whereClause = "WHERE Status = @statusFilter";
                    queryParams["@statusFilter"] = statusFilter.ToUpper();
                }

                // Get total count for pagination
                DataTable countDt = _code.DatabaseQuerySafe(ConnStr,
                    $"SELECT COUNT(*) as Total FROM Accounting_Sync_Queue {whereClause}",
                    queryParams.Count > 0 ? queryParams : null);
                int totalItems = countDt?.Rows.Count > 0 ? Convert.ToInt32(countDt.Rows[0]["Total"]) : 0;
                int totalPages = totalItems > 0 ? (int)Math.Ceiling((double)totalItems / pageSize) : 1;
                if (page > totalPages) page = totalPages;

                // Get paginated items using OFFSET...FETCH (SQL Server 2012+)
                int offset = (page - 1) * pageSize;
                var itemParams = new Dictionary<string, object>
                {
                    { "@offset", offset },
                    { "@pageSize", pageSize }
                };
                if (queryParams.ContainsKey("@statusFilter"))
                    itemParams["@statusFilter"] = queryParams["@statusFilter"];

                DataTable items = _code.DatabaseQuerySafe(ConnStr,
                    $@"SELECT ID, Entity_Type, Entity_ID, Action_Type, Status,
                              Retry_Count, Max_Retries, Error_Message, Created_Date
                       FROM Accounting_Sync_Queue
                       {whereClause}
                       ORDER BY Created_Date DESC
                       OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                    itemParams);

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
                    { "items", itemList },
                    { "page", page },
                    { "pageSize", pageSize },
                    { "totalItems", totalItems },
                    { "totalPages", totalPages }
                };
            }
            catch
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "pending", 0 }, { "processing", 0 }, { "completed", 0 }, { "failed", 0 },
                    { "items", new List<object>() },
                    { "page", 1 }, { "pageSize", 20 }, { "totalItems", 0 }, { "totalPages", 1 }
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

        private Dictionary<string, object> ResyncCompletedItem()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Nexaacc_Response_Id = NULL,
                          Next_Retry_Date = NULL, Error_Message = NULL, Processed_Date = NULL
                      WHERE ID = @id AND Status IN ('COMPLETED', 'FAILED')",
                    new Dictionary<string, object> { { "@id", queueId } });

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Queue #{queueId} reset เป็น PENDING — จะยิง API ใหม่รอบถั��ไป" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> GetAccountMappings()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active
                      FROM Accounting_Account_Mapping ORDER BY Mapping_Type, TakeTime_Code", null);

                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "code", row["TakeTime_Code"]?.ToString() },
                            { "description", row["TakeTime_Description"]?.ToString() },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() },
                            { "accountId", row["Nexaacc_AccountId"] != DBNull.Value ? row["Nexaacc_AccountId"].ToString() : "" },
                            { "mappingType", row["Mapping_Type"]?.ToString() },
                            { "isActive", Convert.ToBoolean(row["Is_Active"]) }
                        });
                    }
                }

                return new Dictionary<string, object> { { "success", true }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> UpdateAccountMapping()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"] ?? "0");
                string newCode = Request.QueryString["accountCode"] ?? "";

                if (id <= 0 || string.IsNullOrEmpty(newCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องระบุ id และ accountCode" } };

                // Update the account code and clear the old account ID (will be re-resolved on next fetch)
                _code.DatabaseInsertSafe(ConnStr,
                    "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code, Nexaacc_AccountId = NULL WHERE ID = @id",
                    new Dictionary<string, object> { { "@code", newCode }, { "@id", id } });

                return new Dictionary<string, object> { { "success", true }, { "message", $"อัปเดต Account Code เป็น {newCode} แล้ว — กรุณากด 'ดึง Chart of Accounts' เพื่อจับคู่ Account ID ใหม่" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> PreviewAutoSyncCleanup()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                DataTable dt = sync.GetAutoSyncCleanupPreview();
                var items = new List<Dictionary<string, object>>();
                int total = 0;
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        int count = Convert.ToInt32(row["Count"]);
                        total += count;
                        items.Add(new Dictionary<string, object>
                        {
                            { "status", row["Status"]?.ToString() },
                            { "actionType", row["Action_Type"]?.ToString() },
                            { "count", count }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "total", total }, { "items", items } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> CleanupOldAutoSync()
        {
            try
            {
                var sync = new Integration.AccountingSyncService(ConnStr);
                int cancelled = sync.CancelOldAutoSyncEntries();
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ยกเลิก auto-sync entries จำนวน {cancelled} รายการ เรียบร้อยแล้ว" },
                    { "cancelled", cancelled }
                };
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
