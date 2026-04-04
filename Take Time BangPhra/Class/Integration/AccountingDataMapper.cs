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
        private readonly code _code = new code();
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
                return accountId;

            throw new Exception($"No Nexaacc account mapping found for TakeTime code: {takeTimeCode}. Please configure in Accounting_Account_Mapping table.");
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

            DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                "SELECT TakeTime_Code, Nexaacc_AccountId FROM Accounting_Account_Mapping WHERE Is_Active = 1 AND Nexaacc_AccountId IS NOT NULL",
                null);

            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string code = row["TakeTime_Code"]?.ToString();
                    if (!string.IsNullOrEmpty(code) && row["Nexaacc_AccountId"] != DBNull.Value)
                    {
                        _accountMappingCache[code] = (Guid)row["Nexaacc_AccountId"];
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
            int reservationId, decimal amount, string paymentMethod, DateTime paymentDate, string customerName)
        {
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");

            return new CreateJournalEntryRequest
            {
                entryDate = paymentDate,
                journalType = NexaaccJournalType.CashReceipts,
                description = $"รับมัดจำ - การจอง #{reservationId} ({customerName})",
                reference = $"RES-{reservationId}-DEP",
                lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        accountId = cashAccountId,
                        debitAmount = amount,
                        creditAmount = 0,
                        description = $"รับมัดจำจากลูกค้า - {paymentMethod}",
                        lineOrder = 1
                    },
                    new JournalEntryLineRequest
                    {
                        accountId = advanceDepositAccountId,
                        debitAmount = 0,
                        creditAmount = amount,
                        description = $"เงินรับล่วงหน้า - การจอง #{reservationId}",
                        lineOrder = 2
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
            string customerName, bool hasVat = false)
        {
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);
            var revenueAccountId = GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    accountId = cashAccountId,
                    debitAmount = amount,
                    creditAmount = 0,
                    description = $"รับชำระค่าห้องพัก - {paymentMethod}",
                    lineOrder = 1
                }
            };

            if (hasVat)
            {
                decimal vatAmount = Math.Round(amount * 7 / 107, 2);
                decimal netAmount = amount - vatAmount;
                var outputVatAccountId = GetAccountId("OUTPUT_VAT");

                lines.Add(new JournalEntryLineRequest
                {
                    accountId = revenueAccountId,
                    debitAmount = 0,
                    creditAmount = netAmount,
                    description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                    lineOrder = 2
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = outputVatAccountId,
                    debitAmount = 0,
                    creditAmount = vatAmount,
                    description = "ภาษีขาย 7%",
                    lineOrder = 3
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = revenueAccountId,
                    debitAmount = 0,
                    creditAmount = amount,
                    description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                    lineOrder = 2
                });
            }

            return new CreateJournalEntryRequest
            {
                entryDate = paymentDate,
                journalType = NexaaccJournalType.CashReceipts,
                description = $"รับชำระค่าห้องพัก - การจอง #{reservationId} ({customerName})",
                reference = $"RES-{reservationId}-PAY",
                lines = lines
            };
        }

        /// <summary>
        /// Maps checkout to a revenue recognition journal entry.
        /// DR: Advance Deposits  CR: Room Revenue (recognize earned revenue)
        /// </summary>
        public CreateJournalEntryRequest MapCheckoutToJournal(
            int reservationId, decimal depositAmount, string customerName, DateTime checkoutDate)
        {
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var revenueAccountId = GetAccountId("ROOM_REVENUE");

            return new CreateJournalEntryRequest
            {
                entryDate = checkoutDate,
                journalType = NexaaccJournalType.Sales,
                description = $"รับรู้รายได้ Checkout - การจอง #{reservationId} ({customerName})",
                reference = $"RES-{reservationId}-CHK",
                lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        accountId = advanceDepositAccountId,
                        debitAmount = depositAmount,
                        creditAmount = 0,
                        description = $"โอนเงินรับล่วงหน้าเป็นรายได้",
                        lineOrder = 1
                    },
                    new JournalEntryLineRequest
                    {
                        accountId = revenueAccountId,
                        debitAmount = 0,
                        creditAmount = depositAmount,
                        description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}",
                        lineOrder = 2
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
                entryDate = refundDate,
                journalType = NexaaccJournalType.CashPayments,
                description = $"คืนเงินมัดจำ - การจอง #{reservationId} ({customerName})",
                reference = $"RES-{reservationId}-REF",
                lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        accountId = advanceDepositAccountId,
                        debitAmount = refundAmount,
                        creditAmount = 0,
                        description = "ล้างเงินรับล่วงหน้า",
                        lineOrder = 1
                    },
                    new JournalEntryLineRequest
                    {
                        accountId = cashAccountId,
                        debitAmount = 0,
                        creditAmount = refundAmount,
                        description = $"คืนเงิน - {paymentMethod}",
                        lineOrder = 2
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
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0)
        {
            var expenseAccountId = GetExpenseCategoryAccountId(expenseCategory);
            var cashAccountId = GetPaymentMethodAccountId(paymentMethod);

            var lines = new List<JournalEntryLineRequest>();
            int lineOrder = 1;

            if (hasInputVat)
            {
                // คำนวณ VAT จากยอดรวม VAT (amount = net + VAT 7%)
                decimal vatAmount = Math.Round(amount * 7 / 107, 2);
                decimal netAmount = amount - vatAmount;
                var inputVatAccountId = GetAccountId("INPUT_VAT");

                // DR: ค่าใช้จ่าย (ยอดก่อน VAT)
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = expenseAccountId,
                    debitAmount = netAmount,
                    creditAmount = 0,
                    description = description,
                    lineOrder = lineOrder++
                });

                // DR: ภาษีซื้อ
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inputVatAccountId,
                    debitAmount = vatAmount,
                    creditAmount = 0,
                    description = "ภาษีซื้อ 7%",
                    lineOrder = lineOrder++
                });
            }
            else
            {
                // DR: ค่าใช้จ่าย (ยอดเต็ม)
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = expenseAccountId,
                    debitAmount = amount,
                    creditAmount = 0,
                    description = description,
                    lineOrder = lineOrder++
                });
            }

            // CR: ภาษีหัก ณ ที่จ่าย (ถ้ามี)
            if (whtAmount > 0)
            {
                var whtAccountId = GetAccountId("WHT_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = whtAccountId,
                    debitAmount = 0,
                    creditAmount = whtAmount,
                    description = $"ภาษีหัก ณ ที่จ่าย {whtRate}%",
                    lineOrder = lineOrder++
                });
            }

            // CR: เงินสด/ธนาคาร (ยอดจ่ายจริง = amount - WHT)
            decimal cashPaid = amount - whtAmount;
            lines.Add(new JournalEntryLineRequest
            {
                accountId = cashAccountId,
                debitAmount = 0,
                creditAmount = cashPaid,
                description = $"จ่ายเงิน - {paymentMethod}",
                lineOrder = lineOrder++
            });

            return new CreateJournalEntryRequest
            {
                entryDate = voucherDate,
                journalType = NexaaccJournalType.CashPayments,
                description = $"ใบสำคัญจ่าย #{voucherId} - {description} ({payeeName})",
                reference = $"PV-{voucherId}",
                lines = lines
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
                    accountId = roomArAccountId,
                    debitAmount = salesAmount,
                    creditAmount = 0,
                    description = $"ค่าสินค้า charge เข้าห้อง - การจอง #{reservationId}",
                    lineOrder = 1
                },
                new JournalEntryLineRequest
                {
                    accountId = productRevenueAccountId,
                    debitAmount = 0,
                    creditAmount = salesAmount,
                    description = "รายได้ขายสินค้า",
                    lineOrder = 2
                }
            };

            // COGS side (if cost is available)
            if (costAmount > 0)
            {
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = cogsAccountId,
                    debitAmount = costAmount,
                    creditAmount = 0,
                    description = "ต้นทุนสินค้าขาย",
                    lineOrder = 3
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inventoryAccountId,
                    debitAmount = 0,
                    creditAmount = costAmount,
                    description = "ลดสินค้าคงเหลือ",
                    lineOrder = 4
                });
            }

            return new CreateJournalEntryRequest
            {
                entryDate = chargeDate,
                journalType = NexaaccJournalType.Sales,
                description = $"Room Charge - การจอง #{reservationId} - {description}",
                reference = $"RC-{reservationId}",
                lines = lines
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
            int lineOrder = 1;

            if (hasInputVat)
            {
                decimal vatAmount = Math.Round(totalCost * 7 / 107, 2);
                decimal netCost = totalCost - vatAmount;
                var inputVatAccountId = GetAccountId("INPUT_VAT");

                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inventoryAccountId,
                    debitAmount = netCost,
                    creditAmount = 0,
                    description = $"สินค้าคงเหลือ - {productName}",
                    lineOrder = lineOrder++
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inputVatAccountId,
                    debitAmount = vatAmount,
                    creditAmount = 0,
                    description = "ภาษีซื้อ 7%",
                    lineOrder = lineOrder++
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inventoryAccountId,
                    debitAmount = totalCost,
                    creditAmount = 0,
                    description = $"สินค้าคงเหลือ - {productName}",
                    lineOrder = lineOrder++
                });
            }

            lines.Add(new JournalEntryLineRequest
            {
                accountId = creditAccountId,
                debitAmount = 0,
                creditAmount = totalCost,
                description = creditDescription,
                lineOrder = lineOrder++
            });

            return new CreateJournalEntryRequest
            {
                entryDate = receiveDate,
                journalType = isCashPurchase ? NexaaccJournalType.CashPayments : NexaaccJournalType.Purchase,
                description = $"รับสินค้าเข้าสต็อก - {productName} ({supplierName})",
                reference = $"SI-{productId}-{receiveDate:yyyyMMdd}",
                lines = lines
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
                documentType = NexaaccDocumentType.Receipt,
                contactId = contactId,
                documentDate = receiptDate,
                reference = $"RES-{reservationId}-{receiptNumber}",
                notes = description,
                lines = new List<DocumentLineRequest>
                {
                    new DocumentLineRequest
                    {
                        description = $"ค่าห้องพัก - การจอง #{reservationId}",
                        quantity = 1,
                        unitPrice = totalAmount - vatAmount,
                        vatPercent = vatAmount > 0 ? 7 : (decimal?)null,
                        accountId = TryGetAccountId("ROOM_REVENUE", out var accId) ? accId : (Guid?)null
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
                documentType = NexaaccDocumentType.CreditNote,
                contactId = contactId,
                documentDate = creditNoteDate,
                reference = creditNoteNumber,
                notes = reason,
                lines = new List<DocumentLineRequest>
                {
                    new DocumentLineRequest
                    {
                        description = $"ใบลดหนี้ - {reason}",
                        quantity = 1,
                        unitPrice = totalAmount - vatAmount,
                        vatPercent = vatAmount > 0 ? 7 : (decimal?)null
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
                name = name ?? "ลูกค้าทั่วไป",
                phone = phone,
                email = email,
                address = address,
                taxId = taxId,
                isCustomer = true,
                isSupplier = false
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
                code = $"TT-{productId:D5}",
                name = productName,
                description = description,
                sellingPrice = sellingPrice,
                costPrice = costPrice,
                unit = unit ?? "ชิ้น",
                salesAccountId = salesAccId,
                purchaseAccountId = purchaseAccId,
                inventoryAccountId = inventoryAccId,
                trackInventory = true
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
            int lineOrder = 1;

            // DR: เงินเดือนและค่าแรง (gross)
            lines.Add(new JournalEntryLineRequest
            {
                accountId = salaryAccountId,
                debitAmount = totalSalary,
                creditAmount = 0,
                description = $"เงินเดือนและค่าแรง - {period}",
                lineOrder = lineOrder++
            });

            // DR: ประกันสังคมส่วนนายจ้าง (ถ้ามี)
            if (socialSecurityEmployer > 0)
            {
                Guid ssfExpenseId = TryGetAccountId("SSF_EMPLOYER_EXPENSE", out var se) ? se : salaryAccountId;
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = ssfExpenseId,
                    debitAmount = socialSecurityEmployer,
                    creditAmount = 0,
                    description = $"ประกันสังคมส่วนนายจ้าง - {period}",
                    lineOrder = lineOrder++
                });
            }

            // CR: ประกันสังคมค้างจ่าย (ส่วนลูกจ้าง + ส่วนนายจ้าง)
            decimal totalSSF = socialSecurityEmployee + socialSecurityEmployer;
            if (totalSSF > 0)
            {
                Guid ssfPayableId = TryGetAccountId("SSF_PAYABLE", out var sp) ? sp : GetAccountId("ACCOUNTS_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = ssfPayableId,
                    debitAmount = 0,
                    creditAmount = totalSSF,
                    description = $"ประกันสังคมค้างจ่าย (ลูกจ้าง {socialSecurityEmployee:N2} + นายจ้าง {socialSecurityEmployer:N2})",
                    lineOrder = lineOrder++
                });
            }

            // CR: ภาษีหัก ณ ที่จ่ายค้างจ่าย (ถ้ามี)
            if (whtAmount > 0)
            {
                var whtPayableId = GetAccountId("WHT_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = whtPayableId,
                    debitAmount = 0,
                    creditAmount = whtAmount,
                    description = $"ภาษีเงินได้หัก ณ ที่จ่าย - {period}",
                    lineOrder = lineOrder++
                });
            }

            // CR: เงินจ่ายจริง (gross - SSF employee - WHT)
            decimal netPay = totalSalary - socialSecurityEmployee - whtAmount;
            lines.Add(new JournalEntryLineRequest
            {
                accountId = cashAccountId,
                debitAmount = 0,
                creditAmount = netPay,
                description = $"จ่ายเงินเดือนสุทธิ - {period}",
                lineOrder = lineOrder++
            });

            return new CreateJournalEntryRequest
            {
                entryDate = payDate,
                journalType = NexaaccJournalType.CashPayments,
                description = $"จ่ายเงินเดือนพนักงาน - งวด {period}",
                reference = $"PR-{payDate:yyyyMM}",
                lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Helper: Payment Method → Account
        // ──────────────────────────────────────────────

        private Guid GetPaymentMethodAccountId(string paymentMethod)
        {
            string mappingKey;
            switch ((paymentMethod ?? "").ToUpper())
            {
                case "CASH": mappingKey = "CASH"; break;
                case "KBANK": mappingKey = "BANK_KBANK"; break;
                case "KTB": mappingKey = "BANK_KTB"; break;
                case "PROMPTPAY": mappingKey = "BANK_KBANK"; break;
                case "CARD": mappingKey = "BANK_CARD"; break;
                case "DIRECTOR": mappingKey = "DIRECTOR_ADVANCE"; break;
                default: mappingKey = "CASH"; break;
            }

            return GetAccountId(mappingKey);
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
                entryDate = cancelDate,
                journalType = NexaaccJournalType.General,
                description = $"ยกเลิกการจอง (ไม่คืนเงิน) - {customerName} - #{reservationId}",
                reference = $"CANCEL-NR-{reservationId}",
                lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        accountId = advanceDepositId,
                        debitAmount = depositAmount,
                        creditAmount = 0,
                        description = $"ล้างเงินรับล่วงหน้า - การจอง #{reservationId}",
                        lineOrder = 1
                    },
                    new JournalEntryLineRequest
                    {
                        accountId = otherIncomeId,
                        debitAmount = 0,
                        creditAmount = depositAmount,
                        description = $"รายได้จากการยึดมัดจำ - {customerName}",
                        lineOrder = 2
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
                    accountId = cashAccountId,
                    debitAmount = totalAmount,
                    creditAmount = 0,
                    description = $"รับชำระ POS - {receiptId}",
                    lineOrder = 1
                },
                new JournalEntryLineRequest
                {
                    accountId = productRevenueId,
                    debitAmount = 0,
                    creditAmount = totalAmount,
                    description = $"รายได้ขายสินค้า - {description}",
                    lineOrder = 2
                }
            };

            // Add COGS entries if cost is available
            if (totalCost > 0)
            {
                var cogsId = GetAccountId("COGS");
                var inventoryId = GetAccountId("INVENTORY");

                lines.Add(new JournalEntryLineRequest
                {
                    accountId = cogsId,
                    debitAmount = totalCost,
                    creditAmount = 0,
                    description = "ต้นทุนสินค้าขาย",
                    lineOrder = 3
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = inventoryId,
                    debitAmount = 0,
                    creditAmount = totalCost,
                    description = "ตัดสินค้าคงเหลือ",
                    lineOrder = 4
                });
            }

            return new CreateJournalEntryRequest
            {
                entryDate = saleDate,
                journalType = NexaaccJournalType.Sales,
                description = $"ขายสินค้า POS - {receiptId}",
                reference = $"POS-{receiptId}",
                lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Postpone Price Diff → Journal Entry
        // ──────────────────────────────────────────────

        public CreateJournalEntryRequest MapPostponePriceDiffToJournal(
            int reservationId, decimal priceDifference, DateTime rescheduleDate, string customerName)
        {
            var roomArId = GetAccountId("ROOM_AR");
            var roomRevenueId = GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>();

            if (priceDifference > 0)
            {
                // New price higher: customer owes more
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = roomArId,
                    debitAmount = priceDifference,
                    creditAmount = 0,
                    description = $"ส่วนต่างราคาเพิ่ม - เลื่อนวัน #{reservationId}",
                    lineOrder = 1
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = roomRevenueId,
                    debitAmount = 0,
                    creditAmount = priceDifference,
                    description = $"รายได้ส่วนต่างราคา - {customerName}",
                    lineOrder = 2
                });
            }
            else
            {
                // New price lower: credit to customer
                decimal absDiff = Math.Abs(priceDifference);
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = roomRevenueId,
                    debitAmount = absDiff,
                    creditAmount = 0,
                    description = $"ปรับลดราคา - เลื่อนวัน #{reservationId}",
                    lineOrder = 1
                });
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = roomArId,
                    debitAmount = 0,
                    creditAmount = absDiff,
                    description = $"เครดิตส่วนต่างราคา - {customerName}",
                    lineOrder = 2
                });
            }

            return new CreateJournalEntryRequest
            {
                entryDate = rescheduleDate,
                journalType = NexaaccJournalType.General,
                description = $"ปรับส่วนต่างราคาเลื่อนวัน - {customerName} - #{reservationId}",
                reference = $"POSTPONE-DIFF-{reservationId}",
                lines = lines
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
                    accountId = advanceDepositId,
                    debitAmount = refundAmount + retainedAmount,
                    creditAmount = 0,
                    description = $"ล้างเงินรับล่วงหน้า - #{reservationId}",
                    lineOrder = 1
                },
                // Credit: Refund portion back to customer
                new JournalEntryLineRequest
                {
                    accountId = cashAccountId,
                    debitAmount = 0,
                    creditAmount = refundAmount,
                    description = $"คืนเงินบางส่วน - {customerName}",
                    lineOrder = 2
                }
            };

            // Retained amount recognized as income
            if (retainedAmount > 0)
            {
                Guid otherIncomeId = TryGetAccountId("OTHER_INCOME", out var oi) ? oi : GetAccountId("ROOM_REVENUE");
                lines.Add(new JournalEntryLineRequest
                {
                    accountId = otherIncomeId,
                    debitAmount = 0,
                    creditAmount = retainedAmount,
                    description = $"รายได้จากค่าธรรมเนียมยกเลิก - {reason}",
                    lineOrder = 3
                });
            }

            return new CreateJournalEntryRequest
            {
                entryDate = refundDate,
                journalType = NexaaccJournalType.CashPayments,
                description = $"คืนเงินบางส่วน - {customerName} - #{reservationId} ({reason})",
                reference = $"PARTIAL-REFUND-{reservationId}",
                lines = lines
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
                    accountId = roomArId,
                    debitAmount = totalCharge,
                    creditAmount = 0,
                    description = $"ค่าเสียหาย/ของหาย - #{reservationId}",
                    lineOrder = 1
                },
                new JournalEntryLineRequest
                {
                    accountId = otherIncomeId,
                    debitAmount = 0,
                    creditAmount = totalCharge,
                    description = $"รายได้ค่าเสียหาย - {customerName} - {description}",
                    lineOrder = 2
                }
            };

            return new CreateJournalEntryRequest
            {
                entryDate = chargeDate,
                journalType = NexaaccJournalType.General,
                description = $"ค่าเสียหาย/ของหาย - {customerName} - #{reservationId}",
                reference = $"DMG-{reservationId}",
                lines = lines
            };
        }
    }
}
