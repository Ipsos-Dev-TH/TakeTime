using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// Maps TakeTime data models to Nexaacc Accounting API DTOs.
    /// Uses Accounting_Account_Mapping table for account code resolution.
    /// </summary>
    public class AccountingDataMapper
    {
        private readonly code _Code = new code();
        private readonly string _connectionString;
        private Dictionary<string, Guid> _accountMappingCache;
        private DateTime _mappingCacheExpiry = DateTime.MinValue;

        public AccountingDataMapper()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public AccountingDataMapper(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ──────────────────────────────────────────────
        // Account Mapping Resolution
        // ──────────────────────────────────────────────

        /// <summary>
        /// Resolves a TakeTime code (e.g., "CASH", "KBANK", "ROOM_REVENUE") to a Nexaacc Account GUID.
        /// </summary>
        public Guid GetAccountId(string takeTimeCode)
        {
            EnsureMappingCache();
            if (_accountMappingCache.TryGetValue(takeTimeCode.ToUpper(), out var accountId))
            {
                if (accountId == Guid.Empty)
                    throw new Exception($"Account mapping '{takeTimeCode}' has empty GUID. กรุณากดปุ่ม 'ดึง Chart of Accounts' ในหน้า Accounting Integration Settings เพื่ออัพเดท Account ID");
                return accountId;
            }

            throw new Exception($"No Nexaacc account mapping found for TakeTime code: {takeTimeCode}. กรุณาตั้งค่าใน Accounting_Account_Mapping table");
        }

        public bool TryGetAccountId(string takeTimeCode, out Guid accountId)
        {
            EnsureMappingCache();
            return _accountMappingCache.TryGetValue(takeTimeCode.ToUpper(), out accountId);
        }

        private void EnsureMappingCache()
        {
            if (_accountMappingCache != null && DateTime.Now < _mappingCacheExpiry)
                return;

            _accountMappingCache = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            DataTable dt = _Code.DatabaseQuerySafe(_connectionString,
                "SELECT TakeTime_Code, Nexaacc_AccountId FROM Accounting_Account_Mapping WHERE Is_Active = 1 AND Nexaacc_AccountId IS NOT NULL",
                null);

            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string ttCode = row["TakeTime_Code"]?.ToString();
                    if (!string.IsNullOrEmpty(ttCode) && row["Nexaacc_AccountId"] != DBNull.Value)
                    {
                        _accountMappingCache[ttCode] = (Guid)row["Nexaacc_AccountId"];
                    }
                }
            }

            _mappingCacheExpiry = DateTime.Now.AddMinutes(10);
        }

        // ──────────────────────────────────────────────
        // Reservation → Journal Entry (Deposit/Payment received)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps a reservation deposit to a CashReceipts journal entry.
        /// DR: Cash/Bank account  CR: Advance Deposits (เงินรับล่วงหน้า)
        /// </summary>
        public CreateJournalEntryRequest MapDepositToJournal(
            int reservationId, decimal amount, string paymentMethod, DateTime paymentDate, string customerName,
            string paymentAccountId = null, string documentNumber = null)
        {
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"RES-{reservationId}-DEP";
            return new CreateJournalEntryRequest
            {
                EntryDate = paymentDate,
                JournalType = NexaaccJournalType.CashReceipts,
                Description = $"รับมัดจำ {refStr} - การจอง #{reservationId} ({customerName})",
                Reference = refStr,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = cashAccountId,
                        DebitAmount = amount,
                        CreditAmount = 0,
                        Description = $"รับมัดจำจากลูกค้า - {paymentMethod}"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositAccountId,
                        DebitAmount = 0,
                        CreditAmount = amount,
                        Description = $"เงินรับล่วงหน้า - การจอง #{reservationId}"
                    }
                }
            };
        }

        /// <summary>
        /// Maps a full payment to a CashReceipts journal entry.
        /// DR: Cash/Bank account  CR: Room Revenue
        /// </summary>
        public CreateJournalEntryRequest MapPaymentToJournal(
            int reservationId, decimal amount, string paymentMethod, DateTime paymentDate,
            string customerName, bool hasVat = false,
            string revenueType = null, string paymentAccountId = null, string documentNumber = null)
        {
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            var revenueAccountId = !string.IsNullOrEmpty(revenueType) ? GetAccountId(revenueType) : GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = amount,
                    CreditAmount = 0,
                    Description = $"รับชำระค่าห้องพัก - {paymentMethod}",
                }
            };

            if (hasVat)
            {
                decimal vatAmount = Math.Round(amount * 7m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal netAmount = amount - vatAmount;
                var outputVatAccountId = GetAccountId("OUTPUT_VAT");

                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = revenueAccountId,
                    DebitAmount = 0,
                    CreditAmount = netAmount,
                    Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = outputVatAccountId,
                    DebitAmount = 0,
                    CreditAmount = vatAmount,
                    Description = "ภาษีขาย 7%",
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = revenueAccountId,
                    DebitAmount = 0,
                    CreditAmount = amount,
                    Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                });
            }

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"RES-{reservationId}-PAY";
            return new CreateJournalEntryRequest
            {
                EntryDate = paymentDate,
                JournalType = NexaaccJournalType.CashReceipts,
                Description = $"รับชำระค่าห้องพัก {refStr} - การจอง #{reservationId} ({customerName})",
                Reference = refStr,
                Lines = lines
            };
        }

        /// <summary>
        /// Maps checkout to a revenue recognition journal entry.
        /// ปกติ: DR Advance Deposits, CR Room Revenue (รับรู้รายได้)
        /// ถ้ามี damageAmount > 0: CR แยกระหว่าง Room Revenue (depositAmount-damageAmount)
        ///                         และ Other Income (damageAmount)
        /// </summary>
        public CreateJournalEntryRequest MapCheckoutToJournal(
            int reservationId, decimal depositAmount, string customerName, DateTime checkoutDate,
            decimal damageAmount = 0, string reservationRef = null)
        {
            if (depositAmount <= 0)
                throw new ArgumentException($"MapCheckoutToJournal: depositAmount ต้อง > 0 (ได้ {depositAmount})");
            if (damageAmount < 0)
                damageAmount = 0;
            if (damageAmount > depositAmount)
                damageAmount = depositAmount; // ค่าเสียหายเกินมัดจำ ต้องไปลงในใบสำคัญจ่าย/ใบแจ้งหนี้แยก

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var revenueAccountId = GetAccountId("ROOM_REVENUE");
            decimal revenuePortion = depositAmount - damageAmount;

            string refStr = !string.IsNullOrEmpty(reservationRef) ? reservationRef : $"RES-{reservationId}-CHK";

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositAmount,
                    CreditAmount = 0,
                    Description = $"ตัดเงินรับล่วงหน้า - การจอง #{reservationId}"
                }
            };

            if (revenuePortion > 0)
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = revenueAccountId,
                    DebitAmount = 0,
                    CreditAmount = revenuePortion,
                    Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}"
                });
            }

            if (damageAmount > 0)
            {
                Guid otherIncomeAccountId;
                try { otherIncomeAccountId = GetAccountId("DAMAGE_INCOME"); }
                catch { otherIncomeAccountId = GetAccountId("OTHER_INCOME"); }

                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = otherIncomeAccountId,
                    DebitAmount = 0,
                    CreditAmount = damageAmount,
                    Description = $"ค่าเสียหาย/หักจากมัดจำ - การจอง #{reservationId}"
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = checkoutDate,
                JournalType = NexaaccJournalType.Sales,
                Description = damageAmount > 0
                    ? $"รับรู้รายได้ Checkout (หักค่าเสียหาย {damageAmount:N2}) - การจอง #{reservationId} ({customerName})"
                    : $"รับรู้รายได้ Checkout - การจอง #{reservationId} ({customerName})",
                Reference = refStr,
                Lines = lines
            };
        }

        /// <summary>
        /// Forfeit deposit (ลูกค้า no-show หรือ cancel ไม่คืนเงิน):
        /// DR Advance Deposits, CR Forfeit/Other Income
        /// </summary>
        public CreateJournalEntryRequest MapForfeitDepositToJournal(
            int reservationId, decimal depositAmount, string customerName, DateTime forfeitDate, string reason = null)
        {
            if (depositAmount <= 0)
                throw new ArgumentException($"MapForfeitDepositToJournal: depositAmount ต้อง > 0 (ได้ {depositAmount})");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            Guid forfeitIncomeAccountId;
            try { forfeitIncomeAccountId = GetAccountId("FORFEIT_INCOME"); }
            catch
            {
                try { forfeitIncomeAccountId = GetAccountId("OTHER_INCOME"); }
                catch { forfeitIncomeAccountId = GetAccountId("ROOM_REVENUE"); }
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = forfeitDate,
                JournalType = NexaaccJournalType.Sales,
                Description = $"ริบมัดจำ ({reason ?? "ไม่มาเข้าพัก/ยกเลิกผิดเงื่อนไข"}) - การจอง #{reservationId} ({customerName})",
                Reference = $"RES-{reservationId}-FORFEIT",
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositAccountId,
                        DebitAmount = depositAmount,
                        CreditAmount = 0,
                        Description = "ตัดเงินรับล่วงหน้า"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = forfeitIncomeAccountId,
                        DebitAmount = 0,
                        CreditAmount = depositAmount,
                        Description = $"รายได้จากการริบมัดจำ - การจอง #{reservationId}"
                    }
                }
            };
        }

        /// <summary>
        /// Maps a refund to a CashPayments journal entry.
        /// DR: Advance Deposits  CR: Cash/Bank
        /// </summary>
        public CreateJournalEntryRequest MapRefundToJournal(
            int reservationId, decimal refundAmount, string paymentMethod, DateTime refundDate, string customerName)
        {
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);

            return new CreateJournalEntryRequest
            {
                EntryDate = refundDate,
                JournalType = NexaaccJournalType.CashPayments,
                Description = $"คืนเงินมัดจำ - การจอง #{reservationId} ({customerName})",
                Reference = $"RES-{reservationId}-REF",
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositAccountId,
                        DebitAmount = refundAmount,
                        CreditAmount = 0,
                        Description = "ล้างเงินรับล่วงหน้า",
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = cashAccountId,
                        DebitAmount = 0,
                        CreditAmount = refundAmount,
                        Description = $"คืนเงิน - {paymentMethod}",
                    }
                }
            };
        }

        // ──────────────────────────────────────────────
        // Payment Voucher → Journal Entry (Expense)
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps a payment voucher to a CashPayments journal entry.
        /// รองรับ Input VAT (ภาษีซื้อ) และ WHT (ภาษีหัก ณ ที่จ่าย) ตามหลักบัญชีไทย
        ///
        /// กรณีไม่มี VAT/WHT:
        ///   DR: Expense account (amount)  CR: Cash/Bank (amount)
        ///
        /// กรณีมี VAT + WHT:
        ///   DR: Expense account (netAmount)
        ///   DR: Input VAT (vatAmount)
        ///   CR: Cash/Bank (amount - whtAmount)
        ///   CR: WHT Payable (whtAmount)
        /// </summary>
        public CreateJournalEntryRequest MapVoucherToJournal(
            int voucherId, string expenseCategory, decimal amount, string paymentMethod,
            DateTime voucherDate, string description, string payeeName,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0,
            string paymentAccountId = null, string expenseAccountId = null,
            List<ExpenseLine> expenseLines = null, string documentNumber = null)
        {
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            var lines = new List<JournalEntryLineRequest>();

            bool hasMultipleLines = expenseLines != null && expenseLines.Count > 0;

            if (hasMultipleLines)
            {
                // Multiple DR lines — one per expense category
                // Line amounts are already pre-VAT (user enters "จำนวนเงิน ไม่รวมภาษี")
                decimal drTotal = 0;
                foreach (var el in expenseLines)
                {
                    var lineAccId = ResolveAccountId(el.AccountId) ?? GetExpenseCategoryAccountId(el.Category);
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = lineAccId,
                        DebitAmount = el.Amount,
                        CreditAmount = 0,
                        Description = el.Description,
                    });
                    drTotal += el.Amount;
                }

                if (hasInputVat)
                {
                    // VAT = post-VAT total minus sum of pre-VAT lines (exact, no rounding drift)
                    decimal vatAmount = amount - drTotal;
                    if (vatAmount > 0)
                    {
                        var inputVatAccountId = GetAccountId("INPUT_VAT");
                        lines.Add(new JournalEntryLineRequest
                        {
                            AccountId = inputVatAccountId,
                            DebitAmount = vatAmount,
                            CreditAmount = 0,
                            Description = "ภาษีซื้อ 7%",
                        });
                    }
                }
            }
            else
            {
                // Single DR line (backward compatible)
                var expAccId = ResolveAccountId(expenseAccountId) ?? GetExpenseCategoryAccountId(expenseCategory);

                if (hasInputVat)
                {
                    decimal vatAmount = Math.Round(amount * 7m / 107m, 2, MidpointRounding.AwayFromZero);
                    decimal netAmount = amount - vatAmount;
                    var inputVatAccountId = GetAccountId("INPUT_VAT");

                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = expAccId,
                        DebitAmount = netAmount,
                        CreditAmount = 0,
                        Description = description,
                    });
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = inputVatAccountId,
                        DebitAmount = vatAmount,
                        CreditAmount = 0,
                        Description = "ภาษีซื้อ 7%",
                    });
                }
                else
                {
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = expAccId,
                        DebitAmount = amount,
                        CreditAmount = 0,
                        Description = description,
                    });
                }
            }

            // CR: ภาษีหัก ณ ที่จ่าย (ถ้ามี)
            if (whtAmount > 0)
            {
                var whtAccountId = GetAccountId("WHT_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = whtAccountId,
                    DebitAmount = 0,
                    CreditAmount = whtAmount,
                    Description = $"ภาษีหัก ณ ที่จ่าย {whtRate}%",
                });
            }

            // CR: เงินสด/ธนาคาร (ยอดจ่ายจริง = amount - WHT)
            decimal cashPaid = amount - whtAmount;
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = cashAccountId,
                DebitAmount = 0,
                CreditAmount = cashPaid,
                Description = $"จ่ายเงิน - {paymentMethod}",
            });

            // DR=CR validation before returning
            decimal totalDr = 0, totalCr = 0;
            foreach (var l in lines) { totalDr += l.DebitAmount; totalCr += l.CreditAmount; }
            if (totalDr != totalCr)
            {
                throw new ArgumentException(
                    $"MapVoucherToJournal DR ({totalDr:#,##0.00}) ≠ CR ({totalCr:#,##0.00}). " +
                    $"Voucher #{voucherId}, amount={amount}, whtAmount={whtAmount}, " +
                    $"hasInputVat={hasInputVat}, lines={expenseLines?.Count ?? 0}");
            }

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"PV-{voucherId}";
            return new CreateJournalEntryRequest
            {
                EntryDate = voucherDate,
                JournalType = NexaaccJournalType.CashPayments,
                Description = $"ใบสำคัญจ่าย {refStr} - {description} ({payeeName})",
                Reference = refStr,
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Product / Room Charge → Journal Entry
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps a room charge (product sale) to a Sales journal entry.
        /// DR: Room AR  CR: Product Sales Revenue + COGS entries
        /// </summary>
        public CreateJournalEntryRequest MapRoomChargeToJournal(
            int reservationId, decimal salesAmount, decimal costAmount, DateTime chargeDate, string description)
        {
            var roomArAccountId = GetAccountId("ROOM_AR");
            var productRevenueAccountId = GetAccountId("PRODUCT_REVENUE");
            var cogsAccountId = GetAccountId("COGS");
            var inventoryAccountId = GetAccountId("INVENTORY");

            var lines = new List<JournalEntryLineRequest>
            {
                // Revenue side
                new JournalEntryLineRequest
                {
                    AccountId = roomArAccountId,
                    DebitAmount = salesAmount,
                    CreditAmount = 0,
                    Description = $"ค่าสินค้า charge เข้าห้อง - การจอง #{reservationId}",
                },
                new JournalEntryLineRequest
                {
                    AccountId = productRevenueAccountId,
                    DebitAmount = 0,
                    CreditAmount = salesAmount,
                    Description = "รายได้ขายสินค้า",
                }
            };

            // COGS side (if cost is available)
            if (costAmount > 0)
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = cogsAccountId,
                    DebitAmount = costAmount,
                    CreditAmount = 0,
                    Description = "ต้นทุนสินค้าขาย",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = inventoryAccountId,
                    DebitAmount = 0,
                    CreditAmount = costAmount,
                    Description = "ลดสินค้าคงเหลือ",
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = chargeDate,
                JournalType = NexaaccJournalType.Sales,
                Description = $"Room Charge - การจอง #{reservationId} - {description}",
                Reference = $"RC-{reservationId}",
                Lines = lines
            };
        }

        /// <summary>
        /// Maps stock purchase/receiving to a Purchase journal entry.
        /// ซื้อเชื่อ: DR Inventory, CR Accounts Payable
        /// ซื้อสด:   DR Inventory, CR Cash/Bank
        /// มี VAT:   DR Inventory (net), DR Input VAT, CR Cash/AP (total)
        /// </summary>
        public CreateJournalEntryRequest MapStockInToJournal(
            int productId, string productName, decimal totalCost, DateTime receiveDate,
            string supplierName, string paymentMethod = null, bool hasInputVat = false)
        {
            var inventoryAccountId = GetAccountId("INVENTORY");
            bool isCashPurchase = !string.IsNullOrEmpty(paymentMethod);

            // บัญชีด้านเครดิต: ซื้อสด = Cash/Bank, ซื้อเชื่อ = AP
            Guid creditAccountId = isCashPurchase
                ? GetPaymentMethodAccountId(paymentMethod)
                : GetAccountId("ACCOUNTS_PAYABLE");

            string creditDescription = isCashPurchase
                ? $"จ่ายค่าสินค้า - {paymentMethod}"
                : $"เจ้าหนี้การค้า - {supplierName}";

            var lines = new List<JournalEntryLineRequest>();

            if (hasInputVat)
            {
                decimal vatAmount = Math.Round(totalCost * 7m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal netCost = totalCost - vatAmount;
                var inputVatAccountId = GetAccountId("INPUT_VAT");

                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = inventoryAccountId,
                    DebitAmount = netCost,
                    CreditAmount = 0,
                    Description = $"สินค้าคงเหลือ - {productName}",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = inputVatAccountId,
                    DebitAmount = vatAmount,
                    CreditAmount = 0,
                    Description = "ภาษีซื้อ 7%",
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = inventoryAccountId,
                    DebitAmount = totalCost,
                    CreditAmount = 0,
                    Description = $"สินค้าคงเหลือ - {productName}",
                });
            }

            lines.Add(new JournalEntryLineRequest
            {
                AccountId = creditAccountId,
                DebitAmount = 0,
                CreditAmount = totalCost,
                Description = creditDescription,
            });

            return new CreateJournalEntryRequest
            {
                EntryDate = receiveDate,
                JournalType = isCashPurchase ? NexaaccJournalType.CashPayments : NexaaccJournalType.Purchase,
                Description = $"รับสินค้าเข้าสต็อก - {productName} ({supplierName})",
                Reference = $"SI-{productId}-{receiveDate:yyyyMMddHHmmss}",
                Lines = lines
            };
        }

        /// <summary>
        /// COGS journal เมื่อสินค้าออกจากสต็อก (ขาย / room charge):
        ///   DR COGS                qty * costPrice
        ///       CR Inventory       qty * costPrice
        /// ใช้ cost price (ไม่ใช่ sell price) — รายได้รับรู้แยกผ่าน receipt
        /// </summary>
        public CreateJournalEntryRequest MapStockOutCogsToJournal(
            int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime outDate, string reason, string reference = null)
        {
            decimal totalCost = Math.Round(quantity * costPerUnit, 2);
            if (totalCost <= 0)
                throw new ArgumentException($"MapStockOutCogsToJournal: ต้นทุนรวม = {totalCost} (ต้อง > 0). product={productName} qty={quantity} cost={costPerUnit}");

            var cogsAccountId = GetAccountId("COGS");
            var inventoryAccountId = GetAccountId("INVENTORY");
            string refStr = !string.IsNullOrEmpty(reference) ? reference : $"SO-{productId}-{outDate:yyyyMMddHHmmss}";

            return new CreateJournalEntryRequest
            {
                EntryDate = outDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ตัดต้นทุนสินค้า - {productName} ({reason ?? "ขาย"})",
                Reference = refStr,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = cogsAccountId,
                        DebitAmount = totalCost,
                        CreditAmount = 0,
                        Description = $"ต้นทุนสินค้าขาย - {productName} qty={quantity:N2}"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = inventoryAccountId,
                        DebitAmount = 0,
                        CreditAmount = totalCost,
                        Description = $"ตัดสต็อก - {productName} qty={quantity:N2}"
                    }
                }
            };
        }

        /// <summary>
        /// Reverse COGS journal เมื่อยกเลิก room charge (คืนสินค้ากลับสต็อก):
        ///   DR Inventory       qty * costPrice    (เอาสต็อกกลับ)
        ///       CR COGS        qty * costPrice    (ยกเลิกต้นทุน)
        /// </summary>
        public CreateJournalEntryRequest MapStockOutCogsReversalToJournal(
            int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime reverseDate, string reason, string reference)
        {
            decimal totalCost = Math.Round(quantity * costPerUnit, 2);
            if (totalCost <= 0)
                throw new ArgumentException($"MapStockOutCogsReversalToJournal: ต้นทุนรวม = {totalCost} (ต้อง > 0)");

            var cogsAccountId = GetAccountId("COGS");
            var inventoryAccountId = GetAccountId("INVENTORY");

            return new CreateJournalEntryRequest
            {
                EntryDate = reverseDate,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการต้นทุน - {productName} ({reason ?? "ยกเลิก room charge"})",
                Reference = reference,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = inventoryAccountId, DebitAmount = totalCost, CreditAmount = 0, Description = $"คืนสต็อก - {productName} qty={quantity:N2}" },
                    new JournalEntryLineRequest { AccountId = cogsAccountId, DebitAmount = 0, CreditAmount = totalCost, Description = $"กลับ COGS - {productName}" }
                }
            };
        }

        /// <summary>
        /// Stock adjustment ตามผลการนับ:
        ///   gain (actual > expected): DR Inventory, CR Inventory Variance (รายได้)
        ///   loss (actual < expected): DR Inventory Variance (ค่าใช้จ่าย), CR Inventory
        /// </summary>
        public CreateJournalEntryRequest MapStockAdjustmentToJournal(
            int productId, string productName, decimal quantityDiff, decimal costPerUnit,
            DateTime adjustDate, string reason, long adjustmentLogId)
        {
            decimal absQty = Math.Abs(quantityDiff);
            decimal totalCost = Math.Round(absQty * costPerUnit, 2);
            if (totalCost <= 0)
                throw new ArgumentException($"MapStockAdjustmentToJournal: total = {totalCost} (ต้อง > 0)");

            var inventoryAccountId = GetAccountId("INVENTORY");
            var varianceAccountId = GetAccountId("INVENTORY_VARIANCE");
            bool isGain = quantityDiff > 0;

            var lines = new List<JournalEntryLineRequest>();
            if (isGain)
            {
                lines.Add(new JournalEntryLineRequest { AccountId = inventoryAccountId, DebitAmount = totalCost, CreditAmount = 0, Description = $"ปรับเพิ่มสต็อก - {productName} qty=+{absQty:N2}" });
                lines.Add(new JournalEntryLineRequest { AccountId = varianceAccountId, DebitAmount = 0, CreditAmount = totalCost, Description = $"ส่วนต่างจากการนับสต็อก (เกิน)" });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest { AccountId = varianceAccountId, DebitAmount = totalCost, CreditAmount = 0, Description = $"ส่วนต่างจากการนับสต็อก (ขาด)" });
                lines.Add(new JournalEntryLineRequest { AccountId = inventoryAccountId, DebitAmount = 0, CreditAmount = totalCost, Description = $"ปรับลดสต็อก - {productName} qty=-{absQty:N2}" });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = adjustDate,
                JournalType = NexaaccJournalType.General,
                Description = $"นับสต็อก ({(isGain ? "เกิน" : "ขาด")}) - {productName}: {reason ?? "ไม่ระบุ"}",
                Reference = $"SADJ-{adjustmentLogId}",
                Lines = lines
            };
        }

        /// <summary>
        /// Stock write-off (สินค้าเสียหาย/หมดอายุ/สูญหาย):
        ///   DR Stock Loss Expense
        ///       CR Inventory
        /// </summary>
        public CreateJournalEntryRequest MapStockWriteOffToJournal(
            int productId, string productName, decimal quantity, decimal costPerUnit,
            DateTime writeOffDate, string reason, long adjustmentLogId)
        {
            decimal totalCost = Math.Round(quantity * costPerUnit, 2);
            if (totalCost <= 0)
                throw new ArgumentException($"MapStockWriteOffToJournal: total = {totalCost} (ต้อง > 0)");

            var stockLossAccountId = GetAccountId("STOCK_LOSS_EXPENSE");
            var inventoryAccountId = GetAccountId("INVENTORY");

            return new CreateJournalEntryRequest
            {
                EntryDate = writeOffDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ตัดจำหน่ายสินค้า ({reason ?? "เสียหาย"}) - {productName}",
                Reference = $"SWO-{adjustmentLogId}",
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = stockLossAccountId, DebitAmount = totalCost, CreditAmount = 0, Description = $"ค่าใช้จ่ายสินค้าสูญเสีย - {productName} qty={quantity:N2}" },
                    new JournalEntryLineRequest { AccountId = inventoryAccountId, DebitAmount = 0, CreditAmount = totalCost, Description = $"ตัดสต็อก - {productName}" }
                }
            };
        }

        // ──────────────────────────────────────────────
        // Receipt / Document Mapping
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps a TakeTime receipt to a Nexaacc document.
        /// </summary>
        public CreateDocumentRequest MapReceiptToDocument(
            int reservationId, string receiptNumber, decimal totalAmount, decimal vatAmount,
            DateTime receiptDate, Guid? contactId, string description)
        {
            return new CreateDocumentRequest
            {
                DocumentType = NexaaccDocumentType.Receipt,
                ContactId = contactId,
                DocumentDate = receiptDate,
                Reference = $"RES-{reservationId}-{receiptNumber}",
                Notes = description,
                Lines = new List<DocumentLineRequest>
                {
                    new DocumentLineRequest
                    {
                        Description = $"ค่าห้องพัก - การจอง #{reservationId}",
                        Quantity = 1,
                        UnitPrice = totalAmount - vatAmount,
                        VatRate = vatAmount > 0 ? 7 : 0,
                        AccountId = TryGetAccountId("ROOM_REVENUE", out var accId) ? accId : (Guid?)null
                    }
                }
            };
        }

        /// <summary>
        /// Maps a TakeTime credit note to a Nexaacc credit note document.
        /// </summary>
        public CreateDocumentRequest MapCreditNoteToDocument(
            string creditNoteNumber, decimal totalAmount, decimal vatAmount,
            DateTime creditNoteDate, Guid? contactId, string reason)
        {
            return new CreateDocumentRequest
            {
                DocumentType = NexaaccDocumentType.CreditNote,
                ContactId = contactId,
                DocumentDate = creditNoteDate,
                Reference = creditNoteNumber,
                Notes = reason,
                Lines = new List<DocumentLineRequest>
                {
                    new DocumentLineRequest
                    {
                        Description = $"ใบลดหนี้ - {reason}",
                        Quantity = 1,
                        UnitPrice = totalAmount - vatAmount,
                        VatRate = vatAmount > 0 ? 7m : 0m
                    }
                }
            };
        }

        // ──────────────────────────────────────────────
        // Contact Mapping
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps TakeTime customer data to a Nexaacc contact.
        /// </summary>
        public CreateContactRequest MapCustomerToContact(
            string name, string phone, string email, string address, string taxId = null)
        {
            return new CreateContactRequest
            {
                Name = name ?? "ลูกค้าทั่วไป",
                Phone = phone,
                Email = email,
                Address = address,
                TaxId = taxId,
                IsCustomer = true,
                IsSupplier = false,
                ContactType = NexaaccContactType.Individual
            };
        }

        // ──────────────────────────────────────────────
        // Product Mapping
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps a TakeTime product to a Nexaacc product.
        /// </summary>
        public CreateProductRequest MapProductToNexaacc(
            int productId, string productName, string description, decimal sellingPrice,
            decimal costPrice, string unit, string categoryName)
        {
            Guid? salesAccId = TryGetAccountId("PRODUCT_REVENUE", out var s) ? s : (Guid?)null;
            Guid? purchaseAccId = TryGetAccountId("COGS", out var p) ? p : (Guid?)null;
            Guid? inventoryAccId = TryGetAccountId("INVENTORY", out var i) ? i : (Guid?)null;

            return new CreateProductRequest
            {
                Code = $"TT-{productId:D5}",
                Name = productName,
                Description = description,
                SellingPrice = sellingPrice,
                CostPrice = costPrice,
                Unit = unit ?? "ชิ้น",
                SalesAccountId = salesAccId,
                PurchaseAccountId = purchaseAccId,
                InventoryAccountId = inventoryAccId,
                TrackInventory = true
            };
        }

        // ──────────────────────────────────────────────
        // Payroll → Journal Entry
        // ──────────────────────────────────────────────

        /// <summary>
        /// Maps payroll payment to a CashPayments journal entry.
        /// ตามหลักบัญชีไทย payroll ต้องแยก:
        ///
        /// DR: Salary Expense (เงินเดือน gross)
        /// DR: SSF Employer Expense (ประกันสังคมส่วนนายจ้าง)
        /// CR: Cash/Bank (เงินจ่ายจริง = gross - SSF employee - WHT)
        /// CR: SSF Payable (ประกันสังคมค้างจ่าย = ส่วนลูกจ้าง + ส่วนนายจ้าง)
        /// CR: WHT Payable (ภาษีหัก ณ ที่จ่ายค้างจ่าย)
        /// </summary>
        public CreateJournalEntryRequest MapPayrollToJournal(
            decimal totalSalary, DateTime payDate, string period,
            decimal socialSecurityEmployee = 0, decimal socialSecurityEmployer = 0,
            decimal whtAmount = 0)
        {
            var salaryAccountId = GetAccountId("SALARY_EXPENSE");
            var cashAccountId = GetAccountId("CASH");

            var lines = new List<JournalEntryLineRequest>();

            // DR: เงินเดือนและค่าแรง (gross)
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = salaryAccountId,
                DebitAmount = totalSalary,
                CreditAmount = 0,
                Description = $"เงินเดือนและค่าแรง - {period}",
            });

            // DR: ประกันสังคมส่วนนายจ้าง (ถ้ามี)
            if (socialSecurityEmployer > 0)
            {
                Guid ssfExpenseId = TryGetAccountId("SSF_EMPLOYER_EXPENSE", out var se) ? se : salaryAccountId;
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = ssfExpenseId,
                    DebitAmount = socialSecurityEmployer,
                    CreditAmount = 0,
                    Description = $"ประกันสังคมส่วนนายจ้าง - {period}",
                });
            }

            // CR: ประกันสังคมค้างจ่าย (ส่วนลูกจ้าง + ส่วนนายจ้าง)
            decimal totalSSF = socialSecurityEmployee + socialSecurityEmployer;
            if (totalSSF > 0)
            {
                Guid ssfPayableId = TryGetAccountId("SSF_PAYABLE", out var sp) ? sp : GetAccountId("ACCOUNTS_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = ssfPayableId,
                    DebitAmount = 0,
                    CreditAmount = totalSSF,
                    Description = $"ประกันสังคมค้างจ่าย (ลูกจ้าง {socialSecurityEmployee:N2} + นายจ้าง {socialSecurityEmployer:N2})",
                });
            }

            // CR: ภาษีหัก ณ ที่จ่ายค้างจ่าย (ถ้ามี)
            if (whtAmount > 0)
            {
                var whtPayableId = GetAccountId("WHT_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = whtPayableId,
                    DebitAmount = 0,
                    CreditAmount = whtAmount,
                    Description = $"ภาษีเงินได้หัก ณ ที่จ่าย - {period}",
                });
            }

            // CR: เงินจ่ายจริง (gross - SSF employee - WHT)
            decimal netPay = totalSalary - socialSecurityEmployee - whtAmount;
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = cashAccountId,
                DebitAmount = 0,
                CreditAmount = netPay,
                Description = $"จ่ายเงินเดือนสุทธิ - {period}",
            });

            return new CreateJournalEntryRequest
            {
                EntryDate = payDate,
                JournalType = NexaaccJournalType.CashPayments,
                Description = $"จ่ายเงินเดือนพนักงาน - งวด {period}",
                Reference = $"PR-{payDate:yyyyMM}",
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Helper: Resolve explicit account ID (GUID string → Guid?)
        // ──────────────────────────────────────────────

        private Guid? ResolveAccountId(string accountIdStr)
        {
            if (string.IsNullOrEmpty(accountIdStr)) return null;
            if (Guid.TryParse(accountIdStr, out Guid id) && id != Guid.Empty)
                return id;
            return null;
        }

        // ──────────────────────────────────────────────
        // Helper: Payment Method → Account (fallback when no explicit ID)
        // ──────────────────────────────────────────────

        private Guid GetPaymentMethodAccountId(string paymentMethod)
        {
            string pm = (paymentMethod ?? "").Trim();
            string pmUpper = pm.ToUpper();
            string mappingKey;

            switch (pmUpper)
            {
                case "CASH": mappingKey = "CASH"; break;
                case "KBANK": mappingKey = "BANK_KBANK"; break;
                case "KTB": mappingKey = "BANK_KTB"; break;
                case "PROMPTPAY": mappingKey = "BANK_KBANK"; break;
                case "CARD": mappingKey = "BANK_CARD"; break;
                case "DIRECTOR": mappingKey = "DIRECTOR_ADVANCE"; break;
                default:
                    if (pm.Contains("กสิกร") || pmUpper.Contains("KBANK"))
                        mappingKey = "BANK_KBANK";
                    else if (pm.Contains("กรุงไทย") || pmUpper.Contains("KTB"))
                        mappingKey = "BANK_KTB";
                    else if (pm.Contains("กรุงเทพ") || pmUpper.Contains("BBL"))
                        mappingKey = "BANK_BBL";
                    else if (pm.Contains("ไทยพาณิชย์") || pmUpper.Contains("SCB"))
                        mappingKey = "BANK_SCB";
                    else if (pm.Contains("พร้อมเพย์") || pmUpper.Contains("PROMPTPAY") || pmUpper.Contains("QR"))
                        mappingKey = "BANK_KBANK";
                    else if (pm.Contains("บัตร") || pmUpper.Contains("CARD") || pm.Contains("เครดิต") || pm.Contains("เดบิต"))
                        mappingKey = "BANK_CARD";
                    else if (pm.Contains("กรรมการ") || pmUpper.Contains("DIRECTOR") || pm.Contains("ทดรอง"))
                        mappingKey = "DIRECTOR_ADVANCE";
                    else if (pm.Contains("เงินสด"))
                        mappingKey = "CASH";
                    else if (pm.Contains("โอน") || pm.Contains("ธนาคาร") || pmUpper.Contains("TRANSFER"))
                        mappingKey = "BANK_KBANK";
                    else if (pm.Contains("เช็ค") || pmUpper.Contains("CHECK") || pmUpper.Contains("CHEQUE"))
                        mappingKey = "BANK_KBANK";
                    else
                    {
                        mappingKey = "CASH";
                        try { _Code.Logs(_connectionString, "AccountingSync", $"GetPaymentMethodAccountId: ไม่รู้จัก '{pm}' — default เป็น CASH", "SYSTEM"); } catch { }
                    }
                    break;
            }

            if (!TryGetAccountId(mappingKey, out var accountId))
            {
                if (mappingKey != "CASH" && TryGetAccountId("CASH", out accountId))
                {
                    try { _Code.Logs(_connectionString, "AccountingSync", $"GetPaymentMethodAccountId: ไม่พบ mapping '{mappingKey}' สำหรับ '{pm}' — fallback เป็น CASH", "SYSTEM"); } catch { }
                    return accountId;
                }
                return GetAccountId(mappingKey);
            }
            return accountId;
        }

        private Guid GetExpenseCategoryAccountId(string category)
        {
            // Try exact match first
            if (TryGetAccountId($"EXPENSE_{(category ?? "OTHER").ToUpper()}", out var accountId))
                return accountId;

            // Fallback to generic expense
            return GetAccountId("EXPENSE_OTHER");
        }

        // ──────────────────────────────────────────────
        // Cancellation No Refund → Journal Entry
        // Forfeited deposit recognized as Other Income
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapCancelNoRefundToJournal(
            int reservationId, decimal depositAmount, string customerName, DateTime cancelDate)
        {
            var advanceDepositId = GetAccountId("ADVANCE_DEPOSIT");
            Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");

            return new CreateJournalEntryRequest
            {
                EntryDate = cancelDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ยกเลิกการจอง (ไม่คืนเงิน) - {customerName} - #{reservationId}",
                Reference = $"CANCEL-NR-{reservationId}",
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositId,
                        DebitAmount = depositAmount,
                        CreditAmount = 0,
                        Description = $"ล้างเงินรับล่วงหน้า - การจอง #{reservationId}",
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = otherIncomeId,
                        DebitAmount = 0,
                        CreditAmount = depositAmount,
                        Description = $"รายได้จากการยึดมัดจำ - {customerName}",
                    }
                }
            };
        }

        // ──────────────────────────────────────────────
        // POS Sale → Journal Entry
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapPOSSaleToJournal(
            string receiptId, decimal totalAmount, decimal totalCost, string paymentMethod,
            DateTime saleDate, string description)
        {
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);
            var productRevenueId = GetAccountId("PRODUCT_REVENUE");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = totalAmount,
                    CreditAmount = 0,
                    Description = $"รับชำระ POS - {receiptId}",
                },
                new JournalEntryLineRequest
                {
                    AccountId = productRevenueId,
                    DebitAmount = 0,
                    CreditAmount = totalAmount,
                    Description = $"รายได้ขายสินค้า - {description}",
                }
            };

            // Add COGS entries if cost is available
            if (totalCost > 0)
            {
                var cogsId = GetAccountId("COGS");
                var inventoryId = GetAccountId("INVENTORY");

                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = cogsId,
                    DebitAmount = totalCost,
                    CreditAmount = 0,
                    Description = "ต้นทุนสินค้าขาย",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = inventoryId,
                    DebitAmount = 0,
                    CreditAmount = totalCost,
                    Description = "ตัดสินค้าคงเหลือ",
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = saleDate,
                JournalType = NexaaccJournalType.Sales,
                Description = $"ขายสินค้า POS - {receiptId}",
                Reference = $"POS-{receiptId}",
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Postpone Price Diff → Journal Entry
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapPostponePriceDiffToJournal(
            int reservationId, decimal priceDifference, DateTime rescheduleDate, string customerName)
        {
            if (priceDifference == 0)
                throw new ArgumentException($"Price difference is zero for reservation #{reservationId}. No journal entry needed.");

            var roomArId = GetAccountId("ROOM_AR");
            var roomRevenueId = GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>();

            if (priceDifference > 0)
            {
                // New price higher: customer owes more
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = roomArId,
                    DebitAmount = priceDifference,
                    CreditAmount = 0,
                    Description = $"ส่วนต่างราคาเพิ่ม - เลื่อนวัน #{reservationId}",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = roomRevenueId,
                    DebitAmount = 0,
                    CreditAmount = priceDifference,
                    Description = $"รายได้ส่วนต่างราคา - {customerName}",
                });
            }
            else
            {
                // New price lower: credit to customer
                decimal absDiff = Math.Abs(priceDifference);
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = roomRevenueId,
                    DebitAmount = absDiff,
                    CreditAmount = 0,
                    Description = $"ปรับลดราคา - เลื่อนวัน #{reservationId}",
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = roomArId,
                    DebitAmount = 0,
                    CreditAmount = absDiff,
                    Description = $"เครดิตส่วนต่างราคา - {customerName}",
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = rescheduleDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ปรับส่วนต่างราคาเลื่อนวัน - {customerName} - #{reservationId}",
                Reference = $"POSTPONE-DIFF-{reservationId}",
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Partial Refund → Journal Entry
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapPartialRefundToJournal(
            int reservationId, decimal refundAmount, decimal retainedAmount,
            string paymentMethod, DateTime refundDate, string customerName, string reason)
        {
            var advanceDepositId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);

            var lines = new List<JournalEntryLineRequest>
            {
                // Debit: Clear advance deposit for total original amount
                new JournalEntryLineRequest
                {
                    AccountId = advanceDepositId,
                    DebitAmount = refundAmount + retainedAmount,
                    CreditAmount = 0,
                    Description = $"ล้างเงินรับล่วงหน้า - #{reservationId}",
                }
            };

            // Credit: Refund portion back to customer (only if > 0)
            if (refundAmount > 0)
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = 0,
                    CreditAmount = refundAmount,
                    Description = $"คืนเงินบางส่วน - {customerName}",
                });
            }

            // Retained amount recognized as income
            if (retainedAmount > 0)
            {
                Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = otherIncomeId,
                    DebitAmount = 0,
                    CreditAmount = retainedAmount,
                    Description = $"รายได้จากค่าธรรมเนียมยกเลิก - {reason}",
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = refundDate,
                JournalType = NexaaccJournalType.CashPayments,
                Description = $"คืนเงินบางส่วน - {customerName} - #{reservationId} ({reason})",
                Reference = $"PARTIAL-REFUND-{reservationId}",
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Damage/Missing Items Charge → Journal Entry
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapDamageChargeToJournal(
            int reservationId, decimal damageAmount, decimal missingItemsAmount,
            DateTime chargeDate, string customerName, string description)
        {
            decimal totalCharge = damageAmount + missingItemsAmount;
            var roomArId = GetAccountId("ROOM_AR");
            Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = roomArId,
                    DebitAmount = totalCharge,
                    CreditAmount = 0,
                    Description = $"ค่าเสียหาย/ของหาย - #{reservationId}",
                },
                new JournalEntryLineRequest
                {
                    AccountId = otherIncomeId,
                    DebitAmount = 0,
                    CreditAmount = totalCharge,
                    Description = $"รายได้ค่าเสียหาย - {customerName} - {description}",
                }
            };

            return new CreateJournalEntryRequest
            {
                EntryDate = chargeDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ค่าเสียหาย/ของหาย - {customerName} - #{reservationId}",
                Reference = $"DMG-{reservationId}",
                Lines = lines
            };
        }

        // ══════════════════════════════════════════════
        // Integration Invoice Mappers (ยอดขาย/รายรับ)
        // ใช้กับ /api/integration/invoices
        // ══════════════════════════════════════════════

        public CreateIntegrationInvoiceRequest MapDepositToInvoice(
            int reservationId, decimal amount, string paymentMethod, DateTime paymentDate, string customerName,
            string paymentAccountId = null)
        {
            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = paymentDate,
                CustomerName = customerName,
                Reference = $"RES-{reservationId}-DEP",
                IncludeVat = true,
                Description = $"รับมัดจำ - การจอง #{reservationId} ({customerName})",
                PaymentMethod = paymentMethod,
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "เงินรับล่วงหน้า",
                        Description = $"เงินรับล่วงหน้า - การจอง #{reservationId}",
                        Quantity = 1,
                        UnitPrice = amount,
                        AccountId = GetAccountId("ADVANCE_DEPOSIT"),
                    }
                }
            };
        }

        public CreateIntegrationInvoiceRequest MapPaymentToInvoice(
            int reservationId, decimal amount, string paymentMethod, DateTime paymentDate,
            string customerName, bool hasVat = false,
            string revenueType = null, string paymentAccountId = null)
        {
            var lines = new List<IntegrationLineRequest>();
            var revenueAccountId = !string.IsNullOrEmpty(revenueType) ? GetAccountId(revenueType) : GetAccountId("ROOM_REVENUE");
            string revenueLabel = !string.IsNullOrEmpty(revenueType) ? revenueType : "ค่าห้องพัก";

            lines.Add(new IntegrationLineRequest
            {
                ItemName = revenueLabel,
                Description = $"รายได้ - การจอง #{reservationId}",
                Quantity = 1,
                UnitPrice = amount,
                VatRate = hasVat ? 7 : 0,
                AccountId = revenueAccountId,
            });

            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = paymentDate,
                CustomerName = customerName,
                Reference = $"RES-{reservationId}-PAY",
                IncludeVat = true,
                Description = $"รับชำระ - การจอง #{reservationId} ({customerName})",
                PaymentMethod = paymentMethod,
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Multi-Line Receipt Mappers
        // รองรับใบเสร็จที่มีหลายรายการ (ห้องพัก + อาหาร + สินค้า + ส่วนลด) →
        // แยกบัญชีรายได้ใน NextAcc พร้อม support การหักมัดจำในใบเสร็จเดียว
        // ──────────────────────────────────────────────

        /// <summary>
        /// 1 line ของใบเสร็จ — ใช้ส่งจาก TakeTime เข้า payload
        /// </summary>
        public class ReceiptLineSpec
        {
            public int? ProductTypeId { get; set; }
            public int? ProductId { get; set; }
            public string Description { get; set; }
            public decimal Quantity { get; set; } = 1;
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }       // = Quantity * UnitPrice (อาจ override)
            public decimal Discount { get; set; }     // ส่วนลด (จะลงเป็นบรรทัดติดลบ/contra)
            public string Unit { get; set; }
            public string RevenueTypeOverride { get; set; }   // ถ้าระบุจะใช้แทนการ lookup จาก ProductTypeId
        }

        /// <summary>
        /// หา revenue mapping code จาก ProductType_ID (Account_ProductType.Revenue_Mapping_Code)
        /// fallback: ROOM_REVENUE
        /// </summary>
        public string GetProductTypeRevenueCode(int? productTypeId)
        {
            if (!productTypeId.HasValue || productTypeId.Value <= 0) return "ROOM_REVENUE";
            try
            {
                var dt = _Code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Revenue_Mapping_Code FROM Account_ProductType WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", productTypeId.Value } });
                if (dt?.Rows.Count > 0)
                {
                    string code = dt.Rows[0]["Revenue_Mapping_Code"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(code)) return code.Trim();
                }
            }
            catch { /* fallback */ }
            return "ROOM_REVENUE";
        }

        /// <summary>
        /// แปลง ReceiptLineSpec list เป็น IntegrationLineRequest list
        /// (ใช้ใน DOCUMENT mode — สร้าง invoice ใน NextAcc)
        /// แต่ละ line ผูกกับ revenue account ที่เหมาะสมกับ ProductTypeId
        /// </summary>
        private List<IntegrationLineRequest> BuildIntegrationLines(List<ReceiptLineSpec> lines, bool hasVat)
        {
            var result = new List<IntegrationLineRequest>();
            foreach (var line in lines)
            {
                if (line == null) continue;
                decimal amount = line.Amount > 0 ? line.Amount : line.Quantity * line.UnitPrice;
                if (amount == 0) continue;

                string revCode = !string.IsNullOrEmpty(line.RevenueTypeOverride)
                    ? line.RevenueTypeOverride
                    : GetProductTypeRevenueCode(line.ProductTypeId);

                result.Add(new IntegrationLineRequest
                {
                    ItemName = !string.IsNullOrEmpty(line.Description) ? line.Description : revCode,
                    Description = line.Description,
                    Quantity = line.Quantity > 0 ? line.Quantity : 1,
                    UnitPrice = line.Quantity > 0 ? amount / line.Quantity : amount,
                    DiscountAmount = line.Discount > 0 ? line.Discount : (decimal?)null,
                    Unit = line.Unit,
                    VatRate = hasVat ? 7 : 0,
                    AccountId = GetAccountId(revCode)
                });
            }
            return result;
        }

        /// <summary>
        /// ใบกำกับภาษีที่มีหลายบรรทัดรายได้ + รองรับ "หักมัดจำ" (depositApplied):
        ///   Total revenue ของ invoice = ผลรวม lines (เช่น 3700)
        ///   ลูกค้าจ่ายเงินสดจริง = Total - depositApplied (เช่น 2700)
        ///   ระบบจะสร้าง adjustment journal เพิ่มเติม:
        ///     DR Advance Deposit (depositApplied)  ← ตัดเจ้าหนี้
        ///     CR Cash/Bank (depositApplied)        ← หักเงินสดที่ invoice บันทึกไว้
        ///   ผลสุทธิ: Cash net = 2700, Advance Deposit cleared, Revenue = 3700 ✓
        /// </summary>
        public CreateIntegrationInvoiceRequest MapMultiLinePaymentToInvoice(
            int reservationId, List<ReceiptLineSpec> lines, string paymentMethod, DateTime paymentDate,
            string customerName, bool hasVat = false, string paymentAccountId = null,
            decimal depositApplied = 0, string receiptNumber = null)
        {
            if (lines == null || lines.Count == 0)
                throw new ArgumentException("MapMultiLinePaymentToInvoice: lines ห้ามว่าง");

            var integrationLines = BuildIntegrationLines(lines, hasVat);
            if (integrationLines.Count == 0)
                throw new ArgumentException("MapMultiLinePaymentToInvoice: ไม่มี line ที่มียอด > 0");

            string refStr = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : $"RES-{reservationId}-PAY";

            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = paymentDate,
                CustomerName = customerName,
                Reference = refStr,
                ExternalRef = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : null,
                ReplaceExistingForSource = !string.IsNullOrEmpty(receiptNumber),
                IncludeVat = hasVat,
                Description = depositApplied > 0
                    ? $"ใบเสร็จ {refStr} — การจอง #{reservationId} ({customerName}) | หักมัดจำ {depositApplied:N2}"
                    : $"ใบเสร็จ {refStr} — การจอง #{reservationId} ({customerName})",
                PaymentMethod = paymentMethod,
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = integrationLines
            };
        }

        /// <summary>
        /// Compound journal สำหรับใบเสร็จที่มี:
        ///   - หลาย revenue lines (รายได้แยกบัญชี)
        ///   - หักมัดจำ (DR Advance Deposit)
        ///   - เงินสดส่วนเพิ่ม (DR Cash/Bank)
        ///   - VAT (ถ้ามี)
        /// </summary>
        public CreateJournalEntryRequest MapMultiLinePaymentToJournal(
            int reservationId, List<ReceiptLineSpec> lines, string paymentMethod, DateTime paymentDate,
            string customerName, bool hasVat = false, string paymentAccountId = null,
            decimal depositApplied = 0, string documentNumber = null)
        {
            if (lines == null || lines.Count == 0)
                throw new ArgumentException("MapMultiLinePaymentToJournal: lines ห้ามว่าง");

            decimal totalGross = 0m;
            foreach (var l in lines)
            {
                decimal amt = l.Amount > 0 ? l.Amount : l.Quantity * l.UnitPrice;
                totalGross += amt - l.Discount;
            }
            if (totalGross <= 0)
                throw new ArgumentException($"MapMultiLinePaymentToJournal: totalGross = {totalGross} (ต้อง > 0)");

            if (depositApplied < 0) depositApplied = 0;
            if (depositApplied > totalGross) depositApplied = totalGross; // กัน inconsistency

            decimal cashPortion = totalGross - depositApplied;
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");

            decimal vatAmount = 0m, totalNet = totalGross;
            if (hasVat)
            {
                vatAmount = Math.Round(totalGross * 7m / 107m, 2, MidpointRounding.AwayFromZero);
                totalNet = totalGross - vatAmount;
            }

            var journalLines = new List<JournalEntryLineRequest>();

            // DR Cash (จำนวนเงินสดที่รับจริง)
            if (cashPortion > 0)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = cashPortion,
                    CreditAmount = 0,
                    Description = $"รับชำระเงินสด - {paymentMethod}"
                });
            }
            // DR Advance Deposit (หักมัดจำ — ตัดเจ้าหนี้)
            if (depositApplied > 0)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositApplied,
                    CreditAmount = 0,
                    Description = $"หักจากเงินรับล่วงหน้า — การจอง #{reservationId}"
                });
            }

            // CR Revenue lines (แยกตาม ProductType)
            // VAT proration ใช้ "สัดส่วน lineGross / totalGross" เพื่อให้ผลรวม VAT พอดีและ lineNet ไม่ติดลบ
            // (แทนการใช้สูตร 7/107 ต่อบรรทัดที่อาจ drift จนบรรทัดสุดท้ายต้องดูดส่วนเกิน)
            decimal vatProrationRunning = 0m;
            int lastNonZeroIdx = -1;
            for (int k = lines.Count - 1; k >= 0; k--)
            {
                decimal g = (lines[k].Amount > 0 ? lines[k].Amount : lines[k].Quantity * lines[k].UnitPrice) - lines[k].Discount;
                if (g != 0) { lastNonZeroIdx = k; break; }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                var l = lines[i];
                decimal lineGross = (l.Amount > 0 ? l.Amount : l.Quantity * l.UnitPrice) - l.Discount;
                if (lineGross == 0) continue;

                string revCode = !string.IsNullOrEmpty(l.RevenueTypeOverride)
                    ? l.RevenueTypeOverride
                    : GetProductTypeRevenueCode(l.ProductTypeId);

                decimal lineNet = lineGross;
                if (hasVat && totalGross > 0)
                {
                    decimal lineVat;
                    if (i == lastNonZeroIdx)
                    {
                        // บรรทัดสุดท้ายดูดส่วนต่างเพื่อให้ผลรวม VAT พอดี
                        lineVat = vatAmount - vatProrationRunning;
                    }
                    else
                    {
                        // โปรเรทตามสัดส่วน lineGross / totalGross
                        lineVat = Math.Round(vatAmount * lineGross / totalGross, 2, MidpointRounding.AwayFromZero);
                    }
                    // กัน lineNet ติดลบจาก rounding drift
                    if (lineVat < 0) lineVat = 0;
                    if (lineVat > lineGross) lineVat = lineGross;
                    vatProrationRunning += lineVat;
                    lineNet = lineGross - lineVat;
                }

                if (lineNet > 0)
                {
                    journalLines.Add(new JournalEntryLineRequest
                    {
                        AccountId = GetAccountId(revCode),
                        DebitAmount = 0,
                        CreditAmount = lineNet,
                        Description = !string.IsNullOrEmpty(l.Description)
                            ? $"{l.Description} ({revCode})"
                            : $"รายได้ {revCode} - การจอง #{reservationId}"
                    });
                }
                else if (lineNet < 0)
                {
                    // ส่วนลด/contra revenue → DR เข้า revenue account หรือ DISCOUNT_EXPENSE
                    Guid discAcc;
                    try { discAcc = GetAccountId("DISCOUNT_EXPENSE"); }
                    catch { discAcc = GetAccountId(revCode); }
                    journalLines.Add(new JournalEntryLineRequest
                    {
                        AccountId = discAcc,
                        DebitAmount = -lineNet, // กลับเครื่องหมาย
                        CreditAmount = 0,
                        Description = $"ส่วนลด - {l.Description}"
                    });
                }
            }

            // CR Output VAT
            if (hasVat && vatAmount > 0)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = GetAccountId("OUTPUT_VAT"),
                    DebitAmount = 0,
                    CreditAmount = vatAmount,
                    Description = "ภาษีขาย 7%"
                });
            }

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"RES-{reservationId}-PAY";
            return new CreateJournalEntryRequest
            {
                EntryDate = paymentDate,
                JournalType = NexaaccJournalType.CashReceipts,
                Description = depositApplied > 0
                    ? $"รับชำระ {refStr} (หักมัดจำ {depositApplied:N2}) — การจอง #{reservationId} ({customerName})"
                    : $"รับชำระ {refStr} — การจอง #{reservationId} ({customerName})",
                Reference = refStr,
                Lines = journalLines
            };
        }

        /// <summary>
        /// Adjustment journal สำหรับ DOCUMENT mode เมื่อมีการหักมัดจำ:
        ///   หลังจากสร้าง invoice เต็มจำนวน (3700) — invoice บันทึก DR Cash 3700, CR Revenue 3700
        ///   adjustment นี้: DR Advance Deposit (depositApplied), CR Cash/Bank (depositApplied)
        ///   ผลสุทธิ: Cash จริง = 3700 - 1000 = 2700, Advance Deposit ลด 1000, Revenue 3700 ✓
        /// </summary>
        public CreateJournalEntryRequest MapDepositAppliedAdjustment(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedAdjustment: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ" : $"RES-{reservationId}-DEPADJ";

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ปรับปรุง — หักมัดจำในใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositAccountId,
                        DebitAmount = depositApplied,
                        CreditAmount = 0,
                        Description = "ตัดเงินรับล่วงหน้า (ใช้ในใบเสร็จ)"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = cashAccountId,
                        DebitAmount = 0,
                        CreditAmount = depositApplied,
                        Description = "ลดเงินสดที่ invoice บันทึกเกินจริง"
                    }
                }
            };
        }

        /// <summary>
        /// Counter-adjustment สำหรับการ void ใบเสร็จที่เคยหักมัดจำ:
        ///   DR Cash/Bank, CR Advance Deposit (กลับ adjustment เดิมเพื่อเอาเจ้าหนี้กลับมา)
        /// </summary>
        public CreateJournalEntryRequest MapDepositAppliedAdjustmentReverse(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedAdjustmentReverse: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ-REV" : $"RES-{reservationId}-DEPADJ-REV";

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการ adjustment — ยกเลิกการหักมัดจำในใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = cashAccountId,
                        DebitAmount = depositApplied,
                        CreditAmount = 0,
                        Description = "กลับลด cash"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = advanceDepositAccountId,
                        DebitAmount = 0,
                        CreditAmount = depositApplied,
                        Description = "เอาเงินรับล่วงหน้ากลับมา (เจ้าหนี้)"
                    }
                }
            };
        }

        public CreateIntegrationInvoiceRequest MapCheckoutToInvoice(
            int reservationId, decimal depositAmount, string customerName, DateTime checkoutDate)
        {
            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = checkoutDate,
                CustomerName = customerName,
                Reference = $"RES-{reservationId}-CHK",
                Description = $"รับรู้รายได้ Checkout - การจอง #{reservationId} ({customerName})",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "ค่าห้องพัก",
                        Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                        Quantity = 1, UnitPrice = depositAmount,
                        AccountId = GetAccountId("ROOM_REVENUE"),
                    }
                }
            };
        }

        public CreateIntegrationInvoiceRequest MapPOSSaleToInvoice(
            string receiptId, decimal totalAmount, decimal totalCost, string paymentMethod,
            DateTime saleDate, string description)
        {
            var lines = new List<IntegrationLineRequest>
            {
                new IntegrationLineRequest
                {
                    ItemName = !string.IsNullOrEmpty(description) ? description : "สินค้า POS",
                    Description = $"รายได้ขายสินค้า - {description}",
                    Quantity = 1, UnitPrice = totalAmount,
                    AccountId = GetAccountId("PRODUCT_REVENUE"),
                }
            };

            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = saleDate,
                CustomerName = "ลูกค้าทั่วไป",
                Reference = $"POS-{receiptId}",
                Description = $"ขายสินค้า POS - {receiptId}",
                PaymentMethod = paymentMethod,
                PaymentAccountId = GetPaymentMethodAccountId(paymentMethod),
                Lines = lines
            };
        }

        public CreateIntegrationInvoiceRequest MapRoomChargeToInvoice(
            int reservationId, decimal salesAmount, decimal costAmount, DateTime chargeDate, string description)
        {
            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = chargeDate,
                CustomerName = $"ลูกค้า - การจอง #{reservationId}",
                Reference = $"RC-{reservationId}",
                Description = $"ชาร์จสินค้าเข้าห้อง - #{reservationId} - {description}",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = !string.IsNullOrEmpty(description) ? description : "ชาร์จสินค้าเข้าห้อง",
                        Description = description,
                        Quantity = 1, UnitPrice = salesAmount,
                        AccountId = GetAccountId("PRODUCT_REVENUE"),
                    }
                }
            };
        }

        public CreateIntegrationInvoiceRequest MapCancelNoRefundToInvoice(
            int reservationId, decimal depositAmount, string customerName, DateTime cancelDate)
        {
            Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");

            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = cancelDate,
                CustomerName = customerName,
                Reference = $"CANCEL-NR-{reservationId}",
                Description = $"ยกเลิกการจอง (ไม่คืนเงิน) - {customerName} - #{reservationId}",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "รายได้จากการยึดมัดจำ",
                        Description = $"รายได้จากการยึดมัดจำ - การจอง #{reservationId}",
                        Quantity = 1, UnitPrice = depositAmount,
                        AccountId = otherIncomeId,
                    }
                }
            };
        }

        public CreateIntegrationInvoiceRequest MapDamageChargeToInvoice(
            int reservationId, decimal damageAmount, decimal missingItemsAmount,
            DateTime chargeDate, string customerName, string description)
        {
            decimal totalCharge = damageAmount + missingItemsAmount;
            Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");

            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = chargeDate,
                CustomerName = customerName,
                Reference = $"DMG-{reservationId}",
                Description = $"ค่าเสียหาย/ของหาย - {customerName} - #{reservationId}",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "ค่าเสียหาย/ของหาย",
                        Description = $"ค่าเสียหาย/ของหาย - {description}",
                        Quantity = 1, UnitPrice = totalCharge,
                        AccountId = otherIncomeId,
                    }
                }
            };
        }

        // ══════════════════════════════════════════════
        // Integration Expense Mappers (ค่าใช้จ่าย)
        // ใช้กับ /api/integration/expenses
        // ══════════════════════════════════════════════

        public CreateIntegrationExpenseRequest MapVoucherToExpense(
            int voucherId, string expenseCategory, decimal amount, string paymentMethod,
            DateTime voucherDate, string description, string payeeName,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0,
            string paymentAccountId = null, string expenseAccountId = null,
            List<ExpenseLine> expenseLines = null, string documentNumber = null)
        {
            var lines = new List<IntegrationLineRequest>();
            bool hasMultipleLines = expenseLines != null && expenseLines.Count > 0;

            if (hasMultipleLines)
            {
                foreach (var el in expenseLines)
                {
                    var lineAccId = ResolveAccountId(el.AccountId) ?? GetExpenseCategoryAccountId(el.Category);
                    string lineItemName = !string.IsNullOrEmpty(el.Category) ? el.Category : "ค่าใช้จ่าย";

                    lines.Add(new IntegrationLineRequest
                    {
                        ItemName = lineItemName,
                        Description = el.Description,
                        Quantity = 1,
                        UnitPrice = el.Amount,
                        VatRate = hasInputVat ? 7 : 0,
                        WithholdingTaxRate = whtRate > 0 ? whtRate : 0,
                        AccountId = lineAccId,
                    });
                }
            }
            else
            {
                var expAccId = ResolveAccountId(expenseAccountId) ?? GetExpenseCategoryAccountId(expenseCategory);
                string itemName = !string.IsNullOrEmpty(expenseCategory) ? expenseCategory
                    : !string.IsNullOrEmpty(description) ? description : "ค่าใช้จ่าย";

                lines.Add(new IntegrationLineRequest
                {
                    ItemName = itemName,
                    Description = description,
                    Quantity = 1,
                    UnitPrice = amount,
                    VatRate = hasInputVat ? 7 : 0,
                    WithholdingTaxRate = whtRate,
                    AccountId = expAccId,
                });
            }

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"PV-{voucherId}";
            return new CreateIntegrationExpenseRequest
            {
                DocumentDate = voucherDate,
                SupplierName = payeeName,
                Reference = refStr,
                ExternalRef = !string.IsNullOrEmpty(documentNumber) ? documentNumber : null,
                ReplaceExistingForSource = !string.IsNullOrEmpty(documentNumber),
                Description = $"ใบสำคัญจ่าย {refStr} - {description} ({payeeName})",
                IncludeVat = hasMultipleLines ? false : hasInputVat,
                PaymentMethod = paymentMethod,
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = lines
            };
        }

        // ══════════════════════════════════════════════
        // Integration Credit Note (ใบลดหนี้)
        // ใช้กับ /api/integration/credit-notes
        // ══════════════════════════════════════════════

        public InboundCreditNoteRequest MapRefundToCreditNote(
            int reservationId, decimal refundAmount, string customerName,
            DateTime refundDate, string reason, string originalReceiptRef = null)
        {
            return new InboundCreditNoteRequest
            {
                ExternalRef = $"CN-RES-{reservationId}",
                OriginalInvoiceRef = originalReceiptRef,
                CustomerName = customerName,
                DocumentDate = refundDate,
                Reason = reason ?? "คืนเงิน",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "คืนเงิน",
                        Description = $"คืนเงิน - การจอง #{reservationId} - {reason}",
                        Quantity = 1,
                        UnitPrice = refundAmount,
                    }
                }
            };
        }

        // ══════════════════════════════════════════════
        // Integration Debit Note (ใบเพิ่มหนี้)
        // ใช้กับ /api/integration/debit-notes
        // ══════════════════════════════════════════════

        public InboundDebitNoteRequest MapDamageChargeToDebitNote(
            int reservationId, decimal damageAmount, decimal missingItemsAmount,
            string customerName, DateTime chargeDate, string description,
            string originalReceiptRef = null)
        {
            decimal totalCharge = damageAmount + missingItemsAmount;
            return new InboundDebitNoteRequest
            {
                ExternalRef = $"DN-DMG-{reservationId}",
                OriginalInvoiceRef = originalReceiptRef,
                CustomerName = customerName,
                DocumentDate = chargeDate,
                Reason = description ?? "ค่าเสียหาย/ของหาย",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "ค่าเสียหาย/ของหาย",
                        Description = $"ค่าเสียหาย/ของหาย - การจอง #{reservationId} - {description}",
                        Quantity = 1,
                        UnitPrice = totalCharge,
                    }
                }
            };
        }

        // ══════════════════════════════════════════════
        // Integration Customer (ลูกค้า/ผู้ติดต่อ)
        // ใช้กับ /api/integration/customers
        // ══════════════════════════════════════════════

        public InboundCustomerRequest MapCustomerToIntegration(
            string name, string phone, string email, string address,
            string taxId = null, string externalId = null)
        {
            return new InboundCustomerRequest
            {
                ExternalId = externalId,
                Name = name ?? "ลูกค้าทั่วไป",
                Phone = phone,
                Email = email,
                Address = address,
                TaxId = taxId,
                IsCustomer = true,
                IsSupplier = false,
                ContactType = "Individual"
            };
        }

        public InboundCustomerRequest MapSupplierToIntegration(
            string name, string phone, string email, string address,
            string taxId = null, string externalId = null)
        {
            return new InboundCustomerRequest
            {
                ExternalId = externalId,
                Name = name,
                Phone = phone,
                Email = email,
                Address = address,
                TaxId = taxId,
                IsCustomer = false,
                IsSupplier = true,
                ContactType = "JuristicPerson"
            };
        }

        public CreateIntegrationExpenseRequest MapStockInToExpense(
            int productId, string productName, decimal totalCost, string paymentMethod,
            DateTime purchaseDate, string supplierName, bool hasInputVat = false)
        {
            return new CreateIntegrationExpenseRequest
            {
                DocumentDate = purchaseDate,
                SupplierName = supplierName ?? "ซัพพลายเออร์",
                Reference = $"STOCK-IN-{productId}",
                Description = $"ซื้อสินค้า - {productName}",
                IncludeVat = hasInputVat,
                PaymentMethod = paymentMethod,
                PaymentAccountId = GetPaymentMethodAccountId(paymentMethod),
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = !string.IsNullOrEmpty(productName) ? productName : "สินค้า",
                        Description = $"สินค้า - {productName}",
                        Quantity = 1, UnitPrice = totalCost,
                        VatRate = hasInputVat ? 7 : 0,
                        AccountId = GetAccountId("INVENTORY"),
                    }
                }
            };
        }

        public CreateIntegrationExpenseRequest MapPayrollToExpense(
            string period, decimal totalSalary, decimal totalSsf, decimal totalWht,
            DateTime payrollDate, string description)
        {
            var lines = new List<IntegrationLineRequest>
            {
                new IntegrationLineRequest
                {
                    ItemName = "เงินเดือน",
                    Description = $"เงินเดือน - {period}",
                    Quantity = 1, UnitPrice = totalSalary,
                    AccountId = GetAccountId("SALARY_EXPENSE"),
                }
            };

            if (totalSsf > 0)
            {
                Guid ssfExpenseId = TryGetAccountId("SSF_EMPLOYER_EXPENSE", out var se)
                    ? se : GetAccountId("SALARY_EXPENSE");
                lines.Add(new IntegrationLineRequest
                {
                    ItemName = "ประกันสังคม (ส่วนนายจ้าง)",
                    Description = $"ประกันสังคม (ส่วนนายจ้าง) - {period}",
                    Quantity = 1, UnitPrice = totalSsf,
                    AccountId = ssfExpenseId,
                });
            }

            return new CreateIntegrationExpenseRequest
            {
                DocumentDate = payrollDate,
                SupplierName = "เงินเดือนพนักงาน",
                Reference = $"PAYROLL-{period}",
                Description = description ?? $"เงินเดือน - {period}",
                IncludeVat = false,
                PaymentMethod = "CASH",
                PaymentAccountId = GetAccountId("CASH"),
                Lines = lines
            };
        }
    }
}
