using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
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
                    { "receiptSyncMode", config.ReceiptSyncMode },
                    { "voucherSyncMode", config.VoucherSyncMode },
                    { "payrollSyncMode", config.PayrollSyncMode },
                    { "attachFiles", config.AttachFiles },
                    { "depositVatRecognition", config.DepositVatRecognition },
                    { "etaxAutoGenerate", config.IsEtaxAutoGenerate },
                    { "etaxAutoSign", config.IsEtaxAutoSign },
                    { "etaxAutoSubmit", config.IsEtaxAutoSubmit },
                    { "etaxAutoSendEmail", config.IsEtaxAutoSendEmail },
                    { "etaxEmailSubject", config.EtaxEmailSubject },
                    { "etaxEmailBody", config.EtaxEmailBody },
                    { "etaxEmailAttachPdf", config.EtaxEmailAttachPdf },
                    { "etaxEmailAttachXml", config.EtaxEmailAttachXml },
                    { "etaxEmailLocalOnly", config.EtaxEmailLocalOnly },
                    { "etaxEmailFallback", config.EtaxEmailFallback },
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
                case "syncAccounts":
                    result = SyncChartOfAccounts();
                    break;
                case "nexaaccAccounts":
                    result = GetNexaaccAccounts();
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
                case "getPaidHowMapping":
                    result = GetPaidHowMapping();
                    break;
                case "getPaidTypeMapping":
                    result = GetPaidTypeMapping();
                    break;
                case "updatePaidHowAccount":
                    result = UpdatePaidHowAccount();
                    break;
                case "updatePaidTypeAccount":
                    result = UpdatePaidTypeAccount();
                    break;
                case "lookupDocSource":
                    result = LookupDocumentSource();
                    break;
                case "depositStatus":
                    result = LookupDepositStatus();
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
                case "deleteQueueItems":
                    result = DeleteQueueItems(data);
                    break;
                case "etaxGenerate":
                    result = ManualEtaxGenerate(data);
                    break;
                case "etaxSendEmail":
                    result = ManualEtaxSendEmail(data);
                    break;
                case "depositManual":
                    result = ManualDepositOperation(data);
                    break;
                case "stockAdjustment":
                    result = ManualStockAdjustment(data);
                    break;
                case "stockProductSync":
                    result = ManualProductSync(data);
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
                if (data.ContainsKey("receiptSyncMode")) config.SetConfig("Nexaacc_SyncMode_Receipt", data["receiptSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("voucherSyncMode")) config.SetConfig("Nexaacc_SyncMode_Voucher", data["voucherSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("payrollSyncMode")) config.SetConfig("Nexaacc_SyncMode_Payroll", data["payrollSyncMode"]?.ToString() ?? "");
                if (data.ContainsKey("attachFiles")) config.SetConfig("Nexaacc_AttachFiles", data["attachFiles"]?.ToString() ?? "true");
                if (data.ContainsKey("depositVatRecognition"))
                {
                    string dvr = (data["depositVatRecognition"]?.ToString() ?? "CHECKOUT").ToUpper();
                    if (dvr != "RECEIPT" && dvr != "CHECKOUT") dvr = "CHECKOUT";
                    config.SetConfig("Deposit_Vat_Recognition", dvr);
                }
                if (data.ContainsKey("etaxAutoGenerate")) config.SetConfig("Etax_AutoGenerate", BoolToFlag(data["etaxAutoGenerate"]));
                if (data.ContainsKey("etaxAutoSign")) config.SetConfig("Etax_AutoSign", BoolToFlag(data["etaxAutoSign"]));
                if (data.ContainsKey("etaxAutoSubmit")) config.SetConfig("Etax_AutoSubmit", BoolToFlag(data["etaxAutoSubmit"]));
                if (data.ContainsKey("etaxAutoSendEmail")) config.SetConfig("Etax_AutoSendEmail", BoolToFlag(data["etaxAutoSendEmail"]));
                if (data.ContainsKey("etaxEmailSubject")) config.SetConfig("Etax_EmailSubject", data["etaxEmailSubject"]?.ToString() ?? "");
                if (data.ContainsKey("etaxEmailBody")) config.SetConfig("Etax_EmailBody", data["etaxEmailBody"]?.ToString() ?? "");
                if (data.ContainsKey("etaxEmailAttachPdf")) config.SetConfig("Etax_EmailAttachPdf", data["etaxEmailAttachPdf"]?.ToString() ?? "true");
                if (data.ContainsKey("etaxEmailAttachXml")) config.SetConfig("Etax_EmailAttachXml", data["etaxEmailAttachXml"]?.ToString() ?? "false");
                if (data.ContainsKey("etaxEmailLocalOnly")) config.SetConfig("Etax_EmailLocalOnly", data["etaxEmailLocalOnly"]?.ToString() ?? "false");
                if (data.ContainsKey("etaxEmailFallback")) config.SetConfig("Etax_EmailFallback", data["etaxEmailFallback"]?.ToString() ?? "true");
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
            // Delegate to SyncChartOfAccounts (same functionality, now with cache)
            return SyncChartOfAccounts();
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

                // Detect optional doc-cache columns (PHASE12 Migration 14)
                DataTable colCheck = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT name FROM sys.columns
                      WHERE object_id = OBJECT_ID('Accounting_Sync_Queue')
                        AND name IN ('Nexaacc_Document_Number','Nexaacc_Document_Type')", null);
                bool hasDocCache = colCheck != null && colCheck.Rows.Count >= 2;

                string docCacheCols = hasDocCache
                    ? ", Nexaacc_Document_Number, Nexaacc_Document_Type"
                    : ", CAST(NULL AS NVARCHAR(50)) AS Nexaacc_Document_Number, CAST(NULL AS NVARCHAR(30)) AS Nexaacc_Document_Type";

                DataTable items = _code.DatabaseQuerySafe(ConnStr,
                    $@"SELECT ID, Entity_Type, Entity_ID, Action_Type, Status,
                              Retry_Count, Max_Retries, Error_Message, Created_Date, Payload,
                              Nexaacc_Response_Id{docCacheCols}
                       FROM Accounting_Sync_Queue
                       {whereClause}
                       ORDER BY Created_Date DESC
                       OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                    itemParams);

                var config = new Integration.AccountingConfig(ConnStr);
                bool isOwner = Session["User"]?.ToString() == "Owner";
                var itemList = new List<Dictionary<string, object>>();
                if (items != null)
                {
                    foreach (DataRow row in items.Rows)
                    {
                        string actionType = row["Action_Type"]?.ToString() ?? "";
                        string entityType = row["Entity_Type"]?.ToString() ?? "";
                        bool isPayroll = actionType == "CREATE_PAYROLL_ENTRY"
                            || entityType == "PAYROLL";
                        bool isEmployeeVoucher = false;
                        if (!isPayroll && actionType == "CREATE_VOUCHER_JOURNAL")
                        {
                            string payload = row.Table.Columns.Contains("Payload")
                                ? (row["Payload"]?.ToString() ?? "") : "";
                            isEmployeeVoucher = payload.Contains("\"expenseCategory\":\"เงินเดือน\"")
                                || payload.Contains("\"expenseCategory\":\"salary\"")
                                || payload.Contains("\"description\":\"เงินเดือน");
                        }
                        bool isSensitive = isPayroll || isEmployeeVoucher;
                        bool mask = isSensitive && !isOwner;

                        string nexaaccId = row.Table.Columns.Contains("Nexaacc_Response_Id")
                            ? (row["Nexaacc_Response_Id"]?.ToString() ?? "") : "";
                        string nexaaccDocNum = row.Table.Columns.Contains("Nexaacc_Document_Number")
                            ? (row["Nexaacc_Document_Number"]?.ToString() ?? "") : "";
                        string nexaaccDocType = row.Table.Columns.Contains("Nexaacc_Document_Type")
                            ? (row["Nexaacc_Document_Type"]?.ToString() ?? "") : "";
                        string nexaaccUrl = "";
                        if (!mask && !string.IsNullOrEmpty(nexaaccId) && !nexaaccId.StartsWith("SKIPPED") && config.IsConfigured)
                        {
                            string basePath = config.RawBaseUrl.TrimEnd('/');
                            string cid = config.CompanyId.ToString();
                            string typePath = "documents";
                            switch ((nexaaccDocType ?? "").ToUpper())
                            {
                                case "INVOICE": typePath = "invoices"; break;
                                case "EXPENSE": typePath = "expenses"; break;
                                case "JOURNAL": typePath = "journals"; break;
                                case "CREDIT_NOTE": typePath = "credit-notes"; break;
                                case "DEBIT_NOTE": typePath = "debit-notes"; break;
                            }
                            nexaaccUrl = $"{basePath}/{cid}/{typePath}/{nexaaccId}";
                        }

                        string errorMsg = row["Error_Message"]?.ToString() ?? "";
                        if (mask && !string.IsNullOrEmpty(errorMsg))
                            errorMsg = "🔒 ข้อมูลถูกจำกัดการเข้าถึง";

                        itemList.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt64(row["ID"]) },
                            { "entityType", mask ? "PAYROLL" : entityType },
                            { "entityId", mask ? 0 : Convert.ToInt32(row["Entity_ID"]) },
                            { "actionType", actionType },
                            { "status", row["Status"]?.ToString() },
                            { "retryCount", Convert.ToInt32(row["Retry_Count"]) },
                            { "maxRetries", Convert.ToInt32(row["Max_Retries"]) },
                            { "error", errorMsg },
                            { "created", Convert.ToDateTime(row["Created_Date"]).ToString("dd/MM HH:mm") },
                            { "nexaaccId", mask ? "" : nexaaccId },
                            { "nexaaccDocNumber", mask ? "🔒" : nexaaccDocNum },
                            { "nexaaccDocType", mask ? "" : nexaaccDocType },
                            { "nexaaccUrl", mask ? "" : nexaaccUrl },
                            { "sensitive", isSensitive }
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
            catch (Exception ex)
            {
                try { _code.Logs(ConnStr, "AccountingIntegration", $"GetQueueData error: {ex.Message}", "SYSTEM"); }
                catch { }
                return new Dictionary<string, object>
                {
                    { "success", false },
                    { "message", "Queue query failed: " + ex.Message },
                    { "pending", 0 }, { "processing", 0 }, { "completed", 0 }, { "failed", 0 },
                    { "items", new List<object>() },
                    { "page", 1 }, { "pageSize", 20 }, { "totalItems", 0 }, { "totalPages", 1 }
                };
            }
        }

        private bool IsSensitiveQueueItem(long queueId)
        {
            var dt = _code.DatabaseQuerySafe(ConnStr,
                "SELECT Action_Type, Entity_Type FROM Accounting_Sync_Queue WHERE ID = @id",
                new Dictionary<string, object> { { "@id", queueId } });
            if (dt == null || dt.Rows.Count == 0) return false;
            string action = dt.Rows[0]["Action_Type"]?.ToString() ?? "";
            string entity = dt.Rows[0]["Entity_Type"]?.ToString() ?? "";
            return action == "CREATE_PAYROLL_ENTRY" || entity == "PAYROLL";
        }

        private Dictionary<string, object> RetryQueueItem()
        {
            try
            {
                long queueId = long.Parse(Request.QueryString["queueId"] ?? "0");
                if (Session["User"]?.ToString() != "Owner" && IsSensitiveQueueItem(queueId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ดำเนินการกับรายการเงินเดือน" } };
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
                bool isOwner = Session["User"]?.ToString() == "Owner";
                string sql = @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL, Error_Message = NULL
                      WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries";
                if (!isOwner)
                    sql += " AND Action_Type != 'CREATE_PAYROLL_ENTRY' AND ISNULL(Entity_Type,'') != 'PAYROLL'";

                _code.DatabaseInsertSafe(ConnStr, sql, null);

                string msg = isOwner
                    ? "Reset failed items ทั้งหมดเป็น PENDING แล้ว"
                    : "Reset failed items เป็น PENDING แล้ว (ไม่รวมรายการเงินเดือน)";
                return new Dictionary<string, object> { { "success", true }, { "message", msg } };
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
                if (Session["User"]?.ToString() != "Owner" && IsSensitiveQueueItem(queueId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ดำเนินการกับรายการเงินเดือน" } };

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING', Retry_Count = 0, Nexaacc_Response_Id = NULL,
                          Next_Retry_Date = NULL, Error_Message = NULL, Processed_Date = NULL
                      WHERE ID = @id AND Status IN ('COMPLETED', 'FAILED')",
                    new Dictionary<string, object> { { "@id", queueId } });

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Queue #{queueId} reset เป็น PENDING — จะยิง API ใหม่รอบถัดไป" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> DeleteQueueItems(Dictionary<string, object> data)
        {
            try
            {
                if (!data.ContainsKey("ids"))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มี ids" } };

                var rawIds = data["ids"] as System.Collections.ArrayList;
                if (rawIds == null || rawIds.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกรายการ" } };

                bool isOwner = Session["User"]?.ToString() == "Owner";
                var idList = new List<string>();
                int blocked = 0;
                foreach (var id in rawIds)
                {
                    long qid = Convert.ToInt64(id);
                    if (!isOwner && IsSensitiveQueueItem(qid))
                    {
                        blocked++;
                        continue;
                    }
                    idList.Add(qid.ToString());
                }

                if (idList.Count == 0 && blocked > 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่มีสิทธิ์ลบรายการเงินเดือน" } };

                if (idList.Count > 0)
                {
                    string idsCsv = string.Join(",", idList);
                    _code.DatabaseInsertSafe(ConnStr,
                        $"DELETE FROM Accounting_Sync_Queue WHERE ID IN ({idsCsv})", null);
                }

                string msg = $"ลบ {idList.Count} รายการจาก Queue สำเร็จ";
                if (blocked > 0) msg += $" (ข้าม {blocked} รายการเงินเดือน — ต้องเป็น Owner)";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", msg }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Chart of Accounts Sync — ดึงผังบัญชีจาก NextAcc แล้ว cache ไว้ local
        // ──────────────────────────────────────────────

        private void EnsureAccountsCacheTable()
        {
            _code.DatabaseInsertSafe(ConnStr, @"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Accounting_Nexaacc_Accounts')
                BEGIN
                    CREATE TABLE Accounting_Nexaacc_Accounts (
                        Nexaacc_AccountId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        Account_Code NVARCHAR(20) NOT NULL,
                        Account_Name NVARCHAR(255),
                        Account_Name_En NVARCHAR(255),
                        Account_Type NVARCHAR(50),
                        Account_Type_Value INT DEFAULT 0,
                        Parent_Account_Id UNIQUEIDENTIFIER NULL,
                        Account_Level INT DEFAULT 0,
                        Is_Active BIT DEFAULT 1,
                        Is_System_Account BIT DEFAULT 0,
                        Description NVARCHAR(500),
                        Last_Synced DATETIME DEFAULT GETDATE()
                    )
                END", null);
        }

        private Dictionary<string, object> SyncChartOfAccounts()
        {
            try
            {
                var config = new Integration.AccountingConfig(ConnStr);
                if (!config.IsConfigured)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ยังไม่ได้ตั้งค่า Nexaacc ครบถ้วน (Base URL, API Key, Company ID)" } };

                // Chart of Accounts endpoint ({company}/accounting/accounts) ต้องใช้ API Key (acc_)
                // ใช้กับ Integration Key (int_) ไม่ได้ — แต่ระบบ sync เอกสารไม่ต้องพึ่ง chart sync แล้ว
                // (journal/invoice ใช้ Nexaacc_AccountCode ที่ seed ไว้ใน Accounting_Account_Mapping โดยตรง)
                if (config.IsIntegrationKey)
                {
                    return new Dictionary<string, object>
                    {
                        { "success", false },
                        { "message", "ℹ️ ไม่จำเป็นต้องดึง Chart of Accounts เมื่อใช้ Integration Key (int_)\n\n" +
                            "ระบบ sync เอกสาร (journal/invoice/ใบเสร็จ) ใช้รหัสบัญชี (AccountCode) ที่ตั้งค่าไว้แล้วใน " +
                            "ตาราง Accounting_Account_Mapping โดยตรง — sync ได้เลยโดยไม่ต้องดึง chart\n\n" +
                            "หมายเหตุ: endpoint ดึง Chart of Accounts ต้องใช้ API Key (acc_) ซึ่งเป็นคนละประเภทกับ " +
                            "Integration Key — ถ้าต้องการ refresh chart ให้ใช้ API Key (acc_) ชั่วคราว" }
                    };
                }

                var client = new Integration.AccountingApiClient(config, ConnStr);
                var result = System.Threading.Tasks.Task.Run(() => client.GetAccountsAsync()).Result;
                bool success = result != null && result.data != null;

                if (!success)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถดึงข้อมูลจาก Nexaacc ได้" } };

                EnsureAccountsCacheTable();

                int upserted = 0;
                foreach (var acc in result.data)
                {
                    if (acc.Id == Guid.Empty || string.IsNullOrEmpty(acc.AccountCode)) continue;

                    _code.DatabaseInsertSafe(ConnStr, @"
                        IF EXISTS (SELECT 1 FROM Accounting_Nexaacc_Accounts WHERE Nexaacc_AccountId = @id)
                            UPDATE Accounting_Nexaacc_Accounts
                            SET Account_Code = @code, Account_Name = @name, Account_Name_En = @nameEn,
                                Account_Type = @type, Account_Type_Value = @typeVal,
                                Parent_Account_Id = @parentId, Account_Level = @level,
                                Is_Active = @isActive, Is_System_Account = @isSys,
                                Description = @desc, Last_Synced = GETDATE()
                            WHERE Nexaacc_AccountId = @id
                        ELSE
                            INSERT INTO Accounting_Nexaacc_Accounts
                                (Nexaacc_AccountId, Account_Code, Account_Name, Account_Name_En,
                                 Account_Type, Account_Type_Value, Parent_Account_Id, Account_Level,
                                 Is_Active, Is_System_Account, Description, Last_Synced)
                            VALUES (@id, @code, @name, @nameEn, @type, @typeVal, @parentId, @level,
                                    @isActive, @isSys, @desc, GETDATE())",
                        new Dictionary<string, object>
                        {
                            { "@id", acc.Id },
                            { "@code", acc.AccountCode.Trim() },
                            { "@name", acc.AccountName ?? "" },
                            { "@nameEn", acc.AccountNameEn ?? "" },
                            { "@type", acc.AccountType ?? "" },
                            { "@typeVal", acc.AccountTypeValue },
                            { "@parentId", (object)acc.ParentAccountId ?? DBNull.Value },
                            { "@level", acc.Level },
                            { "@isActive", acc.IsActive },
                            { "@isSys", acc.IsSystemAccount },
                            { "@desc", acc.Description ?? "" }
                        });
                    upserted++;
                }

                // Fix legacy wrong codes before matching
                int fixed5d = FixLegacyAccountCodes();

                // Auto-match: update Accounting_Account_Mapping with matched GUIDs (exact match only)
                int matched = AutoMatchMappings();

                string msg = $"Sync ผังบั���ชีสำเร็จ — ดึงมา {upserted} บัญชี, จับคู่ mapping ได้ {matched} รายการ";
                if (fixed5d > 0)
                    msg += $", แก้รหัสเก่า {fixed5d} รายการ";

                // Count unmatched for warning
                DataTable unmatchedDt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TakeTime_Code, Nexaacc_AccountCode FROM Accounting_Account_Mapping
                      WHERE Is_Active = 1 AND (Nexaacc_AccountId IS NULL OR Nexaacc_AccountCode = '')", null);
                int unmatched = unmatchedDt?.Rows.Count ?? 0;
                if (unmatched > 0)
                    msg += $"\n⚠ ยังไม่ได้จับคู่ {unmatched} รายการ — กรุณาเลือกบัญชีจาก dropdown ในหน้า Mapping";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", msg },
                    { "totalAccounts", upserted },
                    { "matched", matched },
                    { "unmatched", unmatched }
                };
            }
            catch (AggregateException aex)
            {
                var inner = aex.InnerException ?? aex;
                if (inner is Integration.AccountingApiException apiEx)
                {
                    if (apiEx.StatusCode == 401)
                        return new Dictionary<string, object> { { "success", false }, { "message", "API Key ไม่ถูกต้องหรือหมดอายุ (401)" } };
                    return new Dictionary<string, object> { { "success", false }, { "message", $"Nexaacc API Error ({apiEx.StatusCode}): {apiEx.ResponseBody}" } };
                }
                return new Dictionary<string, object> { { "success", false }, { "message", inner.Message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private int AutoMatchMappings()
        {
            DataTable mappings = _code.DatabaseQuerySafe(ConnStr,
                "SELECT ID, TakeTime_Code, Nexaacc_AccountCode FROM Accounting_Account_Mapping WHERE Is_Active = 1", null);

            if (mappings == null) return 0;

            int matched = 0;
            foreach (DataRow row in mappings.Rows)
            {
                int mappingId = Convert.ToInt32(row["ID"]);
                string accountCode = (row["Nexaacc_AccountCode"]?.ToString() ?? "").Trim();
                if (string.IsNullOrEmpty(accountCode)) continue;

                // Exact match only — no prefix matching (prefix caused wrong matches like "1111" → "11111")
                DataTable found = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 Nexaacc_AccountId FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code AND Is_Active = 1",
                    new Dictionary<string, object> { { "@code", accountCode } });

                if (found?.Rows.Count > 0)
                {
                    Guid matchedId = (Guid)found.Rows[0]["Nexaacc_AccountId"];
                    _code.DatabaseInsertSafe(ConnStr,
                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountId = @accId WHERE ID = @id",
                        new Dictionary<string, object> { { "@accId", matchedId }, { "@id", mappingId } });
                    matched++;
                }
            }
            return matched;
        }

        private int FixLegacyAccountCodes()
        {
            // Fix old 3-4 digit codes that don't match NextAcc's 5-digit codes
            // Only fix if the old code doesn't exist in cache but a correct 5-digit code does
            var fixes = new Dictionary<string, string>
            {
                // Asset codes known from NextAcc chart
                { "111", "11111" },   // เงินสด → exact leaf account
                { "1150", "11500" },  // สินค้าคงเหลือ
                { "1160", "11610" },  // ภาษีซื้อ → ภาษีซื้อ ภ.พ.30
            };

            int fixCount = 0;
            foreach (var fix in fixes)
            {
                // Only fix if old code is still in mapping AND new code exists in cache AND old code doesn't exist in cache
                DataTable hasOld = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", fix.Key } });
                DataTable hasNew = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", fix.Value } });

                if ((hasOld == null || hasOld.Rows.Count == 0) && hasNew?.Rows.Count > 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"UPDATE Accounting_Account_Mapping
                          SET Nexaacc_AccountCode = @newCode, Nexaacc_AccountId = NULL
                          WHERE Nexaacc_AccountCode = @oldCode AND Is_Active = 1",
                        new Dictionary<string, object> { { "@oldCode", fix.Key }, { "@newCode", fix.Value } });
                    fixCount++;
                }
            }

            // Clear codes that are definitely wrong (old 4-digit bank codes that don't exist in NextAcc)
            // These will show as "Not Linked" and user picks correct one from dropdown
            string[] legacyBankCodes = { "1111", "1112", "1113", "1114" };
            foreach (string code in legacyBankCodes)
            {
                DataTable exists = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", code } });

                if (exists == null || exists.Rows.Count == 0)
                {
                    DataTable affected = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT COUNT(*) AS cnt FROM Accounting_Account_Mapping WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                        new Dictionary<string, object> { { "@code", code } });
                    int cnt = affected?.Rows.Count > 0 ? Convert.ToInt32(affected.Rows[0]["cnt"]) : 0;
                    if (cnt > 0)
                    {
                        _code.DatabaseInsertSafe(ConnStr,
                            @"UPDATE Accounting_Account_Mapping
                              SET Nexaacc_AccountCode = '', Nexaacc_AccountId = NULL
                              WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                            new Dictionary<string, object> { { "@code", code } });
                        fixCount += cnt;
                    }
                }
            }

            // Also clear old liability/revenue/expense codes that don't exist
            string[] legacyOtherCodes = { "2110", "21510", "2140", "2150", "2160", "411", "4200", "4210", "4300", "4900", "5100", "5200", "5210", "5300", "5400", "5500", "5600", "5900" };
            foreach (string code in legacyOtherCodes)
            {
                DataTable exists = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 1 FROM Accounting_Nexaacc_Accounts WHERE Account_Code = @code",
                    new Dictionary<string, object> { { "@code", code } });

                if (exists == null || exists.Rows.Count == 0)
                {
                    DataTable affected = _code.DatabaseQuerySafe(ConnStr,
                        "SELECT COUNT(*) AS cnt FROM Accounting_Account_Mapping WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                        new Dictionary<string, object> { { "@code", code } });
                    int cnt = affected?.Rows.Count > 0 ? Convert.ToInt32(affected.Rows[0]["cnt"]) : 0;
                    if (cnt > 0)
                    {
                        _code.DatabaseInsertSafe(ConnStr,
                            @"UPDATE Accounting_Account_Mapping
                              SET Nexaacc_AccountCode = '', Nexaacc_AccountId = NULL
                              WHERE Nexaacc_AccountCode = @code AND Is_Active = 1",
                            new Dictionary<string, object> { { "@code", code } });
                        fixCount += cnt;
                    }
                }
            }

            return fixCount;
        }

        private Dictionary<string, object> GetNexaaccAccounts()
        {
            try
            {
                EnsureAccountsCacheTable();

                string typeFilter = Request.QueryString["type"] ?? "";
                string sql = @"SELECT Nexaacc_AccountId, Account_Code, Account_Name, Account_Name_En,
                                      Account_Type, Account_Type_Value, Account_Level, Is_Active, Last_Synced
                               FROM Accounting_Nexaacc_Accounts WHERE Is_Active = 1";
                Dictionary<string, object> sqlParams = null;

                if (!string.IsNullOrEmpty(typeFilter))
                {
                    sql += " AND Account_Type = @type";
                    sqlParams = new Dictionary<string, object> { { "@type", typeFilter } };
                }
                sql += " ORDER BY Account_Code";

                DataTable dt = _code.DatabaseQuerySafe(ConnStr, sql, sqlParams);
                var items = new List<Dictionary<string, object>>();
                DateTime? lastSync = null;

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", row["Nexaacc_AccountId"].ToString() },
                            { "code", row["Account_Code"]?.ToString() },
                            { "name", row["Account_Name"]?.ToString() },
                            { "nameEn", row["Account_Name_En"]?.ToString() },
                            { "type", row["Account_Type"]?.ToString() },
                            { "typeValue", Convert.ToInt32(row["Account_Type_Value"]) },
                            { "level", Convert.ToInt32(row["Account_Level"]) }
                        });
                        if (lastSync == null && row["Last_Synced"] != DBNull.Value)
                            lastSync = Convert.ToDateTime(row["Last_Synced"]);
                    }
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "items", items },
                    { "total", items.Count },
                    { "lastSync", lastSync?.ToString("dd/MM/yyyy HH:mm") ?? "ยังไม่เคย sync" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message }, { "items", new List<object>() } };
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
                string accountId = Request.QueryString["accountId"] ?? "";

                if (id <= 0 || string.IsNullOrEmpty(newCode))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้องระบุ id และ accountCode" } };

                if (!string.IsNullOrEmpty(accountId) && Guid.TryParse(accountId, out Guid parsedId))
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code, Nexaacc_AccountId = @accId WHERE ID = @id",
                        new Dictionary<string, object> { { "@code", newCode }, { "@accId", parsedId }, { "@id", id } });
                    return new Dictionary<string, object> { { "success", true }, { "message", $"จับคู่ {newCode} สำเร็จ (Account ID linked)" } };
                }
                else
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        "UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = @code, Nexaacc_AccountId = NULL WHERE ID = @id",
                        new Dictionary<string, object> { { "@code", newCode }, { "@id", id } });
                    return new Dictionary<string, object> { { "success", true }, { "message", $"อัปเดต Account Code เป็น {newCode} แล้ว — กด 'Sync บัญชี' เพื่อจับคู่ Account ID" } };
                }
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

        // ──────────────────────────────────────────────
        // Payment Method → Account Mapping (Account_Paid_How)
        // ──────────────────────────────────────────────

        private Dictionary<string, object> GetPaidHowMapping()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, Paid_How,
                             ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), '') AS Nexaacc_AccountId,
                             ISNULL(Nexaacc_AccountCode, '') AS Nexaacc_AccountCode,
                             Status
                      FROM Account_Paid_How WHERE Status = 'True' ORDER BY ID", null);

                var items = new List<Dictionary<string, object>>();
                if (dt?.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "name", row["Paid_How"]?.ToString() ?? "" },
                            { "accountId", row["Nexaacc_AccountId"]?.ToString() ?? "" },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() ?? "" }
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

        private Dictionary<string, object> UpdatePaidHowAccount()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"]);
                string accountId = Request.QueryString["accountId"] ?? "";
                string accountCode = Request.QueryString["accountCode"] ?? "";

                if (string.IsNullOrEmpty(accountId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกบัญชี" } };

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Account_Paid_How SET Nexaacc_AccountId = @accId, Nexaacc_AccountCode = @accCode WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@accId", Guid.Parse(accountId) },
                        { "@accCode", accountCode },
                        { "@id", id }
                    });

                DataTable name = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Paid_How FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                string paidHowName = name?.Rows.Count > 0 ? name.Rows[0]["Paid_How"]?.ToString() : "";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ผูก \"{paidHowName}\" กับบัญชี {accountCode} เรียบร้อย" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Expense Category → Account Mapping (Account_Paid_Type)
        // ──────────────────────────────────────────────

        private Dictionary<string, object> GetPaidTypeMapping()
        {
            try
            {
                DataTable dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT ID, Paid_Type,
                             ISNULL(CAST(Nexaacc_AccountId AS NVARCHAR(50)), '') AS Nexaacc_AccountId,
                             ISNULL(Nexaacc_AccountCode, '') AS Nexaacc_AccountCode,
                             Status
                      FROM Account_Paid_Type WHERE Status = 'True' ORDER BY ID", null);

                var items = new List<Dictionary<string, object>>();
                if (dt?.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "id", Convert.ToInt32(row["ID"]) },
                            { "name", row["Paid_Type"]?.ToString() ?? "" },
                            { "accountId", row["Nexaacc_AccountId"]?.ToString() ?? "" },
                            { "accountCode", row["Nexaacc_AccountCode"]?.ToString() ?? "" }
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

        private Dictionary<string, object> UpdatePaidTypeAccount()
        {
            try
            {
                int id = int.Parse(Request.QueryString["id"]);
                string accountId = Request.QueryString["accountId"] ?? "";
                string accountCode = Request.QueryString["accountCode"] ?? "";

                if (string.IsNullOrEmpty(accountId))
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่ได้เลือกบัญชี" } };

                _code.DatabaseInsertSafe(ConnStr,
                    @"UPDATE Account_Paid_Type SET Nexaacc_AccountId = @accId, Nexaacc_AccountCode = @accCode WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@accId", Guid.Parse(accountId) },
                        { "@accCode", accountCode },
                        { "@id", id }
                    });

                DataTable name = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Paid_Type FROM Account_Paid_Type WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", id } });
                string paidTypeName = name?.Rows.Count > 0 ? name.Rows[0]["Paid_Type"]?.ToString() : "";

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"ผูก \"{paidTypeName}\" กับบัญชี {accountCode} เรียบร้อย" }
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

        private static string BoolToFlag(object value)
        {
            string s = value?.ToString() ?? "";
            return (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1") ? "1" : "0";
        }

        // ──────────────────────────────────────────────
        // Document Source Lookup — query vw_Receipt_Document_Source
        // ──────────────────────────────────────────────

        private Dictionary<string, object> LookupDocumentSource()
        {
            try
            {
                string q = (Request.QueryString["q"] ?? "").Trim();
                if (string.IsNullOrEmpty(q))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จหรือ Reservation ID" } };

                string sql;
                var parameters = new Dictionary<string, object>();
                if (int.TryParse(q, out int resId))
                {
                    sql = @"SELECT TOP 50 * FROM vw_Receipt_Document_Source WHERE Reservation_ID = @resId ORDER BY Created_Date DESC";
                    parameters.Add("@resId", resId);
                }
                else
                {
                    sql = @"SELECT TOP 50 * FROM vw_Receipt_Document_Source WHERE Receipt_Number LIKE @num ORDER BY Created_Date DESC";
                    parameters.Add("@num", "%" + q + "%");
                }

                var dt = _code.DatabaseQuerySafe(ConnStr, sql, parameters);
                var config = new Integration.AccountingConfig(ConnStr);
                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        string nexaaccDocId = row["Nexaacc_Doc_Id"]?.ToString() ?? "";
                        string nexaaccUrl = "";
                        if (!string.IsNullOrEmpty(nexaaccDocId) && config.IsConfigured)
                        {
                            string basePath = config.RawBaseUrl.TrimEnd('/');
                            string cid = config.CompanyId.ToString();
                            nexaaccUrl = $"{basePath}/{cid}/documents/{nexaaccDocId}";
                        }
                        items.Add(new Dictionary<string, object>
                        {
                            { "receiptId", row["Receipt_ID"]?.ToString() },
                            { "receiptNumber", row["Receipt_Number"]?.ToString() },
                            { "reservationId", row["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(row["Reservation_ID"]) : 0 },
                            { "total", row["Total"] != DBNull.Value ? Convert.ToDecimal(row["Total"]) : 0m },
                            { "isDeposit", row["IsDeposit"] != DBNull.Value && Convert.ToBoolean(row["IsDeposit"]) },
                            { "documentSource", row["Document_Source"]?.ToString() ?? "LOCAL" },
                            { "nexaaccDocId", nexaaccDocId },
                            { "nexaaccUrl", nexaaccUrl },
                            { "syncStatus", row["Sync_Status"]?.ToString() },
                            { "syncError", row["Sync_Error"]?.ToString() },
                            { "etaxStatus", row["Etax_Status"]?.ToString() },
                            { "etaxRefNumber", row["Etax_Ref_Number"]?.ToString() },
                            { "etaxPdfUrl", row["Etax_Pdf_Url"]?.ToString() },
                            { "etaxXmlUrl", row["Etax_Xml_Url"]?.ToString() },
                            { "etaxEmailSent", row["Etax_Email_Sent"] != DBNull.Value && Convert.ToBoolean(row["Etax_Email_Sent"]) },
                            { "etaxError", row["Etax_Error"]?.ToString() },
                            { "customerName", row["Customer_FullName"]?.ToString() },
                            { "customerEmail", row["Customer_Email"]?.ToString() },
                            { "customerTaxID", row["Customer_TaxID"]?.ToString() }
                        });
                    }
                }

                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "count", items.Count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // E-Tax manual triggers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> ManualEtaxGenerate(Dictionary<string, object> data)
        {
            try
            {
                string receiptNumber = data.ContainsKey("receiptNumber") ? data["receiptNumber"]?.ToString() : null;
                if (string.IsNullOrEmpty(receiptNumber))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จ" } };

                var service = new Integration.AccountingSyncService(ConnStr);
                var (success, message, etaxRef) = System.Threading.Tasks.Task.Run(() => service.ManualGenerateEtaxAsync(receiptNumber)).Result;
                return new Dictionary<string, object>
                {
                    { "success", success },
                    { "message", message },
                    { "etaxRefNumber", etaxRef ?? "" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Stock Adjustment / Write-off / Product Sync handlers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> ManualStockAdjustment(Dictionary<string, object> data)
        {
            try
            {
                string adjustType = data.ContainsKey("adjustType") ? data["adjustType"]?.ToString() : "";
                string productIdStr = data.ContainsKey("productId") ? data["productId"]?.ToString() : "";
                string qtyStr = data.ContainsKey("quantity") ? data["quantity"]?.ToString() : "";
                string costStr = data.ContainsKey("costPerUnit") ? data["costPerUnit"]?.ToString() : "";
                string reason = data.ContainsKey("reason") ? data["reason"]?.ToString() : "";

                if (!int.TryParse(productIdStr, out int productId) || productId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Product ID ไม่ถูกต้อง" } };
                if (!decimal.TryParse(qtyStr, out decimal quantity))
                    return new Dictionary<string, object> { { "success", false }, { "message", "จำนวนไม่ถูกต้อง" } };
                if (!decimal.TryParse(costStr, out decimal costPerUnit) || costPerUnit <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ต้นทุน/หน่วยไม่ถูกต้อง" } };

                // Insert Stock_Adjustment_Log first
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT Product_Name FROM Product WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt == null || dt.Rows.Count == 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", $"ไม่พบ Product ID {productId}" } };
                string productName = dt.Rows[0]["Product_Name"]?.ToString() ?? "";

                bool isWriteoff = adjustType.Equals("WRITEOFF", StringComparison.OrdinalIgnoreCase);
                decimal absQty = Math.Abs(quantity);
                decimal totalCost = Math.Round(absQty * costPerUnit, 2);

                _code.DatabaseInsertSafe(ConnStr,
                    @"INSERT INTO Stock_Adjustment_Log
                      (Adjustment_Date, Adjustment_Type, Product_ID, Difference_Qty, Cost_PerUnit, Total_Cost, Reason, Created_Date, Sync_Status)
                      VALUES (GETDATE(), @type, @prodId, @diff, @cost, @total, @reason, GETDATE(), 'PENDING')",
                    new Dictionary<string, object>
                    {
                        { "@type", isWriteoff ? "WRITEOFF" : "COUNT_VARIANCE" },
                        { "@prodId", productId },
                        { "@diff", isWriteoff ? -absQty : quantity },
                        { "@cost", costPerUnit },
                        { "@total", totalCost },
                        { "@reason", reason ?? "" }
                    });

                var idDt = _code.DatabaseQuerySafe(ConnStr,
                    "SELECT TOP 1 ID FROM Stock_Adjustment_Log WHERE Product_ID = @id ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@id", productId } });
                long logId = idDt?.Rows.Count > 0 ? Convert.ToInt64(idDt.Rows[0]["ID"]) : 0;

                // Insert Product_In/Product_Out for stock movement
                if (isWriteoff || quantity < 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"INSERT INTO Product_Out (DateTime_Out, Product_ID, Amount, PricePerUnit, OutType, Reason)
                          VALUES (GETDATE(), @id, @qty, @cost, @type, @reason)",
                        new Dictionary<string, object>
                        {
                            { "@id", productId }, { "@qty", absQty }, { "@cost", costPerUnit },
                            { "@type", isWriteoff ? "WRITEOFF" : "ADJUSTMENT_LOSS" }, { "@reason", reason ?? "" }
                        });
                }
                else if (quantity > 0)
                {
                    _code.DatabaseInsertSafe(ConnStr,
                        @"INSERT INTO Product_In (DateTime_In, Product_ID, Amount, PricePerUnit, InType)
                          VALUES (GETDATE(), @id, @qty, @cost, 'ADJUSTMENT_GAIN')",
                        new Dictionary<string, object>
                        {
                            { "@id", productId }, { "@qty", quantity }, { "@cost", costPerUnit }
                        });
                }

                // Enqueue accounting sync
                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId;
                if (isWriteoff)
                    queueId = sync.EnqueueStockWriteOff(logId, productId, productName, absQty, costPerUnit, DateTime.Now, reason);
                else
                    queueId = sync.EnqueueStockAdjustment(logId, productId, productName, quantity, costPerUnit, DateTime.Now, reason);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"{(isWriteoff ? "Write-off" : "Adjustment")}: ส่งเข้าคิวเรียบร้อย — logId={logId}, queueId={queueId}" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualProductSync(Dictionary<string, object> data)
        {
            try
            {
                string productIdStr = data.ContainsKey("productId") ? data["productId"]?.ToString() : "";
                if (!int.TryParse(productIdStr, out int productId) || productId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Product ID ไม่ถูกต้อง" } };

                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId = sync.EnqueueProductSync(productId);
                if (queueId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถ enqueue ได้ (อาจมีรายการเดิมแล้ว หรือ config ไม่พร้อม)" } };

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "message", $"Product sync: ส่งเข้าคิวเรียบร้อย (queueId={queueId})" }
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        // ──────────────────────────────────────────────
        // Deposit Lifecycle handlers
        // ──────────────────────────────────────────────

        private Dictionary<string, object> LookupDepositStatus()
        {
            try
            {
                string q = (Request.QueryString["q"] ?? "").Trim();
                string statusFilter = (Request.QueryString["status"] ?? "").Trim();

                var sb = new System.Text.StringBuilder("SELECT TOP 100 * FROM vw_Reservation_Deposit_Status WHERE DepositPaid > 0");
                var parameters = new Dictionary<string, object>();

                if (!string.IsNullOrEmpty(q))
                {
                    if (int.TryParse(q, out int resId))
                    {
                        sb.Append(" AND Reservation_ID = @resId");
                        parameters.Add("@resId", resId);
                    }
                    else
                    {
                        sb.Append(" AND (Customer_Name LIKE @q OR Customer_MobilePhone LIKE @q)");
                        parameters.Add("@q", "%" + q + "%");
                    }
                }
                if (!string.IsNullOrEmpty(statusFilter))
                {
                    sb.Append(" AND Deposit_Status = @status");
                    parameters.Add("@status", statusFilter);
                }
                sb.Append(" ORDER BY CheckoutDate DESC, Reservation_ID DESC");

                var dt = _code.DatabaseQuerySafe(ConnStr, sb.ToString(), parameters);
                var items = new List<Dictionary<string, object>>();
                if (dt != null)
                {
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        items.Add(new Dictionary<string, object>
                        {
                            { "reservationId", Convert.ToInt32(row["Reservation_ID"]) },
                            { "reservationStatus", row["Reservation_Status"]?.ToString() },
                            { "checkinDate", row["CheckinDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckinDate"]).ToString("yyyy-MM-dd") : null },
                            { "checkoutDate", row["CheckoutDate"] != DBNull.Value ? Convert.ToDateTime(row["CheckoutDate"]).ToString("yyyy-MM-dd") : null },
                            { "customerMobilePhone", row["Customer_MobilePhone"]?.ToString() },
                            { "customerName", row["Customer_Name"]?.ToString() },
                            { "customerEmail", row["Customer_Email"]?.ToString() },
                            { "depositPaid", row["DepositPaid"] != DBNull.Value ? Convert.ToDecimal(row["DepositPaid"]) : 0m },
                            { "depositCleared", row["DepositCleared"] != DBNull.Value ? Convert.ToDecimal(row["DepositCleared"]) : 0m },
                            { "depositOutstanding", row["DepositOutstanding"] != DBNull.Value ? Convert.ToDecimal(row["DepositOutstanding"]) : 0m },
                            { "depositStatus", row["Deposit_Status"]?.ToString() ?? "OPEN" },
                            { "lastClearDate", row["LastClearDate"] != DBNull.Value ? Convert.ToDateTime(row["LastClearDate"]).ToString("yyyy-MM-ddTHH:mm:ss") : null },
                            { "lastClearAction", row["LastClearAction"]?.ToString() },
                            { "lastClearJournalId", row["LastClearJournalId"]?.ToString() }
                        });
                    }
                }
                return new Dictionary<string, object> { { "success", true }, { "items", items }, { "count", items.Count } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualDepositOperation(Dictionary<string, object> data)
        {
            try
            {
                string operation = data.ContainsKey("operation") ? data["operation"]?.ToString() : "";
                string resIdStr = data.ContainsKey("reservationId") ? data["reservationId"]?.ToString() : "";
                string amountStr = data.ContainsKey("amount") ? data["amount"]?.ToString() : "";
                string reason = data.ContainsKey("reason") ? data["reason"]?.ToString() : "";

                if (!int.TryParse(resIdStr, out int resId) || resId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "Reservation ID ไม่ถูกต้อง" } };
                if (!decimal.TryParse(amountStr, out decimal amount) || amount <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "จำนวนเงินไม่ถูกต้อง" } };

                // ดึงชื่อลูกค้า + paid type
                var dt = _code.DatabaseQuerySafe(ConnStr,
                    @"SELECT TOP 1 ISNULL(C.FullName, C.Name) AS Name,
                             (SELECT TOP 1 Paid_Type FROM Account_Receipt WHERE Reservation_ID = @id AND IsDeposit = 1 ORDER BY Created_Date DESC) AS PaidType
                      FROM Reservation R
                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                      WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", resId } });
                string customerName = dt?.Rows.Count > 0 ? dt.Rows[0]["Name"]?.ToString() ?? "" : "";
                string paymentMethod = dt?.Rows.Count > 0 ? dt.Rows[0]["PaidType"]?.ToString() ?? "CASH" : "CASH";

                var sync = new Integration.AccountingSyncService(ConnStr);
                long queueId;
                string actionLabel;

                switch (operation.ToLower())
                {
                    case "checkout":
                        queueId = sync.EnqueueDepositClearingOnCheckout(resId, amount, customerName, DateTime.Now, 0);
                        actionLabel = "ตัดมัดจำ checkout";
                        break;
                    case "refund":
                        queueId = sync.EnqueueDepositRefund(resId, amount, paymentMethod, customerName, DateTime.Now);
                        actionLabel = "คืนเงินมัดจำ";
                        break;
                    case "forfeit":
                        queueId = sync.EnqueueDepositForfeit(resId, amount, customerName,
                            DateTime.Now, !string.IsNullOrEmpty(reason) ? reason : "manual forfeit");
                        actionLabel = "ริบมัดจำ";
                        break;
                    default:
                        return new Dictionary<string, object> { { "success", false }, { "message", "operation ไม่ถูกต้อง (checkout/refund/forfeit)" } };
                }

                if (queueId <= 0)
                    return new Dictionary<string, object> { { "success", false }, { "message", "ไม่สามารถ enqueue ได้ (อาจมีรายการเดิมแล้ว หรือ config ไม่พร้อม)" } };

                return new Dictionary<string, object> { { "success", true }, { "message", $"{actionLabel}: ส่งเข้าคิวเรียบร้อย (queueId={queueId}). กดปุ่ม Process Queue เพื่อดำเนินการ" } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }

        private Dictionary<string, object> ManualEtaxSendEmail(Dictionary<string, object> data)
        {
            try
            {
                string receiptNumber = data.ContainsKey("receiptNumber") ? data["receiptNumber"]?.ToString() : null;
                string overrideEmail = data.ContainsKey("email") ? data["email"]?.ToString() : null;
                if (string.IsNullOrEmpty(receiptNumber))
                    return new Dictionary<string, object> { { "success", false }, { "message", "กรุณาระบุเลขที่ใบเสร็จ" } };

                var service = new Integration.AccountingSyncService(ConnStr);
                var (success, message) = System.Threading.Tasks.Task.Run(() => service.ManualSendEtaxEmailAsync(receiptNumber, overrideEmail)).Result;
                return new Dictionary<string, object> { { "success", success }, { "message", message } };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "success", false }, { "message", ex.Message } };
            }
        }
    }
}
