using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// Orchestrates automatic sync between TakeTime and Nexaacc Accounting.
    /// Uses a database queue for reliable async processing with retry logic.
    ///
    /// ทุก entry ต้องผูกกับเอกสารจริง — ใช้เฉพาะ:
    ///   sync.EnqueueReceipt(resId, receiptNumber, ...);
    ///   sync.EnqueuePaymentVoucher(voucherId, ..., documentNumber: docNum);
    ///   sync.EnqueueVoidReceipt(receiptNumber);
    ///   sync.EnqueueVoidPaymentVoucher(documentNumber);
    /// </summary>
    public class AccountingSyncService
    {
        private readonly code _code = new code();
        private readonly string _connectionString;
        private readonly AccountingConfig _config;
        private readonly AccountingApiClient _apiClient;
        private readonly AccountingDataMapper _mapper;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer();

        public AccountingSyncService()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
            _config = new AccountingConfig(_connectionString);
            _apiClient = new AccountingApiClient(_config, _connectionString);
            _mapper = new AccountingDataMapper(_connectionString);
        }

        public AccountingSyncService(string connectionString)
        {
            _connectionString = connectionString;
            _config = new AccountingConfig(connectionString);
            _apiClient = new AccountingApiClient(_config, connectionString);
            _mapper = new AccountingDataMapper(connectionString);
        }

        // ──────────────────────────────────────────────
        // Enqueue Methods (called from trigger points)
        // Fire-and-forget — adds to queue for background processing
        //
        // ทุก entry ต้องผูกกับเอกสารจริง (receiptNumber หรือ documentNumber)
        // ใช้เฉพาะ: EnqueueReceipt, EnqueuePaymentVoucher, EnqueueVoidReceipt, EnqueueVoidPaymentVoucher
        // ──────────────────────────────────────────────

        /// <summary>
        /// Enqueue payment voucher (expense).
        /// Call after creating voucher in Voucher/Default.aspx.cs
        /// รองรับ VAT ซื้อ (Input VAT) และภาษีหัก ณ ที่จ่าย (WHT) ตามหลักบัญชีไทย
        /// </summary>
        public long EnqueuePaymentVoucher(int voucherId, string expenseCategory, decimal amount,
            string paymentMethod, DateTime voucherDate, string description, string payeeName,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0,
            string documentNumber = null,
            string paymentAccountId = null, string expenseAccountId = null,
            List<Dictionary<string, object>> expenseLines = null)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0) return -1;

            if (!string.IsNullOrEmpty(documentNumber))
            {
                long existing = FindPendingEntry("VOUCHER", "CREATE_VOUCHER_JOURNAL", "documentNumber", documentNumber);
                if (existing > 0) return existing;
            }

            var payload = new Dictionary<string, object>
            {
                { "voucherId", voucherId },
                { "expenseCategory", expenseCategory },
                { "amount", amount },
                { "paymentMethod", paymentMethod },
                { "voucherDate", voucherDate.ToString("yyyy-MM-dd") },
                { "description", description },
                { "payeeName", payeeName },
                { "hasInputVat", hasInputVat },
                { "whtRate", whtRate },
                { "whtAmount", whtAmount }
            };
            if (!string.IsNullOrEmpty(documentNumber))
                payload["documentNumber"] = documentNumber;
            if (!string.IsNullOrEmpty(paymentAccountId))
                payload["paymentAccountId"] = paymentAccountId;
            if (!string.IsNullOrEmpty(expenseAccountId))
                payload["expenseAccountId"] = expenseAccountId;
            if (expenseLines != null && expenseLines.Count > 0)
                payload["expenseLines"] = expenseLines;

            return InsertQueue("VOUCHER", voucherId, "CREATE_VOUCHER_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue receipt document creation.
        /// Call after ReceiptService generates a receipt.
        /// </summary>
        public long EnqueueReceipt(int reservationId, string receiptNumber, decimal totalAmount, decimal vatAmount, DateTime receiptDate, string customerName,
            bool isDeposit = false, string paymentMethod = null,
            string revenueType = null, string paymentAccountId = null)
        {
            if (!_config.IsConfigured) return -1;

            long existing = FindPendingEntry("RECEIPT", "CREATE_RECEIPT_DOCUMENT", "receiptNumber", receiptNumber);
            if (existing > 0) return existing;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "receiptNumber", receiptNumber },
                { "totalAmount", totalAmount },
                { "vatAmount", vatAmount },
                { "receiptDate", receiptDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName },
                { "isDeposit", isDeposit },
                { "paymentMethod", paymentMethod ?? "CASH" }
            };
            if (!string.IsNullOrEmpty(revenueType))
                payload["revenueType"] = revenueType;
            if (!string.IsNullOrEmpty(paymentAccountId))
                payload["paymentAccountId"] = paymentAccountId;

            return InsertQueue("RECEIPT", reservationId, "CREATE_RECEIPT_DOCUMENT", payload);
        }

        // ──────────────────────────────────────────────
        // Void/Cancel Enqueue Methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// Enqueue void for a receipt that was deleted or cancelled.
        /// Looks up the original Nexaacc_Response_Id from queue and voids it.
        /// </summary>
        public long EnqueueVoidReceipt(string receiptNumber)
        {
            if (!_config.IsConfigured) return -1;

            long existing = FindPendingEntry("RECEIPT", "VOID_RECEIPT", "receiptNumber", receiptNumber);
            if (existing > 0) return existing;

            string nexaaccId = LookupNexaaccId(receiptNumber, "RECEIPT");
            if (string.IsNullOrEmpty(nexaaccId)) return -1;

            var payload = new Dictionary<string, object>
            {
                { "receiptNumber", receiptNumber },
                { "nexaaccId", nexaaccId }
            };

            return InsertQueue("RECEIPT", 0, "VOID_RECEIPT", payload);
        }

        /// <summary>
        /// Enqueue void for a payment voucher that was deleted or cancelled.
        /// </summary>
        public long EnqueueVoidPaymentVoucher(string documentNumber)
        {
            if (!_config.IsConfigured) return -1;

            long existing = FindPendingEntry("VOUCHER", "VOID_VOUCHER", "documentNumber", documentNumber);
            if (existing > 0) return existing;

            string nexaaccId = LookupNexaaccId(documentNumber, "VOUCHER");
            if (string.IsNullOrEmpty(nexaaccId)) return -1;

            var payload = new Dictionary<string, object>
            {
                { "documentNumber", documentNumber },
                { "nexaaccId", nexaaccId }
            };

            return InsertQueue("VOUCHER", 0, "VOID_VOUCHER", payload);
        }

        /// <summary>
        /// Look up the Nexaacc_Response_Id for a previously synced document.
        /// </summary>
        private string LookupNexaaccId(string documentNumber, string entityType)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Response_Id
                      FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @entityType
                        AND Status = 'COMPLETED'
                        AND Nexaacc_Response_Id IS NOT NULL
                        AND (Payload LIKE @pattern1 OR Payload LIKE @pattern2)
                      ORDER BY Processed_Date DESC",
                    new Dictionary<string, object>
                    {
                        { "@entityType", entityType },
                        { "@pattern1", $"%\"receiptNumber\":\"{documentNumber}\"%"},
                        { "@pattern2", $"%\"documentNumber\":\"{documentNumber}\"%"}
                    });

                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_Response_Id"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_Response_Id"].ToString();
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupNexaaccId failed for {entityType} '{documentNumber}': {ex.Message}", "SYSTEM");
            }
            return null;
        }

        // ──────────────────────────────────────────────
        // Account ID Lookup Helpers (for caller pages)
        // ──────────────────────────────────────────────

        public string LookupPaidHowAccountId(string paidHowText)
        {
            if (string.IsNullOrEmpty(paidHowText)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_AccountId FROM Account_Paid_How WHERE Paid_How = @name AND Status = 1",
                    new Dictionary<string, object> { { "@name", paidHowText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_AccountId"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_AccountId"].ToString();
            }
            catch { }
            return null;
        }

        public string LookupPaidTypeAccountId(string paidTypeText)
        {
            if (string.IsNullOrEmpty(paidTypeText)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_AccountId FROM Account_Paid_Type WHERE Paid_Type = @name AND Status = 1",
                    new Dictionary<string, object> { { "@name", paidTypeText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_AccountId"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_AccountId"].ToString();
            }
            catch { }
            return null;
        }

        // ──────────────────────────────────────────────
        // Queue Processing (called by background timer/scheduler)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Process pending items in the sync queue.
        /// Should be called periodically by a timer or scheduled task.
        /// </summary>
        public async Task<int> ProcessQueueAsync(int batchSize = 20)
        {
            if (!_config.IsReadyToSync) return 0;

            // Pre-flight connectivity check — skip entire batch if API unreachable
            string connectError;
            if (!_apiClient.IsApiReachable(out connectError))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessQueueAsync skipped: API unreachable — {connectError}", "SYSTEM");
                return 0;
            }

            DataTable pending = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP (@batchSize) * FROM Accounting_Sync_Queue
                  WHERE Status IN ('PENDING', 'FAILED')
                    AND (Next_Retry_Date IS NULL OR Next_Retry_Date <= GETDATE())
                    AND Retry_Count < Max_Retries
                  ORDER BY Created_Date ASC",
                new Dictionary<string, object> { { "@batchSize", batchSize } });

            if (pending == null || pending.Rows.Count == 0) return 0;

            int processed = 0;
            bool infrastructureFailed = false;

            foreach (DataRow row in pending.Rows)
            {
                // If DNS or auth failed during this batch, stop processing remaining items
                if (infrastructureFailed) break;

                long queueId = Convert.ToInt64(row["ID"]);
                string actionType = row["Action_Type"]?.ToString();
                string payload = row["Payload"]?.ToString();

                // Mark as processing
                UpdateQueueStatus(queueId, "PROCESSING", null, null);

                try
                {
                    string nexaaccId = await ProcessSingleItemAsync(actionType, payload);

                    if (nexaaccId == "SKIPPED_ZERO_AMOUNT")
                    {
                        UpdateQueueStatus(queueId, "COMPLETED", "Skipped: zero amount after fallback lookup - no accounting entry needed", nexaaccId);
                    }
                    else if (nexaaccId == "SKIPPED_DEPRECATED")
                    {
                        UpdateQueueStatus(queueId, "COMPLETED", $"Skipped: deprecated action type '{actionType}' — ไม่มีเอกสารผูก", nexaaccId);
                    }
                    else
                    {
                        UpdateQueueStatus(queueId, "COMPLETED", null, nexaaccId);
                    }
                    processed++;
                }
                catch (DnsResolutionException ex)
                {
                    // DNS/infrastructure error — don't count against item retry limit, revert to PENDING
                    UpdateQueueStatus(queueId, "PENDING", $"DNS error (not counted as retry): {ex.Message}", null);
                    infrastructureFailed = true;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessQueueAsync halted: DNS resolution failed — remaining items will be retried next cycle. Error: {ex.Message}", "SYSTEM");
                }
                catch (AuthenticationFailedException ex)
                {
                    // Auth failure (401/403) — don't count against item retry limit, revert to PENDING
                    UpdateQueueStatus(queueId, "PENDING", $"Auth error (not counted as retry): {ex.Message}", null);
                    infrastructureFailed = true;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessQueueAsync halted: API Key authentication failed — all items paused until key is fixed. Error: {ex.Message}", "SYSTEM");
                }
                catch (ArgumentException ex)
                {
                    string errorDetail = $"Queue #{queueId} [{actionType}] validation error: {ex.Message}";
                    UpdateQueueStatus(queueId, "FAILED", errorDetail, null);
                    IncrementRetry(queueId, _config.MaxRetries);
                    _code.Logs(_connectionString, "AccountingSync", errorDetail, "SYSTEM");
                }
                catch (AccountingApiException ex)
                {
                    string errorDetail = $"Queue #{queueId} [{actionType}] API {ex.StatusCode}: {ex.ResponseBody}";
                    UpdateQueueStatus(queueId, "FAILED", errorDetail, null);
                    IncrementRetry(queueId, _config.MaxRetries);
                    _code.Logs(_connectionString, "AccountingSync", errorDetail, "SYSTEM");
                }
                catch (Exception ex)
                {
                    // Check if this is a wrapped infrastructure error
                    if (IsDnsError(ex) || IsAuthError(ex))
                    {
                        UpdateQueueStatus(queueId, "PENDING", $"Infrastructure error (not counted as retry): {ex.Message}", null);
                        infrastructureFailed = true;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessQueueAsync halted: infrastructure error detected — {ex.Message}", "SYSTEM");
                    }
                    else
                    {
                        string errorDetail = $"Queue #{queueId} [{actionType}] error: {ex.Message}";
                        int retryCount = Convert.ToInt32(row["Retry_Count"]) + 1;
                        IncrementRetry(queueId, retryCount);
                        UpdateQueueStatus(queueId, "FAILED", errorDetail, null);
                        _code.Logs(_connectionString, "AccountingSync", errorDetail, "SYSTEM");
                    }
                }
            }

            return processed;
        }

        private static bool IsDnsError(Exception ex)
        {
            if (ex is DnsResolutionException) return true;
            string msg = ex.Message ?? "";
            string innerMsg = ex.InnerException?.Message ?? "";
            return msg.Contains("remote name could not be resolved") ||
                   innerMsg.Contains("remote name could not be resolved") ||
                   msg.Contains("No such host") ||
                   innerMsg.Contains("No such host");
        }

        private static bool IsAuthError(Exception ex)
        {
            if (ex is AuthenticationFailedException) return true;
            string msg = ex.Message ?? "";
            return msg.Contains("Invalid API Key") || msg.Contains("Unauthorized") || msg.Contains("authentication failed");
        }

        private async Task<string> ProcessSingleItemAsync(string actionType, string payloadJson)
        {
            Dictionary<string, object> payload;
            try
            {
                payload = _serializer.Deserialize<Dictionary<string, object>>(payloadJson);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"Invalid payload JSON for {actionType}: {ex.Message}");
            }

            if (payload == null || payload.Count == 0)
                throw new ArgumentException($"Empty or null payload for {actionType}.");

            try
            {
            switch (actionType)
            {
                // ── Active processors (ผูกกับเอกสารจริง) ──
                case "CREATE_RECEIPT_DOCUMENT":
                    return await ProcessReceiptDocument(payload);

                case "CREATE_VOUCHER_JOURNAL":
                    return await ProcessVoucherJournal(payload);

                case "VOID_RECEIPT":
                    return await ProcessVoidReceipt(payload);

                case "VOID_VOUCHER":
                    return await ProcessVoidVoucher(payload);

                // ── Deprecated: ไม่ผูกกับเอกสาร — skip ไม่ยิง API ──
                case "CREATE_DEPOSIT_JOURNAL":
                case "CREATE_PAYMENT_JOURNAL":
                case "CREATE_CHECKOUT_JOURNAL":
                case "CREATE_REFUND_JOURNAL":
                case "CREATE_ROOM_CHARGE_JOURNAL":
                case "CREATE_STOCK_IN_JOURNAL":
                case "SYNC_PRODUCT":
                case "CREATE_CREDIT_NOTE_DOCUMENT":
                case "CREATE_PAYROLL_JOURNAL":
                case "CREATE_CANCEL_NO_REFUND_JOURNAL":
                case "CREATE_POS_SALE_JOURNAL":
                case "CREATE_POSTPONE_PRICE_DIFF_JOURNAL":
                case "CREATE_PARTIAL_REFUND_JOURNAL":
                case "CREATE_DAMAGE_CHARGE_JOURNAL":
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SKIPPED deprecated action type '{actionType}' — ไม่มีเอกสารผูก ใช้ EnqueueReceipt/EnqueuePaymentVoucher แทน", "SYSTEM");
                    return "SKIPPED_DEPRECATED";

                default:
                    throw new Exception($"Unknown action type: {actionType}");
            }
            }
            catch (DnsResolutionException) { throw; }
            catch (AuthenticationFailedException) { throw; }
            catch (ArgumentException) { throw; }
            catch (AccountingApiException) { throw; }
            catch (KeyNotFoundException ex)
            {
                // Missing required field in payload — no point retrying
                throw new ArgumentException($"Missing required field in payload for {actionType}: {ex.Message}");
            }
            catch (FormatException ex)
            {
                // Invalid data format (e.g., bad date string) — no point retrying
                throw new ArgumentException($"Invalid data format in payload for {actionType}: {ex.Message}");
            }
            catch (InvalidCastException ex)
            {
                // Type conversion error — no point retrying
                throw new ArgumentException($"Invalid data type in payload for {actionType}: {ex.Message}");
            }
        }

        // ──────────────────────────────────────────────
        // Safe Post/Approve helpers (NextAcc may auto-post on create)
        // ──────────────────────────────────────────────

        private async Task SafePostJournalAsync(Guid journalId)
        {
            try
            {
                await _apiClient.PostJournalAsync(journalId);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 400 && ex.ResponseBody != null && ex.ResponseBody.Contains("Draft"))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"Journal {journalId} already posted (not Draft) - treating as success", "SYSTEM");
            }
        }

        private async Task SafeApproveDocumentAsync(Guid documentId)
        {
            try
            {
                await _apiClient.ApproveDocumentAsync(documentId);
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 400 && ex.ResponseBody != null && ex.ResponseBody.Contains("Draft"))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"Document {documentId} already approved (not Draft) - treating as success", "SYSTEM");
            }
        }

        // ──────────────────────────────────────────────
        // Individual Processors
        // ──────────────────────────────────────────────

        private async Task<string> ProcessVoucherJournal(Dictionary<string, object> p)
        {
            var amount = Convert.ToDecimal(p["amount"]);
            if (amount <= 0)
                throw new ArgumentException($"Cannot create voucher journal: amount is {amount} (must be > 0). Voucher #{p["voucherId"]}");

            int voucherId = Convert.ToInt32(p["voucherId"]);
            string expenseCategory = p["expenseCategory"]?.ToString();
            string paymentMethod = p["paymentMethod"]?.ToString();
            DateTime voucherDate = DateTime.Parse(p["voucherDate"]?.ToString());
            string description = p["description"]?.ToString();
            string payeeName = p["payeeName"]?.ToString();
            bool hasInputVat = p.ContainsKey("hasInputVat") && Convert.ToBoolean(p["hasInputVat"]);
            decimal whtRate = p.ContainsKey("whtRate") ? Convert.ToDecimal(p["whtRate"]) : 0;
            decimal whtAmount = p.ContainsKey("whtAmount") ? Convert.ToDecimal(p["whtAmount"]) : 0;
            string docNumber = p.ContainsKey("documentNumber") ? p["documentNumber"]?.ToString() : "";
            string paymentAccountId = p.ContainsKey("paymentAccountId") ? p["paymentAccountId"]?.ToString() : null;
            string expenseAccountId = p.ContainsKey("expenseAccountId") ? p["expenseAccountId"]?.ToString() : null;

            // Parse per-line expense data (if present)
            List<ExpenseLine> expenseLines = null;
            if (p.ContainsKey("expenseLines") && p["expenseLines"] != null)
            {
                try
                {
                    var rawLines = p["expenseLines"] as System.Collections.ArrayList;
                    if (rawLines != null && rawLines.Count > 0)
                    {
                        expenseLines = new List<ExpenseLine>();
                        foreach (var rawLine in rawLines)
                        {
                            var lineDict = rawLine as Dictionary<string, object>;
                            if (lineDict != null)
                            {
                                expenseLines.Add(new ExpenseLine
                                {
                                    Category = lineDict.ContainsKey("category") ? lineDict["category"]?.ToString() : expenseCategory,
                                    Description = lineDict.ContainsKey("description") ? lineDict["description"]?.ToString() : "",
                                    Amount = lineDict.ContainsKey("amount") ? Convert.ToDecimal(lineDict["amount"]) : 0,
                                    AccountId = lineDict.ContainsKey("accountId") ? lineDict["accountId"]?.ToString() : null
                                });
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _code.Logs(_connectionString, "AccountingSync", $"ProcessVoucherJournal: failed to parse expenseLines: {ex.Message}", "SYSTEM");
                }
            }

            int lineCount = expenseLines?.Count ?? 0;
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessVoucherJournal: doc={docNumber} amount={amount} category={expenseCategory} payee={payeeName} lines={lineCount} mode={(_config.IsDocumentMode ? "DOCUMENT" : "JOURNAL_ONLY")}",
                "SYSTEM");

            if (_config.IsDocumentMode)
            {
                var expense = _mapper.MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber);
                var result = await _apiClient.CreateExpenseAsync(expense);
                return result.data.Id.ToString();
            }
            else
            {
                var journal = _mapper.MapVoucherToJournal(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber);
                var result = await _apiClient.CreateJournalAsync(journal);
                await SafePostJournalAsync(result.data.Id);
                return result.data.Id.ToString();
            }
        }

        private async Task<string> ProcessReceiptDocument(Dictionary<string, object> p)
        {
            var totalAmount = Convert.ToDecimal(p["totalAmount"]);
            if (totalAmount <= 0)
                throw new ArgumentException($"Cannot create receipt document: totalAmount is {totalAmount} (must be > 0). Reservation #{p["reservationId"]}");

            int reservationId = Convert.ToInt32(p["reservationId"]);
            bool isDeposit = p.ContainsKey("isDeposit") && Convert.ToBoolean(p["isDeposit"]);
            string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : "CASH";
            string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() : "";
            DateTime receiptDate = DateTime.Parse(p["receiptDate"]?.ToString());
            decimal vatAmount = Convert.ToDecimal(p["vatAmount"]);
            string receiptNumber = p.ContainsKey("receiptNumber") ? p["receiptNumber"]?.ToString() : "";
            string revenueType = p.ContainsKey("revenueType") ? p["revenueType"]?.ToString() : null;
            string paymentAccountId = p.ContainsKey("paymentAccountId") ? p["paymentAccountId"]?.ToString() : null;

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessReceiptDocument: receipt={receiptNumber} resId={reservationId} amount={totalAmount} isDeposit={isDeposit} paymentMethod={paymentMethod} revenueType={revenueType ?? "auto"} mode={(_config.IsDocumentMode ? "DOCUMENT" : "JOURNAL_ONLY")}",
                "SYSTEM");

            if (isDeposit)
            {
                if (_config.IsDocumentMode)
                {
                    var invoice = _mapper.MapDepositToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName, paymentAccountId: paymentAccountId);
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.Reference = receiptNumber;
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    var result = await _apiClient.CreateInvoiceAsync(invoice);
                    return result.data.Id.ToString();
                }
                else
                {
                    var journal = _mapper.MapDepositToJournal(reservationId, totalAmount, paymentMethod, receiptDate, customerName, paymentAccountId: paymentAccountId, documentNumber: receiptNumber);
                    var result = await _apiClient.CreateJournalAsync(journal);
                    await SafePostJournalAsync(result.data.Id);
                    return result.data.Id.ToString();
                }
            }
            else
            {
                if (_config.IsDocumentMode)
                {
                    bool hasVat = vatAmount > 0;
                    var invoice = _mapper.MapPaymentToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId);
                    invoice.Reference = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : $"RES-{reservationId}";
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    var result = await _apiClient.CreateInvoiceAsync(invoice);
                    return result.data.Id.ToString();
                }
                else
                {
                    bool hasVat = vatAmount > 0;
                    var journal = _mapper.MapPaymentToJournal(reservationId, totalAmount, paymentMethod, receiptDate, customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId, documentNumber: receiptNumber);
                    var result = await _apiClient.CreateJournalAsync(journal);
                    await SafePostJournalAsync(result.data.Id);
                    return result.data.Id.ToString();
                }
            }
        }

        // ──────────────────────────────────────────────
        // Void/Cancel Processors
        // ──────────────────────────────────────────────

        private async Task<string> ProcessVoidReceipt(Dictionary<string, object> p)
        {
            string nexaaccId = p["nexaaccId"]?.ToString();
            if (string.IsNullOrEmpty(nexaaccId))
                throw new ArgumentException("Cannot void receipt: nexaaccId is missing");

            Guid docId = Guid.Parse(nexaaccId);

            try
            {
                if (_config.IsDocumentMode)
                    await _apiClient.VoidDocumentAsync(docId);
                else
                    await _apiClient.VoidJournalAsync(docId);
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidReceipt: nexaaccId={nexaaccId} already voided in Nexaacc — treating as success",
                    "SYSTEM");
                return $"VOIDED:{nexaaccId} (already voided)";
            }

            return $"VOIDED:{nexaaccId}";
        }

        private async Task<string> ProcessVoidVoucher(Dictionary<string, object> p)
        {
            string nexaaccId = p["nexaaccId"]?.ToString();
            if (string.IsNullOrEmpty(nexaaccId))
                throw new ArgumentException("Cannot void voucher: nexaaccId is missing");

            Guid docId = Guid.Parse(nexaaccId);

            try
            {
                if (_config.IsDocumentMode)
                    await _apiClient.VoidDocumentAsync(docId);
                else
                    await _apiClient.VoidJournalAsync(docId);
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidVoucher: nexaaccId={nexaaccId} already voided in Nexaacc — treating as success",
                    "SYSTEM");
                return $"VOIDED:{nexaaccId} (already voided)";
            }

            return $"VOIDED:{nexaaccId}";
        }

        private static bool IsAlreadyVoided(AccountingApiException ex)
        {
            if (ex.StatusCode != 400) return false;
            string body = ex.ResponseBody ?? "";
            // Check both literal Thai and JSON-escaped unicode forms
            // "ถูกยกเลิกไปแล้ว" = U+0E16 U+0E39 U+0E01 U+0E22 U+0E01 U+0E40 U+0E25 U+0E34 U+0E01 U+0E44 U+0E1B U+0E41 U+0E25 U+0E49 U+0E27
            const string escapedThai = "\\u0E16\\u0E39\\u0E01\\u0E22\\u0E01\\u0E40\\u0E25\\u0E34\\u0E01\\u0E44\\u0E1B\\u0E41\\u0E25\\u0E49\\u0E27";
            return body.Contains("ถูกยกเลิกไปแล้ว")
                || body.IndexOf(escapedThai, StringComparison.OrdinalIgnoreCase) >= 0
                || body.Contains("already cancelled")
                || body.Contains("already voided")
                || body.Contains("already cancelled.")
                || body.Contains("AlreadyVoided")
                || body.Contains("AlreadyCancelled");
        }

        // ──────────────────────────────────────────────
        // Fallback Data Lookup Helpers
        // ──────────────────────────────────────────────

        /// <summary>
        /// Look up actual paid amount from Payment_History when payload amount is 0.
        /// Returns total paid for the given reservation and payment type, or 0 if not found.
        /// </summary>
        private decimal LookupPaidAmount(int reservationId, string paymentType = null)
        {
            try
            {
                string sql;
                var parameters = new Dictionary<string, object> { { "@id", reservationId } };

                if (!string.IsNullOrEmpty(paymentType))
                {
                    sql = @"SELECT ISNULL(SUM(PaymentAmount), 0) AS TotalPaid
                            FROM Payment_History
                            WHERE Reservation_ID = @id AND Status = 'COMPLETED' AND PaymentType = @type
                            ORDER BY Created_Date DESC";
                    parameters.Add("@type", paymentType);
                }
                else
                {
                    sql = @"SELECT ISNULL(SUM(PaymentAmount), 0) AS TotalPaid
                            FROM Payment_History
                            WHERE Reservation_ID = @id AND Status = 'COMPLETED'";
                }

                var dt = _code.DatabaseQuerySafe(_connectionString, sql, parameters);
                if (dt?.Rows.Count > 0)
                {
                    decimal amount = Convert.ToDecimal(dt.Rows[0]["TotalPaid"]);
                    if (amount > 0) return amount;
                }

                // If specific type returned 0, try all payments
                if (!string.IsNullOrEmpty(paymentType))
                {
                    var allParams = new Dictionary<string, object> { { "@id", reservationId } };
                    var dtAll = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT ISNULL(SUM(PaymentAmount), 0) AS TotalPaid
                          FROM Payment_History
                          WHERE Reservation_ID = @id AND Status = 'COMPLETED'",
                        allParams);
                    if (dtAll?.Rows.Count > 0)
                        return Convert.ToDecimal(dtAll.Rows[0]["TotalPaid"]);
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupPaidAmount failed for Reservation #{reservationId}: {ex.Message}", "SYSTEM");
            }
            return 0;
        }

        // ──────────────────────────────────────────────
        // Queue Database Operations
        // ──────────────────────────────────────────────

        private long FindPendingEntry(string entityType, string actionType, string payloadKey, string payloadValue)
        {
            if (string.IsNullOrEmpty(payloadValue)) return -1;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @entityType AND Action_Type = @actionType
                        AND Status IN ('PENDING', 'PROCESSING')
                        AND Payload LIKE @pattern
                      ORDER BY ID DESC",
                    new Dictionary<string, object>
                    {
                        { "@entityType", entityType },
                        { "@actionType", actionType },
                        { "@pattern", $"%\"{payloadKey}\":\"{payloadValue}\"%"}
                    });
                return dt?.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0]["ID"]) : -1;
            }
            catch { return -1; }
        }

        private long InsertQueue(string entityType, int entityId, string actionType, Dictionary<string, object> payload)
        {
            var payloadJson = _serializer.Serialize(payload);

            var parameters = new Dictionary<string, object>
            {
                { "@entityType", entityType },
                { "@entityId", entityId },
                { "@actionType", actionType },
                { "@payload", payloadJson },
                { "@maxRetries", _config.MaxRetries }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"INSERT INTO Accounting_Sync_Queue
                  (Entity_Type, Entity_ID, Action_Type, Payload, Status, Retry_Count, Max_Retries, Created_Date)
                  VALUES (@entityType, @entityId, @actionType, @payload, 'PENDING', 0, @maxRetries, GETDATE())",
                parameters);

            // Get the inserted ID
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT MAX(ID) as LastID FROM Accounting_Sync_Queue WHERE Entity_Type = @entityType AND Entity_ID = @entityId AND Action_Type = @actionType",
                new Dictionary<string, object>
                {
                    { "@entityType", entityType },
                    { "@entityId", entityId },
                    { "@actionType", actionType }
                });

            return dt != null && dt.Rows.Count > 0 ? Convert.ToInt64(dt.Rows[0]["LastID"]) : -1;
        }

        private void UpdateQueueStatus(long queueId, string status, string errorMessage, string nexaaccResponseId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@id", queueId },
                { "@status", status },
                { "@error", (object)errorMessage ?? DBNull.Value },
                { "@nexaaccId", (object)nexaaccResponseId ?? DBNull.Value },
                { "@processedDate", status == "COMPLETED" ? (object)DateTime.Now : DBNull.Value }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Status = @status, Error_Message = @error,
                      Nexaacc_Response_Id = @nexaaccId, Processed_Date = @processedDate
                  WHERE ID = @id",
                parameters);
        }

        private void IncrementRetry(long queueId, int currentRetryCount)
        {
            // Exponential backoff: 30s, 2m, 8m, 30m, 2h
            int[] backoffSeconds = { 30, 120, 480, 1800, 7200 };
            int delayIndex = Math.Min(currentRetryCount - 1, backoffSeconds.Length - 1);
            int delaySec = delayIndex >= 0 ? backoffSeconds[delayIndex] : 30;

            var parameters = new Dictionary<string, object>
            {
                { "@id", queueId },
                { "@retryCount", currentRetryCount },
                { "@nextRetry", DateTime.Now.AddSeconds(delaySec) }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Retry_Count = @retryCount, Next_Retry_Date = @nextRetry
                  WHERE ID = @id",
                parameters);
        }

        // ──────────────────────────────────────────────
        // Status & Monitoring
        // ──────────────────────────────────────────────

        /// <summary>
        /// Get sync queue summary for admin dashboard.
        /// </summary>
        public DataTable GetQueueSummary()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT Status, Entity_Type, COUNT(*) as Count
                  FROM Accounting_Sync_Queue
                  WHERE CAST(Created_Date AS DATE) >= DATEADD(DAY, -7, CAST(GETDATE() AS DATE))
                  GROUP BY Status, Entity_Type
                  ORDER BY Status, Entity_Type",
                null);
        }

        /// <summary>
        /// Get failed items that need attention.
        /// </summary>
        public DataTable GetFailedItems()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, Entity_Type, Entity_ID, Action_Type, Error_Message, Retry_Count, Created_Date
                  FROM Accounting_Sync_Queue
                  WHERE Status = 'FAILED' AND Retry_Count >= Max_Retries
                  ORDER BY Created_Date DESC",
                null);
        }

        /// <summary>
        /// Manually retry a failed queue item.
        /// </summary>
        public void RetryItem(long queueId)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@id", queueId }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Status = 'PENDING', Retry_Count = 0, Next_Retry_Date = NULL, Error_Message = NULL
                  WHERE ID = @id",
                parameters);
        }

        /// <summary>
        /// Prepare for re-sync:
        ///   1. Find existing COMPLETED entry with Nexaacc_Response_Id and enqueue a VOID
        ///      so the old Nexaacc journal/document is reversed (avoids duplicates).
        ///      This is only needed for Journal mode — Document mode handles void+create
        ///      atomically via ReplaceExistingForSource on the request body.
        ///   2. Mark old COMPLETED entries as SUPERSEDED.
        ///   3. Cancel any PENDING/PROCESSING/FAILED entries.
        /// </summary>
        public int PrepareResync(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber)) return 0;

            var lookupParams = new Dictionary<string, object>
            {
                { "@pattern", $"%\"documentNumber\":\"{documentNumber}\"%" },
                { "@patternReceipt", $"%\"receiptNumber\":\"{documentNumber}\"%" }
            };

            // Step 1: For journal mode, enqueue VOID for old Nexaacc journal.
            // (Document mode uses ReplaceExistingForSource so no separate VOID needed.)
            if (!_config.IsDocumentMode)
            {
                var completedDt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID, Entity_Type, Nexaacc_Response_Id
                      FROM Accounting_Sync_Queue
                      WHERE (Payload LIKE @pattern OR Payload LIKE @patternReceipt)
                        AND Status = 'COMPLETED'
                        AND Nexaacc_Response_Id IS NOT NULL
                        AND (Action_Type = 'CREATE_VOUCHER_JOURNAL' OR Action_Type = 'CREATE_RECEIPT_DOCUMENT')
                      ORDER BY ID DESC",
                    lookupParams);

                if (completedDt?.Rows.Count > 0)
                {
                    string entityType = completedDt.Rows[0]["Entity_Type"]?.ToString();
                    string oldNexaaccId = completedDt.Rows[0]["Nexaacc_Response_Id"]?.ToString();

                    if (!string.IsNullOrEmpty(oldNexaaccId))
                    {
                        var voidPayload = new Dictionary<string, object>
                        {
                            { "documentNumber", documentNumber },
                            { "nexaaccId", oldNexaaccId },
                            { "reason", "Superseded by re-sync" }
                        };
                        string voidAction = entityType == "RECEIPT" ? "VOID_RECEIPT" : "VOID_VOUCHER";
                        InsertQueue(entityType, 0, voidAction, voidPayload);

                        _code.Logs(_connectionString, "AccountingSync",
                            $"PrepareResync: doc={documentNumber} enqueued {voidAction} for old nexaaccId={oldNexaaccId}",
                            "SYSTEM");
                    }
                }
            }

            // Step 2 & 3: Mark COMPLETED as SUPERSEDED, cancel PENDING/PROCESSING/FAILED
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Status = 'SUPERSEDED', Error_Message = 'Superseded by re-sync'
                  WHERE (Payload LIKE @pattern OR Payload LIKE @patternReceipt)
                    AND Status = 'COMPLETED'
                    AND (Action_Type = 'CREATE_VOUCHER_JOURNAL' OR Action_Type = 'CREATE_RECEIPT_DOCUMENT');

                  UPDATE Accounting_Sync_Queue
                  SET Status = 'CANCELLED', Error_Message = 'Superseded by re-sync'
                  WHERE (Payload LIKE @pattern OR Payload LIKE @patternReceipt)
                    AND Status IN ('PENDING', 'PROCESSING', 'FAILED');

                  SELECT @@ROWCOUNT AS Affected",
                lookupParams);

            int affected = dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Affected"]) : 0;
            if (affected > 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"PrepareResync: doc={documentNumber} cleaned {affected} existing entries",
                    "SYSTEM");
            }
            return affected;
        }

        /// <summary>
        /// Cancel old auto-sync queue entries that were NOT triggered from manual document pages.
        /// Returns the number of entries cancelled.
        /// </summary>
        public int CancelOldAutoSyncEntries()
        {
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Status = 'CANCELLED', Error_Message = 'Auto-sync disabled — replaced by manual document sync'
                  WHERE Status IN ('PENDING', 'FAILED')
                    AND Payload NOT LIKE '%""receiptNumber""%'
                    AND Payload NOT LIKE '%""documentNumber""%';
                  SELECT @@ROWCOUNT AS Affected",
                null);
            return dt?.Rows.Count > 0 ? Convert.ToInt32(dt.Rows[0]["Affected"]) : 0;
        }

        /// <summary>
        /// Get count of old auto-sync entries that can be cleaned up.
        /// </summary>
        public DataTable GetAutoSyncCleanupPreview()
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT Status, Action_Type, COUNT(*) as Count
                  FROM Accounting_Sync_Queue
                  WHERE Status IN ('PENDING', 'FAILED', 'COMPLETED')
                    AND Payload NOT LIKE '%""receiptNumber""%'
                    AND Payload NOT LIKE '%""documentNumber""%'
                  GROUP BY Status, Action_Type
                  ORDER BY Status, Action_Type",
                null);
        }
    }
}
