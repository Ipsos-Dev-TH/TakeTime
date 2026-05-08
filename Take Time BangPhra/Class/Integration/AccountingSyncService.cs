using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
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

                // Anti-duplicate: if a COMPLETED entry exists within the last 60s, return it
                // (prevents form resubmission / browser refresh from creating duplicates)
                long recent = FindRecentCompletedEntry("VOUCHER", "CREATE_VOUCHER_JOURNAL", "documentNumber", documentNumber, 60);
                if (recent > 0)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnqueuePaymentVoucher: doc={documentNumber} returned recent COMPLETED queueId={recent} (anti-duplicate within 60s window)",
                        "SYSTEM");
                    return recent;
                }
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
        /// Enqueue payroll journal entry with proper SSF/WHT breakdown.
        /// Uses MapPayrollToJournal / MapPayrollToExpense for correct accounting.
        /// </summary>
        public long EnqueuePayrollJournal(decimal totalSalary, DateTime payDate, string period,
            decimal socialSecurityEmployee = 0, decimal socialSecurityEmployer = 0,
            decimal whtAmount = 0, string documentNumber = null, string paymentMethod = null)
        {
            if (!_config.IsConfigured) return -1;
            if (totalSalary <= 0) return -1;

            if (!string.IsNullOrEmpty(documentNumber))
            {
                long existing = FindPendingEntry("PAYROLL", "CREATE_PAYROLL_ENTRY", "documentNumber", documentNumber);
                if (existing > 0) return existing;

                long recent = FindRecentCompletedEntry("PAYROLL", "CREATE_PAYROLL_ENTRY", "documentNumber", documentNumber, 60);
                if (recent > 0) return recent;
            }

            var payload = new Dictionary<string, object>
            {
                { "totalSalary", totalSalary },
                { "payDate", payDate.ToString("yyyy-MM-dd") },
                { "period", period },
                { "socialSecurityEmployee", socialSecurityEmployee },
                { "socialSecurityEmployer", socialSecurityEmployer },
                { "whtAmount", whtAmount }
            };
            if (!string.IsNullOrEmpty(documentNumber))
                payload["documentNumber"] = documentNumber;
            if (!string.IsNullOrEmpty(paymentMethod))
                payload["paymentMethod"] = paymentMethod;

            return InsertQueue("PAYROLL", 0, "CREATE_PAYROLL_ENTRY", payload);
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

            // Anti-duplicate: if a COMPLETED entry exists within the last 60s, return it
            // (prevents form resubmission / browser refresh from creating duplicates)
            long recent = FindRecentCompletedEntry("RECEIPT", "CREATE_RECEIPT_DOCUMENT", "receiptNumber", receiptNumber, 60);
            if (recent > 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueReceipt: receipt={receiptNumber} returned recent COMPLETED queueId={recent} (anti-duplicate within 60s window)",
                    "SYSTEM");
                return recent;
            }

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
        // Deposit Lifecycle Enqueue Methods (ตัดมัดจำ)
        // ──────────────────────────────────────────────

        /// <summary>
        /// ตัดมัดจำ (เจ้าหนี้ ADVANCE_DEPOSIT) ออกตอนลูกค้าเช็คเอาท์ — รับรู้รายได้ห้องพัก
        /// ถ้ามี damageAmount > 0 จะแบ่ง credit ระหว่าง Room Revenue และ Other Income (ค่าเสียหาย)
        /// อ้างอิงด้วย Reservation_ID + ref `RES-{id}-CHK`
        /// </summary>
        public long EnqueueDepositClearingOnCheckout(int reservationId, decimal depositAmount, string customerName,
            DateTime checkoutDate, decimal damageAmount = 0)
        {
            if (!_config.IsConfigured) return -1;
            if (depositAmount <= 0) return -1;

            string reservationRef = $"RES-{reservationId}-CHK";

            long existing = FindPendingEntry("RESERVATION", "CLEAR_DEPOSIT_AT_CHECKOUT", "reservationRef", reservationRef);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("RESERVATION", "CLEAR_DEPOSIT_AT_CHECKOUT", "reservationRef", reservationRef, 86400);
            if (completed > 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueDepositClearingOnCheckout: ref={reservationRef} ตัดไปแล้ว — skip (queueId={completed})", "SYSTEM");
                return completed;
            }

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "reservationRef", reservationRef },
                { "depositAmount", depositAmount },
                { "damageAmount", damageAmount },
                { "customerName", customerName ?? "" },
                { "checkoutDate", checkoutDate.ToString("yyyy-MM-dd") }
            };
            return InsertQueue("RESERVATION", reservationId, "CLEAR_DEPOSIT_AT_CHECKOUT", payload);
        }

        /// <summary>คืนเงินมัดจำ (DR ADVANCE_DEPOSIT, CR Cash/Bank) — กรณียกเลิกแล้วคืนเงิน</summary>
        public long EnqueueDepositRefund(int reservationId, decimal refundAmount, string paymentMethod,
            string customerName, DateTime refundDate)
        {
            if (!_config.IsConfigured) return -1;
            if (refundAmount <= 0) return -1;

            string reservationRef = $"RES-{reservationId}-REF";

            long existing = FindPendingEntry("RESERVATION", "REFUND_DEPOSIT", "reservationRef", reservationRef);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("RESERVATION", "REFUND_DEPOSIT", "reservationRef", reservationRef, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "reservationRef", reservationRef },
                { "refundAmount", refundAmount },
                { "paymentMethod", paymentMethod ?? "CASH" },
                { "customerName", customerName ?? "" },
                { "refundDate", refundDate.ToString("yyyy-MM-dd") }
            };
            return InsertQueue("RESERVATION", reservationId, "REFUND_DEPOSIT", payload);
        }

        /// <summary>ริบมัดจำ (ลูกค้าไม่มา/ยกเลิกผิดเงื่อนไข) — DR ADVANCE_DEPOSIT, CR Forfeit Income</summary>
        public long EnqueueDepositForfeit(int reservationId, decimal forfeitAmount, string customerName,
            DateTime forfeitDate, string reason = null)
        {
            if (!_config.IsConfigured) return -1;
            if (forfeitAmount <= 0) return -1;

            string reservationRef = $"RES-{reservationId}-FORFEIT";

            long existing = FindPendingEntry("RESERVATION", "FORFEIT_DEPOSIT", "reservationRef", reservationRef);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("RESERVATION", "FORFEIT_DEPOSIT", "reservationRef", reservationRef, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "reservationRef", reservationRef },
                { "forfeitAmount", forfeitAmount },
                { "customerName", customerName ?? "" },
                { "forfeitDate", forfeitDate.ToString("yyyy-MM-dd") },
                { "reason", reason ?? "" }
            };
            return InsertQueue("RESERVATION", reservationId, "FORFEIT_DEPOSIT", payload);
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

        // ──────────────────────────────────────────────
        // Public manual triggers (for admin pages)
        // ──────────────────────────────────────────────

        /// <summary>
        /// สร้าง E-Tax Invoice ด้วยตัวเอง (manual trigger จากหน้า Receipt).
        /// Returns: (success, message, etaxRefNumber). หากใบเสร็จยังไม่ถูก sync เข้า NextAcc จะ fail.
        /// </summary>
        public async Task<(bool success, string message, string etaxRef)> ManualGenerateEtaxAsync(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber))
                return (false, "กรุณาระบุเลขที่ใบเสร็จ", null);
            if (!_config.IsConfigured)
                return (false, "ยังไม่ได้ตั้งค่าการเชื่อมต่อ NextAcc", null);

            // Find NextAcc Doc ID via Sync_Queue
            Guid invoiceDocId = LookupNexaaccDocIdByReceipt(receiptNumber);
            if (invoiceDocId == Guid.Empty)
                return (false, $"ใบเสร็จ {receiptNumber} ยังไม่ได้ sync เข้า NextAcc — กรุณา sync ก่อน", null);

            int reservationId = LookupReservationIdByReceipt(receiptNumber);
            decimal amount = LookupReceiptAmount(receiptNumber);
            string guestName = LookupGuestName(reservationId);

            long logId = InsertEtaxLogPending(invoiceDocId, receiptNumber, reservationId);

            try
            {
                var result = await _apiClient.GenerateEtaxAsync(new GenerateEtaxRequest
                {
                    DocumentId = invoiceDocId,
                    DocumentType = "TAX_INVOICE",
                    AutoSign = _config.IsEtaxAutoSign,
                    AutoSubmit = _config.IsEtaxAutoSubmit
                });
                if (result?.data == null)
                {
                    UpdateEtaxLogFailed(logId, "Empty response from /etax/generate");
                    return (false, "API ตอบกลับว่าง — โปรดตรวจสอบ NextAcc", null);
                }
                UpdateEtaxLogSuccess(logId, result.data);
                return (true, $"สร้าง E-Tax สำเร็จ: {result.data.EtaxRefNumber} (สถานะ: {result.data.Status})", result.data.EtaxRefNumber);
            }
            catch (AccountingApiException ex)
            {
                UpdateEtaxLogFailed(logId, $"HTTP {ex.StatusCode}: {ex.ResponseBody}");
                return (false, $"ผิดพลาด ({ex.StatusCode}): {ex.Message}", null);
            }
            catch (Exception ex)
            {
                UpdateEtaxLogFailed(logId, ex.Message);
                return (false, $"ผิดพลาด: {ex.Message}", null);
            }
        }

        /// <summary>
        /// ส่งอีเมล E-Tax ให้ลูกค้า (ทั้งกรณีส่งซ้ำและส่งครั้งแรก).
        /// </summary>
        public async Task<(bool success, string message)> ManualSendEtaxEmailAsync(string receiptNumber, string overrideEmail = null)
        {
            if (string.IsNullOrEmpty(receiptNumber))
                return (false, "กรุณาระบุเลขที่ใบเสร็จ");

            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 1 ID, Nexaacc_Etax_Id, Reservation_ID FROM Accounting_ETax_Log
                  WHERE Receipt_Number = @num AND Nexaacc_Etax_Id IS NOT NULL
                  ORDER BY ID DESC",
                new Dictionary<string, object> { { "@num", receiptNumber } });

            if (dt == null || dt.Rows.Count == 0)
                return (false, "ยังไม่ได้สร้าง E-Tax สำหรับใบเสร็จนี้");

            long logId = Convert.ToInt64(dt.Rows[0]["ID"]);
            Guid etaxId = (Guid)dt.Rows[0]["Nexaacc_Etax_Id"];
            int resId = dt.Rows[0]["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["Reservation_ID"]) : 0;

            string email = !string.IsNullOrWhiteSpace(overrideEmail) ? overrideEmail.Trim() : LookupCustomerEmail(resId);
            if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
                return (false, "ไม่พบอีเมลลูกค้า — โปรดระบุอีเมลปลายทาง");

            decimal amount = LookupReceiptAmount(receiptNumber);
            string guestName = LookupGuestName(resId);

            var (success, channel, msg) = await SendEtaxEmailWithFallbackAsync(etaxId, email, receiptNumber, amount, guestName);
            if (success)
            {
                MarkEtaxEmailSent(logId, $"{email} via {channel}");
                return (true, $"ส่งอีเมลไปยัง {email} สำเร็จ (ช่องทาง: {channel})");
            }
            return (false, $"ส่งอีเมลไม่สำเร็จ: {msg}");
        }

        private Guid LookupNexaaccDocIdByReceipt(string receiptNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 q.Nexaacc_Response_Id
                      FROM Accounting_Sync_Queue q
                      WHERE q.Status = 'COMPLETED'
                        AND q.Nexaacc_Response_Id IS NOT NULL
                        AND q.Nexaacc_Response_Id <> 'SKIPPED_LOCAL_MODE'
                        AND (q.Action_Type LIKE 'CREATE_RECEIPT%' OR q.Action_Type LIKE 'CREATE_DEPOSIT%' OR q.Action_Type LIKE 'CREATE_PAYMENT%')
                        AND ISNULL(JSON_VALUE(q.Payload, '$.receiptNumber'), '') = @num
                      ORDER BY q.ID DESC",
                    new Dictionary<string, object> { { "@num", receiptNumber } });

                if (dt?.Rows.Count > 0)
                {
                    string idStr = dt.Rows[0]["Nexaacc_Response_Id"]?.ToString();
                    if (Guid.TryParse(idStr, out Guid id)) return id;
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupNexaaccDocIdByReceipt failed: {ex.Message}", "SYSTEM");
            }
            return Guid.Empty;
        }

        private int LookupReservationIdByReceipt(string receiptNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Reservation_ID FROM Account_Receipt WHERE Receipt_Number = @num",
                    new Dictionary<string, object> { { "@num", receiptNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Reservation_ID"] != DBNull.Value)
                    return Convert.ToInt32(dt.Rows[0]["Reservation_ID"]);
            }
            catch { }
            return 0;
        }

        private decimal LookupReceiptAmount(string receiptNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Total_Amount FROM Account_Receipt WHERE Receipt_Number = @num",
                    new Dictionary<string, object> { { "@num", receiptNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Total_Amount"] != DBNull.Value)
                    return Convert.ToDecimal(dt.Rows[0]["Total_Amount"]);
            }
            catch { }
            return 0m;
        }

        private string LookupGuestName(int reservationId)
        {
            if (reservationId <= 0) return "";
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ISNULL(C.FullName, C.Name) AS Name
                      FROM Reservation R
                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                      WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt?.Rows.Count > 0)
                    return dt.Rows[0]["Name"]?.ToString() ?? "";
            }
            catch { }
            return "";
        }

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
                    else if (nexaaccId == "SKIPPED_LOCAL_MODE")
                    {
                        UpdateQueueStatus(queueId, "COMPLETED", $"Skipped: SyncMode=LOCAL — ใช้เอกสารจากระบบ TakeTime", nexaaccId);
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

                case "CREATE_PAYROLL_ENTRY":
                    return await ProcessPayrollEntry(payload);

                case "VOID_RECEIPT":
                    return await ProcessVoidReceipt(payload);

                case "VOID_VOUCHER":
                    return await ProcessVoidVoucher(payload);

                // ── Deposit Lifecycle (มัดจำการจอง) ──
                case "CLEAR_DEPOSIT_AT_CHECKOUT":
                    return await ProcessDepositClearing(payload);
                case "REFUND_DEPOSIT":
                    return await ProcessDepositRefund(payload);
                case "FORFEIT_DEPOSIT":
                    return await ProcessDepositForfeit(payload);

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
            // Per-type mode: skip if voucher sync is LOCAL
            if (_config.IsVoucherLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoucherJournal: SKIPPED — VoucherSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

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
                $"ProcessVoucherJournal: doc={docNumber} amount={amount} category={expenseCategory} payee={payeeName} lines={lineCount} mode={_config.VoucherSyncMode}",
                "SYSTEM");

            // Lookup voucher attachment files
            List<IntegrationAttachment> attachments = null;
            if (_config.AttachFiles)
                attachments = LookupVoucherAttachments(voucherId, docNumber, voucherDate);

            if (_config.IsVoucherDocumentMode)
            {
                var expense = _mapper.MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber);
                expense.Attachments = attachments;
                var result = await _apiClient.CreateExpenseAsync(expense);
                string nexaaccId = result.data.Id.ToString();

                // Auto-generate WHT certificate if WHT was applied
                if (whtAmount > 0)
                    await TryAutoGenerateWhtCertAsync(result.data.Id, docNumber);

                return nexaaccId;
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

        private async Task<string> ProcessPayrollEntry(Dictionary<string, object> p)
        {
            // Per-type mode: skip if payroll sync is LOCAL
            if (_config.IsPayrollLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPayrollEntry: SKIPPED — PayrollSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

            decimal totalSalary = Convert.ToDecimal(p["totalSalary"]);
            if (totalSalary <= 0)
                throw new ArgumentException($"Cannot create payroll journal: totalSalary is {totalSalary}");

            DateTime payDate = DateTime.Parse(p["payDate"]?.ToString());
            string period = p.ContainsKey("period") ? p["period"]?.ToString() : "";
            decimal ssfEmployee = p.ContainsKey("socialSecurityEmployee") ? Convert.ToDecimal(p["socialSecurityEmployee"]) : 0;
            decimal ssfEmployer = p.ContainsKey("socialSecurityEmployer") ? Convert.ToDecimal(p["socialSecurityEmployer"]) : 0;
            decimal whtAmount = p.ContainsKey("whtAmount") ? Convert.ToDecimal(p["whtAmount"]) : 0;
            string docNumber = p.ContainsKey("documentNumber") ? p["documentNumber"]?.ToString() : "";

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollEntry: period={period} gross={totalSalary} ssfEmp={ssfEmployee} ssfEr={ssfEmployer} wht={whtAmount} doc={docNumber} mode={_config.PayrollSyncMode}",
                "SYSTEM");

            if (_config.IsPayrollDocumentMode)
            {
                var expense = _mapper.MapPayrollToExpense(period, totalSalary, ssfEmployee + ssfEmployer, whtAmount, payDate,
                    $"เงินเดือน {period}");
                if (!string.IsNullOrEmpty(docNumber))
                {
                    expense.Reference = docNumber;
                    expense.ExternalRef = docNumber;
                    expense.ReplaceExistingForSource = true;
                }
                var result = await _apiClient.CreateExpenseAsync(expense);
                return result.data.Id.ToString();
            }
            else
            {
                var journal = _mapper.MapPayrollToJournal(totalSalary, payDate, period,
                    ssfEmployee, ssfEmployer, whtAmount);
                if (!string.IsNullOrEmpty(docNumber))
                    journal.Reference = docNumber;
                var result = await _apiClient.CreateJournalAsync(journal);
                await SafePostJournalAsync(result.data.Id);
                return result.data.Id.ToString();
            }
        }

        private async Task<string> ProcessReceiptDocument(Dictionary<string, object> p)
        {
            // Per-type mode: skip if receipt sync is LOCAL
            if (_config.IsReceiptLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessReceiptDocument: SKIPPED — ReceiptSyncMode=LOCAL (ใช้ใบกำกับภาษีจากระบบ TakeTime)", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

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
                $"ProcessReceiptDocument: receipt={receiptNumber} resId={reservationId} amount={totalAmount} isDeposit={isDeposit} paymentMethod={paymentMethod} revenueType={revenueType ?? "auto"} mode={_config.ReceiptSyncMode}",
                "SYSTEM");

            // Lookup receipt PDF for attachment
            List<IntegrationAttachment> attachments = null;
            if (_config.AttachFiles)
                attachments = LookupReceiptAttachments(receiptNumber, reservationId);

            if (isDeposit)
            {
                if (_config.IsReceiptDocumentMode)
                {
                    var invoice = _mapper.MapDepositToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName, paymentAccountId: paymentAccountId);
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.Reference = receiptNumber;
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    invoice.Attachments = attachments;
                    var result = await _apiClient.CreateInvoiceAsync(invoice);
                    await TryAutoGenerateEtaxAsync(result.data.Id, receiptNumber, reservationId, totalAmount, customerName);
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
                if (_config.IsReceiptDocumentMode)
                {
                    bool hasVat = vatAmount > 0;
                    var invoice = _mapper.MapPaymentToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId);
                    invoice.Reference = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : $"RES-{reservationId}";
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    invoice.Attachments = attachments;
                    var result = await _apiClient.CreateInvoiceAsync(invoice);
                    await TryAutoGenerateEtaxAsync(result.data.Id, receiptNumber, reservationId, totalAmount, customerName);
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
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : null;

            try
            {
                if (_config.IsDocumentMode)
                {
                    await _apiClient.VoidDocumentAsync(docId);
                }
                else
                {
                    // Prefer reverse (กลับรายการ) over void for better accounting trail
                    try
                    {
                        var reverseReq = new ReverseJournalEntryRequest
                        {
                            ReversalDate = DateTime.Now,
                            Description = reason ?? "กลับรายการ — re-sync"
                        };
                        var reverseResult = await _apiClient.ReverseJournalAsync(docId, reverseReq);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt: reversed journal {nexaaccId} → {reverseResult.data?.Id}",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} → {reverseResult.data?.Id}";
                    }
                    catch (AccountingApiException reverseEx) when (reverseEx.StatusCode == 400 || reverseEx.StatusCode == 404)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt: reverse failed ({reverseEx.StatusCode}), falling back to void",
                            "SYSTEM");
                        await _apiClient.VoidJournalAsync(docId);
                    }
                }
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidReceipt: nexaaccId={nexaaccId} already voided/reversed in Nexaacc — treating as success",
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
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : null;

            try
            {
                if (_config.IsDocumentMode)
                {
                    await _apiClient.VoidDocumentAsync(docId);
                }
                else
                {
                    try
                    {
                        var reverseReq = new ReverseJournalEntryRequest
                        {
                            ReversalDate = DateTime.Now,
                            Description = reason ?? "กลับรายการใบสำคัญจ่าย — re-sync"
                        };
                        var reverseResult = await _apiClient.ReverseJournalAsync(docId, reverseReq);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher: reversed journal {nexaaccId} → {reverseResult.data?.Id}",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} → {reverseResult.data?.Id}";
                    }
                    catch (AccountingApiException reverseEx) when (reverseEx.StatusCode == 400 || reverseEx.StatusCode == 404)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher: reverse failed ({reverseEx.StatusCode}), falling back to void",
                            "SYSTEM");
                        await _apiClient.VoidJournalAsync(docId);
                    }
                }
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidVoucher: nexaaccId={nexaaccId} already voided/reversed in Nexaacc — treating as success",
                    "SYSTEM");
                return $"VOIDED:{nexaaccId} (already voided)";
            }

            return $"VOIDED:{nexaaccId}";
        }

        /// <summary>
        /// Auto-generate WHT certificate in NextAcc after creating an expense document with WHT.
        /// Non-critical — logs error and continues if NextAcc doesn't support or fails.
        /// </summary>
        private async Task TryAutoGenerateWhtCertAsync(Guid documentId, string documentNumber)
        {
            try
            {
                var whtResult = await _apiClient.AutoGenerateWhtCertAsync(new AutoGenerateWhtRequest
                {
                    DocumentId = documentId,
                    AutoIssue = true
                });

                if (whtResult?.data != null)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"WHT cert auto-generated: doc={documentNumber} certNo={whtResult.data.CertificateNumber} taxAmount={whtResult.data.TaxAmount:N2}",
                        "SYSTEM");
                }
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 404 || ex.StatusCode == 400)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"WHT cert auto-generate skipped for doc={documentNumber}: {ex.StatusCode} — {ex.ResponseBody}",
                    "SYSTEM");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"WHT cert auto-generate failed for doc={documentNumber}: {ex.Message}",
                    "SYSTEM");
            }
        }

        // ──────────────────────────────────────────────
        // Deposit Lifecycle Processors (ตัดมัดจำการจอง)
        // หลักบัญชี:
        //   ตอนรับมัดจำ:    DR Cash/Bank | CR Advance Deposit (Liability)
        //   ตอน checkout:   DR Advance Deposit | CR Room Revenue
        //   ตอนหักเสียหาย:   DR Advance Deposit | CR Room Revenue + CR Other Income (ค่าเสียหาย)
        //   ตอนคืนเงิน:      DR Advance Deposit | CR Cash/Bank
        //   ตอนริบ no-show:  DR Advance Deposit | CR Forfeit Income
        // อ้างอิงทุกรายการด้วย reservationRef (RES-{id}-CHK / -REF / -FORFEIT)
        // ──────────────────────────────────────────────

        private async Task<string> ProcessDepositClearing(Dictionary<string, object> p)
        {
            if (_config.IsReceiptLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    "ProcessDepositClearing: SKIPPED — ReceiptSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

            int reservationId = Convert.ToInt32(p["reservationId"]);
            decimal depositAmount = Convert.ToDecimal(p["depositAmount"]);
            decimal damageAmount = p.ContainsKey("damageAmount") ? Convert.ToDecimal(p["damageAmount"]) : 0m;
            string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() ?? "" : "";
            DateTime checkoutDate = DateTime.Parse(p["checkoutDate"]?.ToString());
            string reservationRef = p.ContainsKey("reservationRef") ? p["reservationRef"]?.ToString() : $"RES-{reservationId}-CHK";

            if (depositAmount <= 0)
                throw new ArgumentException($"ProcessDepositClearing: depositAmount ต้อง > 0 (ได้ {depositAmount}) reservation #{reservationId}");

            decimal actualDeposit = LookupActualDepositPaid(reservationId);
            if (actualDeposit > 0 && Math.Abs(actualDeposit - depositAmount) > 0.01m)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositClearing: payload depositAmount={depositAmount} ≠ actual paid={actualDeposit} — ใช้ actual", "SYSTEM");
                depositAmount = actualDeposit;
            }

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessDepositClearing: ref={reservationRef} resId={reservationId} deposit={depositAmount} damage={damageAmount}", "SYSTEM");

            var journal = _mapper.MapCheckoutToJournal(reservationId, depositAmount, customerName, checkoutDate, damageAmount, reservationRef);
            var result = await _apiClient.CreateJournalAsync(journal);
            await SafePostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessDepositRefund(Dictionary<string, object> p)
        {
            if (_config.IsReceiptLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    "ProcessDepositRefund: SKIPPED — ReceiptSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

            int reservationId = Convert.ToInt32(p["reservationId"]);
            decimal refundAmount = Convert.ToDecimal(p["refundAmount"]);
            string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : "CASH";
            string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() ?? "" : "";
            DateTime refundDate = DateTime.Parse(p["refundDate"]?.ToString());

            if (refundAmount <= 0)
                throw new ArgumentException($"ProcessDepositRefund: refundAmount ต้อง > 0 (ได้ {refundAmount}) reservation #{reservationId}");

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessDepositRefund: resId={reservationId} amount={refundAmount} method={paymentMethod}", "SYSTEM");

            var journal = _mapper.MapRefundToJournal(reservationId, refundAmount, paymentMethod, refundDate, customerName);
            var result = await _apiClient.CreateJournalAsync(journal);
            await SafePostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessDepositForfeit(Dictionary<string, object> p)
        {
            if (_config.IsReceiptLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    "ProcessDepositForfeit: SKIPPED — ReceiptSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

            int reservationId = Convert.ToInt32(p["reservationId"]);
            decimal forfeitAmount = Convert.ToDecimal(p["forfeitAmount"]);
            string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() ?? "" : "";
            DateTime forfeitDate = DateTime.Parse(p["forfeitDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : null;

            if (forfeitAmount <= 0)
                throw new ArgumentException($"ProcessDepositForfeit: forfeitAmount ต้อง > 0 (ได้ {forfeitAmount}) reservation #{reservationId}");

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessDepositForfeit: resId={reservationId} amount={forfeitAmount} reason={reason}", "SYSTEM");

            var journal = _mapper.MapForfeitDepositToJournal(reservationId, forfeitAmount, customerName, forfeitDate, reason);
            var result = await _apiClient.CreateJournalAsync(journal);
            await SafePostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        /// <summary>
        /// หาจำนวนมัดจำที่ลูกค้าจ่ายไปทั้งหมด — JOIN Account_Receipt (IsDeposit=1, Status='Normal') กับ Payment_History
        /// ใช้เป็น truth source ของยอดมัดจำ (กันการบันทึกผิด)
        /// </summary>
        private decimal LookupActualDepositPaid(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Total_Amount), 0) AS TotalDeposit
                      FROM Account_Receipt
                      WHERE Reservation_ID = @resId
                        AND IsDeposit = 1
                        AND (Status = 'Normal' OR Status IS NULL OR Status = '1' OR Status = 'COMPLETED')",
                    new Dictionary<string, object> { { "@resId", reservationId } });
                if (dt?.Rows.Count > 0)
                    return Convert.ToDecimal(dt.Rows[0]["TotalDeposit"]);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupActualDepositPaid failed for resId={reservationId}: {ex.Message}", "SYSTEM");
            }
            return 0m;
        }

        // ──────────────────────────────────────────────
        // E-Tax Invoice Auto-Generation + Email
        // ──────────────────────────────────────────────

        /// <summary>
        /// หลังสร้างใบกำกับภาษีใน NextAcc สำเร็จ:
        ///   1. ถ้าเปิด Etax_AutoGenerate → เรียก /etax/generate (auto-sign/submit ตาม config)
        ///   2. บันทึก Accounting_ETax_Log
        ///   3. ถ้าเปิด Etax_AutoSendEmail และมีอีเมลลูกค้า → ส่งให้อัตโนมัติ
        /// ไม่โยน exception ออก — ความล้มเหลวจะ log แต่ไม่กระทบ flow หลัก
        /// </summary>
        private async Task TryAutoGenerateEtaxAsync(Guid invoiceDocId, string receiptNumber, int reservationId, decimal amount, string guestName)
        {
            if (!_config.IsEtaxAutoGenerate) return;

            long logId = InsertEtaxLogPending(invoiceDocId, receiptNumber, reservationId);

            try
            {
                var etaxResult = await _apiClient.GenerateEtaxAsync(new GenerateEtaxRequest
                {
                    DocumentId = invoiceDocId,
                    DocumentType = "TAX_INVOICE",
                    AutoSign = _config.IsEtaxAutoSign,
                    AutoSubmit = _config.IsEtaxAutoSubmit
                });

                if (etaxResult?.data == null)
                {
                    UpdateEtaxLogFailed(logId, "Empty response from /etax/generate");
                    return;
                }

                Guid etaxId = etaxResult.data.Id;
                UpdateEtaxLogSuccess(logId, etaxResult.data);

                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax generated: receipt={receiptNumber} etaxId={etaxId} ref={etaxResult.data.EtaxRefNumber} status={etaxResult.data.Status}",
                    "SYSTEM");

                if (_config.IsEtaxAutoSendEmail)
                {
                    await TrySendEtaxEmailAsync(etaxId, logId, receiptNumber, reservationId, amount, guestName);
                }
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 404 || ex.StatusCode == 400)
            {
                UpdateEtaxLogFailed(logId, $"HTTP {ex.StatusCode}: {ex.ResponseBody}");
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax auto-generate skipped for receipt={receiptNumber}: {ex.StatusCode} — {ex.ResponseBody}",
                    "SYSTEM");
            }
            catch (Exception ex)
            {
                UpdateEtaxLogFailed(logId, ex.Message);
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax auto-generate failed for receipt={receiptNumber}: {ex.Message}",
                    "SYSTEM");
            }
        }

        private async Task TrySendEtaxEmailAsync(Guid etaxId, long logId, string receiptNumber, int reservationId, decimal amount, string guestName)
        {
            string email = LookupCustomerEmail(reservationId);
            if (string.IsNullOrWhiteSpace(email))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax email skipped for receipt={receiptNumber}: ไม่พบอีเมลลูกค้าสำหรับ Reservation_ID={reservationId}",
                    "SYSTEM");
                return;
            }

            var (success, channel, message) = await SendEtaxEmailWithFallbackAsync(etaxId, email, receiptNumber, amount, guestName);
            if (success)
            {
                MarkEtaxEmailSent(logId, $"{email} via {channel}");
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax email sent: receipt={receiptNumber} to={email} channel={channel}", "SYSTEM");
            }
            else
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax email failed for receipt={receiptNumber}: {message}", "SYSTEM");
            }
        }

        /// <summary>
        /// ส่งอีเมล E-Tax โดย:
        ///   1. ถ้า EtaxEmailLocalOnly=true → ส่งผ่าน SMTP ของ TakeTime ทันที (ข้าม NextAcc)
        ///   2. ปกติ → ลอง NextAcc API ก่อน
        ///   3. ถ้า NextAcc ล้มเหลว และ EtaxEmailFallback=true → fallback ส่งผ่าน TakeTime SMTP
        ///        ดาวน์โหลด PDF/XML จาก URL ของ NextAcc แล้วแนบ
        /// Returns: (success, channel, message) — channel = "NEXAACC" | "LOCAL_SMTP" | "FAILED"
        /// </summary>
        private async Task<(bool success, string channel, string message)> SendEtaxEmailWithFallbackAsync(
            Guid etaxId, string email, string receiptNumber, decimal amount, string guestName)
        {
            string subject = FormatEmailTemplate(_config.EtaxEmailSubject, receiptNumber, guestName, amount);
            string body = FormatEmailTemplate(_config.EtaxEmailBody, receiptNumber, guestName, amount);

            // Path 1: บังคับ local SMTP เท่านั้น (ข้าม NextAcc)
            if (_config.EtaxEmailLocalOnly)
            {
                return await SendEtaxViaLocalSmtpAsync(etaxId, email, subject, body, receiptNumber);
            }

            // Path 2: ลอง NextAcc API ก่อน
            try
            {
                await _apiClient.SendEtaxByEmailAsync(etaxId, new SendEtaxByEmailRequest
                {
                    RecipientEmail = email,
                    Subject = subject,
                    Body = body,
                    AttachPdf = _config.EtaxEmailAttachPdf,
                    AttachXml = _config.EtaxEmailAttachXml
                });
                return (true, "NEXAACC", "OK");
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"NextAcc email failed for receipt={receiptNumber}: {ex.Message}" +
                    (_config.EtaxEmailFallback ? " — fallback ไป SMTP ของ TakeTime" : ""),
                    "SYSTEM");

                // Path 3: fallback to local SMTP
                if (_config.EtaxEmailFallback)
                {
                    return await SendEtaxViaLocalSmtpAsync(etaxId, email, subject, body, receiptNumber);
                }
                return (false, "FAILED", ex.Message);
            }
        }

        /// <summary>
        /// ส่ง E-Tax email ผ่าน SMTP ของ TakeTime —
        ///   1. เรียก GetEtaxAsync ดึง URL PDF/XML ล่าสุด
        ///   2. ดาวน์โหลดไฟล์
        ///   3. ส่งผ่าน Take_Time_BangPhra.Services.EmailService
        /// </summary>
        private async Task<(bool success, string channel, string message)> SendEtaxViaLocalSmtpAsync(
            Guid etaxId, string email, string subject, string body, string receiptNumber)
        {
            try
            {
                var attachments = new List<System.Net.Mail.Attachment>();
                var memoryStreams = new List<MemoryStream>();

                // ดึง URL ล่าสุด (เผื่อ DB log ค่าเก่า)
                EtaxInvoiceResponse etax = null;
                try
                {
                    var resp = await _apiClient.GetEtaxAsync(etaxId);
                    etax = resp?.data;
                }
                catch (Exception ex)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"GetEtaxAsync failed during fallback for receipt={receiptNumber}: {ex.Message}", "SYSTEM");
                }

                if (etax != null && _config.EtaxEmailAttachPdf && !string.IsNullOrEmpty(etax.PdfUrl))
                {
                    byte[] pdf = await _apiClient.DownloadFileAsync(etax.PdfUrl);
                    if (pdf != null && pdf.Length > 0)
                    {
                        var ms = new MemoryStream(pdf);
                        memoryStreams.Add(ms);
                        attachments.Add(new System.Net.Mail.Attachment(ms, $"{receiptNumber}_etax.pdf", "application/pdf"));
                    }
                }

                if (etax != null && _config.EtaxEmailAttachXml && !string.IsNullOrEmpty(etax.XmlUrl))
                {
                    byte[] xml = await _apiClient.DownloadFileAsync(etax.XmlUrl);
                    if (xml != null && xml.Length > 0)
                    {
                        var ms = new MemoryStream(xml);
                        memoryStreams.Add(ms);
                        attachments.Add(new System.Net.Mail.Attachment(ms, $"{receiptNumber}_etax.xml", "application/xml"));
                    }
                }

                // ส่งผ่าน TakeTime SMTP — convert plain-text body to HTML (preserve newlines)
                string htmlBody = body?.Replace("\r\n", "\n").Replace("\n", "<br/>") ?? "";
                var smtp = new Take_Time_BangPhra.Services.EmailService();
                smtp.SendEmail(email, subject, htmlBody, attachments.Count > 0 ? attachments.ToArray() : null);

                // Cleanup memory streams after send
                foreach (var ms in memoryStreams) ms.Dispose();

                return (true, "LOCAL_SMTP", $"sent via TakeTime SMTP ({attachments.Count} attachments)");
            }
            catch (Exception ex)
            {
                return (false, "FAILED", "SMTP fallback failed: " + ex.Message);
            }
        }

        /// <summary>หาอีเมลลูกค้าจากการจอง — JOIN Reservation → Customer ผ่าน MobilePhone</summary>
        private string LookupCustomerEmail(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 C.Email
                      FROM Reservation R
                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                      WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });

                if (dt?.Rows.Count > 0)
                {
                    string email = dt.Rows[0]["Email"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(email) && email.Contains("@"))
                        return email.Trim();
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupCustomerEmail failed for resId={reservationId}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        private static string FormatEmailTemplate(string template, string receiptNumber, string guestName, decimal amount)
        {
            if (string.IsNullOrEmpty(template)) return "";
            return template
                .Replace("{ReceiptNumber}", receiptNumber ?? "")
                .Replace("{GuestName}", guestName ?? "")
                .Replace("{Amount}", amount.ToString("N2"))
                .Replace("{Date}", DateTime.Now.ToString("dd/MM/yyyy"))
                .Replace("\\n", "\n");
        }

        private long InsertEtaxLogPending(Guid invoiceDocId, string receiptNumber, int reservationId)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"INSERT INTO Accounting_ETax_Log (Document_Number, Receipt_Number, Reservation_ID, Nexaacc_Doc_Id, Status, Created_Date)
                      VALUES (@docNum, @receiptNum, @resId, @docId, 'PENDING', GETDATE())",
                    new Dictionary<string, object>
                    {
                        { "@docNum", receiptNumber ?? (object)DBNull.Value },
                        { "@receiptNum", receiptNumber ?? (object)DBNull.Value },
                        { "@resId", reservationId },
                        { "@docId", invoiceDocId }
                    });

                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 ID FROM Accounting_ETax_Log WHERE Nexaacc_Doc_Id = @docId ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@docId", invoiceDocId } });
                if (dt?.Rows.Count > 0)
                    return Convert.ToInt64(dt.Rows[0]["ID"]);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"InsertEtaxLogPending failed: {ex.Message}", "SYSTEM");
            }
            return 0;
        }

        private void UpdateEtaxLogSuccess(long logId, EtaxInvoiceResponse data)
        {
            if (logId <= 0) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_ETax_Log
                      SET Nexaacc_Etax_Id = @etaxId,
                          Etax_Ref_Number = @refNum,
                          Status = @status,
                          Signed_Date = @signedAt,
                          Submitted_Date = @submittedAt,
                          Xml_Url = @xmlUrl,
                          Pdf_Url = @pdfUrl,
                          Processed_Date = GETDATE()
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@etaxId", data.Id },
                        { "@refNum", (object)data.EtaxRefNumber ?? DBNull.Value },
                        { "@status", data.Status ?? "GENERATED" },
                        { "@signedAt", (object)data.SignedAt ?? DBNull.Value },
                        { "@submittedAt", (object)data.SubmittedAt ?? DBNull.Value },
                        { "@xmlUrl", (object)data.XmlUrl ?? DBNull.Value },
                        { "@pdfUrl", (object)data.PdfUrl ?? DBNull.Value },
                        { "@id", logId }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"UpdateEtaxLogSuccess failed for logId={logId}: {ex.Message}", "SYSTEM");
            }
        }

        private void UpdateEtaxLogFailed(long logId, string errorMessage)
        {
            if (logId <= 0) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_ETax_Log
                      SET Status = 'FAILED', Error_Message = @err, Processed_Date = GETDATE()
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@err", errorMessage ?? "" },
                        { "@id", logId }
                    });
            }
            catch { /* swallow — error logging is best effort */ }
        }

        private void MarkEtaxEmailSent(long logId, string email)
        {
            if (logId <= 0) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_ETax_Log
                      SET Email_Sent = 1, Error_Message = ISNULL(Error_Message, '') + N' | email→' + @em
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@em", email ?? "" },
                        { "@id", logId }
                    });
            }
            catch { /* swallow */ }
        }

        private static bool IsAlreadyVoided(AccountingApiException ex)
        {
            if (ex.StatusCode != 400) return false;
            string body = ex.ResponseBody ?? "";
            const string escapedThai = "\\u0E16\\u0E39\\u0E01\\u0E22\\u0E01\\u0E40\\u0E25\\u0E34\\u0E01\\u0E44\\u0E1B\\u0E41\\u0E25\\u0E49\\u0E27";
            return body.Contains("ถูกยกเลิกไปแล้ว")
                || body.IndexOf(escapedThai, StringComparison.OrdinalIgnoreCase) >= 0
                || body.Contains("ถูกกลับรายการไปแล้ว")
                || body.Contains("already cancelled")
                || body.Contains("already voided")
                || body.Contains("already reversed")
                || body.Contains("AlreadyVoided")
                || body.Contains("AlreadyCancelled")
                || body.Contains("AlreadyReversed");
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

        /// <summary>
        /// Find a recently COMPLETED entry within the given time window (seconds).
        /// Used as anti-duplicate guard against form resubmission / browser refresh.
        /// </summary>
        private long FindRecentCompletedEntry(string entityType, string actionType, string payloadKey, string payloadValue, int withinSeconds)
        {
            if (string.IsNullOrEmpty(payloadValue)) return -1;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @entityType AND Action_Type = @actionType
                        AND Status = 'COMPLETED'
                        AND Payload LIKE @pattern
                        AND Processed_Date >= DATEADD(SECOND, -@withinSeconds, GETDATE())
                      ORDER BY ID DESC",
                    new Dictionary<string, object>
                    {
                        { "@entityType", entityType },
                        { "@actionType", actionType },
                        { "@pattern", $"%\"{payloadKey}\":\"{payloadValue}\"%"},
                        { "@withinSeconds", withinSeconds }
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

        // ──────────────────────────────────────────────
        // File Attachment Helpers
        // ──────────────────────────────────────────────

        private static readonly long MaxAttachmentSize = 5 * 1024 * 1024; // 5MB base64 limit

        private List<IntegrationAttachment> LookupVoucherAttachments(int voucherId, string docNumber, DateTime voucherDate)
        {
            var attachments = new List<IntegrationAttachment>();
            try
            {
                string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"]
                    ?? ConfigurationManager.AppSettings["BaseFolderPath"];
                if (string.IsNullOrEmpty(basePath)) return null;

                // Pattern 1: Documents/Payment/{Year}/{Month}/ — PaymentVoucher.aspx uploads
                string yearMonth = $"{voucherDate.Year}/{voucherDate.Month}";
                string paymentDir = Path.Combine(basePath, "Documents", "Payment", yearMonth);
                if (Directory.Exists(paymentDir))
                {
                    foreach (var file in Directory.GetFiles(paymentDir))
                    {
                        var fi = new FileInfo(file);
                        if (fi.Length > 0 && fi.Length <= MaxAttachmentSize && IsImageOrPdf(fi.Extension))
                        {
                            attachments.Add(FileToAttachment(fi));
                            if (attachments.Count >= 5) break;
                        }
                    }
                }

                // Pattern 2: Upload/Slip/{VoucherNumber}_*.jpg — Voucher/Default.aspx uploads
                if (attachments.Count == 0 && !string.IsNullOrEmpty(docNumber))
                {
                    string slipDir = Path.Combine(basePath, "Upload", "Slip");
                    if (Directory.Exists(slipDir))
                    {
                        string searchPattern = $"{docNumber}_*";
                        foreach (var file in Directory.GetFiles(slipDir, searchPattern))
                        {
                            var fi = new FileInfo(file);
                            if (fi.Length > 0 && fi.Length <= MaxAttachmentSize)
                            {
                                attachments.Add(FileToAttachment(fi));
                                if (attachments.Count >= 5) break;
                            }
                        }
                    }
                }

                // Pattern 3: Payment_Slips table — SlipFileURL
                if (attachments.Count == 0 && voucherId > 0)
                {
                    try
                    {
                        var dt = _code.DatabaseQuerySafe(_connectionString,
                            "SELECT TOP 3 SlipFileURL, FileName, FileType FROM Payment_Slips WHERE Voucher_ID = @id AND VerificationStatus != 'REJECTED'",
                            new Dictionary<string, object> { { "@id", voucherId } });
                        if (dt?.Rows.Count > 0)
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                string slipUrl = row["SlipFileURL"]?.ToString();
                                if (string.IsNullOrEmpty(slipUrl)) continue;
                                string slipPath = Path.Combine(basePath, slipUrl.TrimStart('/', '\\'));
                                if (File.Exists(slipPath))
                                {
                                    var fi = new FileInfo(slipPath);
                                    if (fi.Length > 0 && fi.Length <= MaxAttachmentSize)
                                        attachments.Add(FileToAttachment(fi));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVoucherAttachments: error for voucher #{voucherId}: {ex.Message}", "SYSTEM");
            }

            if (attachments.Count > 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVoucherAttachments: found {attachments.Count} file(s) for voucher #{voucherId}", "SYSTEM");
            }
            return attachments.Count > 0 ? attachments : null;
        }

        private List<IntegrationAttachment> LookupReceiptAttachments(string receiptNumber, int reservationId)
        {
            var attachments = new List<IntegrationAttachment>();
            try
            {
                string basePath = ConfigurationManager.AppSettings["ReceiptFolderPath"]
                    ?? ConfigurationManager.AppSettings["BaseFolderPath"];
                if (string.IsNullOrEmpty(basePath)) return null;

                // Search in Documents/Receipt/{Year}/{Month}/ for receipt PDFs
                string receiptBaseDir = Path.Combine(basePath, "Documents", "Receipt");
                if (!Directory.Exists(receiptBaseDir))
                    receiptBaseDir = basePath;

                if (Directory.Exists(receiptBaseDir))
                {
                    // Search recent year/month directories
                    var now = DateTime.Now;
                    for (int monthOffset = 0; monthOffset <= 2; monthOffset++)
                    {
                        var dt = now.AddMonths(-monthOffset);
                        string dir = Path.Combine(receiptBaseDir, dt.Year.ToString(), dt.Month.ToString());
                        if (!Directory.Exists(dir)) continue;

                        foreach (var file in Directory.GetFiles(dir, "*.pdf"))
                        {
                            string fn = Path.GetFileNameWithoutExtension(file);
                            // Skip cancelled receipts
                            if (fn.EndsWith("_Cancel", StringComparison.OrdinalIgnoreCase)) continue;
                            // Skip e-tax duplicates
                            if (fn.EndsWith("_etax", StringComparison.OrdinalIgnoreCase)) continue;

                            var fi = new FileInfo(file);
                            if (fi.Length > 0 && fi.Length <= MaxAttachmentSize)
                            {
                                attachments.Add(FileToAttachment(fi));
                                break;
                            }
                        }
                        if (attachments.Count > 0) break;
                    }
                }

                // Also look for payment slips linked to this reservation
                if (reservationId > 0)
                {
                    try
                    {
                        string slipBasePath = ConfigurationManager.AppSettings["BaseFolderPath"] ?? basePath;
                        var dt = _code.DatabaseQuerySafe(_connectionString,
                            "SELECT TOP 2 SlipFileURL, FileName, FileType FROM Payment_Slips WHERE Reservation_ID = @id AND VerificationStatus != 'REJECTED'",
                            new Dictionary<string, object> { { "@id", reservationId } });
                        if (dt?.Rows.Count > 0)
                        {
                            foreach (DataRow row in dt.Rows)
                            {
                                string slipUrl = row["SlipFileURL"]?.ToString();
                                if (string.IsNullOrEmpty(slipUrl)) continue;
                                string slipPath = Path.Combine(slipBasePath, slipUrl.TrimStart('/', '\\'));
                                if (File.Exists(slipPath))
                                {
                                    var fi = new FileInfo(slipPath);
                                    if (fi.Length > 0 && fi.Length <= MaxAttachmentSize)
                                        attachments.Add(FileToAttachment(fi));
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupReceiptAttachments: error for receipt={receiptNumber}: {ex.Message}", "SYSTEM");
            }

            if (attachments.Count > 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupReceiptAttachments: found {attachments.Count} file(s) for receipt={receiptNumber}", "SYSTEM");
            }
            return attachments.Count > 0 ? attachments : null;
        }

        private static IntegrationAttachment FileToAttachment(FileInfo fi)
        {
            byte[] bytes = File.ReadAllBytes(fi.FullName);
            string ext = (fi.Extension ?? "").ToLower();
            string contentType;
            switch (ext)
            {
                case ".pdf": contentType = "application/pdf"; break;
                case ".jpg": case ".jpeg": contentType = "image/jpeg"; break;
                case ".png": contentType = "image/png"; break;
                case ".gif": contentType = "image/gif"; break;
                case ".bmp": contentType = "image/bmp"; break;
                default: contentType = "application/octet-stream"; break;
            }

            return new IntegrationAttachment
            {
                FileName = fi.Name,
                ContentType = contentType,
                Base64Content = Convert.ToBase64String(bytes)
            };
        }

        private static bool IsImageOrPdf(string extension)
        {
            string ext = (extension ?? "").ToLower();
            return ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp";
        }
    }
}
