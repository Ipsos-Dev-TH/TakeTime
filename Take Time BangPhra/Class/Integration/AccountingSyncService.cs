using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
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
        private string _lastDocNumber;
        private string _lastDocType;

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
            List<Dictionary<string, object>> expenseLines = null,
            bool isCredit = false, bool autoRecordPayment = false,
            string supplierExternalId = null, string supplierTaxId = null)
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
                { "whtAmount", whtAmount },
                { "isCredit", isCredit },
                { "autoRecordPayment", autoRecordPayment }
            };
            if (!string.IsNullOrEmpty(documentNumber))
                payload["documentNumber"] = documentNumber;
            if (!string.IsNullOrEmpty(paymentAccountId))
                payload["paymentAccountId"] = paymentAccountId;
            if (!string.IsNullOrEmpty(expenseAccountId))
                payload["expenseAccountId"] = expenseAccountId;
            if (expenseLines != null && expenseLines.Count > 0)
                payload["expenseLines"] = expenseLines;
            if (!string.IsNullOrEmpty(supplierExternalId))
                payload["supplierExternalId"] = supplierExternalId;
            if (!string.IsNullOrEmpty(supplierTaxId))
                payload["supplierTaxId"] = supplierTaxId;

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
            string revenueType = null, string paymentAccountId = null,
            decimal depositApplied = 0)
        {
            if (!_config.IsConfigured) return -1;
            if (totalAmount <= 0) return -1;  // กันใบเสร็จยอด 0/ติดลบ ไม่ให้เข้า queue (จะ fail ที่ processor)

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
            if (depositApplied > 0)
                payload["depositApplied"] = depositApplied;

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
        // Stock / Inventory Enqueue Methods
        // ──────────────────────────────────────────────

        /// <summary>
        /// รับสินค้าเข้าสต็อก (vendor purchase) → DR Inventory, CR Cash/AP
        /// อ้างอิงด้วย stockRef เพื่อ idempotency (เช่น "PIN-{productId}-{ts}")
        /// </summary>
        public long EnqueueStockIn(int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime receiveDate, string supplierName, string paymentMethod = null, bool hasInputVat = false,
            string stockRef = null)
        {
            if (!_config.IsConfigured) return -1;
            if (quantity <= 0 || costPerUnit <= 0) return -1;

            string refStr = !string.IsNullOrEmpty(stockRef) ? stockRef : $"PIN-{productId}-{receiveDate:yyyyMMddHHmmss}";

            long existing = FindPendingEntry("STOCK", "STOCK_IN", "stockRef", refStr);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("STOCK", "STOCK_IN", "stockRef", refStr, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantity", quantity },
                { "costPerUnit", costPerUnit },
                { "totalCost", Math.Round(quantity * costPerUnit, 2) },
                { "receiveDate", receiveDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "supplierName", supplierName ?? "" },
                { "paymentMethod", paymentMethod ?? "" },
                { "hasInputVat", hasInputVat }
            };
            return InsertQueue("STOCK", productId, "STOCK_IN", payload);
        }

        /// <summary>
        /// ตัดสต็อก (ขาย / room charge) → DR COGS, CR Inventory (cost price)
        /// </summary>
        public long EnqueueStockOutCogs(int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime outDate, string reason, string stockRef = null)
        {
            if (!_config.IsConfigured) return -1;
            if (quantity <= 0 || costPerUnit <= 0) return -1;

            string refStr = !string.IsNullOrEmpty(stockRef) ? stockRef : $"POUT-{productId}-{outDate:yyyyMMddHHmmss}";

            long existing = FindPendingEntry("STOCK", "STOCK_OUT_COGS", "stockRef", refStr);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("STOCK", "STOCK_OUT_COGS", "stockRef", refStr, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantity", quantity },
                { "costPerUnit", costPerUnit },
                { "outDate", outDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "reason", reason ?? "" }
            };
            return InsertQueue("STOCK", productId, "STOCK_OUT_COGS", payload);
        }

        /// <summary>Reverse COGS journal เมื่อ cancel room charge (คืนสต็อก) — DR Inventory, CR COGS</summary>
        public long EnqueueStockOutCogsReversal(int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime reverseDate, string reason, string stockRef)
        {
            if (!_config.IsConfigured) return -1;
            if (quantity <= 0 || costPerUnit <= 0) return -1;
            if (string.IsNullOrEmpty(stockRef)) return -1;

            long existing = FindPendingEntry("STOCK", "STOCK_OUT_COGS_REVERSE", "stockRef", stockRef);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("STOCK", "STOCK_OUT_COGS_REVERSE", "stockRef", stockRef, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", stockRef },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantity", quantity },
                { "costPerUnit", costPerUnit },
                { "reverseDate", reverseDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "reason", reason ?? "" }
            };
            return InsertQueue("STOCK", productId, "STOCK_OUT_COGS_REVERSE", payload);
        }

        /// <summary>Stock adjustment จากการนับ — quantityDiff +/- (gain or loss)</summary>
        public long EnqueueStockAdjustment(long adjustmentLogId, int productId, string productName,
            decimal quantityDiff, decimal costPerUnit, DateTime adjustDate, string reason)
        {
            if (!_config.IsConfigured) return -1;
            if (quantityDiff == 0 || costPerUnit <= 0) return -1;

            string refStr = $"SADJ-{adjustmentLogId}";
            long existing = FindPendingEntry("STOCK", "STOCK_ADJUSTMENT", "stockRef", refStr);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("STOCK", "STOCK_ADJUSTMENT", "stockRef", refStr, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "adjustmentLogId", adjustmentLogId },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantityDiff", quantityDiff },
                { "costPerUnit", costPerUnit },
                { "adjustDate", adjustDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "reason", reason ?? "" }
            };
            return InsertQueue("STOCK", productId, "STOCK_ADJUSTMENT", payload);
        }

        /// <summary>Write-off (damage/loss) — DR Stock Loss, CR Inventory</summary>
        public long EnqueueStockWriteOff(long adjustmentLogId, int productId, string productName,
            decimal quantity, decimal costPerUnit, DateTime writeOffDate, string reason)
        {
            if (!_config.IsConfigured) return -1;
            if (quantity <= 0 || costPerUnit <= 0) return -1;

            string refStr = $"SWO-{adjustmentLogId}";
            long existing = FindPendingEntry("STOCK", "STOCK_WRITEOFF", "stockRef", refStr);
            if (existing > 0) return existing;
            long completed = FindRecentCompletedEntry("STOCK", "STOCK_WRITEOFF", "stockRef", refStr, 86400);
            if (completed > 0) return completed;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "adjustmentLogId", adjustmentLogId },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantity", quantity },
                { "costPerUnit", costPerUnit },
                { "writeOffDate", writeOffDate.ToString("yyyy-MM-dd HH:mm:ss") },
                { "reason", reason ?? "" }
            };
            return InsertQueue("STOCK", productId, "STOCK_WRITEOFF", payload);
        }

        /// <summary>Sync product master ไป NextAcc — ใช้ตอนสร้าง/แก้ไขสินค้า</summary>
        public long EnqueueProductSync(int productId)
        {
            if (!_config.IsConfigured || productId <= 0) return -1;

            string refStr = $"PRODUCT-{productId}";
            long existing = FindPendingEntry("PRODUCT", "SYNC_PRODUCT_MASTER", "stockRef", refStr);
            if (existing > 0) return existing;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "productId", productId }
            };
            return InsertQueue("PRODUCT", productId, "SYNC_PRODUCT_MASTER", payload);
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
                // Escape LIKE wildcards (% _ [) ใช้ bracket convention ของ SQL Server
                // กัน wildcard ใน document number match รายการอื่นโดยไม่ตั้งใจ
                string esc = (documentNumber ?? "").Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
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
                        { "@pattern1", $"%\"receiptNumber\":\"{esc}\"%"},
                        { "@pattern2", $"%\"documentNumber\":\"{esc}\"%"}
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
                    "SELECT Nexaacc_AccountId FROM Account_Paid_How WHERE Paid_How = @name AND Status = 'True'",
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
                    "SELECT Nexaacc_AccountId FROM Account_Paid_Type WHERE Paid_Type = @name AND Status = 'True'",
                    new Dictionary<string, object> { { "@name", paidTypeText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_AccountId"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_AccountId"].ToString();
            }
            catch { }
            return null;
        }

        public string LookupPaidTypeAccountCode(string paidTypeText)
        {
            if (string.IsNullOrEmpty(paidTypeText)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_AccountCode FROM Account_Paid_Type WHERE Paid_Type = @name AND Status = 'True'",
                    new Dictionary<string, object> { { "@name", paidTypeText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_AccountCode"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_AccountCode"].ToString();
            }
            catch { }
            return null;
        }

        public string LookupPaidHowAccountCode(string paidHowText)
        {
            if (string.IsNullOrEmpty(paidHowText)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_AccountCode FROM Account_Paid_How WHERE Paid_How = @name AND Status = 'True'",
                    new Dictionary<string, object> { { "@name", paidHowText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_AccountCode"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_AccountCode"].ToString();
            }
            catch { }
            return null;
        }

        public bool IsPaidHowCashOrBank(string paidHowText)
        {
            if (string.IsNullOrEmpty(paidHowText)) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT IsCashOrBank FROM Account_Paid_How WHERE Paid_How = @name AND Status = 'True'",
                    new Dictionary<string, object> { { "@name", paidHowText } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["IsCashOrBank"] != DBNull.Value)
                    return Convert.ToBoolean(dt.Rows[0]["IsCashOrBank"]);
            }
            catch
            {
                // Fallback: detect by name pattern if IsCashOrBank column doesn't exist yet
                string pm = paidHowText.ToLower();
                return pm.Contains("เงินสด") || pm.Contains("ธนาคาร") || pm.Contains("โอน")
                    || pm.Contains("บัญชี") || pm.Contains("cash") || pm.Contains("bank") || pm.Contains("transfer");
            }
            return false;
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
                    "SELECT TOP 1 Reservation_ID FROM Account_Receipt WHERE ID = @num",
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
                    "SELECT TOP 1 Total_Amount FROM Account_Receipt WHERE ID = @num",
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

            // Cleanup orphaned PROCESSING items — ถ้าค้างเกิน 10 นาที (process crash หรือ timeout)
            // ให้กลับไปเป็น PENDING เพื่อให้ retry ในรอบถัดไป
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Accounting_Sync_Queue
                      SET Status = 'PENDING',
                          Error_Message = ISNULL(Error_Message, '') + N' | recovered from orphaned PROCESSING'
                      WHERE Status = 'PROCESSING'
                        AND Created_Date < DATEADD(MINUTE, -10, GETDATE())",
                    null);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessQueueAsync orphan cleanup failed: {ex.Message}", "SYSTEM");
            }

            // Cleanup deprecated/skipped entries เก่ากว่า 7 วัน — ลด queue ขยะ
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"DELETE FROM Accounting_Sync_Queue
                      WHERE (Nexaacc_Response_Id = 'SKIPPED_DEPRECATED' OR Nexaacc_Response_Id = 'SKIPPED_LOCAL_MODE')
                        AND Status = 'COMPLETED'
                        AND Processed_Date < DATEADD(DAY, -7, GETDATE())",
                    null);
            }
            catch { /* best effort */ }

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
                    _lastDocNumber = null;
                    _lastDocType = null;
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
                        UpdateQueueStatus(queueId, "COMPLETED", null, nexaaccId, _lastDocNumber, _lastDocType);
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

                // ── Stock / Inventory ──
                case "STOCK_IN":
                    return await ProcessStockIn(payload);
                case "STOCK_OUT_COGS":
                    return await ProcessStockOutCogs(payload);
                case "STOCK_OUT_COGS_REVERSE":
                    return await ProcessStockOutCogsReversal(payload);
                case "STOCK_ADJUSTMENT":
                    return await ProcessStockAdjustment(payload);
                case "STOCK_WRITEOFF":
                    return await ProcessStockWriteOff(payload);
                case "SYNC_PRODUCT_MASTER":
                    return await ProcessProductSync(payload);

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
            catch (AccountingApiException ex) when (IsAlreadyPostedOrTerminal(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"Journal {journalId} already posted/voided/non-draft - treating as success ({ex.StatusCode})", "SYSTEM");
            }
        }

        /// <summary>
        /// Journal post calls อาจ fail เพราะเอกสารอยู่ในสถานะที่ post ไปแล้ว/voided/reversed
        /// — ทุก state ถือเป็น "ไม่ต้องทำอะไรต่อ" ไม่ใช่ error
        /// </summary>
        private static bool IsAlreadyPostedOrTerminal(AccountingApiException ex)
        {
            if (ex.StatusCode == 404) return true;  // เอกสารไม่อยู่แล้ว = แล้วแต่ caller จัดการ
            if (ex.StatusCode != 400) return false;
            string body = ex.ResponseBody ?? "";
            return body.Contains("Draft")
                || body.Contains("Posted")
                || body.Contains("Voided")
                || body.Contains("Reversed")
                || body.Contains("Cancelled")
                || body.Contains("ลงรายการแล้ว")
                || body.Contains("ยกเลิกไปแล้ว");
        }

        /// <summary>
        /// Approve เอกสาร — JWT-only endpoint. ปัจจุบันไม่มี caller (Integration invoice/expense
        /// auto-approve อยู่แล้ว) คงไว้สำหรับ flow ในอนาคตที่ใช้ DocumentController แยก
        /// </summary>
        [Obsolete("Integration endpoints auto-approve documents. JWT-only — will 401 with Integration Key.")]
        private async Task SafeApproveDocumentAsync(Guid documentId)
        {
            try
            {
                #pragma warning disable CS0618
                await _apiClient.ApproveDocumentAsync(documentId);
                #pragma warning restore CS0618
            }
            catch (AccountingApiException ex) when (IsAlreadyPostedOrTerminal(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"Document {documentId} already approved/non-draft - treating as success ({ex.StatusCode})", "SYSTEM");
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
                                    AccountId = lineDict.ContainsKey("accountId") ? lineDict["accountId"]?.ToString() : null,
                                    AccountCode = lineDict.ContainsKey("accountCode") ? lineDict["accountCode"]?.ToString() : null
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

            bool isCredit = p.ContainsKey("isCredit") && Convert.ToBoolean(p["isCredit"]);
            bool autoRecordPayment = p.ContainsKey("autoRecordPayment") && Convert.ToBoolean(p["autoRecordPayment"]);

            int lineCount = expenseLines?.Count ?? 0;
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessVoucherJournal: doc={docNumber} amount={amount} category={expenseCategory} payee={payeeName} lines={lineCount} isCredit={isCredit} autoPayment={autoRecordPayment} mode={_config.VoucherSyncMode}",
                "SYSTEM");

            // Lookup voucher attachment files
            List<IntegrationAttachment> attachments = null;
            if (_config.AttachFiles)
                attachments = LookupVoucherAttachments(voucherId, docNumber, voucherDate);

            // Ensure supplier exists as Contact in NextAcc (DOCUMENT mode)
            ContactInfo supplierContact = null;
            if (_config.IsVoucherDocumentMode)
            {
                string supplierExternalId = p.ContainsKey("supplierExternalId") ? p["supplierExternalId"]?.ToString() : null;
                string supplierTaxId = p.ContainsKey("supplierTaxId") ? p["supplierTaxId"]?.ToString() : null;
                if (!string.IsNullOrEmpty(supplierExternalId))
                {
                    // Explicit vendor info from the Account_Payment voucher flow (voucherId == 0)
                    supplierContact = await EnsureSupplierContactAsync(new ContactInfo
                    {
                        ExternalId = supplierExternalId,
                        Name = payeeName,
                        TaxId = supplierTaxId
                    });
                }
                else
                {
                    supplierContact = await EnsureSupplierContactAsync(voucherId, payeeName);
                }
            }

            bool isSalaryVoucher = (expenseCategory ?? "").Contains("เงินเดือน")
                || (expenseCategory ?? "").Equals("salary", StringComparison.OrdinalIgnoreCase);

            string nexaaccId = null;
            string nexaaccDocNumber = null;
            string nexaaccDocType = null;

            if (_config.IsVoucherDocumentMode)
            {
                var expense = _mapper.MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber);
                expense.Attachments = attachments;
                if (isSalaryVoucher)
                    expense.Sensitivity = "Payroll";
                ApplyContactToExpense(expense, supplierContact);

                ApiResponse<IntegrationDocumentResponse> result;
                var filePaths = ExtractFilePaths(attachments);
                if (filePaths != null && filePaths.Count > 0)
                    result = await _apiClient.CreateExpenseMultipartAsync(expense, filePaths);
                else
                    result = await _apiClient.CreateExpenseAsync(expense);

                Guid expDocId = RequireValidDocId(result?.data?.Id, $"CreateExpense (voucher) doc={docNumber}");
                nexaaccId = expDocId.ToString();
                nexaaccDocNumber = result?.data?.DocumentNumber;
                nexaaccDocType = "EXPENSE";
                _lastDocNumber = nexaaccDocNumber;
                _lastDocType = nexaaccDocType;

                // Auto-generate WHT certificate if WHT was applied
                if (whtAmount > 0)
                    await TryAutoGenerateWhtCertAsync(expDocId, docNumber);

                // Auto-record payment in NextAcc: Cash/Bank + no credit
                if (autoRecordPayment && !isCredit)
                {
                    await AutoRecordPaymentForVoucher(expDocId, amount, whtAmount, voucherDate,
                        paymentMethod, payeeName, docNumber, paymentAccountId);
                }
            }
            else
            {
                var journal = _mapper.MapVoucherToJournal(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber);
                if (isSalaryVoucher)
                    journal.Sensitivity = "Payroll";
                var result = await _apiClient.CreateJournalAsync(journal);
                Guid jrnlId = RequireValidDocId(result?.data?.Id, $"CreateJournal (voucher) doc={docNumber}");
                nexaaccDocNumber = result?.data?.EntryNumber;
                nexaaccDocType = "JOURNAL";
                _lastDocNumber = nexaaccDocNumber;
                _lastDocType = nexaaccDocType;
                nexaaccId = jrnlId.ToString();
                await SafePostJournalAsync(jrnlId);
            }

            // Backfill NextAcc reference to Account_Payment table
            BackfillNextAccRefToPayment(docNumber, nexaaccId, nexaaccDocNumber);

            return nexaaccId;
        }

        private async System.Threading.Tasks.Task AutoRecordPaymentForVoucher(
            Guid documentId, decimal totalAmount, decimal whtAmount,
            DateTime voucherDate, string paymentMethod, string payeeName,
            string docNumber, string paymentAccountId)
        {
            try
            {
                decimal payAmount = totalAmount - whtAmount;
                if (payAmount <= 0) return;

                var paymentRequest = new CreateIntegrationPaymentRequest
                {
                    ExternalId = $"PAY-{docNumber}",
                    ExternalRef = docNumber,
                    DocumentId = documentId,
                    PaymentDate = voucherDate,
                    Amount = payAmount,
                    PaymentMethod = AccountingDataMapper.NormalizePaymentMethod(paymentMethod),
                    Notes = $"ชำระเงินอัตโนมัติจากใบสำคัญจ่าย {docNumber}"
                };

                var payResult = await _apiClient.CreateIntegrationPaymentAsync(paymentRequest);

                if (payResult?.success == true && payResult.data != null)
                {
                    string paymentId = payResult.data.Id.ToString();
                    _code.Logs(_connectionString, "AccountingSync",
                        $"AutoRecordPayment: SUCCESS doc={docNumber} paymentId={paymentId} amount={payAmount:N2}",
                        "SYSTEM");

                    // Store payment ID in Account_Payment
                    try
                    {
                        _code.DatabaseInsertSafe(_connectionString,
                            "UPDATE Account_Payment SET Nexaacc_Payment_Id = @PaymentId WHERE ID = @DocNum",
                            new Dictionary<string, object>
                            {
                                { "@PaymentId", paymentId },
                                { "@DocNum", docNumber }
                            });
                    }
                    catch (Exception exStore)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"AutoRecordPayment: WARNING failed to store Nexaacc_Payment_Id for doc={docNumber}: {exStore.Message}", "SYSTEM");
                    }
                }
                else
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"AutoRecordPayment: FAILED doc={docNumber} msg={payResult?.message ?? "null response"}",
                        "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"AutoRecordPayment: ERROR doc={docNumber} {ex.Message}", "SYSTEM");
            }
        }

        private void BackfillNextAccRefToPayment(string docNumber, string nexaaccId, string nexaaccDocNumber)
        {
            if (string.IsNullOrEmpty(docNumber)) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Account_Payment
                      SET Nexaacc_Response_Id = COALESCE(@RespId, Nexaacc_Response_Id),
                          Nexaacc_Document_Number = COALESCE(@DocNum, Nexaacc_Document_Number)
                      WHERE ID = @ID",
                    new Dictionary<string, object>
                    {
                        { "@RespId", string.IsNullOrEmpty(nexaaccId) ? (object)DBNull.Value : nexaaccId },
                        { "@DocNum", string.IsNullOrEmpty(nexaaccDocNumber) ? (object)DBNull.Value : nexaaccDocNumber },
                        { "@ID", docNumber }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"BackfillNextAccRefToPayment: ERROR doc={docNumber} {ex.Message}", "SYSTEM");
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

            List<IntegrationAttachment> attachments = null;
            if (_config.AttachFiles)
                attachments = LookupPayrollAttachments(docNumber, payDate);

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
                expense.Attachments = attachments;

                ApiResponse<IntegrationDocumentResponse> result;
                var filePaths = ExtractFilePaths(attachments);
                if (filePaths != null && filePaths.Count > 0)
                    result = await _apiClient.CreateExpenseMultipartAsync(expense, filePaths);
                else
                    result = await _apiClient.CreateExpenseAsync(expense);

                Guid expId = RequireValidDocId(result?.data?.Id, $"CreateExpense (payroll) period={period}");
                _lastDocNumber = result?.data?.DocumentNumber;
                _lastDocType = "EXPENSE";
                return expId.ToString();
            }
            else
            {
                var journal = _mapper.MapPayrollToJournal(totalSalary, payDate, period,
                    ssfEmployee, ssfEmployer, whtAmount);
                if (!string.IsNullOrEmpty(docNumber))
                    journal.Reference = docNumber;
                var result = await _apiClient.CreateJournalAsync(journal);
                Guid payrollId = RequireValidDocId(result?.data?.Id, $"CreateJournal (payroll) period={period}");
                _lastDocNumber = result?.data?.EntryNumber;
                _lastDocType = "JOURNAL";
                await SafePostJournalAsync(payrollId);
                return payrollId.ToString();
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

            // Ensure customer exists as Contact in NextAcc (เฉพาะ DOCUMENT mode ที่สร้าง invoice)
            ContactInfo customerContact = null;
            if (_config.IsReceiptDocumentMode)
            {
                customerContact = await EnsureCustomerContactAsync(reservationId);
            }

            if (isDeposit)
            {
                // โหมด RECEIPT: แยก VAT ออกจากมัดจำตั้งแต่ตอนรับเงิน (ม.78/1 — บริการ)
                bool depositHasVat = LookupBusinessHasVat();
                bool depositVatAtReceipt = _config.IsDepositVatAtReceipt;

                if (_config.IsReceiptDocumentMode)
                {
                    var invoice = _mapper.MapDepositToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName,
                        paymentAccountId: paymentAccountId, hasVat: depositHasVat, vatAtReceipt: depositVatAtReceipt);
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.Reference = receiptNumber;
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    invoice.Attachments = attachments;
                    ApplyContactToInvoice(invoice, customerContact);

                    ApiResponse<IntegrationDocumentResponse> result;
                    var filePaths = ExtractFilePaths(attachments);
                    if (filePaths != null && filePaths.Count > 0)
                        result = await _apiClient.CreateInvoiceMultipartAsync(invoice, filePaths);
                    else
                        result = await _apiClient.CreateInvoiceAsync(invoice);

                    Guid invDocId = RequireValidDocId(result?.data?.Id, $"CreateInvoice (deposit) receipt={receiptNumber}");
                    _lastDocNumber = result?.data?.DocumentNumber;
                    _lastDocType = "INVOICE";
                    await TryAutoGenerateEtaxAsync(invDocId, receiptNumber, reservationId, totalAmount, customerName);
                    return invDocId.ToString();
                }
                else
                {
                    var journal = _mapper.MapDepositToJournal(reservationId, totalAmount, paymentMethod, receiptDate, customerName,
                        paymentAccountId: paymentAccountId, documentNumber: receiptNumber,
                        hasVat: depositHasVat, vatAtReceipt: depositVatAtReceipt);
                    var result = await _apiClient.CreateJournalAsync(journal);
                    Guid jrnlDocId = RequireValidDocId(result?.data?.Id, $"CreateJournal (deposit) receipt={receiptNumber}");
                    _lastDocNumber = result?.data?.EntryNumber;
                    _lastDocType = "JOURNAL";
                    await SafePostJournalAsync(jrnlDocId);
                    return jrnlDocId.ToString();
                }
            }
            else
            {
                // ใช้ Business_Info.Use_Vat เป็นแหล่งความจริง (สอดคล้องกับ deposit/checkout/void branches)
                // ไม่ใช้ vatAmount > 0 จาก receipt — กันกรณี receipt.Vat=0 ทั้งที่กิจการจด VAT
                // → ถ้า deposit รับรู้ VAT ไปแล้ว แต่ใบเสร็จนี้ไม่ split VAT จะทำให้ VAT รับรู้ไม่ครบ
                bool hasVat = LookupBusinessHasVat();
                decimal depositApplied = p.ContainsKey("depositApplied") ? Convert.ToDecimal(p["depositApplied"]) : 0m;
                if (depositApplied <= 0)
                    depositApplied = LookupDepositAppliedFromReceipt(receiptNumber);

                // Auto-build line breakdown จาก Account_Receipt_Detail
                // ถ้าไม่พบ → fallback เป็น single line ตาม revenueType
                decimal depositFromLines;
                var lines = LookupReceiptLinesEx(receiptNumber, reservationId, totalAmount, revenueType, out depositFromLines);

                // Reserve.aspx check-in สร้าง "ส่วนลด" line ติดลบเพื่อชดเชยมัดจำ → แปลงเป็น depositApplied แทน
                // เพื่อให้ MapMultiLinePaymentToJournal คิด VAT ถูกและ checkout clearing ไม่ double-debit
                if (depositFromLines > 0)
                {
                    depositApplied += depositFromLines;
                    // Persist depositApplied ลง Account_Receipt เพื่อให้ TryEnqueueDepositClearing เห็น (anti-double-clear)
                    try
                    {
                        _code.DatabaseInsertSafe(_connectionString,
                            "UPDATE Account_Receipt SET Deposit_Applied_Amount = @amt WHERE ID = @num AND ISNULL(Deposit_Applied_Amount, 0) < @amt",
                            new Dictionary<string, object> { { "@amt", depositApplied }, { "@num", receiptNumber } });
                    }
                    catch { }
                }

                bool useMultiLine = lines != null && (lines.Count > 1 || depositApplied > 0);

                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessReceiptDocument(payment): receipt={receiptNumber} lines={lines?.Count ?? 0} depositApplied={depositApplied} (from negativeLines={depositFromLines}) multiLine={useMultiLine}",
                    "SYSTEM");

                if (_config.IsReceiptDocumentMode)
                {
                    CreateIntegrationInvoiceRequest invoice;
                    if (useMultiLine)
                    {
                        invoice = _mapper.MapMultiLinePaymentToInvoice(reservationId, lines, paymentMethod, receiptDate,
                            customerName, hasVat, paymentAccountId, depositApplied, receiptNumber);
                    }
                    else
                    {
                        invoice = _mapper.MapPaymentToInvoice(reservationId, totalAmount, paymentMethod, receiptDate,
                            customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId);
                        invoice.Reference = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : $"RES-{reservationId}";
                        if (!string.IsNullOrEmpty(receiptNumber))
                        {
                            invoice.ExternalRef = receiptNumber;
                            invoice.ReplaceExistingForSource = true;
                        }
                    }
                    invoice.Attachments = attachments;
                    ApplyContactToInvoice(invoice, customerContact);

                    ApiResponse<IntegrationDocumentResponse> result;
                    var fpay = ExtractFilePaths(attachments);
                    if (fpay != null && fpay.Count > 0)
                        result = await _apiClient.CreateInvoiceMultipartAsync(invoice, fpay);
                    else
                        result = await _apiClient.CreateInvoiceAsync(invoice);

                    Guid invDocId = RequireValidDocId(result?.data?.Id, $"CreateInvoice (payment) receipt={receiptNumber}");

                    // Adjustment journal: ถ้ามี deposit applied ให้ตัด ADVANCE_DEPOSIT + ลด Cash
                    if (depositApplied > 0)
                    {
                        try
                        {
                            var adj = _mapper.MapDepositAppliedAdjustment(reservationId, depositApplied, paymentMethod,
                                receiptDate, customerName, paymentAccountId, receiptNumber,
                                hasVat: hasVat, vatAtReceipt: _config.IsDepositVatAtReceipt);
                            var adjResult = await _apiClient.CreateJournalAsync(adj);
                            Guid adjId = RequireValidDocId(adjResult?.data?.Id, $"DepositAppliedAdjustment receipt={receiptNumber}");
                            await SafePostJournalAsync(adjId);
                            _code.Logs(_connectionString, "AccountingSync",
                                $"Deposit-applied adjustment posted: receipt={receiptNumber} amount={depositApplied} journalId={adjId}",
                                "SYSTEM");
                        }
                        catch (Exception adjEx)
                        {
                            // ต้อง re-throw — ถ้ากลืน exception ใบเสร็จจะถูกมาร์ค COMPLETED
                            // แต่ ADVANCE_DEPOSIT ไม่ถูกตัด → มัดจำค้างถาวร ไม่มี retry
                            // re-throw → queue retry → invoice idempotent (ExternalRef) ไม่สร้างซ้ำ + adjustment retry
                            _code.Logs(_connectionString, "AccountingSync",
                                $"Deposit-applied adjustment FAILED for receipt={receiptNumber}: {adjEx.Message} — จะ retry ทั้งรายการ", "SYSTEM");
                            throw new Exception(
                                $"Deposit-applied adjustment failed for receipt {receiptNumber} (invoice {invDocId} created OK, will retry): {adjEx.Message}", adjEx);
                        }
                    }

                    _lastDocNumber = result?.data?.DocumentNumber;
                    _lastDocType = "INVOICE";
                    await TryAutoGenerateEtaxAsync(invDocId, receiptNumber, reservationId, totalAmount, customerName);
                    return invDocId.ToString();
                }
                else
                {
                    CreateJournalEntryRequest journal;
                    if (useMultiLine)
                    {
                        journal = _mapper.MapMultiLinePaymentToJournal(reservationId, lines, paymentMethod, receiptDate,
                            customerName, hasVat, paymentAccountId, depositApplied, receiptNumber,
                            vatAtReceipt: _config.IsDepositVatAtReceipt);
                    }
                    else
                    {
                        journal = _mapper.MapPaymentToJournal(reservationId, totalAmount, paymentMethod, receiptDate,
                            customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId,
                            documentNumber: receiptNumber);
                    }
                    var result = await _apiClient.CreateJournalAsync(journal);
                    Guid jrnlDocId = RequireValidDocId(result?.data?.Id, $"CreateJournal (payment) receipt={receiptNumber}");
                    _lastDocNumber = result?.data?.EntryNumber;
                    _lastDocType = "JOURNAL";
                    await SafePostJournalAsync(jrnlDocId);
                    return jrnlDocId.ToString();
                }
            }
        }

        /// <summary>
        /// อ่าน lines จาก Account_Receipt_Detail สำหรับใบเสร็จ — ใช้ build invoice/journal แบบ multi-line
        /// ถ้าไม่พบ detail (ใบเสร็จเก่า) → return null (caller จะ fallback ไป single-line)
        /// </summary>
        private List<AccountingDataMapper.ReceiptLineSpec> LookupReceiptLines(
            string receiptNumber, int reservationId, decimal totalAmount, string revenueTypeFallback)
        {
            decimal _;
            return LookupReceiptLinesEx(receiptNumber, reservationId, totalAmount, revenueTypeFallback, out _);
        }

        /// <summary>
        /// อ่าน lines + แยก negative lines (deposit applied via UI workaround) ออกเป็น depositFromLines
        /// Reserve.aspx check-in ใส่ "ส่วนลด" line ติดลบเพื่อชดเชยมัดจำ — ถ้าเราเอาเข้า journal ตรงๆ
        /// VAT proration จะผิดเพราะ totalGross ลดลง แต่ positive lines ได้ proportional VAT inflate
        /// → แยกออกเป็น depositApplied แทน แล้ว mapper ใช้ MapMultiLinePaymentToJournal pattern ปกติ
        /// </summary>
        private List<AccountingDataMapper.ReceiptLineSpec> LookupReceiptLinesEx(
            string receiptNumber, int reservationId, decimal totalAmount, string revenueTypeFallback,
            out decimal depositFromNegativeLines)
        {
            depositFromNegativeLines = 0m;
            try
            {
                if (string.IsNullOrEmpty(receiptNumber)) return null;
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT D.ProductType_ID, D.Product_ID, D.Product_Data, D.Product_Amount,
                             D.Price_PerPeice, D.Price_Amount, D.Product_Unit
                      FROM Account_Receipt_Detail D
                      INNER JOIN Account_Receipt R ON R.ID = D.Receipt_ID
                      WHERE R.ID = @num
                      ORDER BY D.Number ASC",
                    new Dictionary<string, object> { { "@num", receiptNumber } });

                if (dt == null || dt.Rows.Count == 0) return null;

                var lines = new List<AccountingDataMapper.ReceiptLineSpec>();
                decimal sum = 0;
                foreach (DataRow row in dt.Rows)
                {
                    decimal qty = row["Product_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Product_Amount"]) : 1m;
                    decimal unitPrice = row["Price_PerPeice"] != DBNull.Value ? Convert.ToDecimal(row["Price_PerPeice"]) : 0m;
                    decimal amt = row["Price_Amount"] != DBNull.Value ? Convert.ToDecimal(row["Price_Amount"]) : qty * unitPrice;
                    if (amt == 0) continue;

                    sum += amt;

                    // Negative line = deposit application (UI workaround). อย่าส่งเข้า lines ของ mapper
                    // เพราะจะทำให้ VAT proration ผิด — แยกออกเป็น depositApplied แทน
                    if (amt < 0)
                    {
                        depositFromNegativeLines += -amt;
                        continue;
                    }

                    int? prodTypeId = row["ProductType_ID"] != DBNull.Value ? (int?)Convert.ToInt32(row["ProductType_ID"]) : null;
                    string desc = row["Product_Data"]?.ToString() ?? "";
                    string unit = row["Product_Unit"]?.ToString();

                    lines.Add(new AccountingDataMapper.ReceiptLineSpec
                    {
                        ProductTypeId = prodTypeId,
                        Description = desc,
                        Quantity = qty > 0 ? qty : 1,
                        UnitPrice = unitPrice,
                        Amount = amt,
                        Unit = unit,
                        RevenueTypeOverride = !string.IsNullOrEmpty(revenueTypeFallback) && !prodTypeId.HasValue ? revenueTypeFallback : null
                    });
                }

                if (lines.Count == 0) return null;

                // Sanity check: line sum (รวม negatives) ควรใกล้เคียง totalAmount
                if (Math.Abs(sum - totalAmount) > 0.05m)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"LookupReceiptLines: receipt={receiptNumber} line sum={sum} ≠ totalAmount={totalAmount} (depositFromLines={depositFromNegativeLines})",
                        "SYSTEM");
                }
                return lines;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupReceiptLines failed for receipt={receiptNumber}: {ex.Message}", "SYSTEM");
                return null;
            }
        }

        /// <summary>ดึง voucherId + supplier name สำหรับ debit note void flow</summary>
        private (int voucherId, string supplierName)? LookupSupplierFromVoucherDoc(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 P.UID AS VoucherId, ISNULL(V.Name, '') AS SupplierName
                      FROM Account_Payment P
                      LEFT JOIN Vendor V ON V.ID = P.Vendor_ID
                      WHERE P.ID = @num",
                    new Dictionary<string, object> { { "@num", documentNumber } });
                if (dt?.Rows.Count > 0)
                {
                    int vid = dt.Rows[0]["VoucherId"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["VoucherId"]) : 0;
                    string name = dt.Rows[0]["SupplierName"]?.ToString() ?? "";
                    return (vid, name);
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupSupplierFromVoucherDoc failed for doc={documentNumber}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>ดึงยอดรวมของ voucher (Account_Payment.Total_Amount) สำหรับ debit note void</summary>
        private decimal LookupVoucherAmount(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber)) return 0m;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Total_Amount FROM Account_Payment WHERE ID = @num",
                    new Dictionary<string, object> { { "@num", documentNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Total_Amount"] != DBNull.Value)
                    return Convert.ToDecimal(dt.Rows[0]["Total_Amount"]);
            }
            catch { }
            return 0m;
        }

        /// <summary>ดึงข้อมูล header ของใบเสร็จ (resId, customerName, paymentMethod, paymentAccountId) สำหรับ counter-adjustment</summary>
        private (int reservationId, string customerName, string paymentMethod, string paymentAccountId)? LookupReceiptHeaderInfo(string receiptNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 R.Reservation_ID, R.Paid_Type,
                             ISNULL(C.FullName, C.Name) AS CustName
                      FROM Account_Receipt R
                      LEFT JOIN Reservation Res ON Res.ID = R.Reservation_ID
                      LEFT JOIN Customer C ON C.MobilePhone = Res.Customer_MobilePhone
                      WHERE R.ID = @num",
                    new Dictionary<string, object> { { "@num", receiptNumber } });
                if (dt?.Rows.Count > 0)
                {
                    int rid = dt.Rows[0]["Reservation_ID"] != DBNull.Value ? Convert.ToInt32(dt.Rows[0]["Reservation_ID"]) : 0;
                    string name = dt.Rows[0]["CustName"]?.ToString() ?? "";
                    string method = dt.Rows[0]["Paid_Type"]?.ToString() ?? "CASH";
                    string accId = LookupPaidHowAccountId(method);
                    return (rid, name, method, accId);
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupReceiptHeaderInfo failed for receipt={receiptNumber}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>กันไม่ให้ Nexaacc_Response_Id เป็น empty Guid — บังคับ retry ถ้า API คืน null/empty</summary>
        private static Guid RequireValidDocId(Guid? id, string operation)
        {
            if (id == null || id == Guid.Empty)
                throw new Exception($"{operation}: NextAcc API returned empty document Id — sync will be retried");
            return id.Value;
        }

        /// <summary>หาจำนวนมัดจำที่หักในใบเสร็จ — จาก Account_Receipt.Deposit_Applied_Amount</summary>
        private decimal LookupDepositAppliedFromReceipt(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return 0m;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Deposit_Applied_Amount FROM Account_Receipt WHERE ID = @num",
                    new Dictionary<string, object> { { "@num", receiptNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Deposit_Applied_Amount"] != DBNull.Value)
                    return Convert.ToDecimal(dt.Rows[0]["Deposit_Applied_Amount"]);
            }
            catch { /* fallback to 0 */ }
            return 0m;
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
            string receiptNumber = p.ContainsKey("receiptNumber") ? p["receiptNumber"]?.ToString() : null;

            try
            {
                if (_config.IsReceiptDocumentMode)
                {
                    // DOCUMENT mode: ใบเสร็จถูกออกผ่าน /api/integration/invoices ซึ่ง auto-creates journal
                    // VoidDocumentAsync เรียก /document/{id}/void → ต้อง JWT auth (ไม่มีใน Integration Key)
                    // → ใช้ credit note (/api/integration/credit-notes) แทน — ออกใบลดหนี้ที่กลับ Revenue + VAT
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        var info = LookupReceiptHeaderInfo(receiptNumber);
                        decimal totalAmount = LookupReceiptAmount(receiptNumber);
                        bool hasVat = LookupBusinessHasVat();
                        int resId = info?.reservationId ?? LookupReservationIdByReceipt(receiptNumber);
                        string custName = info?.customerName ?? "";

                        if (totalAmount > 0)
                        {
                            var creditNote = _mapper.MapReceiptVoidToCreditNote(
                                resId, receiptNumber, totalAmount, hasVat, custName, DateTime.Now, reason);
                            var cnResult = await _apiClient.CreateIntegrationCreditNoteAsync(creditNote);
                            Guid cnId = RequireValidDocId(cnResult?.data?.Id, $"CreditNote (void receipt) receipt={receiptNumber}");

                            // ถ้าใบเสร็จเดิมมี deposit applied → ต้อง restore ADVANCE_DEPOSIT
                            // (DOCUMENT mode flow มี separate adjustment journal ที่ต้อง undo)
                            decimal applied = LookupDepositAppliedFromReceipt(receiptNumber);
                            if (applied > 0 && info != null)
                            {
                                // ห้ามกลืน exception — ถ้า counter-adj fail แล้วมาร์ค COMPLETED
                                // ADVANCE_DEPOSIT จะไม่ถูก restore → phantom liability ค้างถาวร
                                // re-throw → queue retry (credit note idempotent ด้วย ExternalRef ไม่สร้างซ้ำ)
                                var counterAdj = _mapper.MapDepositAppliedAdjustmentReverse(
                                    resId, applied, info.Value.paymentMethod, DateTime.Now,
                                    custName, info.Value.paymentAccountId, receiptNumber);
                                var counterResult = await _apiClient.CreateJournalAsync(counterAdj);
                                RequireValidDocId(counterResult?.data?.Id, $"VoidReceipt counter-adj receipt={receiptNumber}");
                                _code.Logs(_connectionString, "AccountingSync",
                                    $"ProcessVoidReceipt: counter-adj for depositApplied={applied} on receipt={receiptNumber}",
                                    "SYSTEM");
                            }

                            return $"CREDIT_NOTE:{cnId}";
                        }
                    }

                    // ไม่มี receiptNumber/totalAmount → log warning, ไม่ throw (caller ลบในระบบไปแล้ว)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidReceipt(DOCUMENT): missing receiptNumber/total for nexaaccId={nexaaccId} — manual review required",
                        "SYSTEM");
                    return $"VOID_SKIPPED:{nexaaccId} (manual review needed)";
                }
                else
                {
                    // JOURNAL mode: ใบเสร็จเป็น compound journal ที่รวม deposit-debit อยู่แล้ว
                    // → reverse เพียงพอ (NextAcc auto-flip DR↔CR ทุก line รวมทั้ง Advance Deposit)
                    // ห้ามโพสต์ counter-adjustment เพิ่ม เพราะจะทำให้เกิด phantom liability
                    try
                    {
                        var reverseReq = new ReverseJournalEntryRequest
                        {
                            ReversalDate = DateTime.Now,
                            Description = reason ?? $"กลับรายการใบเสร็จ {receiptNumber}"
                        };
                        var reverseResult = await _apiClient.ReverseJournalAsync(docId, reverseReq);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(JOURNAL): reversed {nexaaccId} → {reverseResult.data?.Id}",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} → {reverseResult.data?.Id}";
                    }
                    catch (AccountingApiException reverseEx) when (IsAlreadyVoided(reverseEx))
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(JOURNAL): {nexaaccId} already reversed in NextAcc — treating as success",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} (already)";
                    }
                }
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidReceipt: nexaaccId={nexaaccId} already voided/reversed — treating as success",
                    "SYSTEM");
                return $"VOIDED:{nexaaccId} (already voided)";
            }
        }

        private async Task<string> ProcessVoidVoucher(Dictionary<string, object> p)
        {
            string nexaaccId = p["nexaaccId"]?.ToString();
            if (string.IsNullOrEmpty(nexaaccId))
                throw new ArgumentException("Cannot void voucher: nexaaccId is missing");

            Guid docId = Guid.Parse(nexaaccId);
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : null;
            string documentNumber = p.ContainsKey("documentNumber") ? p["documentNumber"]?.ToString() : null;

            try
            {
                if (_config.IsVoucherDocumentMode)
                {
                    // DOCUMENT mode: voucher = expense doc + journal posted by NextAcc
                    // VoidDocumentAsync (JWT-only) ไม่ work — ใช้ /api/integration/debit-notes แทน
                    // (ใบเพิ่มหนี้ฝั่งซื้อ: CR Expense + CR VAT / DR A/P → กลับ expense + ลด BalanceDue)
                    if (!string.IsNullOrEmpty(documentNumber))
                    {
                        var supplier = LookupSupplierFromVoucherDoc(documentNumber);
                        decimal totalAmount = LookupVoucherAmount(documentNumber);
                        bool hasVat = LookupBusinessHasVat();

                        if (totalAmount > 0)
                        {
                            var debitNote = _mapper.MapVoucherVoidToDebitNote(
                                supplier?.voucherId ?? 0, documentNumber, totalAmount, hasVat,
                                supplier?.supplierName ?? "", DateTime.Now, reason);
                            var dnResult = await _apiClient.CreateIntegrationDebitNoteAsync(debitNote);
                            Guid dnId = RequireValidDocId(dnResult?.data?.Id, $"DebitNote (void voucher) doc={documentNumber}");
                            _code.Logs(_connectionString, "AccountingSync",
                                $"ProcessVoidVoucher(DOCUMENT): debit note created for voucher {documentNumber} → {dnId}",
                                "SYSTEM");
                            return $"DEBIT_NOTE:{dnId}";
                        }
                    }

                    // ไม่มี documentNumber/total → log warning
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidVoucher(DOCUMENT): missing documentNumber/total for nexaaccId={nexaaccId} — manual review",
                        "SYSTEM");
                    return $"VOID_SKIPPED:{nexaaccId} (DOCUMENT mode — manual review needed)";
                }
                else
                {
                    try
                    {
                        var reverseReq = new ReverseJournalEntryRequest
                        {
                            ReversalDate = DateTime.Now,
                            Description = reason ?? "กลับรายการใบสำคัญจ่าย"
                        };
                        var reverseResult = await _apiClient.ReverseJournalAsync(docId, reverseReq);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher(JOURNAL): reversed {nexaaccId} → {reverseResult.data?.Id}",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} → {reverseResult.data?.Id}";
                    }
                    catch (AccountingApiException reverseEx) when (IsAlreadyVoided(reverseEx))
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher(JOURNAL): {nexaaccId} already reversed — treating as success",
                            "SYSTEM");
                        return $"REVERSED:{nexaaccId} (already)";
                    }
                }
            }
            catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessVoidVoucher: nexaaccId={nexaaccId} already voided — treating as success",
                    "SYSTEM");
                return $"VOIDED:{nexaaccId} (already voided)";
            }
        }

        /// <summary>
        /// Auto-generate WHT certificate in NextAcc after creating an expense document with WHT.
        /// Non-critical — logs error and continues if NextAcc doesn't support or fails.
        /// </summary>
        private async Task TryAutoGenerateWhtCertAsync(Guid documentId, string documentNumber)
        {
            // WHT cert endpoint ({company}/withholding-tax-certs/*) ต้องใช้ API Key (acc_)
            // ข้ามเมื่อใช้ Integration Key (int_) — กัน 401 ซ้ำ
            if (_config.IsIntegrationKey)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"WHT cert auto-generate ข้าม doc={documentNumber}: ระบบใช้ Integration Key (int_) " +
                    "ซึ่งใช้กับ WHT endpoint ไม่ได้ — ต้องใช้ API Key (acc_) แยก", "SYSTEM");
                return;
            }
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

            // RE-COMPUTE toClear ณ เวลา process (ไม่ใช้ payload เพียงอย่างเดียว):
            //   payload depositAmount ถูกคำนวณตอน checkout (sync) — ถ้าใบเสร็จที่หักมัดจำยังไม่ถูก
            //   queue ประมวลผล Deposit_Applied_Amount จะยังเป็น 0 → payload สูงเกิน → double-clear
            //   ที่เวลานี้ queue FIFO ได้ประมวลใบเสร็จก่อนหน้าแล้ว → Deposit_Applied_Amount เป็นปัจจุบัน
            //   toClear = มัดจำที่จ่ายจริง − มัดจำที่ถูกหักในใบเสร็จไปแล้ว
            decimal actualDeposit = LookupActualDepositPaid(reservationId);
            decimal alreadyApplied = LookupDepositAppliedForReservation(reservationId);
            decimal recomputedToClear = actualDeposit - alreadyApplied;

            if (recomputedToClear < depositAmount)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositClearing: payload={depositAmount} แต่ recomputed toClear={recomputedToClear} " +
                    $"(มัดจำจ่ายจริง {actualDeposit} − หักในใบเสร็จแล้ว {alreadyApplied}) — ใช้ค่า recomputed", "SYSTEM");
                depositAmount = recomputedToClear;
            }
            if (depositAmount <= 0.01m)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositClearing: ไม่มียอดที่ต้อง clear (ถูกตัดในใบเสร็จไปแล้ว) — skip reservation #{reservationId}", "SYSTEM");
                return "SKIPPED_NO_BALANCE";
            }

            bool hasVat = LookupBusinessHasVat();

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessDepositClearing: ref={reservationRef} resId={reservationId} deposit={depositAmount} damage={damageAmount} vat={hasVat}", "SYSTEM");

            var journal = _mapper.MapCheckoutToJournal(reservationId, depositAmount, customerName, checkoutDate, damageAmount, reservationRef, hasVat, _config.IsDepositVatAtReceipt);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid clearId = RequireValidDocId(result?.data?.Id, $"DepositClearing resId={reservationId}");
            await SafePostJournalAsync(clearId);
            return clearId.ToString();
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
            Guid refundId = RequireValidDocId(result?.data?.Id, $"DepositRefund resId={reservationId}");
            await SafePostJournalAsync(refundId);
            return refundId.ToString();
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
            Guid forfeitId = RequireValidDocId(result?.data?.Id, $"DepositForfeit resId={reservationId}");
            await SafePostJournalAsync(forfeitId);
            return forfeitId.ToString();
        }

        // ──────────────────────────────────────────────
        // Stock / Inventory Processors
        // ──────────────────────────────────────────────

        private async Task<string> ProcessStockIn(Dictionary<string, object> p)
        {
            int productId = Convert.ToInt32(p["productId"]);
            string productName = p.ContainsKey("productName") ? p["productName"]?.ToString() : "";
            decimal quantity = Convert.ToDecimal(p["quantity"]);
            decimal costPerUnit = Convert.ToDecimal(p["costPerUnit"]);
            decimal totalCost = p.ContainsKey("totalCost") ? Convert.ToDecimal(p["totalCost"]) : Math.Round(quantity * costPerUnit, 2);
            DateTime receiveDate = DateTime.Parse(p["receiveDate"]?.ToString());
            string supplierName = p.ContainsKey("supplierName") ? p["supplierName"]?.ToString() : "";
            string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : null;
            bool hasInputVat = p.ContainsKey("hasInputVat") && Convert.ToBoolean(p["hasInputVat"]);

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessStockIn: product={productName} qty={quantity} cost={costPerUnit} total={totalCost} supplier={supplierName}", "SYSTEM");

            var journal = _mapper.MapStockInToJournal(productId, productName, totalCost, receiveDate, supplierName,
                string.IsNullOrEmpty(paymentMethod) ? null : paymentMethod, hasInputVat);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"StockIn product={productId}");
            await SafePostJournalAsync(docId);
            return docId.ToString();
        }

        private async Task<string> ProcessStockOutCogs(Dictionary<string, object> p)
        {
            int productId = Convert.ToInt32(p["productId"]);
            string productName = p.ContainsKey("productName") ? p["productName"]?.ToString() : "";
            decimal quantity = Convert.ToDecimal(p["quantity"]);
            decimal costPerUnit = Convert.ToDecimal(p["costPerUnit"]);
            DateTime outDate = DateTime.Parse(p["outDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : "ขาย";
            string stockRef = p.ContainsKey("stockRef") ? p["stockRef"]?.ToString() : null;

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessStockOutCogs: product={productName} qty={quantity} cost={costPerUnit} reason={reason}", "SYSTEM");

            var journal = _mapper.MapStockOutCogsToJournal(productId, productName, quantity, costPerUnit, outDate, reason, stockRef);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"StockOutCogs product={productId} ref={stockRef}");
            await SafePostJournalAsync(docId);
            return docId.ToString();
        }

        private async Task<string> ProcessStockOutCogsReversal(Dictionary<string, object> p)
        {
            int productId = Convert.ToInt32(p["productId"]);
            string productName = p.ContainsKey("productName") ? p["productName"]?.ToString() : "";
            decimal quantity = Convert.ToDecimal(p["quantity"]);
            decimal costPerUnit = Convert.ToDecimal(p["costPerUnit"]);
            DateTime reverseDate = DateTime.Parse(p["reverseDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : "ยกเลิก room charge";
            string stockRef = p.ContainsKey("stockRef") ? p["stockRef"]?.ToString() : null;

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessStockOutCogsReversal: product={productName} qty={quantity} cost={costPerUnit} ref={stockRef}", "SYSTEM");

            var journal = _mapper.MapStockOutCogsReversalToJournal(productId, productName, quantity, costPerUnit, reverseDate, reason, stockRef);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"StockOutCogsReversal ref={stockRef}");
            await SafePostJournalAsync(docId);
            return docId.ToString();
        }

        private async Task<string> ProcessStockAdjustment(Dictionary<string, object> p)
        {
            long adjustmentLogId = Convert.ToInt64(p["adjustmentLogId"]);
            int productId = Convert.ToInt32(p["productId"]);
            string productName = p.ContainsKey("productName") ? p["productName"]?.ToString() : "";
            decimal quantityDiff = Convert.ToDecimal(p["quantityDiff"]);
            decimal costPerUnit = Convert.ToDecimal(p["costPerUnit"]);
            DateTime adjustDate = DateTime.Parse(p["adjustDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : "";

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessStockAdjustment: logId={adjustmentLogId} product={productName} diff={quantityDiff} cost={costPerUnit}", "SYSTEM");

            var journal = _mapper.MapStockAdjustmentToJournal(productId, productName, quantityDiff, costPerUnit, adjustDate, reason, adjustmentLogId);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"StockAdjustment logId={adjustmentLogId}");
            await SafePostJournalAsync(docId);
            UpdateStockAdjustmentLog(adjustmentLogId, "SYNCED", docId, null);
            return docId.ToString();
        }

        private async Task<string> ProcessStockWriteOff(Dictionary<string, object> p)
        {
            long adjustmentLogId = Convert.ToInt64(p["adjustmentLogId"]);
            int productId = Convert.ToInt32(p["productId"]);
            string productName = p.ContainsKey("productName") ? p["productName"]?.ToString() : "";
            decimal quantity = Convert.ToDecimal(p["quantity"]);
            decimal costPerUnit = Convert.ToDecimal(p["costPerUnit"]);
            DateTime writeOffDate = DateTime.Parse(p["writeOffDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : "เสียหาย";

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessStockWriteOff: logId={adjustmentLogId} product={productName} qty={quantity} reason={reason}", "SYSTEM");

            var journal = _mapper.MapStockWriteOffToJournal(productId, productName, quantity, costPerUnit, writeOffDate, reason, adjustmentLogId);
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid docId = RequireValidDocId(result?.data?.Id, $"StockWriteOff logId={adjustmentLogId}");
            await SafePostJournalAsync(docId);
            UpdateStockAdjustmentLog(adjustmentLogId, "SYNCED", docId, null);
            return docId.ToString();
        }

        private async Task<string> ProcessProductSync(Dictionary<string, object> p)
        {
            int productId = Convert.ToInt32(p["productId"]);
            var info = LookupProductInfo(productId);
            if (info == null)
                throw new ArgumentException($"ProcessProductSync: ไม่พบ product #{productId} ในฐานข้อมูล");

            // ใช้ /api/integration/products (X-Integration-Key) — upsert by Code อัตโนมัติ
            // แทน CreateProductAsync/UpdateProductAsync ที่เป็น JWT-only endpoint
            var req = _mapper.MapProductToIntegration(productId, info.Value.name, info.Value.description,
                info.Value.sellPrice, info.Value.costPrice, info.Value.unit, info.Value.categoryName);

            var result = await _apiClient.SyncIntegrationProductAsync(req);
            // Integration product response อาจไม่ส่ง Guid ID (return Code แทน) — fallback to cached if needed
            Guid? existingNexaaccId = LookupCachedNexaaccProductId(productId);
            Guid productGuid;
            if (result?.data?.Id != null && result.data.Id != Guid.Empty)
                productGuid = result.data.Id;
            else if (existingNexaaccId.HasValue && existingNexaaccId.Value != Guid.Empty)
                productGuid = existingNexaaccId.Value;
            else
                productGuid = Guid.Empty; // upsert succeeded but no Guid yet — Code ใช้ lookup ครั้งถัดไป

            UpsertProductMap(productId, productGuid, info.Value.name, $"TT-{productId:D5}", "SYNCED", null);
            return productGuid == Guid.Empty ? $"SYNCED:{req.Code}" : productGuid.ToString();
        }

        private (string name, string description, decimal sellPrice, decimal costPrice, string unit, string categoryName)? LookupProductInfo(int productId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 P.Product_Name, ISNULL(P.Detail, '') AS Detail,
                             ISNULL(P.Sell_Price, 0) AS Sell_Price,
                             ISNULL(P.Cost_Price, 0) AS Cost_Price,
                             ISNULL(P.Unit, '') AS Unit,
                             (SELECT TOP 1 Category_Name FROM Product_Category WHERE ID = P.Category_ID) AS CategoryName
                      FROM Product P WHERE P.ID = @id",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt?.Rows.Count > 0)
                {
                    var r = dt.Rows[0];
                    return (
                        r["Product_Name"]?.ToString() ?? "",
                        r["Detail"]?.ToString() ?? "",
                        Convert.ToDecimal(r["Sell_Price"]),
                        Convert.ToDecimal(r["Cost_Price"]),
                        r["Unit"]?.ToString() ?? "ชิ้น",
                        r["CategoryName"]?.ToString() ?? ""
                    );
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupProductInfo failed productId={productId}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        private Guid? LookupCachedNexaaccProductId(int productId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Product_Id FROM Accounting_Product_Map WHERE TakeTime_Product_ID = @id AND Sync_Status = 'SYNCED'",
                    new Dictionary<string, object> { { "@id", productId } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_Product_Id"] != DBNull.Value)
                    return (Guid)dt.Rows[0]["Nexaacc_Product_Id"];
            }
            catch { }
            return null;
        }

        private void UpsertProductMap(int productId, Guid? nexaaccId, string name, string code, string status, string error)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"IF EXISTS (SELECT 1 FROM Accounting_Product_Map WHERE TakeTime_Product_ID = @id)
                        UPDATE Accounting_Product_Map
                        SET Nexaacc_Product_Id = @cid, Product_Code = @code, Product_Name = @name,
                            Last_Synced = GETDATE(), Sync_Status = @status, Sync_Error = @err,
                            Updated_Date = GETDATE()
                        WHERE TakeTime_Product_ID = @id
                      ELSE
                        INSERT INTO Accounting_Product_Map
                        (TakeTime_Product_ID, Nexaacc_Product_Id, Product_Code, Product_Name,
                         Last_Synced, Sync_Status, Sync_Error)
                        VALUES (@id, @cid, @code, @name, GETDATE(), @status, @err)",
                    new Dictionary<string, object>
                    {
                        { "@id", productId },
                        { "@cid", (object)nexaaccId ?? DBNull.Value },
                        { "@code", (object)code ?? DBNull.Value },
                        { "@name", (object)name ?? DBNull.Value },
                        { "@status", status },
                        { "@err", (object)error ?? DBNull.Value }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"UpsertProductMap failed productId={productId}: {ex.Message}", "SYSTEM");
            }
        }

        private void UpdateStockAdjustmentLog(long logId, string status, Guid? journalId, string error)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"UPDATE Stock_Adjustment_Log
                      SET Sync_Status = @status, Nexaacc_Journal_Id = @jid
                      WHERE ID = @id",
                    new Dictionary<string, object>
                    {
                        { "@id", logId },
                        { "@status", status },
                        { "@jid", (object)journalId ?? DBNull.Value }
                    });
            }
            catch { }
        }

        /// <summary>
        /// หาจำนวนมัดจำที่ลูกค้าจ่ายไปทั้งหมด — JOIN Account_Receipt (IsDeposit=1, Status='Normal') กับ Payment_History
        /// ใช้เป็น truth source ของยอดมัดจำ (กันการบันทึกผิด)
        /// </summary>
        private decimal LookupActualDepositPaid(int reservationId)
        {
            try
            {
                // Status filter ใช้ 'Normal'/NULL ให้ตรงกับ query มัดจำอื่นทั้งหมด
                // (GetReservationData, LookupDepositAppliedForReservation ฯลฯ) — กัน mismatch
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Total_Amount), 0) AS TotalDeposit
                      FROM Account_Receipt
                      WHERE Reservation_ID = @resId
                        AND IsDeposit = 1
                        AND (Status = 'Normal' OR Status IS NULL)",
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

        /// <summary>มัดจำที่ถูกหักในใบเสร็จไปแล้ว (sum Deposit_Applied_Amount จากใบเสร็จที่ไม่ใช่มัดจำ)</summary>
        private decimal LookupDepositAppliedForReservation(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ISNULL(SUM(Deposit_Applied_Amount), 0) AS Applied
                      FROM Account_Receipt
                      WHERE Reservation_ID = @resId
                        AND IsDeposit = 0
                        AND (Status = 'Normal' OR Status IS NULL)",
                    new Dictionary<string, object> { { "@resId", reservationId } });
                if (dt?.Rows.Count > 0)
                    return Convert.ToDecimal(dt.Rows[0]["Applied"]);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupDepositAppliedForReservation failed for resId={reservationId}: {ex.Message}", "SYSTEM");
            }
            return 0m;
        }

        private bool LookupBusinessHasVat()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                if (dt?.Rows.Count > 0)
                    return dt.Rows[0]["Use_Vat"]?.ToString() == "True";
            }
            catch { }
            return false;
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

            // E-Tax endpoint ({company}/etax/*) ต้องใช้ API Key (acc_) — ใช้กับ Integration Key (int_) ไม่ได้
            // ข้ามไปเพื่อไม่ให้เกิด 401 ซ้ำทุกใบเสร็จ (core sync ไม่กระทบ)
            if (_config.IsIntegrationKey)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax auto-generate ข้าม receipt={receiptNumber}: ระบบใช้ Integration Key (int_) " +
                    "ซึ่งใช้กับ E-Tax endpoint ไม่ได้ — ต้องใช้ API Key (acc_) แยก หรือสร้าง E-Tax เองในหน้าจัดการ", "SYSTEM");
                return;
            }

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

        // ──────────────────────────────────────────────
        // Contact / Customer upsert ใน NextAcc
        // ──────────────────────────────────────────────

        /// <summary>ข้อมูลลูกค้าสำหรับ map เข้า NextAcc invoice (CustomerExternalId, Name, TaxId, ฯลฯ)</summary>
        public class ContactInfo
        {
            public string ExternalId { get; set; }
            public string Name { get; set; }
            public string TaxId { get; set; }
            public string Email { get; set; }
            public string Phone { get; set; }
            public string Address { get; set; }
            public Guid? NexaaccContactId { get; set; }
        }

        /// <summary>
        /// ดึงข้อมูลลูกค้าจาก Reservation → Customer table.
        /// ใช้ MobilePhone เป็น External ID (natural key) ของ NextAcc contact.
        /// </summary>
        private ContactInfo LookupCustomerFromReservation(int reservationId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1
                         C.MobilePhone, ISNULL(C.FullName, C.Name) AS Name,
                         C.TaxID, C.Email, C.Address
                      FROM Reservation R
                      LEFT JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
                      WHERE R.ID = @id",
                    new Dictionary<string, object> { { "@id", reservationId } });
                if (dt?.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    string phone = row["MobilePhone"]?.ToString();
                    if (string.IsNullOrEmpty(phone)) return null;
                    return new ContactInfo
                    {
                        ExternalId = phone,
                        Name = row["Name"]?.ToString() ?? phone,
                        TaxId = row["TaxID"]?.ToString(),
                        Email = row["Email"]?.ToString(),
                        Phone = phone,
                        Address = row["Address"]?.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupCustomerFromReservation failed for resId={reservationId}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>
        /// ตรวจ cache ใน Accounting_Contact_Map ก่อน — ถ้ามี Nexaacc_Contact_Id อยู่แล้ว ส่งคืน
        /// ถ้าไม่มี/Sync เก่ากว่า 30 วัน → upsert ผ่าน CreateIntegrationCustomerAsync
        /// (NextAcc ใช้ ExternalId เป็น natural key — ส่งซ้ำได้โดย idempotent)
        /// </summary>
        private async Task<ContactInfo> EnsureCustomerContactAsync(int reservationId)
        {
            var info = LookupCustomerFromReservation(reservationId);
            if (info == null || string.IsNullOrEmpty(info.ExternalId))
            {
                // Walk-in / no phone — สร้าง synthetic ExternalId จาก Reservation_ID
                // เพื่อให้ NextAcc มี contact entity ทุก invoice (อย่างน้อยผูกกับการจอง)
                info = new ContactInfo
                {
                    ExternalId = $"RES-{reservationId}",
                    Name = LookupGuestName(reservationId)
                };
                if (string.IsNullOrEmpty(info.Name))
                    info.Name = $"Walk-in #{reservationId}";
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureCustomerContactAsync: walk-in fallback for resId={reservationId} → ExternalId={info.ExternalId}",
                    "SYSTEM");
            }

            // Try cache
            try
            {
                var cached = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Contact_Id, Last_Synced FROM Accounting_Contact_Map
                      WHERE External_Id = @ext AND Contact_Type = 'CUSTOMER' AND Sync_Status = 'SYNCED'",
                    new Dictionary<string, object> { { "@ext", info.ExternalId } });
                if (cached?.Rows.Count > 0)
                {
                    DateTime lastSync = cached.Rows[0]["Last_Synced"] != DBNull.Value
                        ? Convert.ToDateTime(cached.Rows[0]["Last_Synced"]) : DateTime.MinValue;
                    if (cached.Rows[0]["Nexaacc_Contact_Id"] != DBNull.Value
                        && (DateTime.Now - lastSync).TotalDays < 30)
                    {
                        info.NexaaccContactId = (Guid)cached.Rows[0]["Nexaacc_Contact_Id"];
                        return info;
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureCustomerContactAsync cache lookup failed: {ex.Message}", "SYSTEM");
            }

            // Upsert via NextAcc API
            try
            {
                var req = new InboundCustomerRequest
                {
                    ExternalId = info.ExternalId,
                    Name = info.Name,
                    TaxId = info.TaxId,
                    Email = info.Email,
                    Phone = info.Phone,
                    Address = info.Address,
                    IsCustomer = true,
                    IsSupplier = false,
                    ContactType = "INDIVIDUAL"
                };
                var resp = await _apiClient.CreateIntegrationCustomerAsync(req);
                if (resp?.data != null && resp.data.Id != Guid.Empty)
                {
                    info.NexaaccContactId = resp.data.Id;
                    UpsertContactMap(info, "CUSTOMER", "SYNCED", null);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnsureCustomerContactAsync: upserted {info.Name} ({info.ExternalId}) → {info.NexaaccContactId}",
                        "SYSTEM");
                }
                else
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnsureCustomerContactAsync: API returned empty Id for {info.ExternalId} — invoice will fall back to ExternalId+Name",
                        "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureCustomerContactAsync upsert failed for {info.ExternalId}: {ex.Message}", "SYSTEM");
                UpsertContactMap(info, "CUSTOMER", "FAILED", ex.Message);
                // ไม่ throw — invoice ยังส่งได้โดยใช้ ExternalId+Name+TaxId
            }

            return info;
        }

        /// <summary>Upsert/Insert Accounting_Contact_Map entry (cache table)</summary>
        private void UpsertContactMap(ContactInfo info, string contactType, string status, string error)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"IF EXISTS (SELECT 1 FROM Accounting_Contact_Map WHERE External_Id = @ext AND Contact_Type = @type)
                        UPDATE Accounting_Contact_Map
                        SET Nexaacc_Contact_Id = @cid, Name = @name, Tax_Id = @taxId,
                            Email = @email, Phone = @phone, Address = @addr,
                            Last_Synced = GETDATE(), Sync_Status = @status, Sync_Error = @err,
                            Updated_Date = GETDATE()
                        WHERE External_Id = @ext AND Contact_Type = @type
                      ELSE
                        INSERT INTO Accounting_Contact_Map
                        (External_Id, Contact_Type, Nexaacc_Contact_Id, Name, Tax_Id, Email, Phone, Address,
                         Last_Synced, Sync_Status, Sync_Error)
                        VALUES (@ext, @type, @cid, @name, @taxId, @email, @phone, @addr,
                                GETDATE(), @status, @err)",
                    new Dictionary<string, object>
                    {
                        { "@ext", info.ExternalId },
                        { "@type", contactType },
                        { "@cid", (object)info.NexaaccContactId ?? DBNull.Value },
                        { "@name", (object)info.Name ?? DBNull.Value },
                        { "@taxId", (object)info.TaxId ?? DBNull.Value },
                        { "@email", (object)info.Email ?? DBNull.Value },
                        { "@phone", (object)info.Phone ?? DBNull.Value },
                        { "@addr", (object)info.Address ?? DBNull.Value },
                        { "@status", status },
                        { "@err", (object)error ?? DBNull.Value }
                    });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"UpsertContactMap failed: {ex.Message}", "SYSTEM");
            }
        }

        /// <summary>Apply ContactInfo to invoice: populate CustomerExternalId/Name/TaxId fields</summary>
        private static void ApplyContactToInvoice(CreateIntegrationInvoiceRequest invoice, ContactInfo info)
        {
            if (invoice == null || info == null) return;
            invoice.CustomerExternalId = info.ExternalId;
            if (string.IsNullOrEmpty(invoice.CustomerName)) invoice.CustomerName = info.Name;
            if (!string.IsNullOrEmpty(info.TaxId)) invoice.CustomerTaxId = info.TaxId;
        }

        /// <summary>Apply ContactInfo to expense (supplier): populate SupplierExternalId/Name/TaxId fields</summary>
        private static void ApplyContactToExpense(CreateIntegrationExpenseRequest expense, ContactInfo info)
        {
            if (expense == null || info == null) return;
            expense.SupplierExternalId = info.ExternalId;
            if (string.IsNullOrEmpty(expense.SupplierName)) expense.SupplierName = info.Name;
            if (!string.IsNullOrEmpty(info.TaxId)) expense.SupplierTaxId = info.TaxId;
        }

        /// <summary>
        /// Lookup supplier (Vendor) จาก Payment_Voucher.Vendor_ID — ใช้ Vendor.ID เป็น External ID
        /// </summary>
        private ContactInfo LookupSupplierFromVoucher(int voucherId, string fallbackName)
        {
            try
            {
                // Vendor schema: ID, Name, IDNumber (Tax), Address, Vendor_Group, Status (no MobilePhone/Email columns)
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 V.ID, V.Name, V.IDNumber AS TaxId, V.Address
                      FROM Payment_Voucher PV
                      LEFT JOIN Vendor V ON V.ID = PV.Vendor_ID
                      WHERE PV.ID = @id",
                    new Dictionary<string, object> { { "@id", voucherId } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["ID"] != DBNull.Value)
                {
                    var row = dt.Rows[0];
                    return new ContactInfo
                    {
                        ExternalId = "VENDOR-" + row["ID"].ToString(),
                        Name = row["Name"]?.ToString() ?? fallbackName,
                        TaxId = row["TaxId"]?.ToString(),
                        Address = row["Address"]?.ToString()
                    };
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupSupplierFromVoucher failed for voucherId={voucherId}: {ex.Message}", "SYSTEM");
            }
            // Fallback: ใช้ payeeName เป็น ExternalId เพื่อ idempotency
            if (!string.IsNullOrEmpty(fallbackName))
            {
                return new ContactInfo
                {
                    ExternalId = "PAYEE-" + fallbackName.GetHashCode().ToString("X"),
                    Name = fallbackName
                };
            }
            return null;
        }

        /// <summary>Cache + upsert supplier ใน NextAcc (เหมือน customer แต่ IsSupplier=true)</summary>
        private async Task<ContactInfo> EnsureSupplierContactAsync(int voucherId, string payeeName)
        {
            var info = LookupSupplierFromVoucher(voucherId, payeeName);
            return await EnsureSupplierContactAsync(info);
        }

        /// <summary>
        /// Overload that upserts an explicitly-built supplier ContactInfo. Used by the
        /// Account_Payment-based PaymentVoucher flow where voucherId is 0 and the vendor's
        /// ExternalId/TaxId are supplied directly through the queue payload (the voucherId
        /// lookup queries the legacy Payment_Voucher table and can't resolve this flow).
        /// </summary>
        private async Task<ContactInfo> EnsureSupplierContactAsync(ContactInfo info)
        {
            if (info == null || string.IsNullOrEmpty(info.ExternalId)) return null;

            try
            {
                var cached = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Contact_Id, Last_Synced FROM Accounting_Contact_Map
                      WHERE External_Id = @ext AND Contact_Type = 'SUPPLIER' AND Sync_Status = 'SYNCED'",
                    new Dictionary<string, object> { { "@ext", info.ExternalId } });
                if (cached?.Rows.Count > 0)
                {
                    DateTime lastSync = cached.Rows[0]["Last_Synced"] != DBNull.Value
                        ? Convert.ToDateTime(cached.Rows[0]["Last_Synced"]) : DateTime.MinValue;
                    if (cached.Rows[0]["Nexaacc_Contact_Id"] != DBNull.Value
                        && (DateTime.Now - lastSync).TotalDays < 30)
                    {
                        info.NexaaccContactId = (Guid)cached.Rows[0]["Nexaacc_Contact_Id"];
                        return info;
                    }
                }
            }
            catch { }

            try
            {
                var req = new InboundCustomerRequest
                {
                    ExternalId = info.ExternalId,
                    Name = info.Name,
                    TaxId = info.TaxId,
                    Email = info.Email,
                    Phone = info.Phone,
                    Address = info.Address,
                    IsCustomer = false,
                    IsSupplier = true,
                    ContactType = !string.IsNullOrEmpty(info.TaxId) && info.TaxId.Length == 13 ? "INDIVIDUAL" : "COMPANY"
                };
                var resp = await _apiClient.CreateIntegrationCustomerAsync(req);
                if (resp?.data != null && resp.data.Id != Guid.Empty)
                {
                    info.NexaaccContactId = resp.data.Id;
                    UpsertContactMap(info, "SUPPLIER", "SYNCED", null);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnsureSupplierContactAsync: upserted {info.Name} ({info.ExternalId}) → {info.NexaaccContactId}",
                        "SYSTEM");
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureSupplierContactAsync upsert failed: {ex.Message}", "SYSTEM");
                UpsertContactMap(info, "SUPPLIER", "FAILED", ex.Message);
            }

            return info;
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

            // INSERT + SELECT SCOPE_IDENTITY() ในคำสั่งเดียว — race-free
            // (เดิมใช้ MAX(ID) WHERE entity/action ซึ่งคืน ID ผิดถ้า enqueue พร้อมกัน 2 รายการ
            //  ที่มี Entity_Type/Entity_ID/Action_Type เหมือนกัน — เช่น 2 ใบเสร็จของการจองเดียวกัน)
            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                @"INSERT INTO Accounting_Sync_Queue
                  (Entity_Type, Entity_ID, Action_Type, Payload, Status, Retry_Count, Max_Retries, Created_Date)
                  VALUES (@entityType, @entityId, @actionType, @payload, 'PENDING', 0, @maxRetries, GETDATE());
                  SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS NewID;",
                parameters);

            return dt != null && dt.Rows.Count > 0 && dt.Rows[0]["NewID"] != DBNull.Value
                ? Convert.ToInt64(dt.Rows[0]["NewID"]) : -1;
        }

        private void UpdateQueueStatus(long queueId, string status, string errorMessage, string nexaaccResponseId,
            string documentNumber = null, string documentType = null)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@id", queueId },
                { "@status", status },
                { "@error", (object)errorMessage ?? DBNull.Value },
                { "@nexaaccId", (object)nexaaccResponseId ?? DBNull.Value },
                { "@processedDate", status == "COMPLETED" ? (object)DateTime.Now : DBNull.Value },
                { "@docNumber", (object)documentNumber ?? DBNull.Value },
                { "@docType", (object)documentType ?? DBNull.Value }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Accounting_Sync_Queue
                  SET Status = @status, Error_Message = @error,
                      Nexaacc_Response_Id = @nexaaccId, Processed_Date = @processedDate,
                      Nexaacc_Document_Number = COALESCE(@docNumber, Nexaacc_Document_Number),
                      Nexaacc_Document_Type = COALESCE(@docType, Nexaacc_Document_Type)
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
                Base64Content = Convert.ToBase64String(bytes),
                FilePath = fi.FullName
            };
        }

        private static bool IsImageOrPdf(string extension)
        {
            string ext = (extension ?? "").ToLower();
            return ext == ".pdf" || ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp";
        }

        private List<IntegrationAttachment> LookupPayrollAttachments(string docNumber, DateTime payDate)
        {
            if (string.IsNullOrEmpty(docNumber)) return null;

            try
            {
                // Account_Payment.ID = document number string (e.g. "PAY-2024-0001")
                // Reuse LookupVoucherAttachments with voucherId=0 (skips Payment_Slips pattern,
                // uses directory scan + docNumber filename match)
                var attachments = LookupVoucherAttachments(0, docNumber, payDate);
                if (attachments != null)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"LookupPayrollAttachments: found {attachments.Count} file(s) via voucher lookup for doc={docNumber}",
                        "SYSTEM");
                    return attachments;
                }

                // Fallback: search Documents/Payment/{Year}/{Month}/ for files matching docNumber
                string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"]
                    ?? ConfigurationManager.AppSettings["BaseFolderPath"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    string yearMonth = $"{payDate.Year}/{payDate.Month}";
                    string paymentDir = Path.Combine(basePath, "Documents", "Payment", yearMonth);
                    if (Directory.Exists(paymentDir))
                    {
                        attachments = new List<IntegrationAttachment>();
                        foreach (var file in Directory.GetFiles(paymentDir))
                        {
                            var fi = new FileInfo(file);
                            if (fi.Name.Contains(docNumber) && fi.Length > 0
                                && fi.Length <= MaxAttachmentSize && IsImageOrPdf(fi.Extension))
                            {
                                attachments.Add(FileToAttachment(fi));
                                if (attachments.Count >= 5) break;
                            }
                        }
                        if (attachments.Count > 0)
                        {
                            _code.Logs(_connectionString, "AccountingSync",
                                $"LookupPayrollAttachments: found {attachments.Count} file(s) by docNumber match for doc={docNumber}",
                                "SYSTEM");
                            return attachments;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupPayrollAttachments: error for doc={docNumber}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        private static List<string> ExtractFilePaths(List<IntegrationAttachment> attachments)
        {
            if (attachments == null || attachments.Count == 0) return null;
            var paths = new List<string>();
            foreach (var a in attachments)
            {
                if (!string.IsNullOrEmpty(a.FilePath) && File.Exists(a.FilePath))
                    paths.Add(a.FilePath);
            }
            return paths.Count > 0 ? paths : null;
        }

        // ══════════════════════════════════════════════
        // Document Lookup — for UI display
        // ══════════════════════════════════════════════

        public DataTable GetSyncDocumentInfo(string takeTimeDocNumber)
        {
            return _code.DatabaseQuerySafe(_connectionString,
                @"SELECT TOP 1
                    q.ID AS QueueId, q.Entity_Type, q.Action_Type, q.Status,
                    q.Nexaacc_Response_Id, q.Nexaacc_Document_Number, q.Nexaacc_Document_Type,
                    q.Created_Date, q.Processed_Date, q.Error_Message, q.Retry_Count
                  FROM Accounting_Sync_Queue q
                  WHERE q.Status = 'COMPLETED'
                    AND q.Nexaacc_Response_Id IS NOT NULL
                    AND q.Nexaacc_Response_Id NOT LIKE 'SKIPPED%'
                    AND (q.Payload LIKE @pattern1 OR q.Payload LIKE @pattern2)
                  ORDER BY q.Processed_Date DESC",
                new Dictionary<string, object>
                {
                    { "@pattern1", $"%\"documentNumber\":\"{takeTimeDocNumber}\"%"},
                    { "@pattern2", $"%\"receiptNumber\":\"{takeTimeDocNumber}\"%"}
                });
        }

        public DataTable GetSyncQueueForDisplay(string statusFilter = null, int page = 1, int pageSize = 20)
        {
            string whereClause = "";
            var parms = new Dictionary<string, object>
            {
                { "@offset", (page - 1) * pageSize },
                { "@pageSize", pageSize }
            };
            if (!string.IsNullOrEmpty(statusFilter))
            {
                whereClause = "WHERE Status = @statusFilter";
                parms["@statusFilter"] = statusFilter;
            }

            return _code.DatabaseQuerySafe(_connectionString,
                $@"SELECT ID, Entity_Type, Entity_ID, Action_Type, Status,
                    Nexaacc_Response_Id, Nexaacc_Document_Number, Nexaacc_Document_Type,
                    Error_Message, Retry_Count, Max_Retries, Created_Date, Processed_Date
                   FROM Accounting_Sync_Queue {whereClause}
                   ORDER BY ID DESC
                   OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                parms);
        }

        public string BuildNexaaccDocumentUrl(string nexaaccResponseId, string documentType)
        {
            if (string.IsNullOrEmpty(nexaaccResponseId) || !_config.IsConfigured) return null;
            string baseUrl = _config.RawBaseUrl.TrimEnd('/');
            string companyId = _config.CompanyId.ToString();
            if (string.IsNullOrEmpty(documentType)) documentType = "JOURNAL";
            switch (documentType.ToUpper())
            {
                case "INVOICE": return $"{baseUrl}/{companyId}/invoices/{nexaaccResponseId}";
                case "EXPENSE": return $"{baseUrl}/{companyId}/expenses/{nexaaccResponseId}";
                case "JOURNAL": return $"{baseUrl}/{companyId}/journals/{nexaaccResponseId}";
                case "CREDIT_NOTE": return $"{baseUrl}/{companyId}/credit-notes/{nexaaccResponseId}";
                case "DEBIT_NOTE": return $"{baseUrl}/{companyId}/debit-notes/{nexaaccResponseId}";
                default: return $"{baseUrl}/{companyId}/documents/{nexaaccResponseId}";
            }
        }

        // ──────────────────────────────────────────────
        // Outbound: ดึงเอกสารฝั่งจ่ายจาก NextAcc + ไฟล์แนบ (ยกเว้นเงินเดือน)
        // ใช้ X-Api-Key (int_ key) → company endpoints ผ่าน ApiKeyMiddleware fallback
        // ──────────────────────────────────────────────

        /// <summary>DocumentType ฝั่งจ่ายที่ดึงมาแสดง (ตรงกับ enum ของ NextAcc)</summary>
        private static readonly Dictionary<string, string> PaymentDocTypeLabels = new Dictionary<string, string>
        {
            { "Expense", "ใบบันทึกค่าใช้จ่าย" },
            { "PaymentVoucher", "ใบสำคัญจ่าย" },
            { "PurchaseInvoice", "ใบแจ้งหนี้ซื้อ" },
            { "CertificateInLieu", "ใบรับรองแทนใบเสร็จ" }
        };

        /// <summary>
        /// ดึงเอกสารฝั่งจ่ายทั้งหมดที่ออกจาก NextAcc พร้อมไฟล์แนบ มาแสดงในระบบ TakeTime
        /// ยกเว้นเอกสารเงินเดือน (payroll) ตามที่กำหนด
        /// </summary>
        public async System.Threading.Tasks.Task<List<NextAccPaymentDoc>> FetchNextAccPaymentDocumentsAsync(
            DateTime fromDate, DateTime toDate, bool includeAttachments = true)
        {
            var result = new List<NextAccPaymentDoc>();
            if (!_config.IsConfigured || !_config.Enabled) return result;

            string baseUrl = _config.RawBaseUrl.TrimEnd('/');
            var seen = new HashSet<Guid>();

            foreach (var typeName in PaymentDocTypeLabels.Keys)
            {
                int page = 1;
                while (true)
                {
                    PagedResponse<OutboundDocumentResponse> resp;
                    try
                    {
                        resp = await _apiClient.GetIntegrationDocumentsAsync(new OutboundQueryParams
                        {
                            FromDate = fromDate,
                            ToDate = toDate,
                            Type = typeName,
                            Page = page,
                            PageSize = 50
                        });
                    }
                    catch (Exception ex)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"FetchNextAccPaymentDocuments: ดึง type={typeName} page={page} ล้มเหลว: {ex.Message}", "SYSTEM");
                        break;
                    }

                    if (resp?.Items == null || resp.Items.Count == 0) break;

                    foreach (var d in resp.Items)
                    {
                        if (d == null || seen.Contains(d.Id)) continue;
                        if (IsPayrollDocument(d)) continue; // ยกเว้นเงินเดือน
                        seen.Add(d.Id);

                        var doc = new NextAccPaymentDoc
                        {
                            Id = d.Id,
                            DocumentNumber = d.DocumentNumber,
                            DocumentType = d.DocumentType,
                            DocumentTypeLabel = PaymentDocTypeLabels.TryGetValue(d.DocumentType ?? "", out var lbl) ? lbl : d.DocumentType,
                            Status = d.Status,
                            DocumentDate = d.DocumentDate,
                            DueDate = d.DueDate,
                            ContactName = d.ContactName,
                            ContactTaxId = d.ContactTaxId,
                            SubTotal = d.SubTotal,
                            VatAmount = d.VatAmount,
                            TotalAmount = d.TotalAmount,
                            PaidAmount = d.PaidAmount,
                            BalanceDue = d.BalanceDue,
                            Reference = d.Reference,
                            Notes = d.Notes,
                            DocumentUrl = BuildNexaaccDocumentUrl(d.Id.ToString(), "EXPENSE")
                        };

                        if (includeAttachments)
                            doc.Attachments = await FetchDocumentAttachmentsAsync(d.Id, baseUrl);

                        result.Add(doc);
                    }

                    if (resp.Items.Count < 50 || page >= resp.TotalPages) break;
                    page++;
                }
            }

            return result.OrderByDescending(x => x.DocumentDate).ThenByDescending(x => x.DocumentNumber).ToList();
        }

        /// <summary>เอกสารเงินเดือน = Reference ขึ้นต้น PAYROLL- หรือชื่อผู้ติดต่อมีคำว่า "เงินเดือน"</summary>
        private static bool IsPayrollDocument(OutboundDocumentResponse d)
        {
            string r = d.Reference ?? "";
            string c = d.ContactName ?? "";
            if (r.StartsWith("PAYROLL-", StringComparison.OrdinalIgnoreCase)) return true;
            if (c.Contains("เงินเดือน")) return true;
            return false;
        }

        /// <summary>เอกสารที่ยกเลิก/void บน NextAcc แล้ว (NextAcc DocumentStatus: Voided=6, Rejected=8)</summary>
        private static bool IsVoidedDocument(OutboundDocumentResponse d)
        {
            string s = (d?.Status ?? "").Trim();
            return s.Equals("Voided", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Cancelled", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Canceled", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Rejected", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>ดึงรายการไฟล์แนบของเอกสาร (entityType=Document) + สร้าง URL static</summary>
        private async System.Threading.Tasks.Task<List<NextAccAttachment>> FetchDocumentAttachmentsAsync(Guid documentId, string baseUrl)
        {
            var list = new List<NextAccAttachment>();
            try
            {
                var resp = await _apiClient.GetAttachmentsAsync("Document", documentId);
                if (resp?.data != null)
                {
                    foreach (var a in resp.data)
                    {
                        string storage = (a.StoragePath ?? "").Replace("\\", "/").TrimStart('/');
                        string url = !string.IsNullOrEmpty(storage) ? $"{baseUrl}/{storage}" : null;
                        list.Add(new NextAccAttachment
                        {
                            Id = a.Id,
                            FileName = a.OriginalFileName ?? a.FileName,
                            ContentType = a.ContentType,
                            FileSize = a.FileSize,
                            Url = url,
                            IsImage = (a.ContentType ?? "").StartsWith("image", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"FetchDocumentAttachments: doc={documentId} ล้มเหลว: {ex.Message}", "SYSTEM");
            }
            return list;
        }

        // ──────────────────────────────────────────────
        // Outbound: ดาวน์โหลดเอกสารฝั่งจ่าย (PDF อย่างเป็นทางการ + ไฟล์แนบ) จาก NextAcc
        //           มาเก็บไว้ที่ฝั่ง TakeTime — ใช้ในหน้า CheckPayment เปิดดู PDF จาก NextAcc
        //           แทน PDF ที่ระบบ TakeTime ออกเอง
        // เก็บไว้ที่ {PaymentFolderPath}\NextAcc\{docNum}\ → เสิร์ฟผ่าน /Documents/Payment/NextAcc/...
        // ──────────────────────────────────────────────

        /// <summary>
        /// หา Nexaacc_Response_Id ของ action ที่ระบุ (CREATE_VOUCHER_JOURNAL / VOID_VOUCHER)
        /// แยกตาม Action_Type เพื่อไม่ให้ entry ยกเลิก (VOID) มาบังตัวเอกสารต้นฉบับ
        /// </summary>
        private string LookupVoucherActionResponse(string voucherDocNumber, string actionType)
        {
            if (string.IsNullOrEmpty(voucherDocNumber)) return null;
            try
            {
                string esc = voucherDocNumber.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Response_Id FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'VOUCHER' AND Action_Type = @action AND Status = 'COMPLETED'
                        AND Nexaacc_Response_Id IS NOT NULL
                        AND Payload LIKE @pattern
                      ORDER BY Processed_Date DESC",
                    new Dictionary<string, object>
                    {
                        { "@action", actionType },
                        { "@pattern", $"%\"documentNumber\":\"{esc}\"%" }
                    });
                if (dt?.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return dt.Rows[0][0].ToString();
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVoucherActionResponse({actionType}) doc={voucherDocNumber}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>ดึง Guid ตัวแรกจากสตริง response (รองรับ "DEBIT_NOTE:{guid}", "REVERSED:{guid} → ...", "{guid}")</summary>
        private static Guid ExtractGuid(string s)
        {
            if (string.IsNullOrEmpty(s)) return Guid.Empty;
            var m = System.Text.RegularExpressions.Regex.Match(s,
                @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
            return m.Success && Guid.TryParse(m.Value, out Guid g) ? g : Guid.Empty;
        }

        private static string MakeSafeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "file";
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        /// <summary>
        /// ดาวน์โหลดเอกสารใบสำคัญจ่ายอย่างเป็นทางการจาก NextAcc (PDF + ไฟล์แนบ) มาเก็บที่ฝั่ง TakeTime
        /// คืน NextAccCachedDocument พร้อม relative URL ของ PDF (ถ้าสำเร็จ)
        /// ถ้าเอกสารยังไม่ sync / NextAcc ไม่มี template → Found=false (caller fallback ไป PDF ระบบเดิม)
        /// </summary>
        public async System.Threading.Tasks.Task<NextAccCachedDocument> DownloadVoucherDocumentFromNextAccAsync(
            string voucherDocNumber, bool forceRefresh = false, bool isCancelled = false)
        {
            var result = new NextAccCachedDocument();
            if (string.IsNullOrEmpty(voucherDocNumber)) { result.Message = "ไม่มีเลขที่เอกสาร"; return result; }
            if (!_config.IsConfigured || !_config.Enabled) { result.Message = "ยังไม่ได้ตั้งค่า NextAcc"; return result; }

            string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
            if (string.IsNullOrEmpty(basePath)) { result.Message = "ไม่ได้ตั้งค่า PaymentFolderPath"; return result; }

            // ── Fast path: ถ้า PDF cache อยู่แล้วบนดิสก์ → คืนทันที ไม่ต้อง query DB/ยิง API ──
            // (ทำให้การค้นหาซ้ำช่วงเดิมเร็วมาก — โหลดจริงเฉพาะครั้งแรกต่อเอกสาร)
            {
                string safeEarly = MakeSafeFileName(voucherDocNumber);
                string suffixEarly = isCancelled ? "_Cancel" : "";
                string folderEarly = Path.Combine(basePath, "NextAcc", safeEarly);
                string pdfEarly = Path.Combine(folderEarly, safeEarly + suffixEarly + ".pdf");
                string relEarly = "/Documents/Payment/NextAcc/" + safeEarly;
                if (!forceRefresh && File.Exists(pdfEarly) && new FileInfo(pdfEarly).Length > 0)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfEarly;
                    result.PdfRelativeUrl = relEarly + "/" + safeEarly + suffixEarly + ".pdf";
                    try
                    {
                        string attPrefix = "att" + suffixEarly; // "att" หรือ "att_Cancel"
                        foreach (var f in Directory.GetFiles(folderEarly, attPrefix + "*"))
                        {
                            string fn = Path.GetFileName(f);
                            if (suffixEarly == "" && fn.StartsWith("att_Cancel")) continue; // กันชนกับชุดยกเลิก
                            result.AttachmentCount++;
                            result.AttachmentRelativeUrls.Add(relEarly + "/" + fn);
                        }
                    }
                    catch { }
                    return result;
                }
            }

            // หาเอกสารปลายทางใน NextAcc:
            //   ปกติ → เอกสารใบสำคัญจ่ายต้นฉบับ (CREATE_VOUCHER_JOURNAL)
            //   ยกเลิก → ใบเพิ่มหนี้/เอกสารยกเลิกจาก NextAcc (VOID_VOUCHER → "DEBIT_NOTE:{id}")
            //            ถ้าไม่มีใบเพิ่มหนี้ (เช่น void แบบ JOURNAL reverse) → ใช้เอกสารต้นฉบับ + ลายน้ำ "ยกเลิก"
            Guid docId;
            string watermark = null;
            string fileSuffix = "";
            Guid attachmentDocId;

            Guid originalDocId = ExtractGuid(LookupVoucherActionResponse(voucherDocNumber, "CREATE_VOUCHER_JOURNAL"));

            if (isCancelled)
            {
                fileSuffix = "_Cancel";
                string voidResp = LookupVoucherActionResponse(voucherDocNumber, "VOID_VOUCHER");
                Guid debitNoteId = (voidResp ?? "").IndexOf("DEBIT_NOTE", StringComparison.OrdinalIgnoreCase) >= 0
                    ? ExtractGuid(voidResp) : Guid.Empty;

                if (debitNoteId != Guid.Empty)
                {
                    docId = debitNoteId;                 // เอกสารยกเลิกฝั่ง NextAcc (ใบเพิ่มหนี้)
                    attachmentDocId = debitNoteId;
                }
                else
                {
                    docId = originalDocId;               // fallback: ต้นฉบับ + ลายน้ำยกเลิก
                    attachmentDocId = originalDocId;
                    watermark = "ยกเลิก";
                }
            }
            else
            {
                docId = originalDocId;
                attachmentDocId = originalDocId;
            }

            if (docId == Guid.Empty) { result.Message = "เอกสารนี้ยังไม่ได้ sync เข้า NextAcc"; return result; }

            string safeDoc = MakeSafeFileName(voucherDocNumber);
            string folder = Path.Combine(basePath, "NextAcc", safeDoc);
            string pdfPath = Path.Combine(folder, safeDoc + fileSuffix + ".pdf");
            string relPrefix = "/Documents/Payment/NextAcc/" + safeDoc;

            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // 1) PDF อย่างเป็นทางการจาก NextAcc
                if (forceRefresh || !File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                {
                    byte[] pdf = await _apiClient.GenerateDocumentPdfAsync(docId, watermark: watermark);
                    if (pdf != null && pdf.Length > 0)
                        File.WriteAllBytes(pdfPath, pdf);
                }
                if (File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + fileSuffix + ".pdf";
                }
                else
                {
                    result.Message = "NextAcc ไม่มี PDF/template สำหรับเอกสารนี้";
                }

                // 2) ไฟล์แนบ — เก็บชื่อไฟล์เป็น ASCII (att{n}{ext}) กันปัญหา URL ภาษาไทย
                try
                {
                    var attResp = await _apiClient.GetAttachmentsAsync("Document", attachmentDocId);
                    if (attResp?.data != null && attResp.data.Count > 0)
                    {
                        string baseUrl = _config.RawBaseUrl.TrimEnd('/');
                        int idx = 0;
                        foreach (var a in attResp.data)
                        {
                            idx++;
                            string storage = (a.StoragePath ?? "").Replace("\\", "/").TrimStart('/');
                            if (string.IsNullOrEmpty(storage)) continue;

                            string origName = a.OriginalFileName ?? a.FileName ?? ("att" + idx);
                            string ext = Path.GetExtension(origName);
                            if (string.IsNullOrEmpty(ext)) ext = ExtFromContentType(a.ContentType);
                            string attName = $"att{fileSuffix}{idx}{ext}";
                            string attLocal = Path.Combine(folder, attName);

                            if (forceRefresh || !File.Exists(attLocal) || new FileInfo(attLocal).Length == 0)
                            {
                                byte[] bytes = await _apiClient.DownloadFileAsync($"{baseUrl}/{storage}");
                                if (bytes != null && bytes.Length > 0)
                                    File.WriteAllBytes(attLocal, bytes);
                            }
                            if (File.Exists(attLocal) && new FileInfo(attLocal).Length > 0)
                            {
                                result.AttachmentCount++;
                                result.AttachmentRelativeUrls.Add(relPrefix + "/" + attName);
                            }
                        }
                    }
                }
                catch (Exception exAtt)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"DownloadVoucherDocument: attachments doc={voucherDocNumber} ล้มเหลว: {exAtt.Message}", "SYSTEM");
                }

                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadVoucherDocument: doc={voucherDocNumber} pdf={result.Found} attachments={result.AttachmentCount}", "SYSTEM");
            }
            catch (AuthenticationFailedException exAuth)
            {
                result.Message = "NextAcc auth ล้มเหลว: " + exAuth.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadVoucherDocument: doc={voucherDocNumber} {exAuth.Message}", "SYSTEM");
            }
            catch (Exception ex)
            {
                result.Message = "ดาวน์โหลดเอกสารล้มเหลว: " + ex.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadVoucherDocument: doc={voucherDocNumber} {ex.Message}", "SYSTEM");
            }

            return result;
        }

        private static string ExtFromContentType(string contentType)
        {
            switch ((contentType ?? "").ToLower())
            {
                case "application/pdf": return ".pdf";
                case "image/jpeg": return ".jpg";
                case "image/png": return ".png";
                case "image/gif": return ".gif";
                case "image/bmp": return ".bmp";
                case "image/webp": return ".webp";
                default: return ".bin";
            }
        }

        /// <summary>
        /// ดึงเอกสารฝั่งจ่ายที่ "สร้างบน NextAcc" ในช่วงวันที่ที่กำหนด (ผ่าน /api/integration/documents
        /// ซึ่งใช้ได้แน่นอนกับ Integration Key) แล้วดาวน์โหลด PDF + ไฟล์แนบมาเก็บที่ฝั่ง TakeTime
        /// คืน map: Reference (เลขที่ใบสำคัญจ่ายฝั่ง TakeTime) → NextAccCachedDocument
        ///
        /// วิธีนี้ไม่พึ่ง Nexaacc_Response_Id ใน Sync Queue — จึงเจอเอกสารที่ออกบน NextAcc เสมอ
        /// แม้ generate-pdf จะไม่มี template (จะยังมี DeepLinkUrl + ไฟล์แนบให้เปิดดู)
        /// </summary>
        public async System.Threading.Tasks.Task<List<NextAccCachedDocument>> DownloadVoucherDocumentsForRangeAsync(
            DateTime fromDate, DateTime toDate, bool includeAttachments = true)
        {
            var list = new List<NextAccCachedDocument>();
            if (!_config.IsConfigured || !_config.Enabled) return list;

            string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
            if (string.IsNullOrEmpty(basePath)) return list;
            string baseUrl = _config.RawBaseUrl.TrimEnd('/');

            var seen = new HashSet<Guid>();

            foreach (var typeName in PaymentDocTypeLabels.Keys)
            {
                int page = 1;
                while (true)
                {
                    PagedResponse<OutboundDocumentResponse> resp;
                    try
                    {
                        resp = await _apiClient.GetIntegrationDocumentsAsync(new OutboundQueryParams
                        {
                            FromDate = fromDate,
                            ToDate = toDate,
                            Type = typeName,
                            Page = page,
                            PageSize = 50
                        });
                    }
                    catch (Exception ex)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"DownloadVoucherDocumentsForRange: type={typeName} page={page} ล้มเหลว: {ex.Message}", "SYSTEM");
                        break;
                    }

                    if (resp?.Items == null || resp.Items.Count == 0) break;

                    foreach (var d in resp.Items)
                    {
                        if (d == null || seen.Contains(d.Id)) continue;
                        if (IsPayrollDocument(d)) continue;       // ยกเว้นเงินเดือน
                        if (IsVoidedDocument(d)) continue;        // ยกเว้นเอกสารที่ยกเลิก/void บน NextAcc แล้ว
                        seen.Add(d.Id);

                        var cached = await CacheNextAccDocumentAsync(d, basePath, baseUrl, includeAttachments);
                        list.Add(cached);
                    }

                    if (resp.Items.Count < 50 || page >= resp.TotalPages) break;
                    page++;
                }
            }

            _code.Logs(_connectionString, "AccountingSync",
                $"DownloadVoucherDocumentsForRange: {fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd} พบ {list.Count} เอกสาร", "SYSTEM");
            return list;
        }

        /// <summary>เก็บ URL ไฟล์แนบที่ cache ไว้บนดิสก์ (ชื่อ att*) เข้า result</summary>
        private static void GlobCachedAttachments(string folder, string relPrefix, NextAccCachedDocument result)
        {
            try
            {
                foreach (var f in Directory.GetFiles(folder, "att*"))
                {
                    string fn = Path.GetFileName(f);
                    result.AttachmentCount++;
                    result.AttachmentRelativeUrls.Add(relPrefix + "/" + fn);
                }
            }
            catch { }
        }

        /// <summary>
        /// เก็บ PDF + ไฟล์แนบของเอกสาร NextAcc 1 ใบลงดิสก์ฝั่ง TakeTime
        /// ใช้ marker บนดิสก์กัน hit API ซ้ำ: ถ้า PDF cache แล้ว / เคยรู้ว่าไม่มี PDF / เคยโหลดไฟล์แนบแล้ว
        /// จะไม่เรียก API ในการค้นหาครั้งถัดไป
        /// </summary>
        private async System.Threading.Tasks.Task<NextAccCachedDocument> CacheNextAccDocumentAsync(
            OutboundDocumentResponse d, string basePath, string baseUrl, bool includeAttachments)
        {
            var result = new NextAccCachedDocument
            {
                DeepLinkUrl = BuildNexaaccDocumentUrl(d.Id.ToString(), "EXPENSE"),
                NextAccId = d.Id,
                Reference = d.Reference,
                DocumentNumber = d.DocumentNumber,
                DocumentTypeLabel = PaymentDocTypeLabels.TryGetValue(d.DocumentType ?? "", out var lbl) ? lbl : d.DocumentType,
                DocumentDate = d.DocumentDate,
                ContactName = d.ContactName,
                ContactTaxId = d.ContactTaxId,
                TotalAmount = d.TotalAmount,
                VatAmount = d.VatAmount,
                Status = d.Status
            };

            // โฟลเดอร์ cache: ใช้ Reference (เลขใบสำคัญจ่ายฝั่ง TakeTime) ถ้ามี ไม่งั้นใช้เลขเอกสาร NextAcc
            // → เอกสารที่ sync จาก TakeTime ใช้โฟลเดอร์เดียวกับปุ่ม "ดู PDF", เอกสารที่สร้างบน NextAcc ใช้เลข NextAcc
            string safeDoc = MakeSafeFileName(!string.IsNullOrEmpty(d.Reference) ? d.Reference : d.DocumentNumber);
            string folder = Path.Combine(basePath, "NextAcc", safeDoc);
            string pdfPath = Path.Combine(folder, safeDoc + ".pdf");
            string noPdfMarker = Path.Combine(folder, "_nopdf.marker");
            string attDoneMarker = Path.Combine(folder, "_att.done");
            string relPrefix = "/Documents/Payment/NextAcc/" + safeDoc;

            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                bool pdfCached = File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0;

                // ── Fast path: PDF cache แล้ว → ไม่ต้องยิง API ใดๆ ──
                if (pdfCached)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                    GlobCachedAttachments(folder, relPrefix, result);
                    return result;
                }

                // PDF อย่างเป็นทางการ (ถ้า NextAcc มี template) — ข้ามถ้าเคยรู้ว่าไม่มี
                if (!File.Exists(noPdfMarker))
                {
                    byte[] pdf = await _apiClient.GenerateDocumentPdfAsync(d.Id);
                    if (pdf != null && pdf.Length > 0)
                    {
                        File.WriteAllBytes(pdfPath, pdf);
                        result.Found = true;
                        result.PdfLocalPath = pdfPath;
                        result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                    }
                    else
                    {
                        // NextAcc ไม่มี template/PDF — เขียน marker กันลองซ้ำทุกครั้ง (เปิดดูผ่าน DeepLink แทน)
                        try { File.WriteAllText(noPdfMarker, DateTime.Now.ToString("o")); } catch { }
                    }
                }

                // ไฟล์แนบ — โหลดครั้งแรกแล้วทำ marker; ครั้งถัดไปอ่านจากดิสก์
                if (includeAttachments)
                {
                    if (File.Exists(attDoneMarker))
                    {
                        GlobCachedAttachments(folder, relPrefix, result);
                    }
                    else
                    {
                        try
                        {
                            var attResp = await _apiClient.GetAttachmentsAsync("Document", d.Id);
                            if (attResp?.data != null && attResp.data.Count > 0)
                            {
                                int idx = 0;
                                foreach (var a in attResp.data)
                                {
                                    idx++;
                                    string storage = (a.StoragePath ?? "").Replace("\\", "/").TrimStart('/');
                                    if (string.IsNullOrEmpty(storage)) continue;

                                    string ext = Path.GetExtension(a.OriginalFileName ?? a.FileName ?? "");
                                    if (string.IsNullOrEmpty(ext)) ext = ExtFromContentType(a.ContentType);
                                    string attName = $"att{idx}{ext}";
                                    string attLocal = Path.Combine(folder, attName);

                                    if (!File.Exists(attLocal) || new FileInfo(attLocal).Length == 0)
                                    {
                                        byte[] bytes = await _apiClient.DownloadFileAsync($"{baseUrl}/{storage}");
                                        if (bytes != null && bytes.Length > 0)
                                            File.WriteAllBytes(attLocal, bytes);
                                    }
                                    if (File.Exists(attLocal) && new FileInfo(attLocal).Length > 0)
                                    {
                                        result.AttachmentCount++;
                                        result.AttachmentRelativeUrls.Add(relPrefix + "/" + attName);
                                    }
                                }
                            }
                            try { File.WriteAllText(attDoneMarker, DateTime.Now.ToString("o")); } catch { }
                        }
                        catch (Exception exAtt)
                        {
                            _code.Logs(_connectionString, "AccountingSync",
                                $"CacheNextAccDocument: attachments doc={d.DocumentNumber} ({d.Reference}) ล้มเหลว: {exAtt.Message}", "SYSTEM");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                result.Message = ex.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"CacheNextAccDocument: doc={d.DocumentNumber} ({d.Reference}) ล้มเหลว: {ex.Message}", "SYSTEM");
            }

            return result;
        }
    }
}
