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
        private Dictionary<string, string> _accountCodeCache;
        private Dictionary<Guid, string> _accountIdToCodeCache;
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
            _accountCodeCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _accountIdToCodeCache = new Dictionary<Guid, string>();

            DataTable dt = _Code.DatabaseQuerySafe(_connectionString,
                "SELECT TakeTime_Code, Nexaacc_AccountCode, Nexaacc_AccountId FROM Accounting_Account_Mapping WHERE Is_Active = 1",
                null);

            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    string ttCode = row["TakeTime_Code"]?.ToString();
                    if (string.IsNullOrEmpty(ttCode)) continue;

                    string nexaaccCode = row["Nexaacc_AccountCode"]?.ToString();
                    if (!string.IsNullOrEmpty(nexaaccCode))
                        _accountCodeCache[ttCode] = nexaaccCode;

                    Guid id;
                    if (row["Nexaacc_AccountId"] != DBNull.Value)
                    {
                        id = (Guid)row["Nexaacc_AccountId"];
                    }
                    else if (!string.IsNullOrEmpty(nexaaccCode))
                    {
                        // ไม่มี Nexaacc_AccountId (ยังไม่เคย "ดึง Chart of Accounts" หรือใช้ Integration Key
                        // ที่ดึง chart ไม่ได้) — สร้าง Guid สังเคราะห์จาก AccountCode ที่ seed ไว้
                        // Guid นี้ไม่เคยถูกส่งไป NextAcc (ConvertJournalToIntegration แปลงกลับเป็น
                        // AccountCode ก่อนส่งเสมอ) — ทำให้ journal/invoice sync ไม่ต้องพึ่ง chart sync
                        id = DeterministicGuidFromCode(nexaaccCode);
                    }
                    else
                    {
                        continue; // ไม่มีทั้ง Id และ Code — ข้าม
                    }

                    _accountMappingCache[ttCode] = id;
                    if (!string.IsNullOrEmpty(nexaaccCode))
                        _accountIdToCodeCache[id] = nexaaccCode;
                }
            }

            _mappingCacheExpiry = DateTime.Now.AddMinutes(10);
        }

        /// <summary>สร้าง Guid คงที่จาก AccountCode (MD5) — ใช้เป็น placeholder เมื่อ chart ยังไม่ sync</summary>
        private static Guid DeterministicGuidFromCode(string code)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes("TT-ACC:" + code));
                return new Guid(hash);
            }
        }

        /// <summary>หา NexaAcc AccountCode (string เช่น "1110") จาก TakeTime code (เช่น "CASH")</summary>
        public string GetAccountCode(string takeTimeCode)
        {
            if (string.IsNullOrEmpty(takeTimeCode)) return null;
            EnsureMappingCache();
            if (_accountCodeCache.TryGetValue(takeTimeCode.ToUpper(), out var code) && !string.IsNullOrEmpty(code))
                return code;
            throw new Exception($"No NexaAcc AccountCode mapping for TakeTime code: {takeTimeCode}. ตรวจสอบ Accounting_Account_Mapping.Nexaacc_AccountCode");
        }

        /// <summary>Reverse lookup: AccountId Guid → NexaAcc AccountCode string. ใช้แปลง legacy mapper output → integration request</summary>
        public string GetAccountCodeFromId(Guid accountId)
        {
            if (accountId == Guid.Empty) return null;
            EnsureMappingCache();
            if (_accountIdToCodeCache.TryGetValue(accountId, out var code) && !string.IsNullOrEmpty(code))
                return code;

            // Fallback: mapping row อาจมี Nexaacc_AccountId แต่ขาด Nexaacc_AccountCode
            // → ดูจาก Accounting_Nexaacc_Accounts (chart-of-accounts cache ที่ sync จาก NextAcc โดยตรง
            //   มี Id↔Code ครบเสมอ). cache ผลลัพธ์ลง _accountIdToCodeCache เพื่อไม่ query ซ้ำ
            try
            {
                var dt = _Code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Account_Code FROM Accounting_Nexaacc_Accounts WHERE Nexaacc_AccountId = @id",
                    new Dictionary<string, object> { { "@id", accountId } });
                if (dt?.Rows.Count > 0)
                {
                    string resolved = dt.Rows[0]["Account_Code"]?.ToString();
                    if (!string.IsNullOrEmpty(resolved))
                    {
                        _accountIdToCodeCache[accountId] = resolved;
                        return resolved;
                    }
                }
            }
            catch { /* fall through to null */ }

            return null;
        }

        /// <summary>แปลง JournalType int → string ที่ NextAcc รับ ("general", "sales", "cashreceipts", ฯลฯ)</summary>
        public static string MapJournalTypeToString(int journalType)
        {
            switch (journalType)
            {
                case 1: return "sales";
                case 2: return "purchase";
                case 3: return "cashreceipts";
                case 4: return "cashpayments";
                default: return "general";
            }
        }

        /// <summary>
        /// แปลง legacy CreateJournalEntryRequest (Guid-based) → CreateIntegrationJournalRequest (string AccountCode)
        /// เพื่อให้ส่งผ่าน /api/integration/journals ได้ (ใช้ X-Integration-Key auth + auto-Posted)
        /// </summary>
        public CreateIntegrationJournalRequest ConvertJournalToIntegration(CreateJournalEntryRequest src)
        {
            if (src == null) return null;
            EnsureMappingCache();

            var lines = new List<IntegrationJournalLineRequest>();
            if (src.Lines != null)
            {
                foreach (var line in src.Lines)
                {
                    string code = GetAccountCodeFromId(line.AccountId);
                    if (string.IsNullOrEmpty(code))
                        throw new Exception($"ConvertJournalToIntegration: ไม่พบ Nexaacc_AccountCode สำหรับ AccountId {line.AccountId} " +
                            "ทั้งใน Accounting_Account_Mapping และ Accounting_Nexaacc_Accounts. " +
                            "กรุณากด 'ดึง Chart of Accounts' ในหน้า Accounting Integration เพื่อ sync ผังบัญชีให้ครบ");
                    lines.Add(new IntegrationJournalLineRequest
                    {
                        AccountCode = code,
                        DebitAmount = line.DebitAmount,
                        CreditAmount = line.CreditAmount,
                        Description = line.Description
                    });
                }
            }

            return new CreateIntegrationJournalRequest
            {
                ExternalId = src.SourceDocumentNumber,
                ExternalRef = src.Reference,
                EntryDate = src.EntryDate,
                JournalType = MapJournalTypeToString(src.JournalType),
                Description = src.Description,
                Lines = lines,
                AutoBalanceVat = false,
                Sensitivity = src.Sensitivity
            };
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
            string paymentAccountId = null, string documentNumber = null,
            bool hasVat = false, bool vatAtReceipt = false, bool deferOutputVat = false)
        {
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");

            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"RES-{reservationId}-DEP";

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = amount,
                    CreditAmount = 0,
                    Description = $"รับมัดจำจากลูกค้า - {paymentMethod}"
                }
            };

            if (hasVat && vatAtReceipt)
            {
                // ม.78/1 — บริการ: จุดความรับผิด VAT = วันรับเงิน
                // แยก VAT ออกจากมัดจำตั้งแต่ตอนรับเงิน → CR เงินรับล่วงหน้า (net) + CR ภาษีขาย
                // ⚠ ต้องคำนวณ netAmount = round(amount*100/107) ให้ตรงกับ MapCheckoutToJournal
                //   (ถ้าใช้ amount - round(amount*7/107) อาจต่างกัน 0.01 → เศษค้างใน ADVANCE_DEPOSIT)
                decimal netAmount = Math.Round(amount * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal vatAmount = amount - netAmount;

                // โหมด deferred (opt-in): พัก VAT ไว้ที่ "ภาษีขายรอเรียกเก็บ/รอรับรู้" (21913)
                // แล้วโอนเข้า "ภาษีขาย" (21911) ตอน check-out. ถ้ายังไม่ map OUTPUT_VAT_DEFERRED
                // จะ fallback กลับไป OUTPUT_VAT (พฤติกรรมเดิม) เพื่อไม่ให้ JE ไม่บาลานซ์.
                Guid deferredVatId = Guid.Empty;
                bool useDeferred = deferOutputVat
                    && TryGetAccountId("OUTPUT_VAT_DEFERRED", out deferredVatId) && deferredVatId != Guid.Empty;
                var outputVatAccountId = useDeferred ? deferredVatId : GetAccountId("OUTPUT_VAT");

                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = netAmount,
                    Description = $"เงินรับล่วงหน้า (ไม่รวม VAT) - การจอง #{reservationId}"
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = outputVatAccountId,
                    DebitAmount = 0,
                    CreditAmount = vatAmount,
                    Description = useDeferred ? "ภาษีขายรอเรียกเก็บ 7% (มัดจำ)" : "ภาษีขาย 7% (มัดจำ)"
                });
            }
            else
            {
                // CHECKOUT mode — มัดจำเป็นหนี้สินเต็มจำนวน VAT รับรู้ตอนตัดมัดจำ
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = amount,
                    Description = $"เงินรับล่วงหน้า - การจอง #{reservationId}"
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = paymentDate,
                JournalType = NexaaccJournalType.CashReceipts,
                Description = $"รับมัดจำ {refStr} - การจอง #{reservationId} ({customerName})",
                Reference = refStr,
                Lines = lines
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
            decimal damageAmount = 0, string reservationRef = null, bool hasVat = false,
            bool vatAtReceipt = false, bool deferOutputVat = false)
        {
            if (depositAmount <= 0)
                throw new ArgumentException($"MapCheckoutToJournal: depositAmount ต้อง > 0 (ได้ {depositAmount})");
            if (damageAmount < 0)
                damageAmount = 0;

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var revenueAccountId = GetAccountId("ROOM_REVENUE");
            string refStr = !string.IsNullOrEmpty(reservationRef) ? reservationRef : $"RES-{reservationId}-CHK";

            // depositAmount = ยอดที่ลูกค้าจ่าย (gross). ในโหมด RECEIPT บัญชี ADVANCE_DEPOSIT
            // เก็บเฉพาะยอด net (VAT ถูกแยกตอนรับเงินแล้ว) → ต้องตัดด้วยยอด net
            // และ CR รายได้แบบไม่มี VAT line (รับรู้ VAT ไปแล้ว)
            bool depositVatAlreadyRecognized = hasVat && vatAtReceipt;
            decimal clearAmount = depositVatAlreadyRecognized
                ? Math.Round(depositAmount * 100m / 107m, 2, MidpointRounding.AwayFromZero)
                : depositAmount;

            if (damageAmount > clearAmount)
                damageAmount = clearAmount;
            decimal revenuePortion = clearAmount - damageAmount;

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = clearAmount,
                    CreditAmount = 0,
                    Description = $"ตัดเงินรับล่วงหน้า - การจอง #{reservationId}"
                }
            };

            if (revenuePortion > 0)
            {
                if (hasVat && !depositVatAlreadyRecognized)
                {
                    decimal vatOnRevenue = Math.Round(revenuePortion * 7m / 107m, 2, MidpointRounding.AwayFromZero);
                    decimal netRevenue = revenuePortion - vatOnRevenue;
                    var outputVatAccountId = GetAccountId("OUTPUT_VAT");

                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = revenueAccountId,
                        DebitAmount = 0,
                        CreditAmount = netRevenue,
                        Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}"
                    });
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = outputVatAccountId,
                        DebitAmount = 0,
                        CreditAmount = vatOnRevenue,
                        Description = "ภาษีขาย 7%"
                    });
                }
                else
                {
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = revenueAccountId,
                        DebitAmount = 0,
                        CreditAmount = revenuePortion,
                        Description = $"รายได้ค่าห้องพัก - การจอง #{reservationId}"
                    });
                }
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

            // Realize deferred output VAT: ตอนรับมัดจำในโหมด deferred ภาษีถูกพักไว้ที่
            // "ภาษีขายรอเรียกเก็บ" (21913). check-out = จุดรับรู้รายได้ → โอนกลับเข้า
            // "ภาษีขาย" (21911) เพื่อให้ขึ้น ภ.พ.30. Dr 21913 / Cr 21911 (บาลานซ์ในตัว).
            if (depositVatAlreadyRecognized && deferOutputVat
                && TryGetAccountId("OUTPUT_VAT_DEFERRED", out var deferredVatId) && deferredVatId != Guid.Empty)
            {
                decimal vatPortion = depositAmount - clearAmount; // VAT ที่แยกไว้ตอนรับเงิน
                if (vatPortion > 0)
                {
                    var outputVatAccountId = GetAccountId("OUTPUT_VAT");
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = deferredVatId,
                        DebitAmount = vatPortion,
                        CreditAmount = 0,
                        Description = $"โอนภาษีขายรอเรียกเก็บเป็นภาษีขาย (มัดจำ) - การจอง #{reservationId}"
                    });
                    lines.Add(new JournalEntryLineRequest
                    {
                        AccountId = outputVatAccountId,
                        DebitAmount = 0,
                        CreditAmount = vatPortion,
                        Description = "ภาษีขาย 7% (รับรู้จากมัดจำ)"
                    });
                }
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
            List<ExpenseLine> expenseLines = null, string documentNumber = null,
            bool isCredit = false, string supplierTaxId = null)
        {
            // ซื้อเครดิต → CR เจ้าหนี้การค้า (ยังไม่จ่ายเงิน) แทน CR เงินสด/ธนาคาร
            var cashAccountId = isCredit
                ? GetAccountId("ACCOUNTS_PAYABLE")
                : (ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod));
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

            // Compute total DR from all debit lines built so far
            decimal totalDebit = 0;
            foreach (var l in lines) totalDebit += l.DebitAmount;

            // CR: ภาษีหัก ณ ที่จ่าย (ถ้ามี) — แยกบัญชีตามผู้ถูกหัก: นิติบุคคล ภ.ง.ด.53 / บุคคล ภ.ง.ด.3
            if (whtAmount > 0)
            {
                var whtAccountId = GetWhtPayableAccountId(supplierTaxId);
                string whtForm = IsJuristicPerson(supplierTaxId) ? "ภ.ง.ด.53" : "ภ.ง.ด.3";
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = whtAccountId,
                    DebitAmount = 0,
                    CreditAmount = whtAmount,
                    Description = $"ภาษีหัก ณ ที่จ่าย {whtRate}% ({whtForm})",
                });
            }

            // CR: เงินสด/ธนาคาร (จ่ายสด) หรือ เจ้าหนี้การค้า (เครดิต) = DR total - WHT
            decimal cashPaid = totalDebit - whtAmount;
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = cashAccountId,
                DebitAmount = 0,
                CreditAmount = cashPaid,
                Description = isCredit ? $"ตั้งหนี้เจ้าหนี้ - {payeeName}" : $"จ่ายเงิน - {paymentMethod}",
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
                // เครดิต = ตั้งหนี้ → สมุดรายวันซื้อ / จ่ายสด → สมุดรายวันจ่าย
                JournalType = isCredit ? NexaaccJournalType.Purchase : NexaaccJournalType.CashPayments,
                Description = (isCredit ? $"ตั้งหนี้ใบสำคัญจ่าย {refStr}" : $"ใบสำคัญจ่าย {refStr}") + $" - {description} ({payeeName})",
                Reference = refStr,
                Lines = lines
            };
        }

        /// <summary>
        /// Map a credit voucher payment to journal: DR Accounts Payable, CR Cash/Bank
        /// </summary>
        public CreateJournalEntryRequest MapCreditPaymentToJournal(string originalDocNumber, decimal amount,
            string paymentMethod, DateTime paymentDate, string vendorName,
            string paymentAccountId = null)
        {
            var lines = new List<JournalEntryLineRequest>();

            // DR: Accounts Payable (ลดหนี้)
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = GetAccountId("ACCOUNTS_PAYABLE"),
                DebitAmount = amount,
                CreditAmount = 0,
                Description = $"ชำระหนี้ - {vendorName}"
            });

            // CR: Cash or Bank (จ่ายเงิน)
            Guid cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            lines.Add(new JournalEntryLineRequest
            {
                AccountId = cashAccountId,
                DebitAmount = 0,
                CreditAmount = amount,
                Description = $"จ่ายชำระ {originalDocNumber} - {paymentMethod}"
            });

            return new CreateJournalEntryRequest
            {
                EntryDate = paymentDate,
                JournalType = NexaaccJournalType.CashPayments,
                Description = $"ชำระหนี้ใบสำคัญจ่าย {originalDocNumber}",
                Reference = originalDocNumber,
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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

        /// <summary>
        /// Map TakeTime product → /api/integration/products payload (upsert by Code, X-Integration-Key auth)
        /// แทน CreateProductAsync/UpdateProductAsync ที่เป็น JWT-only
        /// </summary>
        public InboundProductRequest MapProductToIntegration(
            int productId, string productName, string description, decimal sellingPrice,
            decimal costPrice, string unit, string categoryName)
        {
            string revenueCode = null;
            try { revenueCode = GetAccountCode("PRODUCT_REVENUE"); } catch { /* optional */ }

            return new InboundProductRequest
            {
                ExternalId = $"TT-{productId:D5}",
                Code = $"TT-{productId:D5}",
                Name = productName,
                Price = sellingPrice,
                CostPrice = costPrice,
                Unit = unit ?? "ชิ้น",
                Category = categoryName,
                AccountCode = revenueCode,
                ProductType = "Product",
                IsActive = true,
                Notes = description
            };
        }

        /// <summary>
        /// Build debit note for voiding an expense voucher — ใช้แทน VoidDocumentAsync (JWT-only)
        /// NextAcc /api/integration/debit-notes auto-creates journal: CR Expense + CR VAT / DR A/P
        /// </summary>
        public InboundDebitNoteRequest MapVoucherVoidToDebitNote(
            int voucherId, string voucherNumber, decimal totalAmount, bool hasVat,
            string supplierName, DateTime voidDate, string reason)
        {
            decimal vatAmount = hasVat ? Math.Round(totalAmount * 7m / 107m, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal netAmount = totalAmount - vatAmount;

            return new InboundDebitNoteRequest
            {
                ExternalRef = $"DN-{voucherNumber}",
                OriginalInvoiceRef = voucherNumber,
                CustomerName = supplierName,
                DocumentDate = voidDate,
                Reason = reason ?? $"ยกเลิกใบสำคัญจ่าย {voucherNumber}",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = $"ยกเลิกใบสำคัญจ่าย {voucherNumber}",
                        Description = $"ยกเลิกใบสำคัญจ่าย {voucherNumber} - voucher #{voucherId}",
                        Quantity = 1,
                        UnitPrice = netAmount,
                        VatRate = hasVat ? 7m : 0m
                    }
                }
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
            decimal whtAmount = 0, string paymentMethod = null)
        {
            var salaryAccountId = GetAccountId("SALARY_EXPENSE");
            // บัญชีด้านจ่าย: ใช้ช่องทางจ่ายจริง (เงินสด/ธนาคาร) ถ้าระบุมา ไม่งั้น default เงินสด
            var cashAccountId = string.IsNullOrEmpty(paymentMethod)
                ? GetAccountId("CASH")
                : GetPaymentMethodAccountId(paymentMethod);

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

            // CR: ภาษีเงินได้หัก ณ ที่จ่ายค้างจ่าย จากเงินเดือน = ภ.ง.ด.1 (WHT_PAYABLE_PND1)
            //      fallback เป็น WHT_PAYABLE ถ้ายังไม่ได้แยกบัญชี ภ.ง.ด.1
            if (whtAmount > 0)
            {
                var whtPayableId = TryGetAccountId("WHT_PAYABLE_PND1", out var pnd1) ? pnd1 : GetAccountId("WHT_PAYABLE");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = whtPayableId,
                    DebitAmount = 0,
                    CreditAmount = whtAmount,
                    Description = $"ภาษีเงินได้หัก ณ ที่จ่าย (ภ.ง.ด.1) - {period}",
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
                Sensitivity = "Payroll",
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

        /// <summary>
        /// Normalize a free-text payment method to one of NextAcc's exact enum tokens.
        /// NextAcc IntegrationService.ParsePaymentMethod accepts (case-insensitive):
        ///   "cash" → Cash; "banktransfer"/"transfer" → BankTransfer;
        ///   "creditcard"/"credit_card" → CreditCard; "cheque"/"check" → Cheque;
        ///   "promptpay"/"prompt_pay" → PromptPay; "ewallet"/"e_wallet" → EWallet.
        /// Anything else falls through to PaymentMethod.Other, which is why returning
        /// "BANK_TRANSFER" (old value) silently became "Other" in NextAcc.
        /// </summary>
        public static string NormalizePaymentMethod(string paymentMethod)
        {
            if (string.IsNullOrEmpty(paymentMethod)) return "Cash";
            string pm = paymentMethod.Trim();
            string pmUpper = pm.ToUpper();

            if (pmUpper == "CASH" || pm.Contains("เงินสด")) return "Cash";
            if (pmUpper.Contains("PROMPTPAY") || pm.Contains("พร้อมเพย์") || pmUpper.Contains("QR")) return "PromptPay";
            if (pmUpper.Contains("EWALLET") || pmUpper.Contains("E-WALLET") || pmUpper.Contains("WALLET")
                || pm.Contains("วอลเล็ท") || pm.Contains("วอลเลท")) return "EWallet";
            if (pmUpper.Contains("CHECK") || pmUpper.Contains("CHEQUE") || pm.Contains("เช็ค")) return "Cheque";
            if (pmUpper.Contains("CARD") || pm.Contains("บัตร") || pm.Contains("เดบิต") || pm.Contains("เครดิต")) return "CreditCard";
            if (pmUpper.Contains("TRANSFER") || pm.Contains("โอน")) return "BankTransfer";
            if (pmUpper.Contains("KBANK") || pm.Contains("กสิกร")) return "BankTransfer";
            if (pmUpper.Contains("KTB") || pm.Contains("กรุงไทย")) return "BankTransfer";
            if (pmUpper.Contains("BBL") || pm.Contains("กรุงเทพ")) return "BankTransfer";
            if (pmUpper.Contains("SCB") || pm.Contains("ไทยพาณิชย์")) return "BankTransfer";
            if (pm.Contains("ธนาคาร") || pmUpper.Contains("BANK")) return "BankTransfer";
            if (pmUpper.Contains("DIRECTOR") || pm.Contains("กรรมการ") || pm.Contains("ทดรอง")) return "Cash";

            return "Cash";
        }

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
                // จ่ายจากเงินทดรองกรรมการ → CR เจ้าหนี้กรรมการ (21230, หนี้สิน) ไม่ใช่ลูกหนี้/เงินสด
                // (บริษัทค้างจ่ายกรรมการที่สำรองจ่ายแทน) — ใช้ DIRECTOR_ADVANCE_REPAY
                case "DIRECTOR": mappingKey = "DIRECTOR_ADVANCE_REPAY"; break;
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
                        mappingKey = "DIRECTOR_ADVANCE_REPAY";   // CR เจ้าหนี้กรรมการ (21230) — ดูหมายเหตุ case "DIRECTOR"
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

        /// <summary>
        /// นิติบุคคลไทย: เลขประจำตัวผู้เสียภาษี 13 หลักขึ้นต้นด้วย '0' (เลขทะเบียนนิติบุคคล)
        /// ส่วนบุคคลธรรมดาใช้เลขบัตรประชาชนขึ้นต้น 1-8. ใช้กำหนดแบบภาษีหัก ณ ที่จ่าย:
        /// นิติบุคคล → ภ.ง.ด.53, บุคคลธรรมดา → ภ.ง.ด.3. (อย่าใช้ความยาว 13 หลักตัดสิน — เท่ากันทั้งคู่)
        /// </summary>
        public static bool IsJuristicPerson(string taxId)
        {
            if (string.IsNullOrWhiteSpace(taxId)) return false;
            string t = taxId.Trim().Replace("-", "").Replace(" ", "");
            return t.Length == 13 && t.StartsWith("0");
        }

        /// <summary>
        /// บัญชีภาษีหัก ณ ที่จ่ายค้างจ่าย ตามประเภทผู้ถูกหัก:
        /// นิติบุคคล (ภ.ง.ด.53) → WHT_PAYABLE_PND53, บุคคลธรรมดา (ภ.ง.ด.3) → WHT_PAYABLE.
        /// ถ้ายังไม่ได้ map WHT_PAYABLE_PND53 จะ fallback เป็น WHT_PAYABLE เพื่อไม่ให้ sync ล้ม.
        /// </summary>
        private Guid GetWhtPayableAccountId(string supplierTaxId)
        {
            if (IsJuristicPerson(supplierTaxId) && TryGetAccountId("WHT_PAYABLE_PND53", out var corp))
                return corp;
            return GetAccountId("WHT_PAYABLE");
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
            string paymentAccountId = null, bool hasVat = false, bool vatAtReceipt = false)
        {
            // โหมด RECEIPT: แยก VAT จากมัดจำตอนรับเงิน → IncludeVat=true, line VatRate=7
            //   (NextAcc: UnitPrice รวม VAT แล้ว → CR ADVANCE_DEPOSIT net + CR Output VAT)
            // โหมด CHECKOUT: มัดจำเป็นหนี้สินเต็มจำนวน ไม่มี VAT (รับรู้ตอนตัดมัดจำ)
            bool splitVat = hasVat && vatAtReceipt;
            return new CreateIntegrationInvoiceRequest
            {
                DocumentDate = paymentDate,
                CustomerName = customerName,
                Reference = $"RES-{reservationId}-DEP",
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
                IncludeVat = splitVat,
                Description = $"รับมัดจำ - การจอง #{reservationId} ({customerName})",
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = "เงินรับล่วงหน้า",
                        Description = $"เงินรับล่วงหน้า - การจอง #{reservationId}",
                        Quantity = 1,
                        UnitPrice = amount,
                        VatRate = splitVat ? 7m : 0m,
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
                IncludeVat = true,
                Description = $"รับชำระ - การจอง #{reservationId} ({customerName})",
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = lines
            };
        }

        // ──────────────────────────────────────────────
        // Company /document Receipt mappers (DOCUMENT mode, acc_ key)
        // NextAcc Receipt doc (approve) ลง GL ถูกต้องในเอกสารเดียว:
        //   Dr เงินสด/ธนาคาร (PaymentAccountId) / Cr รายได้ "ราย line" (docLine.AccountId) / Cr ภาษีขาย
        //   มัดจำ (IsDeposit): Cr ขายรอรับรู้ (DepositDeferredAccountCode) แทนรายได้ + VAT รอเรียกเก็บได้
        // → แก้ทั้ง multi-line revenue flattening และ deposit-as-revenue ที่ /integration/invoices มี
        // ──────────────────────────────────────────────

        /// <summary>account code ของ mapping (null ถ้าไม่ได้ map — กัน exception)</summary>
        private string SafeGetAccountCode(string takeTimeCode)
        {
            try { return GetAccountCode(takeTimeCode); }
            catch { return null; }
        }

        /// <summary>แปลง ReceiptLineSpec → DocumentLineRequest (UnitPrice รวม VAT, ผูกบัญชีรายได้ราย line)</summary>
        private List<DocumentLineRequest> BuildDocumentLines(List<ReceiptLineSpec> lines, bool hasVat)
        {
            var result = new List<DocumentLineRequest>();
            foreach (var line in lines)
            {
                if (line == null) continue;
                decimal amount = line.Amount > 0 ? line.Amount : line.Quantity * line.UnitPrice;
                if (amount == 0) continue;

                string revCode = !string.IsNullOrEmpty(line.RevenueTypeOverride)
                    ? line.RevenueTypeOverride
                    : GetProductTypeRevenueCode(line.ProductTypeId);

                decimal qty = line.Quantity > 0 ? line.Quantity : 1;
                result.Add(new DocumentLineRequest
                {
                    Description = !string.IsNullOrEmpty(line.Description) ? line.Description : revCode,
                    Quantity = qty,
                    Unit = line.Unit,
                    UnitPrice = amount / qty,
                    VatRate = hasVat ? 7m : 0m,
                    AccountId = GetAccountId(revCode)
                });
            }
            return result;
        }

        /// <summary>
        /// สร้าง Receipt document (DocumentType=3) สำหรับ company /document endpoint.
        /// - แหล่งเงินบังคับผ่าน PaymentAccountId (Dr บัญชีนั้นตรง ๆ)
        /// - รายได้แยกราย line (AccountId ต่อบรรทัด) — NextAcc ยึดให้
        /// - มัดจำ (isDeposit): Cr "ขายรอรับรู้" = บัญชี ADVANCE_DEPOSIT (ให้ตรงกับ checkout clearing)
        ///   + VAT รอเรียกเก็บ (21913) เมื่อ deferOutputVat; VAT แยกเฉพาะโหมด RECEIPT (depositVatAtReceipt)
        /// </summary>
        public CreateDocumentRequest MapReceiptToDocument(
            int reservationId, List<ReceiptLineSpec> lines, decimal totalAmount, string revenueType,
            string paymentMethod, DateTime paymentDate, string customerName, Guid contactId,
            string paymentAccountId, bool hasVat, string receiptNumber,
            bool isDeposit = false, bool depositVatAtReceipt = false, bool deferOutputVat = false,
            int documentType = NexaaccDocumentType.Receipt)
        {
            var docLines = new List<DocumentLineRequest>();

            if (isDeposit)
            {
                bool splitVat = hasVat && depositVatAtReceipt;
                docLines.Add(new DocumentLineRequest
                {
                    Description = $"เงินรับล่วงหน้า - การจอง #{reservationId}",
                    Quantity = 1,
                    UnitPrice = totalAmount,
                    VatRate = splitVat ? 7m : 0m,
                    AccountId = null   // IsDeposit → NextAcc ใช้ DepositDeferredAccountCode เป็นฝั่งเครดิต
                });
            }
            else if (lines != null && lines.Count > 0)
            {
                docLines = BuildDocumentLines(lines, hasVat);
            }

            if (docLines.Count == 0)
            {
                string revCode = !string.IsNullOrEmpty(revenueType) ? revenueType : "ROOM_REVENUE";
                docLines.Add(new DocumentLineRequest
                {
                    Description = $"รายได้ - การจอง #{reservationId}",
                    Quantity = 1,
                    UnitPrice = totalAmount,
                    VatRate = hasVat ? 7m : 0m,
                    AccountId = GetAccountId(revCode)
                });
            }

            // แหล่งเงิน — ใช้นิพจน์เดียวกับฝั่ง adjustment (Cr Cash) เพื่อให้บัญชีตรงกันเสมอ
            Guid cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);

            // TaxInvoice = เอกสารเปิดลูกหนี้ ปิดด้วย payment แยก (SettleReceiptInNextAcc) —
            // ห้ามส่ง PaymentDate/PaymentAccountId (ฟิลด์เงินสดของ Receipt/ReceiptVoucher)
            // กัน NextAcc ตีความเป็นรับเงินสดในใบ → เงินสดเบิลกับ payment ที่ settle ตามมา
            bool isArDoc = documentType == NexaaccDocumentType.TaxInvoice;

            return new CreateDocumentRequest
            {
                // Receipt (3) = ใบเสร็จรับเงิน เงินสดจบในใบ (มัดจำ) / TaxInvoice (4) = ใบกำกับภาษี
                // เปิดลูกหนี้ ปิดด้วย settle (เช็คเอาท์/รับชำระเต็ม) — เลือกจาก caller
                DocumentType = documentType,
                DocumentDate = paymentDate,
                PaymentDate = isArDoc ? (DateTime?)null : paymentDate,
                ContactId = contactId,
                // รหัสอ้างอิง = รหัสการจอง (RES-{id}) ทั้งใบเสร็จมัดจำและใบกำกับ → จับคู่เอกสาร
                // ทั้งชุดของการจองเดียวกันได้ด้วย ref เดียว (มัดจำ RES-x / ใบกำกับ RES-x /
                // การกลับยอด RES-x-DEPADJ). เลขใบเสร็จฝั่งเราเก็บใน Notes;
                // การ match ภายในใช้ sync queue ไม่ใช้ Reference (POS resId=0 → ใช้เลขใบเสร็จ)
                Reference = reservationId > 0
                    ? $"RES-{reservationId}"
                    : (!string.IsNullOrEmpty(receiptNumber) ? receiptNumber : $"RES-{reservationId}"),
                // เลขจอง (booking) → NextAcc ผูกเอกสารชุดเดียวกันของการจอง + auto-suggest หักมัดจำ
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
                PaymentAccountId = isArDoc ? (Guid?)null : cashAccountId,
                PricesIncludeVat = true,
                IsDeposit = isDeposit,
                DepositDeferredAccountCode = isDeposit ? SafeGetAccountCode("ADVANCE_DEPOSIT") : null,
                DepositOutputVatDeferred = isDeposit && hasVat && depositVatAtReceipt && deferOutputVat,
                // ไม่ต่อท้ายเลขใบเสร็จ local (receiptNumber) — เป็น id ภายใน TakeTime ไม่ควรโผล่บนใบลูกค้า
                // (เอกสารมีเลข NextAcc บนหัวแล้ว, การ match ภายในใช้ Reference=RES-{id} + queue Payload ไม่ใช่ Notes)
                Notes = isDeposit
                    ? $"รับมัดจำ - การจอง #{reservationId} ({customerName})"
                    : $"รับชำระ - การจอง #{reservationId} ({customerName})",
                Lines = docLines
            };
        }

        /// <summary>
        /// Adjustment เมื่อใบเสร็จ (Receipt doc) หักมัดจำ: ใบเสร็จ Dr เงินสดเต็มจำนวน แต่ลูกค้าจ่ายจริง
        /// = total − depositApplied → ตัดส่วนมัดจำออกจากเงินสดและล้างเงินรับล่วงหน้า:
        ///   Dr ADVANCE_DEPOSIT (net ถ้า VAT รับรู้ตอนรับมัดจำ) [+ Dr VAT มัดจำ (21913 ถ้า defer ไม่งั้น 21911)]
        ///   Cr เงินสด/ธนาคาร (gross depositApplied)
        /// บัญชีเงินสดใช้นิพจน์เดียวกับ Receipt doc (PaymentAccountId) เพื่อหักล้างพอดี
        /// </summary>
        /// <summary>
        /// Journal แก้ VAT มัดจำ คู่กับการตัดมัดจำแบบ "document payment" (Dr ADVANCE gross / Cr AR):
        /// ADVANCE เก็บเฉพาะ net เมื่อ VAT รับรู้ตอนรับมัดจำ → ย้ายส่วน VAT จากฝั่ง Dr ADVANCE
        /// ไป Dr บัญชี VAT (21913 ถ้า defer+map แล้ว ไม่งั้น 21911 กัน VAT ซ้ำกับใบกำกับ):
        ///   ปกติ:   Dr VATacct vat / Cr ADVANCE_DEPOSIT vat
        ///   reverse: Dr ADVANCE_DEPOSIT vat / Cr VATacct vat (ตอน void)
        /// </summary>
        public CreateJournalEntryRequest MapDepositVatCorrection(
            int reservationId, decimal depositApplied, string receiptNumber,
            bool deferOutputVat, bool reverse = false)
        {
            decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
            decimal depositVat = depositApplied - depositNet;
            if (depositVat <= 0)
                throw new ArgumentException("MapDepositVatCorrection: depositVat ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            Guid vatAcc = (deferOutputVat
                && TryGetAccountId("OUTPUT_VAT_DEFERRED", out var dv) && dv != Guid.Empty)
                ? dv : GetAccountId("OUTPUT_VAT");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = reverse ? advanceDepositAccountId : vatAcc,
                    DebitAmount = depositVat,
                    CreditAmount = 0,
                    Description = reverse ? "คืนเงินรับล่วงหน้า (กลับรายการ VAT มัดจำ)" : "รับรู้/ตัด VAT มัดจำ"
                },
                new JournalEntryLineRequest
                {
                    AccountId = reverse ? vatAcc : advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = depositVat,
                    Description = reverse ? "กลับรายการ VAT มัดจำ (void)" : "ปรับเงินรับล่วงหน้าเหลือ net"
                }
            };

            return new CreateJournalEntryRequest
            {
                EntryDate = DateTime.Now,
                JournalType = NexaaccJournalType.General,
                Description = reverse
                    ? $"กลับรายการ VAT มัดจำ — void ใบกำกับ {receiptNumber} (การจอง #{reservationId})"
                    : $"ปรับ VAT มัดจำที่ตัดเข้าใบกำกับ {receiptNumber} (การจอง #{reservationId})",
                Reference = $"RES-{reservationId}-DEPVAT" + (reverse ? "-REV" : ""),
                Lines = lines
            };
        }

        /// <summary>
        /// รับรู้รายได้ของมัดจำตอนเช็คเอาท์ (โหมด §78/1 เคร่ง: มัดจำออกใบกำกับ+VAT ตั้งแต่รับเงิน,
        /// เช็คเอาท์ออกใบกำกับ "เฉพาะยอดคงเหลือ" → ยอดมัดจำเดิมยังเป็น Cr 21712 net ค้าง ต้องย้ายเป็นรายได้):
        ///   Dr 21712 เงินรับล่วงหน้า (net) / Cr รายได้ (net) — VAT รับรู้ไปแล้วตอนรับมัดจำ ไม่แตะ
        /// </summary>
        public CreateJournalEntryRequest MapDepositRevenueRecognition(
            int reservationId, decimal depositApplied, string receiptNumber, string revenueType, bool hasVat)
        {
            decimal net = hasVat
                ? Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero)
                : depositApplied;
            if (net <= 0)
                throw new ArgumentException("MapDepositRevenueRecognition: net ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var revenueAccountId = !string.IsNullOrEmpty(revenueType) ? GetAccountId(revenueType) : GetAccountId("ROOM_REVENUE");

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId, DebitAmount = net, CreditAmount = 0,
                    Description = $"รับรู้รายได้จากมัดจำ (net) - การจอง #{reservationId}"
                },
                new JournalEntryLineRequest
                {
                    AccountId = revenueAccountId, DebitAmount = 0, CreditAmount = net,
                    Description = $"รายได้ค่าห้องพัก (จากมัดจำ) - การจอง #{reservationId}"
                }
            };
            return new CreateJournalEntryRequest
            {
                EntryDate = DateTime.Now,
                JournalType = NexaaccJournalType.General,
                Description = $"รับรู้รายได้มัดจำตอนเช็คเอาท์ {receiptNumber} (การจอง #{reservationId})",
                Reference = $"RES-{reservationId}-DEPREV",
                Lines = lines
            };
        }

        public CreateJournalEntryRequest MapDepositAppliedReceiptAdjustment(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null,
            bool hasVat = false, bool vatAtReceipt = false, bool deferOutputVat = false)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedReceiptAdjustment: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ" : $"RES-{reservationId}-DEPADJ";

            var lines = new List<JournalEntryLineRequest>();

            if (hasVat && vatAtReceipt)
            {
                decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal depositVat = depositApplied - depositNet;
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositNet,
                    CreditAmount = 0,
                    Description = "ตัดเงินรับล่วงหน้า net (หักมัดจำในใบเสร็จ)"
                });
                // มัดจำ defer → VAT ค้างที่ 21913 (ใบเสร็จสุดท้าย Cr 21911 เต็ม) → Dr 21913
                // มัดจำ immediate → VAT อยู่ 21911 แล้ว (ใบเสร็จสุดท้าย Cr 21911 ซ้ำ) → Dr 21911 กันซ้ำ
                Guid vatAcc = (deferOutputVat
                    && TryGetAccountId("OUTPUT_VAT_DEFERRED", out var dv) && dv != Guid.Empty)
                    ? dv : GetAccountId("OUTPUT_VAT");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = vatAcc,
                    DebitAmount = depositVat,
                    CreditAmount = 0,
                    Description = "ตัด/กลับ VAT มัดจำ (กัน VAT ซ้ำกับใบเสร็จ)"
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositApplied,
                    CreditAmount = 0,
                    Description = "ตัดเงินรับล่วงหน้า (หักมัดจำในใบเสร็จ)"
                });
            }

            lines.Add(new JournalEntryLineRequest
            {
                AccountId = cashAccountId,
                DebitAmount = 0,
                CreditAmount = depositApplied,
                Description = "ลดเงินสดที่ใบเสร็จบันทึกเกิน (ส่วนที่จ่ายด้วยมัดจำ)"
            });

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ปรับปรุง — หักมัดจำในใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
                Lines = lines
            };
        }

        /// <summary>กลับรายการ MapDepositAppliedReceiptAdjustment (ตอน void ใบเสร็จ Receipt doc):
        ///   Dr เงินสด/ธนาคาร / Cr ADVANCE_DEPOSIT (+ Cr VAT มัดจำ) — เปิดเงินรับล่วงหน้าคืน</summary>
        public CreateJournalEntryRequest MapDepositAppliedReceiptAdjustmentReverse(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null,
            bool hasVat = false, bool vatAtReceipt = false, bool deferOutputVat = false)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedReceiptAdjustmentReverse: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var cashAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod);
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ-REV" : $"RES-{reservationId}-DEPADJ-REV";

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = cashAccountId,
                    DebitAmount = depositApplied,
                    CreditAmount = 0,
                    Description = "คืนเงินสดส่วนที่หักมัดจำ (กลับรายการ void)"
                }
            };

            if (hasVat && vatAtReceipt)
            {
                decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal depositVat = depositApplied - depositNet;
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = depositNet,
                    Description = "เปิดเงินรับล่วงหน้าคืน net (กลับรายการ void)"
                });
                Guid vatAcc = (deferOutputVat
                    && TryGetAccountId("OUTPUT_VAT_DEFERRED", out var dv) && dv != Guid.Empty)
                    ? dv : GetAccountId("OUTPUT_VAT");
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = vatAcc,
                    DebitAmount = 0,
                    CreditAmount = depositVat,
                    Description = "คืน VAT มัดจำ (กลับรายการ void)"
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = depositApplied,
                    Description = "เปิดเงินรับล่วงหน้าคืน (กลับรายการ void)"
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการหักมัดจำ — void ใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
                ExternalRef = !string.IsNullOrEmpty(receiptNumber) ? receiptNumber : null,
                ReplaceExistingForSource = !string.IsNullOrEmpty(receiptNumber),
                IncludeVat = hasVat,
                Description = depositApplied > 0
                    ? $"ใบเสร็จ {refStr} — การจอง #{reservationId} ({customerName}) | หักมัดจำ {depositApplied:N2}"
                    : $"ใบเสร็จ {refStr} — การจอง #{reservationId} ({customerName})",
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
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
            decimal depositApplied = 0, string documentNumber = null, bool vatAtReceipt = false,
            bool deferOutputVat = false)
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

            // โหมด RECEIPT: VAT ของมัดจำถูกแยกตอนรับเงินแล้ว → ADVANCE_DEPOSIT เก็บเฉพาะ net
            //   - DR Advance Deposit = net ของมัดจำ (ไม่ใช่ gross)
            //   - CR Output VAT = VAT ทั้งบิล − VAT ที่รับรู้ไปแล้วกับมัดจำ
            decimal depositAppliedDebit = depositApplied;   // ยอดที่จะ DR เข้า ADVANCE_DEPOSIT
            decimal depositVatAlready = 0m;                 // VAT ของมัดจำที่รับรู้ไปแล้ว
            if (hasVat && vatAtReceipt && depositApplied > 0)
            {
                decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                depositVatAlready = depositApplied - depositNet;
                // clamp กันกรณี rounding ทำให้ depositVatAlready > vatAmount (vatToRecognize จะติดลบ
                // → journal ไม่สมดุล). clamp แล้ว derive depositAppliedDebit ใหม่ให้ DR=CR เสมอ
                if (depositVatAlready > vatAmount)
                    depositVatAlready = vatAmount;
                depositAppliedDebit = depositApplied - depositVatAlready;
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
            // DR Advance Deposit (หักมัดจำ — ตัดเจ้าหนี้). โหมด RECEIPT ใช้ยอด net
            if (depositAppliedDebit > 0)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositAppliedDebit,
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

            // CR Output VAT — โหมด RECEIPT หัก VAT ของมัดจำที่รับรู้ไปแล้วออก
            decimal vatToRecognize = vatAmount - depositVatAlready;
            if (vatToRecognize < 0) vatToRecognize = 0;
            if (hasVat && vatToRecognize > 0)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = GetAccountId("OUTPUT_VAT"),
                    DebitAmount = 0,
                    CreditAmount = vatToRecognize,
                    Description = depositVatAlready > 0
                        ? $"ภาษีขาย 7% (หัก VAT มัดจำ {depositVatAlready:N2} ที่รับรู้แล้ว)"
                        : "ภาษีขาย 7%"
                });
            }

            // โหมด deferred: VAT ของมัดจำถูกพักไว้ที่ "ภาษีขายรอเรียกเก็บ" (21913) ตอนรับเงิน —
            // การหักมัดจำในใบเสร็จนี้ = จุดรับรู้รายได้ → ต้อง realize Dr 21913 / Cr 21911 ที่นี่
            // (checkout clearing จะถูก SKIP เพราะมัดจำถูกหักครบในใบเสร็จ → ไม่มีใครโอน 21913 ให้
            //  ถ้าไม่ทำตรงนี้ 21913 จะค้างสะสม + ภ.พ.30 ขาดเท่า VAT มัดจำ)
            // guard mapping เดียวกับ MapDepositToJournal: 21913 ไม่ได้ map → มัดจำลง 21911 ตรงอยู่แล้ว ไม่ต้องโอน
            if (hasVat && vatAtReceipt && deferOutputVat && depositVatAlready > 0
                && TryGetAccountId("OUTPUT_VAT_DEFERRED", out var deferredVatAcc) && deferredVatAcc != Guid.Empty)
            {
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = deferredVatAcc,
                    DebitAmount = depositVatAlready,
                    CreditAmount = 0,
                    Description = $"โอนภาษีขายรอเรียกเก็บ (มัดจำ) เป็นภาษีขาย - การจอง #{reservationId}"
                });
                journalLines.Add(new JournalEntryLineRequest
                {
                    AccountId = GetAccountId("OUTPUT_VAT"),
                    DebitAmount = 0,
                    CreditAmount = depositVatAlready,
                    Description = "ภาษีขาย 7% (รับรู้จาก VAT มัดจำรอเรียกเก็บ)"
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
        ///   invoice บันทึก DR ลูกหนี้การค้า (total) / CR รายได้ + CR ภาษีขาย (ตาม contract NextAcc)
        ///   adjustment นี้ตัด "ส่วนที่จ่ายด้วยมัดจำ" ออกจากลูกหนี้ ด้วยเงินรับล่วงหน้า:
        ///     DR เงินรับล่วงหน้า (ADVANCE_DEPOSIT, net ถ้า VAT รับรู้ตอนรับมัดจำ)
        ///     [DR ภาษีขาย (กลับ VAT มัดจำที่รับรู้ไปแล้ว กัน VAT ซ้ำ) — เฉพาะโหมด RECEIPT]
        ///     CR ลูกหนี้การค้า (ROOM_AR) = depositApplied
        ///   ส่วนเงินสดจริงที่รับเพิ่ม (total − depositApplied) บันทึกแยกผ่าน integration payment
        ///   (AutoRecordReceiptPayment) ซึ่งลง DR เงินสด / CR ลูกหนี้ → ลูกหนี้สุทธิ = 0 ✓
        /// </summary>
        public CreateJournalEntryRequest MapDepositAppliedAdjustment(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null,
            bool hasVat = false, bool vatAtReceipt = false)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedAdjustment: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var arAccountId = GetAccountId("ROOM_AR");
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ" : $"RES-{reservationId}-DEPADJ";

            var lines = new List<JournalEntryLineRequest>();

            if (hasVat && vatAtReceipt)
            {
                // โหมด RECEIPT: ADVANCE_DEPOSIT เก็บ net, VAT รับรู้ตอนรับมัดจำแล้ว
                //   invoice บันทึก revenue+VAT เต็ม → VAT ซ้ำ → ต้อง DR Output VAT คืน depositVat
                //   DR Advance Deposit (net) + DR Output VAT (depositVat) / CR ลูกหนี้ (gross)
                decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal depositVat = depositApplied - depositNet;
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositNet,
                    CreditAmount = 0,
                    Description = "ตัดเงินรับล่วงหน้า net (ใช้ในใบเสร็จ)"
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = GetAccountId("OUTPUT_VAT"),
                    DebitAmount = depositVat,
                    CreditAmount = 0,
                    Description = "กลับ VAT มัดจำที่รับรู้ไปแล้ว (กัน VAT ซ้ำ)"
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = depositApplied,
                    CreditAmount = 0,
                    Description = "ตัดเงินรับล่วงหน้า (ใช้ในใบเสร็จ)"
                });
            }

            lines.Add(new JournalEntryLineRequest
            {
                AccountId = arAccountId,
                DebitAmount = 0,
                CreditAmount = depositApplied,
                Description = "ตัดลูกหนี้ด้วยมัดจำที่รับล่วงหน้า"
            });

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"ปรับปรุง — หักมัดจำในใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
                Lines = lines
            };
        }

        /// <summary>
        /// Counter-adjustment สำหรับการ void ใบเสร็จที่เคยหักมัดจำ — กลับรายการของ
        /// MapDepositAppliedAdjustment (manual JE ไม่ถูก cascade ตอน void เอกสาร):
        ///   DR ลูกหนี้การค้า (เปิดลูกหนี้คืน) / CR เงินรับล่วงหน้า (+ CR ภาษีขาย ถ้าโหมด RECEIPT)
        /// ต้องส่ง hasVat/vatAtReceipt ให้ตรงกับตอน settle เพื่อให้ VAT กลับถูกต้อง
        /// </summary>
        public CreateJournalEntryRequest MapDepositAppliedAdjustmentReverse(
            int reservationId, decimal depositApplied, string paymentMethod, DateTime entryDate,
            string customerName, string paymentAccountId = null, string receiptNumber = null,
            bool hasVat = false, bool vatAtReceipt = false)
        {
            if (depositApplied <= 0)
                throw new ArgumentException("MapDepositAppliedAdjustmentReverse: depositApplied ต้อง > 0");

            var advanceDepositAccountId = GetAccountId("ADVANCE_DEPOSIT");
            var arAccountId = GetAccountId("ROOM_AR");
            string refStr = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ-REV" : $"RES-{reservationId}-DEPADJ-REV";

            var lines = new List<JournalEntryLineRequest>
            {
                new JournalEntryLineRequest
                {
                    AccountId = arAccountId,
                    DebitAmount = depositApplied,
                    CreditAmount = 0,
                    Description = "กลับการตัดลูกหนี้ด้วยมัดจำ (เปิดลูกหนี้คืน)"
                }
            };

            if (hasVat && vatAtReceipt)
            {
                decimal depositNet = Math.Round(depositApplied * 100m / 107m, 2, MidpointRounding.AwayFromZero);
                decimal depositVat = depositApplied - depositNet;
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = depositNet,
                    Description = "เอาเงินรับล่วงหน้ากลับมา (net)"
                });
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = GetAccountId("OUTPUT_VAT"),
                    DebitAmount = 0,
                    CreditAmount = depositVat,
                    Description = "กลับ VAT มัดจำที่เคยกลับไว้ในใบเสร็จ"
                });
            }
            else
            {
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = advanceDepositAccountId,
                    DebitAmount = 0,
                    CreditAmount = depositApplied,
                    Description = "เอาเงินรับล่วงหน้ากลับมา (เจ้าหนี้)"
                });
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = entryDate,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการ adjustment — ยกเลิกการหักมัดจำในใบเสร็จ {receiptNumber} (การจอง #{reservationId})",
                Reference = refStr,
                Lines = lines
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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
            List<ExpenseLine> expenseLines = null, string documentNumber = null,
            decimal vatAmount = 0)
        {
            var lines = new List<IntegrationLineRequest>();
            bool hasMultipleLines = expenseLines != null && expenseLines.Count > 0;

            if (hasMultipleLines)
            {
                foreach (var el in expenseLines)
                {
                    var lineAccId = ResolveAccountId(el.AccountId) ?? GetExpenseCategoryAccountId(el.Category);
                    string lineItemName = !string.IsNullOrEmpty(el.Category) ? el.Category : "ค่าใช้จ่าย";
                    // NextAcc resolves the expense account-code on the line, and its
                    // auto-journal maps by ExternalCode == line.ItemCode (ProductCode).
                    // Carry the account-code in BOTH fields so the chart-of-account
                    // category resolves whether NextAcc reads AccountCode or maps ItemCode.
                    string lineAccCode = !string.IsNullOrEmpty(el.AccountCode)
                        ? el.AccountCode
                        : (lineAccId != Guid.Empty ? GetAccountCodeFromId(lineAccId) : null);

                    lines.Add(new IntegrationLineRequest
                    {
                        ItemCode = !string.IsNullOrEmpty(lineAccCode) ? lineAccCode : null,
                        ItemName = lineItemName,
                        Description = el.Description,
                        Quantity = 1,
                        UnitPrice = el.Amount,
                        VatRate = hasInputVat ? 7 : 0,
                        WithholdingTaxRate = whtRate > 0 ? whtRate : 0,
                        AccountId = lineAccId,
                        AccountCode = !string.IsNullOrEmpty(lineAccCode) ? lineAccCode : null,
                        Category = !string.IsNullOrEmpty(el.Category) ? el.Category : null,
                    });
                }
            }
            else
            {
                var expAccId = ResolveAccountId(expenseAccountId) ?? GetExpenseCategoryAccountId(expenseCategory);
                string itemName = !string.IsNullOrEmpty(expenseCategory) ? expenseCategory
                    : !string.IsNullOrEmpty(description) ? description : "ค่าใช้จ่าย";
                string expAccCode = (expAccId != Guid.Empty) ? GetAccountCodeFromId(expAccId) : null;

                lines.Add(new IntegrationLineRequest
                {
                    ItemCode = !string.IsNullOrEmpty(expAccCode) ? expAccCode : null,
                    ItemName = itemName,
                    Description = description,
                    Quantity = 1,
                    UnitPrice = amount,
                    VatRate = hasInputVat ? 7 : 0,
                    WithholdingTaxRate = whtRate,
                    AccountId = expAccId,
                    AccountCode = !string.IsNullOrEmpty(expAccCode) ? expAccCode : null,
                    Category = !string.IsNullOrEmpty(expenseCategory) ? expenseCategory : null,
                });
            }

            // ภาษีซื้อผสม (มีรายการไม่เสียภาษีปน): VAT จริง ≠ 7% ของยอดรวม net → ถ้าส่ง VatRate=7
            // ทุก line NextAcc จะคิด 7% เต็ม (เกินจริง) → ยอดไม่ตรง เกิดค้างชำระ. แก้โดยแตกเป็น
            // 2 บรรทัด: ส่วนมีภาษี (taxableNet@7% ให้ VAT = vatAmount พอดี) + ส่วนไม่มีภาษี (@0%).
            // ใช้บัญชี/หมวดของ line แรก (ใบสำคัญจ่ายส่วนใหญ่หมวดเดียว). NextAcc honor per-line VatRate
            // เมื่อ IncludeVat=false (โดยเฉพาะ company /document PV).
            decimal totalNet = 0m;
            foreach (var ln in lines) totalNet += ln.UnitPrice * (ln.Quantity > 0 ? ln.Quantity : 1);
            // VAT ผสมจริง = VAT ขาดจาก 7% เต็มของ net เกิน 1 บาท (ไม่ใช่เศษปัดเศษ).
            // เดิมเช็ค nonTaxableNet >= 0.01 ทำให้ใบภาษีเต็มถูกแตกเป็น line "ไม่มีภาษี" จิ๋ว ๆ (เช่น 0.04) โดยไม่จำเป็น
            decimal expectedFullVat = Math.Round(totalNet * 0.07m, 2, MidpointRounding.AwayFromZero);
            if (hasInputVat && vatAmount > 0 && totalNet > 0 && (expectedFullVat - vatAmount) > 1.00m)
            {
                decimal taxableNet = Math.Round(vatAmount / 0.07m, 2, MidpointRounding.AwayFromZero);
                decimal nonTaxableNet = Math.Round(totalNet - taxableNet, 2, MidpointRounding.AwayFromZero);
                if (nonTaxableNet >= 0.01m && taxableNet > 0)
                {
                    var primary = lines[0];
                    lines = new List<IntegrationLineRequest>
                    {
                        new IntegrationLineRequest
                        {
                            ItemCode = primary.ItemCode, ItemName = primary.ItemName,
                            Description = !string.IsNullOrEmpty(primary.Description) ? primary.Description + " (ส่วนมีภาษี)" : "ส่วนมีภาษี",
                            Quantity = 1, UnitPrice = taxableNet, VatRate = 7,
                            WithholdingTaxRate = whtRate,   // WHT คิดบนฐาน net ทั้งใบ → ใส่ทั้งสอง line
                            AccountId = primary.AccountId, AccountCode = primary.AccountCode, Category = primary.Category
                        },
                        new IntegrationLineRequest
                        {
                            ItemCode = primary.ItemCode, ItemName = primary.ItemName,
                            Description = !string.IsNullOrEmpty(primary.Description) ? primary.Description + " (ส่วนไม่มีภาษี)" : "ส่วนไม่มีภาษี",
                            Quantity = 1, UnitPrice = nonTaxableNet, VatRate = 0,
                            WithholdingTaxRate = whtRate,
                            AccountId = primary.AccountId, AccountCode = primary.AccountCode, Category = primary.Category
                        }
                    };
                }
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
                VatRate = hasInputVat ? 7 : 0,
                IncludeVat = lines.Count > 1 ? false : hasInputVat,
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
                PaymentAccountId = ResolveAccountId(paymentAccountId) ?? GetPaymentMethodAccountId(paymentMethod),
                Lines = lines
            };
        }

        /// <summary>
        /// Map ใบสำคัญจ่ายที่ "จ่ายเงินแล้ว" → PV request (/api/integration/payment-vouchers)
        /// ใบเดียวจบ: NextAcc สร้างเอกสาร PV Approved จ่ายครบ + journal
        /// (DR ค่าใช้จ่าย+ภาษีซื้อ / CR เงินสด + CR WHT ค้างจ่าย 21916/21917) + 50ทวิ อัตโนมัติ
        /// — ไม่ผ่านเจ้าหนี้ 21220 จึงไม่เกิดเจ้าหนี้หลอกใน GL
        /// reuse การสร้างบรรทัดจาก MapVoucherToExpense เพื่อให้ account mapping เหมือนกันทุกกรณี
        /// </summary>
        public CreateIntegrationPaymentVoucherRequest MapVoucherToPaymentVoucher(
            int voucherId, string expenseCategory, decimal amount, string paymentMethod,
            DateTime voucherDate, string description, string payeeName,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0,
            string paymentAccountId = null, string expenseAccountId = null,
            List<ExpenseLine> expenseLines = null, string documentNumber = null,
            decimal vatAmount = 0)
        {
            var exp = MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                paymentAccountId, expenseAccountId, expenseLines, documentNumber, vatAmount);

            return new CreateIntegrationPaymentVoucherRequest
            {
                ExternalRef = !string.IsNullOrEmpty(exp.ExternalRef) ? exp.ExternalRef : exp.Reference,
                SupplierExternalId = exp.SupplierExternalId,
                SupplierName = exp.SupplierName,
                SupplierTaxId = exp.SupplierTaxId,
                DocumentDate = exp.DocumentDate,
                PaymentDate = voucherDate,
                Lines = exp.Lines,
                VatRate = exp.VatRate ?? (hasInputVat ? 7 : 0),
                IncludeVat = exp.IncludeVat,
                // Forward the resolved credit (จ่ายเงินจาก) account so NextAcc credits the
                // correct account (e.g. เจ้าหนี้กรรมการ when paid from เงินทดรองกรรมการ) instead
                // of always defaulting to เงินสด. MapVoucherToExpense already resolved these.
                PaymentMethod = exp.PaymentMethod,
                PaymentAccountId = exp.PaymentAccountId,
                Notes = exp.Description
            };
        }

        /// <summary>
        /// สร้าง "ใบสำคัญจ่าย (PaymentVoucher, DocumentType=13)" ผ่าน company /document endpoint.
        /// ใช้เมื่อ "จ่ายเงินจริง" (ไม่ใช่เครดิต) — เงินออกจริง → NextAcc PV branch:
        ///   Dr ค่าใช้จ่าย (ราย line) + Dr ภาษีซื้อ / Cr แหล่งเงิน (PaymentAccountId — เงินสด/ธนาคาร/
        ///   เจ้าหนี้กรรมการ ตามที่ map) − WHT 21916/21917 (ตาม ContactType). standalone PV →
        ///   NextAcc default PaymentType=Cash (เงินออกทันที). แก้ปัญหาที่ /integration/payment-vouchers
        ///   บังคับ Cr เงินสดเสมอ จนกรณีจ่ายแบบไม่ใช่เงินสดตกไปเป็น Expense.
        /// </summary>
        public CreateDocumentRequest MapVoucherToDocument(
            int voucherId, string expenseCategory, decimal amount, string paymentMethod,
            DateTime voucherDate, string description, string payeeName, Guid contactId,
            bool hasInputVat = false, decimal whtRate = 0, decimal whtAmount = 0,
            string paymentAccountId = null, string expenseAccountId = null,
            List<ExpenseLine> expenseLines = null, string documentNumber = null,
            decimal vatAmount = 0)
        {
            // reuse line/VAT/WHT/แหล่งเงิน resolution จาก expense mapper (รวม split VAT ผสม)
            var exp = MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                paymentAccountId, expenseAccountId, expenseLines, documentNumber, vatAmount);

            var docLines = new List<DocumentLineRequest>();
            foreach (var l in exp.Lines)
            {
                docLines.Add(new DocumentLineRequest
                {
                    Description = !string.IsNullOrEmpty(l.Description) ? l.Description : l.ItemName,
                    Quantity = l.Quantity > 0 ? l.Quantity : 1,
                    UnitPrice = l.UnitPrice,
                    VatRate = l.VatRate,
                    WithholdingTaxRate = l.WithholdingTaxRate,
                    AccountId = (l.AccountId != Guid.Empty) ? l.AccountId : (Guid?)null,
                    AccountCode = l.AccountCode
                });
            }

            return new CreateDocumentRequest
            {
                DocumentType = 13, // PaymentVoucher (ใบสำคัญจ่าย)
                DocumentDate = voucherDate,
                PaymentDate = voucherDate,
                ContactId = contactId,
                Reference = exp.Reference,
                SupplierInvoiceNumber = exp.ExternalRef,
                // แหล่งเงิน (ฝั่งเครดิต/เงินออก) — NextAcc PV Cr บัญชีนี้ตรง ๆ
                PaymentAccountId = exp.PaymentAccountId,
                // standalone PV → NextAcc default PaymentType=Cash (เงินออกทันที, ไม่เปิดเจ้าหนี้)
                PricesIncludeVat = exp.IncludeVat,
                Notes = exp.Description,
                Lines = docLines
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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

        /// <summary>
        /// Build credit note ที่กลับใบเสร็จเต็มจำนวน — ใช้แทน VoidDocumentAsync (JWT-only)
        /// NextAcc auto-creates journal: DR Revenue + DR VAT / CR A/R และลด BalanceDue ของ original
        /// </summary>
        public InboundCreditNoteRequest MapReceiptVoidToCreditNote(
            int reservationId, string receiptNumber, decimal totalAmount, bool hasVat,
            string customerName, DateTime voidDate, string reason)
        {
            decimal vatAmount = hasVat ? Math.Round(totalAmount * 7m / 107m, 2, MidpointRounding.AwayFromZero) : 0m;
            decimal netAmount = totalAmount - vatAmount;

            return new InboundCreditNoteRequest
            {
                ExternalRef = $"CN-{receiptNumber}",
                OriginalInvoiceRef = receiptNumber,
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
                CustomerName = customerName,
                DocumentDate = voidDate,
                Reason = reason ?? $"ยกเลิกใบเสร็จ {receiptNumber}",
                Lines = new List<IntegrationLineRequest>
                {
                    new IntegrationLineRequest
                    {
                        ItemName = $"ยกเลิกใบเสร็จ {receiptNumber}",
                        Description = $"ยกเลิกใบเสร็จ {receiptNumber} - การจอง #{reservationId}",
                        Quantity = 1,
                        UnitPrice = netAmount,
                        AccountId = GetAccountId("ROOM_REVENUE"),
                        VatRate = hasVat ? 7m : 0m
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
                BookingNumber = reservationId > 0 ? $"RES-{reservationId}" : null,
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
                ContactType = IsJuristicPerson(taxId) ? "JuristicPerson" : "Individual"
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
                ContactType = IsJuristicPerson(taxId) ? "JuristicPerson" : "Individual"
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
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
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
            DateTime payrollDate, string description, string paymentMethod = null)
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
                PaymentMethod = NormalizePaymentMethod(paymentMethod),
                PaymentAccountId = string.IsNullOrEmpty(paymentMethod) ? GetAccountId("CASH") : GetPaymentMethodAccountId(paymentMethod),
                Sensitivity = "Payroll",
                Lines = lines
            };
        }

        /// <summary>
        /// กลับรายการจ่ายเงินเดือน — ต้องสะท้อนสิ่งที่บันทึกไว้จริง:
        ///   DOCUMENT mode: expense บันทึกแค่ DR เงินเดือน + DR ปกส.นายจ้าง / CR เงินสด(เต็มยอด)
        ///                  → กลับเป็น DR เงินสด / CR เงินเดือน + CR ปกส.นายจ้าง
        ///   JOURNAL mode:  บันทึกแบบเต็ม (มี ปกส.ค้างจ่าย / ภาษีค้างจ่าย) → กลับด้านทุกบรรทัด
        /// </summary>
        public CreateJournalEntryRequest MapPayrollReversalToJournal(
            decimal totalSalary, DateTime reverseDate, string period,
            decimal socialSecurityEmployee, decimal socialSecurityEmployer,
            decimal whtAmount, string paymentMethod, string documentNumber, bool isDocumentMode)
        {
            string refStr = !string.IsNullOrEmpty(documentNumber) ? documentNumber : $"PR-{reverseDate:yyyyMM}";
            var cashAccountId = string.IsNullOrEmpty(paymentMethod) ? GetAccountId("CASH") : GetPaymentMethodAccountId(paymentMethod);

            List<JournalEntryLineRequest> lines;

            if (isDocumentMode)
            {
                // ตรงข้ามกับ MapPayrollToExpense (DR เงินเดือน+ปกส.นายจ้าง / CR เงินสดเต็มยอด)
                decimal totalSsf = socialSecurityEmployee + socialSecurityEmployer;
                decimal docTotal = totalSalary + totalSsf;
                var salaryAccountId = GetAccountId("SALARY_EXPENSE");

                lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest { AccountId = cashAccountId, DebitAmount = docTotal, CreditAmount = 0, Description = $"กลับเงินจ่ายเงินเดือน - {period}" },
                    new JournalEntryLineRequest { AccountId = salaryAccountId, DebitAmount = 0, CreditAmount = totalSalary, Description = $"กลับเงินเดือน - {period}" }
                };
                if (totalSsf > 0)
                {
                    Guid ssfExpenseId = TryGetAccountId("SSF_EMPLOYER_EXPENSE", out var se) ? se : GetAccountId("SALARY_EXPENSE");
                    lines.Add(new JournalEntryLineRequest { AccountId = ssfExpenseId, DebitAmount = 0, CreditAmount = totalSsf, Description = $"กลับประกันสังคมส่วนนายจ้าง - {period}" });
                }
            }
            else
            {
                // ตรงข้ามกับ MapPayrollToJournal ทุกบรรทัด
                var original = MapPayrollToJournal(totalSalary, reverseDate, period,
                    socialSecurityEmployee, socialSecurityEmployer, whtAmount, paymentMethod);
                lines = original.Lines;
                foreach (var line in lines)
                {
                    decimal dr = line.DebitAmount, cr = line.CreditAmount;
                    line.DebitAmount = cr;
                    line.CreditAmount = dr;
                }
            }

            return new CreateJournalEntryRequest
            {
                EntryDate = reverseDate,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการจ่ายเงินเดือน {refStr} - งวด {period}",
                Reference = $"{refStr}-VOID",
                Sensitivity = "Payroll",
                Lines = lines
            };
        }

        // ══════════════════════════════════════════════
        // Asset Reclassification Journal
        // (DR สินทรัพย์ถาวร / CR ค่าใช้จ่าย — เปลี่ยนประเภทจากค่าใช้จ่ายเป็นสินทรัพย์)
        // ══════════════════════════════════════════════

        /// <summary>
        /// สร้าง journal เพื่อ reclassify รายจ่ายที่บันทึกเป็นค่าใช้จ่ายแล้ว ให้เป็นสินทรัพย์ถาวร
        /// PV/Expense ที่โพสต์ไปแล้ว: DR Expense / CR Cash
        /// Reclassification: DR Fixed Asset / CR Expense → ผลสุทธิ: DR Fixed Asset / CR Cash
        /// </summary>
        public CreateJournalEntryRequest MapAssetReclassificationToJournal(
            decimal assetAmount, string assetName, DateTime purchaseDate,
            string voucherDocNumber, string expenseAccountId = null, string expenseCategory = null)
        {
            Guid fixedAssetAccountId = TryGetAccountId("FIXED_ASSET", out var fa) ? fa : GetAccountId("EQUIPMENT");
            Guid expAccId = !string.IsNullOrEmpty(expenseAccountId)
                ? (ResolveAccountId(expenseAccountId) ?? GetExpenseCategoryAccountId(expenseCategory ?? "OTHER"))
                : GetExpenseCategoryAccountId(expenseCategory ?? "OTHER");

            return new CreateJournalEntryRequest
            {
                EntryDate = purchaseDate,
                JournalType = NexaaccJournalType.General,
                Description = $"บันทึกสินทรัพย์ - {assetName} (จาก {voucherDocNumber})",
                Reference = $"ASSET-{voucherDocNumber}",
                Lines = new List<JournalEntryLineRequest>
                {
                    new JournalEntryLineRequest
                    {
                        AccountId = fixedAssetAccountId,
                        DebitAmount = assetAmount,
                        CreditAmount = 0,
                        Description = $"สินทรัพย์ถาวร - {assetName}"
                    },
                    new JournalEntryLineRequest
                    {
                        AccountId = expAccId,
                        DebitAmount = 0,
                        CreditAmount = assetAmount,
                        Description = $"เปลี่ยนประเภทจากค่าใช้จ่ายเป็นสินทรัพย์ - {voucherDocNumber}"
                    }
                }
            };
        }
    }
}
