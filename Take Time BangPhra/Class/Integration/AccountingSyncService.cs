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
    /// Usage from trigger points:
    ///   var sync = new AccountingSyncService();
    ///   sync.EnqueueReservationDeposit(reservationId, amount, paymentMethod, date, customerName);
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
        // ──────────────────────────────────────────────

        /// <summary>
        /// Enqueue deposit received for a reservation.
        /// Call after recording deposit in MakePayment.aspx.cs
        /// </summary>
        public long EnqueueReservationDeposit(int reservationId, decimal amount, string paymentMethod, DateTime paymentDate, string customerName)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueReservationDeposit skipped: amount is {amount} for Reservation #{reservationId}. Caller should check amount before enqueuing.", "SYSTEM");
                return -1;
            }

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "amount", amount },
                { "paymentMethod", paymentMethod },
                { "paymentDate", paymentDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_DEPOSIT_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue full payment received for a reservation.
        /// Call after recording payment in MakePayment.aspx.cs
        /// </summary>
        public long EnqueueReservationPayment(int reservationId, decimal amount, string paymentMethod, DateTime paymentDate, string customerName, bool hasVat = false)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueReservationPayment skipped: amount is {amount} for Reservation #{reservationId}. Caller should check amount before enqueuing.", "SYSTEM");
                return -1;
            }

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "amount", amount },
                { "paymentMethod", paymentMethod },
                { "paymentDate", paymentDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName },
                { "hasVat", hasVat }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_PAYMENT_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue checkout for revenue recognition.
        /// Call after successful checkout in Checkout.aspx.cs
        /// </summary>
        public long EnqueueCheckout(int reservationId, decimal depositAmount, string customerName, DateTime checkoutDate)
        {
            if (!_config.IsConfigured) return -1;

            // If depositAmount is 0, try to recover from Payment_History before skipping
            if (depositAmount <= 0)
            {
                try
                {
                    var fallbackParams = new Dictionary<string, object> { { "@id", reservationId } };
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT ISNULL(SUM(PaymentAmount), 0) AS TotalPaid
                          FROM Payment_History
                          WHERE Reservation_ID = @id AND Status = 'COMPLETED'",
                        fallbackParams);
                    if (dt?.Rows.Count > 0)
                    {
                        decimal recovered = Convert.ToDecimal(dt.Rows[0]["TotalPaid"]);
                        if (recovered > 0)
                        {
                            depositAmount = recovered;
                            _code.Logs(_connectionString, "AccountingSync",
                                $"EnqueueCheckout: depositAmount was 0, recovered {depositAmount:N2} from Payment_History for Reservation #{reservationId}.", "SYSTEM");
                        }
                    }
                }
                catch { }
            }

            // Still 0? Enqueue anyway - the processor has its own fallback chain
            if (depositAmount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueCheckout: depositAmount is {depositAmount} for Reservation #{reservationId}. Enqueuing with 0 - processor will attempt fallback.", "SYSTEM");
            }

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "depositAmount", depositAmount },
                { "customerName", customerName },
                { "checkoutDate", checkoutDate.ToString("yyyy-MM-dd") }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_CHECKOUT_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue refund for a cancelled reservation.
        /// Call after cancellation with refund.
        /// </summary>
        public long EnqueueRefund(int reservationId, decimal refundAmount, string paymentMethod, DateTime refundDate, string customerName)
        {
            if (!_config.IsConfigured) return -1;
            if (refundAmount <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "refundAmount", refundAmount },
                { "paymentMethod", paymentMethod },
                { "refundDate", refundDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_REFUND_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue payment voucher (expense).
        /// Call after creating voucher in Voucher/Default.aspx.cs
        /// รองรับ VAT ซื้อ (Input VAT) และภาษีหัก ณ ที่จ่าย (WHT) ตามหลักบัญชีไทย
        /// </summary>
        public long EnqueuePaymentVoucher(int voucherId, string expenseCategory, decimal amount,
            string paymentMethod, DateTime voucherDate, string description, string payeeName,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0) return -1;

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

            return InsertQueue("VOUCHER", voucherId, "CREATE_VOUCHER_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue room charge (product sold to guest).
        /// Call after RoomChargeService.ChargeToRoom()
        /// </summary>
        public long EnqueueRoomCharge(int reservationId, decimal salesAmount, decimal costAmount, DateTime chargeDate, string description)
        {
            if (!_config.IsConfigured) return -1;
            if (salesAmount <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "salesAmount", salesAmount },
                { "costAmount", costAmount },
                { "chargeDate", chargeDate.ToString("yyyy-MM-dd") },
                { "description", description }
            };

            return InsertQueue("ROOM_CHARGE", reservationId, "CREATE_ROOM_CHARGE_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue stock received (purchase).
        /// Call after Product/In.aspx.cs stock adjustment.
        /// รองรับทั้งซื้อเงินสด (DR Inventory, CR Cash/Bank) และซื้อเชื่อ (DR Inventory, CR AP)
        /// </summary>
        public long EnqueueStockIn(int productId, string productName, decimal totalCost, DateTime receiveDate,
            string supplierName, string paymentMethod = null, bool hasInputVat = false)
        {
            if (!_config.IsConfigured) return -1;
            if (totalCost <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "productId", productId },
                { "productName", productName },
                { "totalCost", totalCost },
                { "receiveDate", receiveDate.ToString("yyyy-MM-dd") },
                { "supplierName", supplierName },
                { "paymentMethod", paymentMethod ?? "" },
                { "hasInputVat", hasInputVat }
            };

            return InsertQueue("PRODUCT", productId, "CREATE_STOCK_IN_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue product sync (create/update product in Nexaacc).
        /// Call after product creation in Product/Default.aspx.cs
        /// </summary>
        public long EnqueueProductSync(int productId, string productName, string description, decimal sellingPrice, decimal costPrice, string unit, string categoryName)
        {
            if (!_config.IsConfigured) return -1;

            var payload = new Dictionary<string, object>
            {
                { "productId", productId },
                { "productName", productName },
                { "description", description },
                { "sellingPrice", sellingPrice },
                { "costPrice", costPrice },
                { "unit", unit },
                { "categoryName", categoryName }
            };

            return InsertQueue("PRODUCT", productId, "SYNC_PRODUCT", payload);
        }

        /// <summary>
        /// Enqueue receipt document creation.
        /// Call after ReceiptService generates a receipt.
        /// </summary>
        public long EnqueueReceipt(int reservationId, string receiptNumber, decimal totalAmount, decimal vatAmount, DateTime receiptDate, string customerName)
        {
            if (!_config.IsConfigured) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "receiptNumber", receiptNumber },
                { "totalAmount", totalAmount },
                { "vatAmount", vatAmount },
                { "receiptDate", receiptDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName }
            };

            return InsertQueue("RECEIPT", reservationId, "CREATE_RECEIPT_DOCUMENT", payload);
        }

        /// <summary>
        /// Enqueue credit note document creation.
        /// Call after AccountingService.CreateCreditNote()
        /// </summary>
        public long EnqueueCreditNote(long creditNoteId, string creditNoteNumber, decimal totalAmount, decimal vatAmount, DateTime creditNoteDate, string reason)
        {
            if (!_config.IsConfigured) return -1;

            var payload = new Dictionary<string, object>
            {
                { "creditNoteId", creditNoteId },
                { "creditNoteNumber", creditNoteNumber },
                { "totalAmount", totalAmount },
                { "vatAmount", vatAmount },
                { "creditNoteDate", creditNoteDate.ToString("yyyy-MM-dd") },
                { "reason", reason }
            };

            return InsertQueue("CREDIT_NOTE", (int)creditNoteId, "CREATE_CREDIT_NOTE_DOCUMENT", payload);
        }

        /// <summary>
        /// Enqueue payroll journal entry.
        /// Call after PayrollService processes payroll.
        /// รองรับประกันสังคม (SSF) และภาษีหัก ณ ที่จ่าย (WHT) ตามกฎหมายไทย
        /// </summary>
        public long EnqueuePayroll(decimal totalSalary, DateTime payDate, string period,
            decimal socialSecurityEmployee = 0, decimal socialSecurityEmployer = 0,
            decimal whtAmount = 0)
        {
            if (!_config.IsConfigured) return -1;
            if (totalSalary <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "totalSalary", totalSalary },
                { "payDate", payDate.ToString("yyyy-MM-dd") },
                { "period", period },
                { "socialSecurityEmployee", socialSecurityEmployee },
                { "socialSecurityEmployer", socialSecurityEmployer },
                { "whtAmount", whtAmount }
            };

            return InsertQueue("PAYROLL", 0, "CREATE_PAYROLL_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue cancellation without refund — forfeited deposit recognized as revenue.
        /// Call after cancellation where customer does NOT get a refund.
        /// DR: Advance Deposit (เงินรับล่วงหน้า)  CR: Other Income (รายได้อื่น)
        /// </summary>
        public long EnqueueCancellationNoRefund(int reservationId, decimal depositAmount, string customerName, DateTime cancelDate)
        {
            if (!_config.IsConfigured) return -1;
            if (depositAmount <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "depositAmount", depositAmount },
                { "customerName", customerName },
                { "cancelDate", cancelDate.ToString("yyyy-MM-dd") }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_CANCEL_NO_REFUND_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue standalone POS sale (not charged to room).
        /// DR: Cash/Bank  CR: Product Revenue + COGS entries
        /// </summary>
        public long EnqueuePOSSale(string receiptId, decimal totalAmount, decimal totalCost, string paymentMethod, DateTime saleDate, string description)
        {
            if (!_config.IsConfigured) return -1;
            if (totalAmount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueuePOSSale skipped: totalAmount is {totalAmount} for Receipt: {receiptId}.", "SYSTEM");
                return -1;
            }

            var payload = new Dictionary<string, object>
            {
                { "receiptId", receiptId },
                { "totalAmount", totalAmount },
                { "totalCost", totalCost },
                { "paymentMethod", paymentMethod },
                { "saleDate", saleDate.ToString("yyyy-MM-dd") },
                { "description", description }
            };

            return InsertQueue("POS_SALE", 0, "CREATE_POS_SALE_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue postpone price difference adjustment.
        /// When a guest reschedules to a different date with price difference.
        /// If newPrice > oldPrice: DR: Customer AR  CR: Room Revenue (additional charge)
        /// If newPrice < oldPrice: DR: Room Revenue  CR: Customer AR (partial refund/credit)
        /// </summary>
        public long EnqueuePostponePriceDiff(int reservationId, decimal priceDifference, DateTime rescheduleDate, string customerName)
        {
            if (!_config.IsConfigured) return -1;
            if (priceDifference == 0) return -1; // No price difference, no journal entry needed

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "priceDifference", priceDifference },
                { "rescheduleDate", rescheduleDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_POSTPONE_PRICE_DIFF_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue partial refund for a reservation.
        /// When only part of the deposit is refunded (e.g., cancellation fee deducted).
        /// </summary>
        public long EnqueuePartialRefund(int reservationId, decimal refundAmount, decimal retainedAmount, string paymentMethod, DateTime refundDate, string customerName, string reason)
        {
            if (!_config.IsConfigured) return -1;
            if (refundAmount <= 0 && retainedAmount <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "refundAmount", refundAmount },
                { "retainedAmount", retainedAmount },
                { "paymentMethod", paymentMethod },
                { "refundDate", refundDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName },
                { "reason", reason }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_PARTIAL_REFUND_JOURNAL", payload);
        }

        /// <summary>
        /// Enqueue damage/missing item charge at checkout.
        /// DR: Cash/Bank or Customer AR  CR: Other Income
        /// </summary>
        public long EnqueueDamageCharge(int reservationId, decimal damageAmount, decimal missingItemsAmount, DateTime chargeDate, string customerName, string description)
        {
            if (!_config.IsConfigured) return -1;
            if (damageAmount <= 0 && missingItemsAmount <= 0) return -1;

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "damageAmount", damageAmount },
                { "missingItemsAmount", missingItemsAmount },
                { "chargeDate", chargeDate.ToString("yyyy-MM-dd") },
                { "customerName", customerName },
                { "description", description }
            };

            return InsertQueue("RESERVATION", reservationId, "CREATE_DAMAGE_CHARGE_JOURNAL", payload);
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
            bool dnsFailed = false;

            foreach (DataRow row in pending.Rows)
            {
                // If DNS failed during this batch, stop processing remaining items
                if (dnsFailed) break;

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
                    dnsFailed = true;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessQueueAsync halted: DNS resolution failed — remaining items will be retried next cycle. Error: {ex.Message}", "SYSTEM");
                }
                catch (ArgumentException ex)
                {
                    // Validation error (e.g., zero amounts) — don't retry
                    UpdateQueueStatus(queueId, "FAILED", ex.Message, null);
                    IncrementRetry(queueId, _config.MaxRetries);
                }
                catch (AccountingApiException ex)
                {
                    // Client error (4xx) — don't retry
                    UpdateQueueStatus(queueId, "FAILED", ex.Message, null);
                    IncrementRetry(queueId, _config.MaxRetries);
                }
                catch (Exception ex)
                {
                    // Check if this is a wrapped DNS error
                    if (IsDnsError(ex))
                    {
                        UpdateQueueStatus(queueId, "PENDING", $"DNS error (not counted as retry): {ex.Message}", null);
                        dnsFailed = true;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessQueueAsync halted: DNS error detected — {ex.Message}", "SYSTEM");
                    }
                    else
                    {
                        // Server/network error — schedule retry
                        int retryCount = Convert.ToInt32(row["Retry_Count"]) + 1;
                        IncrementRetry(queueId, retryCount);
                        UpdateQueueStatus(queueId, "FAILED", ex.Message, null);
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
                case "CREATE_DEPOSIT_JOURNAL":
                    return await ProcessDepositJournal(payload);

                case "CREATE_PAYMENT_JOURNAL":
                    return await ProcessPaymentJournal(payload);

                case "CREATE_CHECKOUT_JOURNAL":
                    return await ProcessCheckoutJournal(payload);

                case "CREATE_REFUND_JOURNAL":
                    return await ProcessRefundJournal(payload);

                case "CREATE_VOUCHER_JOURNAL":
                    return await ProcessVoucherJournal(payload);

                case "CREATE_ROOM_CHARGE_JOURNAL":
                    return await ProcessRoomChargeJournal(payload);

                case "CREATE_STOCK_IN_JOURNAL":
                    return await ProcessStockInJournal(payload);

                case "SYNC_PRODUCT":
                    return await ProcessProductSync(payload);

                case "CREATE_RECEIPT_DOCUMENT":
                    return await ProcessReceiptDocument(payload);

                case "CREATE_CREDIT_NOTE_DOCUMENT":
                    return await ProcessCreditNoteDocument(payload);

                case "CREATE_PAYROLL_JOURNAL":
                    return await ProcessPayrollJournal(payload);

                case "CREATE_CANCEL_NO_REFUND_JOURNAL":
                    return await ProcessCancelNoRefundJournal(payload);

                case "CREATE_POS_SALE_JOURNAL":
                    return await ProcessPOSSaleJournal(payload);

                case "CREATE_POSTPONE_PRICE_DIFF_JOURNAL":
                    return await ProcessPostponePriceDiffJournal(payload);

                case "CREATE_PARTIAL_REFUND_JOURNAL":
                    return await ProcessPartialRefundJournal(payload);

                case "CREATE_DAMAGE_CHARGE_JOURNAL":
                    return await ProcessDamageChargeJournal(payload);

                default:
                    throw new Exception($"Unknown action type: {actionType}");
            }
            }
            catch (DnsResolutionException) { throw; } // DNS/infra error — don't wrap
            catch (ArgumentException) { throw; } // Already a validation error — don't wrap
            catch (AccountingApiException) { throw; } // API error — don't wrap
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
        // Individual Processors
        // ──────────────────────────────────────────────

        private async Task<string> ProcessDepositJournal(Dictionary<string, object> p)
        {
            int reservationId = Convert.ToInt32(p["reservationId"]);
            var amount = Convert.ToDecimal(p["amount"]);

            // Fallback: if payload amount is 0, look up actual deposit from Payment_History
            if (amount <= 0)
            {
                amount = LookupPaidAmount(reservationId, "DEPOSIT");
                if (amount > 0)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessDepositJournal: payload amount was 0, recovered {amount:N2} from Payment_History. Reservation #{reservationId}", "SYSTEM");
            }

            if (amount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositJournal skipped: amount is {amount} after fallback lookup. Reservation #{reservationId}", "SYSTEM");
                return "SKIPPED_ZERO_AMOUNT";
            }

            var invoice = _mapper.MapDepositToInvoice(
                reservationId,
                amount,
                p["paymentMethod"]?.ToString(),
                DateTime.Parse(p["paymentDate"]?.ToString()),
                p["customerName"]?.ToString());

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessPaymentJournal(Dictionary<string, object> p)
        {
            int reservationId = Convert.ToInt32(p["reservationId"]);
            var amount = Convert.ToDecimal(p["amount"]);

            // Fallback: if payload amount is 0, look up actual payment from Payment_History
            if (amount <= 0)
            {
                amount = LookupPaidAmount(reservationId, "ADDITIONAL");
                if (amount > 0)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessPaymentJournal: payload amount was 0, recovered {amount:N2} from Payment_History. Reservation #{reservationId}", "SYSTEM");
            }

            if (amount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPaymentJournal skipped: amount is {amount} after fallback lookup. Reservation #{reservationId}", "SYSTEM");
                return "SKIPPED_ZERO_AMOUNT";
            }

            var invoice = _mapper.MapPaymentToInvoice(
                reservationId,
                amount,
                p["paymentMethod"]?.ToString(),
                DateTime.Parse(p["paymentDate"]?.ToString()),
                p["customerName"]?.ToString(),
                p.ContainsKey("hasVat") && Convert.ToBoolean(p["hasVat"]));

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessCheckoutJournal(Dictionary<string, object> p)
        {
            int reservationId = Convert.ToInt32(p["reservationId"]);
            var depositAmount = Convert.ToDecimal(p["depositAmount"]);

            // Fallback chain when payload depositAmount is 0:
            // 1. Payment_History (most reliable - actual payments received)
            // 2. Reservation.Deposit field
            // 3. Reservation.TotalPrice (last resort for revenue recognition)
            if (depositAmount <= 0)
            {
                try
                {
                    var fallbackParams = new Dictionary<string, object> { { "@id", reservationId } };
                    var dt = _code.DatabaseQuerySafe(_connectionString,
                        @"SELECT
                            ISNULL(ph.TotalPaid, 0) AS TotalPaid,
                            ISNULL(r.Deposit, 0) AS Deposit,
                            ISNULL(r.TotalPrice, 0) AS TotalPrice
                          FROM Reservation r
                          LEFT JOIN (
                              SELECT Reservation_ID, SUM(PaymentAmount) AS TotalPaid
                              FROM Payment_History
                              WHERE Status = 'COMPLETED'
                              GROUP BY Reservation_ID
                          ) ph ON ph.Reservation_ID = r.ID
                          WHERE r.ID = @id",
                        fallbackParams);

                    if (dt?.Rows.Count > 0)
                    {
                        decimal totalPaid = Convert.ToDecimal(dt.Rows[0]["TotalPaid"]);
                        decimal deposit = Convert.ToDecimal(dt.Rows[0]["Deposit"]);
                        decimal totalPrice = Convert.ToDecimal(dt.Rows[0]["TotalPrice"]);

                        // Priority: TotalPaid > Deposit > TotalPrice
                        if (totalPaid > 0)
                            depositAmount = totalPaid;
                        else if (deposit > 0)
                            depositAmount = deposit;
                        else if (totalPrice > 0)
                            depositAmount = totalPrice;

                        if (depositAmount > 0)
                            _code.Logs(_connectionString, "AccountingSync",
                                $"ProcessCheckoutJournal: payload depositAmount was 0, recovered {depositAmount:N2} from DB (TotalPaid={totalPaid:N2}, Deposit={deposit:N2}, TotalPrice={totalPrice:N2}). Reservation #{reservationId}", "SYSTEM");
                    }
                }
                catch (Exception ex)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessCheckoutJournal: fallback lookup failed for Reservation #{reservationId}: {ex.Message}", "SYSTEM");
                }
            }

            if (depositAmount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessCheckoutJournal skipped: depositAmount is {depositAmount} after all fallback lookups. Reservation #{reservationId}", "SYSTEM");
                return "SKIPPED_ZERO_AMOUNT";
            }

            var invoice = _mapper.MapCheckoutToInvoice(
                reservationId,
                depositAmount,
                p["customerName"]?.ToString(),
                DateTime.Parse(p["checkoutDate"]?.ToString()));

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessRefundJournal(Dictionary<string, object> p)
        {
            var refundAmount = Convert.ToDecimal(p["refundAmount"]);
            if (refundAmount <= 0)
                throw new ArgumentException($"Cannot create refund journal: refundAmount is {refundAmount} (must be > 0). Reservation #{p["reservationId"]}");

            var journal = _mapper.MapRefundToJournal(
                Convert.ToInt32(p["reservationId"]),
                refundAmount,
                p["paymentMethod"]?.ToString(),
                DateTime.Parse(p["refundDate"]?.ToString()),
                p["customerName"]?.ToString());

            var result = await _apiClient.CreateJournalAsync(journal);
            await _apiClient.PostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessVoucherJournal(Dictionary<string, object> p)
        {
            var amount = Convert.ToDecimal(p["amount"]);
            if (amount <= 0)
                throw new ArgumentException($"Cannot create voucher journal: amount is {amount} (must be > 0). Voucher #{p["voucherId"]}");

            bool hasInputVat = p.ContainsKey("hasInputVat") && Convert.ToBoolean(p["hasInputVat"]);
            decimal whtRate = p.ContainsKey("whtRate") ? Convert.ToDecimal(p["whtRate"]) : 0;
            decimal whtAmount = p.ContainsKey("whtAmount") ? Convert.ToDecimal(p["whtAmount"]) : 0;

            var expense = _mapper.MapVoucherToExpense(
                Convert.ToInt32(p["voucherId"]),
                p["expenseCategory"]?.ToString(),
                amount,
                p["paymentMethod"]?.ToString(),
                DateTime.Parse(p["voucherDate"]?.ToString()),
                p["description"]?.ToString(),
                p["payeeName"]?.ToString(),
                hasInputVat,
                whtRate,
                whtAmount);

            var result = await _apiClient.CreateExpenseAsync(expense);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessRoomChargeJournal(Dictionary<string, object> p)
        {
            var salesAmount = Convert.ToDecimal(p["salesAmount"]);
            if (salesAmount <= 0)
                throw new ArgumentException($"Cannot create room charge journal: salesAmount is {salesAmount} (must be > 0). Reservation #{p["reservationId"]}");

            var invoice = _mapper.MapRoomChargeToInvoice(
                Convert.ToInt32(p["reservationId"]),
                salesAmount,
                Convert.ToDecimal(p["costAmount"]),
                DateTime.Parse(p["chargeDate"]?.ToString()),
                p["description"]?.ToString());

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessStockInJournal(Dictionary<string, object> p)
        {
            var totalCost = Convert.ToDecimal(p["totalCost"]);
            if (totalCost <= 0)
                throw new ArgumentException($"Cannot create stock-in journal: totalCost is {totalCost} (must be > 0). Product #{p["productId"]}");

            string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : "CASH";

            var expense = _mapper.MapStockInToExpense(
                Convert.ToInt32(p["productId"]),
                p["productName"]?.ToString(),
                totalCost,
                paymentMethod,
                DateTime.Parse(p["receiveDate"]?.ToString()),
                p["supplierName"]?.ToString());

            var result = await _apiClient.CreateExpenseAsync(expense);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessProductSync(Dictionary<string, object> p)
        {
            var product = _mapper.MapProductToNexaacc(
                Convert.ToInt32(p["productId"]),
                p["productName"]?.ToString(),
                p["description"]?.ToString(),
                Convert.ToDecimal(p["sellingPrice"]),
                Convert.ToDecimal(p["costPrice"]),
                p["unit"]?.ToString(),
                p["categoryName"]?.ToString());

            var result = await _apiClient.CreateProductAsync(product);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessReceiptDocument(Dictionary<string, object> p)
        {
            var totalAmount = Convert.ToDecimal(p["totalAmount"]);
            if (totalAmount <= 0)
                throw new ArgumentException($"Cannot create receipt document: totalAmount is {totalAmount} (must be > 0). Reservation #{p["reservationId"]}");

            var document = _mapper.MapReceiptToDocument(
                Convert.ToInt32(p["reservationId"]),
                p["receiptNumber"]?.ToString(),
                totalAmount,
                Convert.ToDecimal(p["vatAmount"]),
                DateTime.Parse(p["receiptDate"]?.ToString()),
                null, // contactId — would need lookup
                $"ใบเสร็จ - การจอง #{p["reservationId"]}");

            var result = await _apiClient.CreateDocumentAsync(document);
            await _apiClient.ApproveDocumentAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessCreditNoteDocument(Dictionary<string, object> p)
        {
            var totalAmount = Convert.ToDecimal(p["totalAmount"]);
            if (totalAmount <= 0)
                throw new ArgumentException($"Cannot create credit note document: totalAmount is {totalAmount} (must be > 0). CreditNote #{p["creditNoteId"]}");

            var document = _mapper.MapCreditNoteToDocument(
                p["creditNoteNumber"]?.ToString(),
                totalAmount,
                Convert.ToDecimal(p["vatAmount"]),
                DateTime.Parse(p["creditNoteDate"]?.ToString()),
                null,
                p["reason"]?.ToString());

            var result = await _apiClient.CreateDocumentAsync(document);
            await _apiClient.ApproveDocumentAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessPayrollJournal(Dictionary<string, object> p)
        {
            var totalSalary = Convert.ToDecimal(p["totalSalary"]);
            if (totalSalary <= 0)
                throw new ArgumentException($"Cannot create payroll journal: totalSalary is {totalSalary} (must be > 0). Period: {p["period"]}");

            decimal ssfEmployee = p.ContainsKey("socialSecurityEmployee") ? Convert.ToDecimal(p["socialSecurityEmployee"]) : 0;
            decimal ssfEmployer = p.ContainsKey("socialSecurityEmployer") ? Convert.ToDecimal(p["socialSecurityEmployer"]) : 0;
            decimal whtAmount = p.ContainsKey("whtAmount") ? Convert.ToDecimal(p["whtAmount"]) : 0;

            var expense = _mapper.MapPayrollToExpense(
                p["period"]?.ToString(),
                totalSalary,
                ssfEmployee + ssfEmployer,
                whtAmount,
                DateTime.Parse(p["payDate"]?.ToString()),
                p.ContainsKey("description") ? p["description"]?.ToString() : null);

            var result = await _apiClient.CreateExpenseAsync(expense);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessCancelNoRefundJournal(Dictionary<string, object> p)
        {
            var depositAmount = Convert.ToDecimal(p["depositAmount"]);
            if (depositAmount <= 0)
                throw new ArgumentException($"Cannot create cancel-no-refund journal: depositAmount is {depositAmount} (must be > 0). Reservation #{p["reservationId"]}");

            var invoice = _mapper.MapCancelNoRefundToInvoice(
                Convert.ToInt32(p["reservationId"]),
                depositAmount,
                p["customerName"]?.ToString(),
                DateTime.Parse(p["cancelDate"]?.ToString()));

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessPOSSaleJournal(Dictionary<string, object> p)
        {
            var totalAmount = Convert.ToDecimal(p["totalAmount"]);
            string receiptId = p["receiptId"]?.ToString() ?? "UNKNOWN";
            string paymentMethod = p["paymentMethod"]?.ToString() ?? "CASH";

            if (totalAmount <= 0)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPOSSaleJournal skipped: totalAmount is {totalAmount}. Receipt: {receiptId}", "SYSTEM");
                return "SKIPPED_ZERO_AMOUNT";
            }

            // Validate account mappings exist before calling API to prevent "must have debit/credit" errors
            if (!_mapper.TryGetAccountId("PRODUCT_REVENUE", out _))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPOSSaleJournal skipped: PRODUCT_REVENUE account not mapped. Receipt: {receiptId}", "SYSTEM");
                throw new ArgumentException($"Cannot create POS sale journal: PRODUCT_REVENUE account is not mapped. Please configure in Accounting_Account_Mapping table. Receipt: {receiptId}");
            }

            // Validate payment method account exists
            string paymentAccountKey = ResolvePaymentMethodKey(paymentMethod);
            if (!_mapper.TryGetAccountId(paymentAccountKey, out _))
            {
                // Try CASH as fallback
                if (!_mapper.TryGetAccountId("CASH", out _))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessPOSSaleJournal skipped: No account mapping for payment method '{paymentMethod}' (key: {paymentAccountKey}) and no CASH fallback. Receipt: {receiptId}", "SYSTEM");
                    throw new ArgumentException($"Cannot create POS sale journal: No account mapping for payment method '{paymentMethod}'. Please configure in Accounting_Account_Mapping table. Receipt: {receiptId}");
                }
                paymentMethod = "CASH"; // Use CASH as fallback
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPOSSaleJournal: No mapping for '{p["paymentMethod"]}', falling back to CASH. Receipt: {receiptId}", "SYSTEM");
            }

            var journal = _mapper.MapPOSSaleToJournal(
                receiptId,
                totalAmount,
                Convert.ToDecimal(p["totalCost"]),
                paymentMethod,
                DateTime.Parse(p["saleDate"]?.ToString()),
                p["description"]?.ToString());

            // Final validation: ensure journal has valid lines
            if (journal.Lines == null || journal.Lines.Count < 2)
            {
                throw new ArgumentException($"Cannot create POS sale journal: mapped journal has {journal.Lines?.Count ?? 0} lines (need at least 2). Receipt: {receiptId}");
            }

            var result = await _apiClient.CreateJournalAsync(journal);
            await _apiClient.PostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        /// <summary>
        /// Resolve payment method string to account mapping key.
        /// </summary>
        private string ResolvePaymentMethodKey(string paymentMethod)
        {
            if (string.IsNullOrEmpty(paymentMethod)) return "CASH";
            switch (paymentMethod.ToUpper())
            {
                case "CASH": case "เงินสด": return "CASH";
                case "KBANK": return "BANK_KBANK";
                case "KTB": return "BANK_KTB";
                case "PROMPTPAY": case "พร้อมเพย์": return "BANK_KBANK";
                case "CARD": case "บัตรเครดิต": return "BANK_CARD";
                case "DIRECTOR": return "DIRECTOR_ADVANCE";
                default: return "CASH";
            }
        }

        private async Task<string> ProcessPostponePriceDiffJournal(Dictionary<string, object> p)
        {
            var priceDifference = Convert.ToDecimal(p["priceDifference"]);
            if (priceDifference == 0)
                throw new ArgumentException($"Cannot create postpone price-diff journal: priceDifference is 0. Reservation #{p["reservationId"]}");

            var journal = _mapper.MapPostponePriceDiffToJournal(
                Convert.ToInt32(p["reservationId"]),
                priceDifference,
                DateTime.Parse(p["rescheduleDate"]?.ToString()),
                p["customerName"]?.ToString());

            var result = await _apiClient.CreateJournalAsync(journal);
            await _apiClient.PostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessPartialRefundJournal(Dictionary<string, object> p)
        {
            var refundAmount = Convert.ToDecimal(p["refundAmount"]);
            var retainedAmount = Convert.ToDecimal(p["retainedAmount"]);
            if (refundAmount <= 0 && retainedAmount <= 0)
                throw new ArgumentException($"Cannot create partial refund journal: refundAmount is {refundAmount} and retainedAmount is {retainedAmount} (at least one must be > 0). Reservation #{p["reservationId"]}");

            var journal = _mapper.MapPartialRefundToJournal(
                Convert.ToInt32(p["reservationId"]),
                refundAmount,
                retainedAmount,
                p["paymentMethod"]?.ToString(),
                DateTime.Parse(p["refundDate"]?.ToString()),
                p["customerName"]?.ToString(),
                p["reason"]?.ToString());

            var result = await _apiClient.CreateJournalAsync(journal);
            await _apiClient.PostJournalAsync(result.data.Id);
            return result.data.Id.ToString();
        }

        private async Task<string> ProcessDamageChargeJournal(Dictionary<string, object> p)
        {
            var damageAmount = Convert.ToDecimal(p["damageAmount"]);
            var missingItemsAmount = Convert.ToDecimal(p["missingItemsAmount"]);
            if (damageAmount <= 0 && missingItemsAmount <= 0)
                throw new ArgumentException($"Cannot create damage charge journal: damageAmount is {damageAmount} and missingItemsAmount is {missingItemsAmount} (at least one must be > 0). Reservation #{p["reservationId"]}");

            var invoice = _mapper.MapDamageChargeToInvoice(
                Convert.ToInt32(p["reservationId"]),
                damageAmount,
                missingItemsAmount,
                DateTime.Parse(p["chargeDate"]?.ToString()),
                p["customerName"]?.ToString(),
                p["description"]?.ToString());

            var result = await _apiClient.CreateInvoiceAsync(invoice);
            return result.data.Id.ToString();
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
    }
}
