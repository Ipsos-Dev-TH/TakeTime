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

        // ══════════════════════════════════════════════════════════════════════
        //  Date era/culture safety — กันวันที่ พ.ศ./ปีเพี้ยน หลุดไป NextAcc
        //  ปัญหา: บน thread ที่ CurrentCulture = th-TH ปฏิทินเริ่มต้นเป็น "พุทธ"
        //         → DateTime.ToString("yyyy-MM-dd") ได้ปี พ.ศ. (2569) ไม่ใช่ ค.ศ. (2026)
        //         → DateTime.Parse ก็ตีความสตริงเป็น พ.ศ. ด้วย
        //         → ถ้า enqueue/parse ข้าม thread คนละ culture ปีจะเพี้ยน (2026↔2569↔1483)
        //  วิธีแก้: ทุกจุดที่ serialize/parse วันที่ของ NextAcc ใช้ InvariantCulture (ค.ศ. เสมอ)
        //         + era-guard: ปี>2400 → −543 (พ.ศ.→ค.ศ.), ปี<1900 → +543 (คืนค่าที่ถูกลบเกิน)
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>บังคับให้ DateTime อยู่ในช่วง ค.ศ. ปกติเสมอ (กัน พ.ศ. 2569 / ค่าเพี้ยน 1483)</summary>
        internal static DateTime NormalizeEra(DateTime d)
        {
            int y = d.Year;
            if (y > 2400) return d.AddYears(-543);   // พ.ศ. → ค.ศ.
            if (y < 1900) return d.AddYears(543);    // ค่าที่ถูกลบ 543 เกินไปแล้ว → คืน ค.ศ.
            return d;
        }

        /// <summary>serialize วันที่สำหรับ payload/NextAcc: ค.ศ. + culture-invariant เสมอ</summary>
        internal static string AcctDate(DateTime d, bool withTime = false)
        {
            d = NormalizeEra(d);
            return d.ToString(withTime ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd",
                System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>ถอด \uXXXX escape ใน response JSON ให้อ่านเป็นภาษาไทย (ใช้กับ Error_Message ในคิว)</summary>
        internal static string DecodeUnicodeEscapes(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf("\\u", StringComparison.OrdinalIgnoreCase) < 0) return s;
            try
            {
                return System.Text.RegularExpressions.Regex.Replace(s, @"\\u([0-9a-fA-F]{4})",
                    m => ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString());
            }
            catch { return s; }
        }

        /// <summary>เติมคำแนะนำภาษาไทยต่อท้าย error ที่รู้จัก เพื่อให้ผู้ใช้แก้เองได้จากหน้าคิว</summary>
        private static string BuildApiErrorHint(string body)
        {
            string b = body ?? "";
            if (b.Contains("86/4"))
                return "\n💡 วิธีแก้: NextAcc เปิดบังคับข้อมูลผู้ซื้อเต็มรูป (§86/4) — " +
                       "(1) เติม เลขผู้เสียภาษี 13 หลัก + ที่อยู่ ของลูกค้าในระบบ TakeTime แล้วกด Retry " +
                       "(ระบบจะส่งข้อมูลลูกค้าไปอัปเดต contact ให้เอง) หรือ " +
                       "(2) ลูกค้าบุคคลทั่วไปไม่มีเลขภาษี → ปิด setting 'บังคับ §86/4 ครบทุก field' ในหน้าตั้งค่า NextAcc";
            if (b.Contains("งวดบัญชี") && (b.Contains("ปิด") || b.Contains("Closed")))
                return "\n💡 วิธีแก้: งวดบัญชีเดือนนั้นปิดแล้ว — เปิดงวดชั่วคราวบน NextAcc หรือปรับผ่านใบลดหนี้/เพิ่มหนี้เดือนปัจจุบัน";
            return "";
        }

        /// <summary>parse วันที่จาก payload: culture-invariant + era-guard (คืน ค.ศ. เสมอ)</summary>
        internal static DateTime ParseAcctDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return NormalizeEra(DateTime.Now);
            DateTime d;
            if (!DateTime.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out d))
            {
                if (!DateTime.TryParse(s, out d)) return NormalizeEra(DateTime.Now);
            }
            return NormalizeEra(d);
        }

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
            string supplierExternalId = null, string supplierTaxId = null,
            decimal vatAmount = 0)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0) return -1;

            if (!string.IsNullOrEmpty(documentNumber))
            {
                long existing = FindPendingEntry("VOUCHER", "CREATE_VOUCHER_JOURNAL", "documentNumber", documentNumber);
                if (existing > 0) return existing;

                // Anti-duplicate: if a COMPLETED entry exists within the window, return it
                // (prevents form resubmission / browser refresh from creating duplicates)
                // ยกเว้น edit flow: ถ้ามี VOID_VOUCHER ของเอกสารเดียวกันที่ใหม่กว่า CREATE เดิม
                // แปลว่าเอกสารเดิมถูก void เพื่อสร้างใหม่ด้วยเลขเดิม — ต้องปล่อยให้สร้าง
                // ไม่งั้นเอกสารจะโดน void แล้วหายจาก NextAcc ถาวร
                long recent = FindRecentCompletedEntry("VOUCHER", "CREATE_VOUCHER_JOURNAL", "documentNumber", documentNumber, 86400);
                if (recent > 0 && !HasNewerVoidEntry("VOUCHER", "VOID_VOUCHER", "documentNumber", documentNumber, recent))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnqueuePaymentVoucher: doc={documentNumber} returned recent COMPLETED queueId={recent} (anti-duplicate within 86400s window)",
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
                { "voucherDate", AcctDate(voucherDate) },
                { "description", description },
                { "payeeName", payeeName },
                { "hasInputVat", hasInputVat },
                { "whtRate", whtRate },
                { "whtAmount", whtAmount },
                { "isCredit", isCredit },
                { "autoRecordPayment", autoRecordPayment },
                { "vatAmount", vatAmount }
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
        /// Enqueue payment for a credit voucher that was previously recorded (ตั้งหนี้แล้ว).
        /// Creates journal: DR Accounts Payable / CR Cash|Bank
        /// Call when user marks a credit voucher as paid.
        /// </summary>
        public long EnqueueCreditVoucherPayment(string originalDocumentNumber, decimal amount,
            string paymentMethod, DateTime paymentDate, string vendorName,
            string paymentAccountId = null)
        {
            if (!_config.IsConfigured) return -1;
            if (amount <= 0 || string.IsNullOrEmpty(originalDocumentNumber)) return -1;

            string refKey = $"CREDITPAY-{originalDocumentNumber}";

            long existing = FindPendingEntry("VOUCHER", "PAY_CREDIT_VOUCHER", "creditPayRef", refKey);
            if (existing > 0) return existing;

            long recent = FindRecentCompletedEntry("VOUCHER", "PAY_CREDIT_VOUCHER", "creditPayRef", refKey, 86400);
            if (recent > 0) return recent;

            var payload = new Dictionary<string, object>
            {
                { "creditPayRef", refKey },
                { "originalDocumentNumber", originalDocumentNumber },
                { "amount", amount },
                { "paymentMethod", paymentMethod },
                { "paymentDate", AcctDate(paymentDate) },
                { "vendorName", vendorName ?? "" }
            };
            if (!string.IsNullOrEmpty(paymentAccountId))
                payload["paymentAccountId"] = paymentAccountId;

            return InsertQueue("VOUCHER", 0, "PAY_CREDIT_VOUCHER", payload);
        }

        /// <summary>
        /// Enqueue payroll journal entry with proper SSF/WHT breakdown.
        /// เงินเดือน (ม.40(1)) โพสต์เป็น journal เสมอ (ไม่ออก 50ทวิ รายเดือน); ภงด.1 ออกใบแนบฝั่ง TakeTime เอง.
        /// </summary>
        public long EnqueuePayrollJournal(decimal totalSalary, DateTime payDate, string period,
            decimal socialSecurityEmployee = 0, decimal socialSecurityEmployer = 0,
            decimal whtAmount = 0, string documentNumber = null, string paymentMethod = null,
            string employeeName = null, string citizenId = null)
        {
            if (!_config.IsConfigured) return -1;
            if (totalSalary <= 0) return -1;

            if (!string.IsNullOrEmpty(documentNumber))
            {
                long existing = FindPendingEntry("PAYROLL", "CREATE_PAYROLL_ENTRY", "documentNumber", documentNumber);
                if (existing > 0) return existing;

                long recent = FindRecentCompletedEntry("PAYROLL", "CREATE_PAYROLL_ENTRY", "documentNumber", documentNumber, 86400);
                if (recent > 0 && !HasNewerVoidEntry("PAYROLL", "VOID_PAYROLL", "documentNumber", documentNumber, recent))
                    return recent;
            }

            var payload = new Dictionary<string, object>
            {
                { "totalSalary", totalSalary },
                { "payDate", AcctDate(payDate) },
                { "period", period },
                { "socialSecurityEmployee", socialSecurityEmployee },
                { "socialSecurityEmployer", socialSecurityEmployer },
                { "whtAmount", whtAmount }
            };
            if (!string.IsNullOrEmpty(documentNumber))
                payload["documentNumber"] = documentNumber;
            if (!string.IsNullOrEmpty(paymentMethod))
                payload["paymentMethod"] = paymentMethod;
            if (!string.IsNullOrEmpty(employeeName))
                payload["employeeName"] = employeeName;
            if (!string.IsNullOrEmpty(citizenId))
                payload["citizenId"] = citizenId;

            return InsertQueue("PAYROLL", 0, "CREATE_PAYROLL_ENTRY", payload);
        }

        /// <summary>
        /// Enqueue full payroll run sync to NextAcc payroll system.
        /// Flow: Sync พนักงาน → Create PayrollRun → Calculate → Approve → Pay
        /// NextAcc จัดการ GL + ภงด.1 + สปส.1-10 + 50ทวิ + payslip ทั้งหมด
        /// ใช้เมื่อ PayrollSyncMode = DOCUMENT
        /// </summary>
        public long EnqueuePayrollRunSync(int payrollPeriodId)
        {
            if (!_config.IsConfigured) return -1;

            string refKey = $"PAYROLL-PERIOD-{payrollPeriodId}";
            long existing = FindPendingEntry("PAYROLL", "SYNC_PAYROLL_RUN", "refKey", refKey);
            if (existing > 0) return existing;
            long recent = FindRecentCompletedEntry("PAYROLL", "SYNC_PAYROLL_RUN", "refKey", refKey, 86400);
            if (recent > 0) return recent;

            var payload = new Dictionary<string, object>
            {
                { "refKey", refKey },
                { "payrollPeriodId", payrollPeriodId }
            };

            return InsertQueue("PAYROLL", payrollPeriodId, "SYNC_PAYROLL_RUN", payload);
        }

        /// <summary>
        /// Enqueue payroll IMPORT to NextAcc (Option A): ส่งยอดที่ TakeTime คำนวณเองต่อพนักงาน เข้า
        /// POST /payroll/runs/import (Recalculate=false) → NextAcc สร้าง run Calculated ตามยอดเรา →
        /// approve → pay → ออก GL + ภงด.1 + สปส.1-10 + 50ทวิ + payslip จากยอดของเรา (ไม่คำนวณใหม่).
        /// ใช้เมื่อ PayrollSyncMode = DOCUMENT_IMPORT (รองรับยอดผันแปรต่องวด)
        /// </summary>
        public long EnqueuePayrollRunImport(int payrollPeriodId)
        {
            if (!_config.IsConfigured) return -1;

            string refKey = $"PAYROLL-IMPORT-{payrollPeriodId}";
            long existing = FindPendingEntry("PAYROLL", "IMPORT_PAYROLL_RUN", "refKey", refKey);
            if (existing > 0) return existing;
            long recent = FindRecentCompletedEntry("PAYROLL", "IMPORT_PAYROLL_RUN", "refKey", refKey, 86400);
            if (recent > 0) return recent;

            var payload = new Dictionary<string, object>
            {
                { "refKey", refKey },
                { "payrollPeriodId", payrollPeriodId }
            };

            return InsertQueue("PAYROLL", payrollPeriodId, "IMPORT_PAYROLL_RUN", payload);
        }

        /// <summary>
        /// Enqueue asset reclassification journal.
        /// เมื่อซื้อสินทรัพย์ถาวรผ่านใบสำคัญจ่าย PV จะลงบัญชี DR ค่าใช้จ่าย / CR เงินสด
        /// journal นี้ reclassify: DR สินทรัพย์ถาวร / CR ค่าใช้จ่าย → ผลสุทธิ DR สินทรัพย์ / CR เงินสด
        /// </summary>
        public long EnqueueAssetReclassification(decimal assetAmount, string assetName,
            DateTime purchaseDate, string voucherDocNumber,
            string expenseAccountId = null, string expenseCategory = null)
        {
            if (!_config.IsConfigured) return -1;
            if (assetAmount <= 0 || string.IsNullOrEmpty(voucherDocNumber)) return -1;

            string refKey = $"ASSET-{voucherDocNumber}";
            long existing = FindPendingEntry("ASSET", "ASSET_RECLASSIFICATION", "refKey", refKey);
            if (existing > 0) return existing;
            long recent = FindRecentCompletedEntry("ASSET", "ASSET_RECLASSIFICATION", "refKey", refKey, 86400);
            if (recent > 0) return recent;

            var payload = new Dictionary<string, object>
            {
                { "refKey", refKey },
                { "assetAmount", assetAmount },
                { "assetName", assetName ?? "สินทรัพย์ถาวร" },
                { "purchaseDate", AcctDate(purchaseDate) },
                { "voucherDocNumber", voucherDocNumber }
            };
            if (!string.IsNullOrEmpty(expenseAccountId))
                payload["expenseAccountId"] = expenseAccountId;
            if (!string.IsNullOrEmpty(expenseCategory))
                payload["expenseCategory"] = expenseCategory;

            return InsertQueue("ASSET", 0, "ASSET_RECLASSIFICATION", payload);
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

            // Anti-duplicate: if a COMPLETED entry exists within the window, return it
            // (prevents form resubmission / browser refresh from creating duplicates)
            // ยกเว้น edit flow (void → สร้างใหม่เลขเดิม): ถ้ามี VOID_RECEIPT ที่ใหม่กว่า ต้องปล่อยให้สร้าง
            long recent = FindRecentCompletedEntry("RECEIPT", "CREATE_RECEIPT_DOCUMENT", "receiptNumber", receiptNumber, 86400);
            if (recent > 0 && !HasNewerVoidEntry("RECEIPT", "VOID_RECEIPT", "receiptNumber", receiptNumber, recent))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueReceipt: receipt={receiptNumber} returned recent COMPLETED queueId={recent} (anti-duplicate within 86400s window)",
                    "SYSTEM");
                return recent;
            }

            var payload = new Dictionary<string, object>
            {
                { "reservationId", reservationId },
                { "receiptNumber", receiptNumber },
                { "totalAmount", totalAmount },
                { "vatAmount", vatAmount },
                { "receiptDate", AcctDate(receiptDate) },
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
                { "checkoutDate", AcctDate(checkoutDate) }
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
                { "refundDate", AcctDate(refundDate) }
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
                { "forfeitDate", AcctDate(forfeitDate) },
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
                { "receiveDate", AcctDate(receiveDate, true) },
                { "supplierName", supplierName ?? "" },
                { "paymentMethod", paymentMethod ?? "" },
                { "hasInputVat", hasInputVat }
            };
            EnqueueStockQtyPush(productId, productName, quantity, "IN", costPerUnit, receiveDate, refStr);
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
                { "outDate", AcctDate(outDate, true) },
                { "reason", reason ?? "" }
            };
            EnqueueStockQtyPush(productId, productName, quantity, "OUT", costPerUnit, outDate, refStr);
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
                { "reverseDate", AcctDate(reverseDate, true) },
                { "reason", reason ?? "" }
            };
            EnqueueStockQtyPush(productId, productName, quantity, "IN", costPerUnit, reverseDate, "REV-" + stockRef);
            return InsertQueue("STOCK", productId, "STOCK_OUT_COGS_REVERSE", payload);
        }

        // ──────────────────────────────────────────────
        // รวบยอดขายหน้าร้าน "ไม่ออกใบกำกับ" เป็นใบรับเงินสดสรุปรายวัน (auto, ไม่ต้องกด)
        // ──────────────────────────────────────────────

        /// <summary>
        /// รวบการขายหน้าร้านที่ไม่ออกใบกำกับ (Product_Out: Remark='ขาย', Account_Receipt_ID='0',
        /// Pos_Rollup_Ref IS NULL) ของ "วันที่ผ่านมาแล้ว" เป็นใบรับเงินสดสรุป 1 ใบ/วัน/แหล่งรับเงิน →
        /// EnqueueReceipt (รายได้+VAT ขาย) + EnqueueStockOutCogs ต่อสินค้า (ตัดสต๊อก Dr COGS/Cr Inventory).
        /// เรียกจาก background timer (Global.asax) — idempotent (marker Pos_Rollup_Ref + queue dedup).
        /// </summary>
        public void RollupPosDailySalesIfDue(int maxDaysPerRun = 14)
        {
            if (!_config.IsConfigured || !_config.Enabled || !_config.IsPosDailyRollupEnabled) return;

            try
            {
                // หา (วัน, แหล่งรับเงิน) ที่ยังไม่รวบ — เฉพาะวันที่จบแล้ว (< วันนี้) กันรวบวันที่ยังขายอยู่
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@cap) CAST(DateTime_Out AS DATE) AS D, Account_Paid_How_ID AS PH
                      FROM Product_Out
                      WHERE Remark = N'ขาย'
                        AND (Account_Receipt_ID = '0' OR Account_Receipt_ID IS NULL)
                        AND Pos_Rollup_Ref IS NULL
                        AND CAST(DateTime_Out AS DATE) < CAST(GETDATE() AS DATE)
                      GROUP BY CAST(DateTime_Out AS DATE), Account_Paid_How_ID
                      ORDER BY CAST(DateTime_Out AS DATE)",
                    new Dictionary<string, object> { { "@cap", maxDaysPerRun } });

                if (dt == null || dt.Rows.Count == 0) return;

                foreach (DataRow g in dt.Rows)
                {
                    DateTime day = Convert.ToDateTime(g["D"]);
                    string paidHowId = g["PH"]?.ToString() ?? "";
                    try { ProcessOnePosDay(day, paidHowId); }
                    catch (Exception exDay)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"PosDailyRollup: day={day:yyyy-MM-dd} paidHowId={paidHowId} ล้มเหลว: {exDay.Message}", "SYSTEM");
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"RollupPosDailySalesIfDue: {ex.Message}", "SYSTEM");
            }
        }

        private void ProcessOnePosDay(DateTime day, string paidHowId)
        {
            string ds = day.ToString("yyyyMMdd");
            string rollupRef = $"POSDAY-{ds}-{paidHowId}";

            // 1) ยอดต่อสินค้าในกลุ่ม (วัน+แหล่งรับเงิน) ที่ยังไม่รวบ
            var prod = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT po.Product_ID AS PID, SUM(po.Amount) AS Qty,
                         SUM(po.Amount * po.PricePerUnit) AS Gross,
                         MAX(p.Product_Name) AS Name, MAX(p.Cost_Price) AS Cost
                  FROM Product_Out po
                  LEFT JOIN Product p ON p.ID = po.Product_ID
                  WHERE po.Remark = N'ขาย'
                    AND (po.Account_Receipt_ID = '0' OR po.Account_Receipt_ID IS NULL)
                    AND po.Pos_Rollup_Ref IS NULL
                    AND CAST(po.DateTime_Out AS DATE) = @d
                    AND po.Account_Paid_How_ID = @ph
                  GROUP BY po.Product_ID",
                new Dictionary<string, object> { { "@d", day.Date }, { "@ph", paidHowId } });

            if (prod == null || prod.Rows.Count == 0) return;

            decimal grossTotal = 0m;
            foreach (DataRow r in prod.Rows) grossTotal += SafeDec(r["Gross"]);
            if (grossTotal <= 0m)
            {
                // ไม่มียอด (ราคา 0) → mark กันวนซ้ำ แล้วข้าม
                MarkPosRowsRolledUp(day, paidHowId, rollupRef);
                return;
            }

            // 2) ชื่อแหล่งรับเงิน + บัญชี NextAcc (สำหรับ Dr เงินสด/ธนาคาร)
            string paidHowName = "เงินสด";
            string paidHowAccId = null;
            try
            {
                var ph = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Paid_How, Nexaacc_AccountId FROM Account_Paid_How WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", paidHowId } });
                if (ph?.Rows.Count > 0)
                {
                    paidHowName = ph.Rows[0]["Paid_How"]?.ToString() ?? "เงินสด";
                    string acc = ph.Rows[0]["Nexaacc_AccountId"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(acc)) paidHowAccId = acc;
                }
            }
            catch { }
            if (string.IsNullOrEmpty(paidHowAccId)) paidHowAccId = LookupPaidHowAccountId(paidHowName);

            bool useVat = BusinessUsesVat();
            decimal exVat = useVat ? Math.Round(grossTotal / 1.07m, 2, MidpointRounding.AwayFromZero) : grossTotal;
            decimal vat = useVat ? (grossTotal - exVat) : 0m;

            // 3) Idempotency guard: Account_Receipt(rollupRef) = "เคย enqueue ครบแล้ว" (สร้างเป็น step สุดท้าย
            //    ก่อน mark). ถ้ามีอยู่แล้ว = รอบก่อน enqueue ไปแล้ว แต่ crash ก่อน mark → แค่ mark ซ้ำ ไม่ enqueue ใหม่
            //    (กันใบรับเงิน/COGS ซ้ำเกินหน้าต่าง dedup 24 ชม.)
            bool receiptExists = false;
            try
            {
                var ex = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 ID FROM Account_Receipt WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", rollupRef } });
                receiptExists = ex?.Rows.Count > 0;
            }
            catch { }

            if (receiptExists)
            {
                MarkPosRowsRolledUp(day, paidHowId, rollupRef);
                return;
            }

            // 4) รายได้ → EnqueueReceipt (ใช้ path เดิม: Dr เงินสด/Cr รายได้สินค้า/Cr ภาษีขาย, VAT คำนวณ downstream)
            EnqueueReceipt(0, rollupRef, grossTotal, 0, day.Date,
                $"ขายสดหน้าร้าน {day:dd/MM/yyyy} ({paidHowName})",
                isDeposit: false, paymentMethod: paidHowName,
                revenueType: "PRODUCT_REVENUE", paymentAccountId: paidHowAccId);

            // 5) COGS ตัดสต๊อก ต่อสินค้า (Dr COGS / Cr Inventory) — ต้นทุนจาก Product.Cost_Price
            foreach (DataRow r in prod.Rows)
            {
                int pid = 0; int.TryParse(r["PID"]?.ToString(), out pid);
                decimal qty = SafeDec(r["Qty"]);
                decimal cost = SafeDec(r["Cost"]);
                string name = r["Name"]?.ToString() ?? "";
                if (pid > 0 && qty > 0 && cost > 0)
                    EnqueueStockOutCogs(pid, name, qty, cost, day.Date, "ขายสดหน้าร้าน (รวบรายวัน)",
                        stockRef: $"POSDAY-COGS-{ds}-{paidHowId}-{pid}");
            }

            // 6) สร้าง Account_Receipt "ใบรับเงินสดสรุปรายวัน" (durable marker ว่า enqueue ครบ) — หลัง enqueue
            //    เพื่อให้ "receiptExists" ข้างบนหมายถึง enqueue เสร็จแน่ ๆ (ProcessReceipt อ่านแถวนี้รอบ timer ถัดไป)
            var rp = new Dictionary<string, object>
            {
                { "@ID", rollupRef }, { "@CreatedDate", day.Date }, { "@TotalAmount", grossTotal },
                { "@Vat", vat }, { "@ExVat", exVat }, { "@PaidType", paidHowName }
            };
            _code.DatabaseInsertSafe(_connectionString,
                "INSERT INTO [dbo].[Account_Receipt] " +
                "([ID],[Reservation_ID],[Created_Date],[Total_Amount],[Vat],[Total_Amount_Exclude_Vat]," +
                "[IsDeposit],[UseDeposit],[Paid_Type],[Status],[Created_By_ID],[Etax],[Customer_ID]) " +
                "VALUES (@ID,'0',@CreatedDate,@TotalAmount,@Vat,@ExVat,0,0,@PaidType,'Normal',0,0,0)",
                rp);
            _code.DatabaseInsertSafe(_connectionString,
                "INSERT INTO [dbo].[Account_Receipt_Detail] " +
                "([Number],[Receipt_ID],[ProductType_ID],[Product_ID],[Product_Data],[Product_Amount],[Product_Unit],[Price_PerPeice],[Price_Amount]) " +
                "VALUES (1,@ReceiptID,'3','0',@Data,1,N'ครั้ง',@Total,@Total)",
                new Dictionary<string, object>
                {
                    { "@ReceiptID", rollupRef },
                    { "@Data", $"สรุปยอดขายหน้าร้าน {day:dd/MM/yyyy} ({paidHowName})" },
                    { "@Total", grossTotal }
                });

            // 7) mark แถว Product_Out ว่ารวบแล้ว (กันรวบซ้ำ)
            MarkPosRowsRolledUp(day, paidHowId, rollupRef);

            _code.Logs(_connectionString, "AccountingSync",
                $"PosDailyRollup: doc={rollupRef} gross={grossTotal:N2} vat={vat:N2} products={prod.Rows.Count} paidHow={paidHowName}", "SYSTEM");
        }

        private void MarkPosRowsRolledUp(DateTime day, string paidHowId, string rollupRef)
        {
            _code.DatabaseInsertSafe(_connectionString,
                @"UPDATE Product_Out SET Pos_Rollup_Ref = @ref
                  WHERE Remark = N'ขาย'
                    AND (Account_Receipt_ID = '0' OR Account_Receipt_ID IS NULL)
                    AND Pos_Rollup_Ref IS NULL
                    AND CAST(DateTime_Out AS DATE) = @d
                    AND Account_Paid_How_ID = @ph",
                new Dictionary<string, object>
                {
                    { "@ref", rollupRef }, { "@d", day.Date }, { "@ph", paidHowId }
                });
        }

        /// <summary>ธุรกิจจดทะเบียน VAT หรือไม่ (Business_Info.Use_Vat) — ใช้ถอด VAT จากยอดขายรวม</summary>
        private bool BusinessUsesVat()
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString, "SELECT TOP 1 Use_Vat FROM Business_Info", null);
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Use_Vat"] != DBNull.Value)
                {
                    string v = dt.Rows[0]["Use_Vat"].ToString();
                    return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase) || v.Equals("True", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { }
            return false;
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
                { "adjustDate", AcctDate(adjustDate, true) },
                { "reason", reason ?? "" }
            };
            EnqueueStockQtyPush(productId, productName, Math.Abs(quantityDiff), quantityDiff > 0 ? "IN" : "OUT", costPerUnit, adjustDate, refStr);
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
                { "writeOffDate", AcctDate(writeOffDate, true) },
                { "reason", reason ?? "" }
            };
            EnqueueStockQtyPush(productId, productName, quantity, "OUT", costPerUnit, writeOffDate, refStr);
            return InsertQueue("STOCK", productId, "STOCK_WRITEOFF", payload);
        }

        // ──────────────────────────────────────────────
        // Sync จำนวนสต๊อก (qty) 2 ทาง กับ NextAcc /product/stock/*  (feature-flag, default off)
        //   ขาออก: STOCK_QTY_PUSH → POST /product/stock/adjust (qty-only, ไม่ซ้ำ GL)
        //   ขากลับ: PullNextAccStockMovementsIfDue → GET /product/{id}/stock/movements → ลง Product_In/Out
        //   echo-safe: movement ที่เรา push บันทึกใน Nexaacc_Stock_Movement_Seen → ขากลับข้าม
        // ──────────────────────────────────────────────

        /// <summary>คู่กับ EnqueueStock* — ส่ง qty push เข้าคิว (no-op ถ้า flag ปิด). idempotent ด้วย ref ของตัวเอง</summary>
        private void EnqueueStockQtyPush(int productId, string productName, decimal qty, string movementType,
            decimal unitCost, DateTime moveDate, string srcRef)
        {
            if (!_config.IsConfigured || !_config.IsStockQtySyncEnabled) return;
            if (productId <= 0 || qty <= 0) return;

            string refStr = $"QTY-{movementType}-{srcRef}";
            if (FindPendingEntry("STOCK", "STOCK_QTY_PUSH", "stockRef", refStr) > 0) return;
            if (FindRecentCompletedEntry("STOCK", "STOCK_QTY_PUSH", "stockRef", refStr, 86400) > 0) return;

            var payload = new Dictionary<string, object>
            {
                { "stockRef", refStr },
                { "productId", productId },
                { "productName", productName ?? "" },
                { "quantity", qty },
                { "movementType", movementType },
                { "unitCost", unitCost },
                { "moveDate", AcctDate(moveDate, true) }
            };
            InsertQueue("STOCK", productId, "STOCK_QTY_PUSH", payload);
        }

        private async Task<string> ProcessStockQtyPush(Dictionary<string, object> p)
        {
            if (!_config.IsStockQtySyncEnabled) return "SKIPPED_QTY_SYNC_OFF";

            int productId = Convert.ToInt32(p["productId"]);
            decimal qty = SafeDec(p["quantity"]);
            string movementType = p.ContainsKey("movementType") ? p["movementType"]?.ToString() : "ADJUST";
            decimal unitCost = p.ContainsKey("unitCost") ? SafeDec(p["unitCost"]) : 0m;
            string srcRef = p.ContainsKey("stockRef") ? p["stockRef"]?.ToString() : "";
            if (qty <= 0) return "SKIPPED_ZERO_QTY";

            Guid nexId = ResolveNexaaccProductId(productId);
            if (nexId == Guid.Empty)
            {
                // ยังไม่ map → sync product master ก่อน แล้ว retry รอบหน้า
                EnqueueProductSync(productId);
                throw new Exception($"StockQtyPush: product {productId} ยังไม่ map กับ NextAcc — enqueue product sync แล้ว retry");
            }

            var req = new StockAdjustmentRequest
            {
                ProductId = nexId,
                Quantity = qty,
                MovementType = movementType,
                UnitCost = unitCost > 0 ? (decimal?)unitCost : null,
                Reference = srcRef,
                Note = "TakeTime stock sync"
            };
            var res = await _apiClient.AdjustStockAsync(req);
            if (res?.success != true || res.data == null)
                throw new Exception($"StockQtyPush: NextAcc ปฏิเสธ product={productId}: {res?.message ?? "null"}");

            // กัน pull ขากลับดึง movement นี้กลับเข้ามา (echo)
            // หมายเหตุ: มี crash window แคบ ๆ ระหว่าง AdjustStockAsync สำเร็จ ↔ MarkMovementSeen
            // ถ้า crash ตรงนี้ retry จะ push ซ้ำ (NextAcc /stock/adjust ยังไม่ idempotent by Reference).
            // เราส่ง Reference=srcRef ไปแล้ว → ปิดช่องนี้ 100% เมื่อ NextAcc ทำ dedup by Reference (ดู inventory spec §4.1)
            MarkMovementSeen(res.data.Id, "PUSH", productId);
            _code.Logs(_connectionString, "AccountingSync",
                $"StockQtyPush: product={productId} nex={nexId} {movementType} qty={qty:N2} → movId={res.data.Id}", "SYSTEM");
            return res.data.Id.ToString();
        }

        /// <summary>ดึง stock movement ที่ปรับฝั่ง NextAcc เอง → ลง Product_In/Out ฝั่ง TakeTime (ขากลับ).
        /// per-product polling (round-robin ตาม Stock_Last_Pulled), dedup/echo ด้วย Nexaacc_Stock_Movement_Seen.
        /// เรียกจาก background timer (Global.asax). no-op ถ้า flag ปิด</summary>
        public async Task PullNextAccStockMovementsIfDue(int maxProductsPerRun = 25)
        {
            if (!_config.IsConfigured || !_config.Enabled || !_config.IsStockQtyPullEnabled) return;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP (@n) TakeTime_Product_ID, Nexaacc_Product_Id
                      FROM Accounting_Product_Map
                      WHERE Nexaacc_Product_Id IS NOT NULL
                      ORDER BY CASE WHEN Stock_Last_Pulled IS NULL THEN 0 ELSE 1 END, Stock_Last_Pulled ASC",
                    new Dictionary<string, object> { { "@n", maxProductsPerRun } });
                if (dt == null || dt.Rows.Count == 0) return;

                foreach (DataRow r in dt.Rows)
                {
                    int ttId = 0; int.TryParse(r["TakeTime_Product_ID"]?.ToString(), out ttId);
                    Guid nexId; Guid.TryParse(r["Nexaacc_Product_Id"]?.ToString(), out nexId);
                    if (ttId <= 0 || nexId == Guid.Empty) continue;

                    try { await PullOneProductMovements(ttId, nexId); }
                    catch (Exception exP)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"StockQtyPull: product={ttId} ล้มเหลว: {exP.Message}", "SYSTEM");
                    }
                    // ขยับ cursor เสมอ กันค้างอยู่ที่สินค้าเดิม
                    try
                    {
                        _code.DatabaseInsertSafe(_connectionString,
                            "UPDATE Accounting_Product_Map SET Stock_Last_Pulled = GETDATE() WHERE TakeTime_Product_ID = @id",
                            new Dictionary<string, object> { { "@id", ttId } });
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"PullNextAccStockMovementsIfDue: {ex.Message}", "SYSTEM");
            }
        }

        private async Task PullOneProductMovements(int ttProductId, Guid nexProductId)
        {
            var resp = await _apiClient.GetProductStockMovementsAsync(nexProductId);
            var moves = resp?.data;
            if (moves == null || moves.Count == 0) return;

            foreach (var m in moves)
            {
                if (m == null || m.Id == Guid.Empty) continue;
                if (IsMovementSeen(m.Id)) continue;   // เราเคย push/import แล้ว → ข้าม (กัน echo/ซ้ำ)

                decimal qty = Math.Abs(m.Quantity);
                if (qty <= 0) { MarkMovementSeen(m.Id, "PULL", ttProductId); continue; }

                string mt = (m.MovementType ?? "").ToUpperInvariant();
                bool isIn;
                if (mt == "IN" || mt == "TRANSFER_IN") isIn = true;
                else if (mt == "OUT" || mt == "TRANSFER_OUT") isIn = false;
                else isIn = m.Quantity >= 0;   // ADJUST → ตามเครื่องหมาย

                if (isIn) InsertInboundProductIn(ttProductId, qty, m.UnitCost, m.MovementDate);
                else InsertInboundProductOut(ttProductId, qty, m.UnitCost, m.MovementDate);

                MarkMovementSeen(m.Id, "PULL", ttProductId);
            }
            _code.Logs(_connectionString, "AccountingSync",
                $"StockQtyPull: product={ttProductId} ตรวจ {moves.Count} movement", "SYSTEM");
        }

        private void InsertInboundProductIn(int productId, decimal qty, decimal unitCost, DateTime when)
        {
            _code.DatabaseInsertSafe(_connectionString,
                "INSERT INTO [dbo].[Product_In] ([DateTime_In],[Product_ID],[Amount],[PricePerUnit]) " +
                "VALUES (@d,@pid,@amt,@cost)",
                new Dictionary<string, object>
                {
                    { "@d", when }, { "@pid", productId }, { "@amt", qty }, { "@cost", unitCost }
                });
        }

        private void InsertInboundProductOut(int productId, decimal qty, decimal unitCost, DateTime when)
        {
            // Remark='NEXTACC_SYNC' (ไม่ใช่ 'ขาย') → POS daily rollup ไม่หยิบ; Pos_Rollup_Ref='NEXTACC' กันอีกชั้น
            _code.DatabaseInsertSafe(_connectionString,
                "INSERT INTO [dbo].[Product_Out] ([DateTime_Out],[Product_ID],[Amount],[PricePerUnit],[Account_Receipt_ID],[Remark],[Pos_Rollup_Ref]) " +
                "VALUES (@d,@pid,@amt,@cost,'0',N'NEXTACC_SYNC',N'NEXTACC')",
                new Dictionary<string, object>
                {
                    { "@d", when }, { "@pid", productId }, { "@amt", qty }, { "@cost", unitCost }
                });
        }

        private Guid ResolveNexaaccProductId(int takeTimeProductId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Product_Id FROM Accounting_Product_Map WHERE TakeTime_Product_ID = @id AND Nexaacc_Product_Id IS NOT NULL",
                    new Dictionary<string, object> { { "@id", takeTimeProductId } });
                if (dt?.Rows.Count > 0 && Guid.TryParse(dt.Rows[0][0]?.ToString(), out var g)) return g;
            }
            catch { }
            return Guid.Empty;
        }

        private bool IsMovementSeen(Guid movementId)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 1 FROM Nexaacc_Stock_Movement_Seen WHERE Nexaacc_Movement_Id = @id",
                    new Dictionary<string, object> { { "@id", movementId } });
                return dt?.Rows.Count > 0;
            }
            catch { return false; }
        }

        private void MarkMovementSeen(Guid movementId, string direction, int takeTimeProductId)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    @"IF NOT EXISTS (SELECT 1 FROM Nexaacc_Stock_Movement_Seen WHERE Nexaacc_Movement_Id = @id)
                      INSERT INTO Nexaacc_Stock_Movement_Seen (Nexaacc_Movement_Id, Direction, TakeTime_Product_ID, Created_Date)
                      VALUES (@id, @dir, @pid, GETDATE())",
                    new Dictionary<string, object> { { "@id", movementId }, { "@dir", direction }, { "@pid", takeTimeProductId } });
            }
            catch { }
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
            if (string.IsNullOrEmpty(nexaaccId))
            {
                // ไม่ใช่ใบสำคัญจ่ายทั่วไป — อาจเป็นใบจ่ายเงินเดือน (sync เป็น entity "PAYROLL")
                return EnqueueVoidPayroll(documentNumber);
            }

            var payload = new Dictionary<string, object>
            {
                { "documentNumber", documentNumber },
                { "nexaaccId", nexaaccId }
            };

            return InsertQueue("VOUCHER", 0, "VOID_VOUCHER", payload);
        }

        /// <summary>
        /// ยกเลิก/กลับรายการใบจ่ายเงินเดือนที่ sync ไป NextAcc แล้ว (entity "PAYROLL")
        /// โพสต์ journal กลับรายการตามยอดเดิม → หักล้างทั้ง expense doc (DOCUMENT) และ journal (JOURNAL)
        /// </summary>
        public long EnqueueVoidPayroll(string documentNumber)
        {
            if (!_config.IsConfigured) return -1;
            if (string.IsNullOrEmpty(documentNumber)) return -1;

            long existing = FindPendingEntry("PAYROLL", "VOID_PAYROLL", "documentNumber", documentNumber);
            if (existing > 0) return existing;

            string nexaaccId = LookupNexaaccId(documentNumber, "PAYROLL");
            if (string.IsNullOrEmpty(nexaaccId)) return -1;  // ยังไม่เคย sync เงินเดือนนี้

            var payload = new Dictionary<string, object>
            {
                { "documentNumber", documentNumber },
                { "nexaaccId", nexaaccId }
            };

            return InsertQueue("PAYROLL", 0, "VOID_PAYROLL", payload);
        }

        /// <summary>
        /// Look up the Nexaacc_Response_Id for a previously synced document.
        /// </summary>
        // ──────────────────────────────────────────────
        // Reconciliation: ลบ record ฝั่งเราเมื่อเอกสารหายจาก NextAcc (ยืนยัน 404 เท่านั้น)
        // ใช้เฉพาะ DOCUMENT mode + company endpoints. transient error (timeout/5xx/network/401) → ไม่ลบ
        // ──────────────────────────────────────────────
        public class ReconcileResult
        {
            public int Checked;
            public int Deleted;
            public int Skipped;
            public int Errors;
            public List<string> DeletedDocs = new List<string>();
        }

        /// <summary>ตรวจว่าเอกสารที่ sync แล้วยังอยู่บน NextAcc หรือไม่ (สำหรับปุ่มลบราย record):
        /// true = หายแล้ว/ไม่เคย sync (ลบ local ได้), false = ยังอยู่บน NextAcc (อย่าเพิ่งลบ),
        /// null = ตรวจไม่ได้ (transient/ปิด company endpoint — อย่าเพิ่งลบ).</summary>
        public async System.Threading.Tasks.Task<bool?> IsNextAccDocumentGoneAsync(string documentNumber, string entityType)
        {
            if (!_config.CanUseCompanyEndpoints) return null;   // ตรวจไม่ได้
            string nexaaccId = LookupNexaaccId(documentNumber, entityType);
            if (string.IsNullOrEmpty(nexaaccId) || !Guid.TryParse(nexaaccId, out var docId) || docId == Guid.Empty)
                return true;   // ไม่เคย sync → ไม่มีบน NextAcc → ลบ local ได้
            try
            {
                var doc = await _apiClient.GetDocumentAsync(docId);
                return doc?.data == null ? (bool?)null : false;   // เจอ → ยังอยู่
            }
            catch (AccountingApiException ex) when (ex.StatusCode == 404)
            {
                return true;   // 404 → หายจาก NextAcc แล้ว
            }
            catch
            {
                return null;   // transient → ไม่แน่ใจ อย่าเพิ่งลบ
            }
        }

        /// <summary>รอบตรวจ: เอกสารที่ sync แล้ว ถ้า NextAcc ตอบ 404 (ไม่มีเอกสารแล้ว) → hard DELETE
        /// record ฝั่ง TakeTime. ลบเฉพาะเมื่อ 404 ชัดเจน; error อื่นข้าม (กันลบจาก transient).</summary>
        public async System.Threading.Tasks.Task<ReconcileResult> ReconcileDeletedDocumentsAsync(int maxPerType = 200)
        {
            var r = new ReconcileResult();
            if (!_config.IsConfigured || !_config.Enabled) return r;
            if (!_config.CanUseCompanyEndpoints)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    "ReconcileDeletedDocuments: ข้าม — company endpoint ปิดอยู่ (ต้องเปิดเพื่อตรวจเอกสารใน NextAcc)", "SYSTEM");
                return r;
            }

            if (_config.IsReceiptDocumentMode)
                await ReconcileEntityAsync("RECEIPT", "Account_Receipt", maxPerType, r);
            if (_config.IsVoucherDocumentMode)
                await ReconcileEntityAsync("VOUCHER", "Account_Payment", maxPerType, r);

            _code.Logs(_connectionString, "AccountingSync",
                $"ReconcileDeletedDocuments: checked={r.Checked} deleted={r.Deleted} skipped={r.Skipped} errors={r.Errors}", "SYSTEM");
            return r;
        }

        private async System.Threading.Tasks.Task ReconcileEntityAsync(string entityType, string table, int maxRecords, ReconcileResult r)
        {
            System.Data.DataTable dt;
            try
            {
                dt = _code.DatabaseQuerySafe(_connectionString,
                    $"SELECT TOP ({maxRecords}) ID FROM {table} WHERE (Status = 'Normal' OR Status IS NULL) ORDER BY Created_Date DESC", null);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"Reconcile({entityType}): query failed {ex.Message}", "SYSTEM");
                return;
            }
            if (dt == null) return;

            foreach (System.Data.DataRow row in dt.Rows)
            {
                string docNumber = row["ID"]?.ToString();
                if (string.IsNullOrEmpty(docNumber)) continue;

                // เฉพาะที่เคย sync (มี NextAcc doc id จาก queue) — ไม่แตะใบที่ยังไม่ sync/LOCAL/pending
                string nexaaccId = LookupNexaaccId(docNumber, entityType);
                if (string.IsNullOrEmpty(nexaaccId) || !Guid.TryParse(nexaaccId, out var docId) || docId == Guid.Empty)
                    continue;

                r.Checked++;
                try
                {
                    var doc = await _apiClient.GetDocumentAsync(docId);
                    if (doc?.data == null)
                        r.Skipped++;   // คืนค่าว่างผิดปกติ → ไม่ลบ (ปลอดภัยไว้ก่อน)
                }
                catch (AccountingApiException ex) when (ex.StatusCode == 404)
                {
                    bool ok = entityType == "RECEIPT" ? DeleteReceiptRecord(docNumber) : DeleteVoucherRecord(docNumber);
                    if (ok)
                    {
                        r.Deleted++;
                        r.DeletedDocs.Add(docNumber);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"Reconcile: HARD-DELETED {entityType} {docNumber} — NextAcc doc {docId} ตอบ 404 (ไม่มีเอกสารแล้ว)", "SYSTEM");
                    }
                    else r.Errors++;
                }
                catch (Exception ex)
                {
                    // transient (timeout/5xx/network/401) → ไม่ลบ
                    r.Errors++;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"Reconcile: {entityType} {docNumber} ตรวจไม่ได้ (ไม่ลบ — กัน transient): {ex.Message}", "SYSTEM");
                }
            }
        }

        private bool DeleteReceiptRecord(string docNumber)
        {
            try
            {
                var p = new Dictionary<string, object> { { "@ID", docNumber } };
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Payment_History] WHERE Receipt_ID = @ID", p);
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Payment_Slips] WHERE Account_Receipt_ID = @ID", p);
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Account_Receipt_Detail] WHERE Receipt_ID = @ID", p);
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Account_Receipt] WHERE ID = @ID", p);
                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"DeleteReceiptRecord({docNumber}) failed: {ex.Message}", "SYSTEM");
                return false;
            }
        }

        private bool DeleteVoucherRecord(string docNumber)
        {
            try
            {
                var p = new Dictionary<string, object> { { "@ID", docNumber } };
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Account_Payment_Detail] WHERE Payment_ID = @ID", p);
                _code.DatabaseInsertSafe(_connectionString, "DELETE FROM [dbo].[Account_Payment] WHERE ID = @ID", p);
                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"DeleteVoucherRecord({docNumber}) failed: {ex.Message}", "SYSTEM");
                return false;
            }
        }

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
                  ORDER BY Created_Date ASC, ID ASC",
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
                    // ถอด \uXXXX ให้อ่านเป็นภาษาไทย + เติมคำแนะนำสำหรับ error ที่รู้จัก (§86/4 ฯลฯ)
                    string bodyReadable = DecodeUnicodeEscapes(ex.ResponseBody);
                    string hint = BuildApiErrorHint(bodyReadable);
                    string errorDetail = $"Queue #{queueId} [{actionType}] API {ex.StatusCode}: {bodyReadable}{hint}";
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

            // Set X-Acting-User from queue payload (captured at enqueue time)
            _apiClient.ActingUser = payload.ContainsKey("operatorUser") ? payload["operatorUser"]?.ToString() : null;

            try
            {
            switch (actionType)
            {
                // ── Active processors (ผูกกับเอกสารจริง) ──
                case "CREATE_RECEIPT_DOCUMENT":
                    return await ProcessReceiptDocument(payload);

                case "CREATE_VOUCHER_JOURNAL":
                    return await ProcessVoucherJournal(payload);

                case "PAY_CREDIT_VOUCHER":
                    return await ProcessCreditVoucherPayment(payload);

                case "CREATE_PAYROLL_ENTRY":
                    return await ProcessPayrollEntry(payload);

                case "SYNC_PAYROLL_RUN":
                    return await ProcessPayrollRunSync(payload);

                case "IMPORT_PAYROLL_RUN":
                    return await ProcessPayrollRunImport(payload);

                case "STOCK_QTY_PUSH":
                    return await ProcessStockQtyPush(payload);

                case "VOID_RECEIPT":
                    return await ProcessVoidReceipt(payload);

                case "VOID_VOUCHER":
                    return await ProcessVoidVoucher(payload);

                case "VOID_PAYROLL":
                    return await ProcessVoidPayroll(payload);

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

                // ── Asset Reclassification (DR สินทรัพย์ / CR ค่าใช้จ่าย) ──
                case "ASSET_RECLASSIFICATION":
                    return await ProcessAssetReclassification(payload);

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
        /// เฉพาะกรณี "อนุมัติ/ลงรายการไปแล้วจริง" — ใช้ตอน approve เอกสารที่เพิ่งสร้าง.
        /// ต่างจาก IsAlreadyPostedOrTerminal: **ไม่กลืน 404** (เอกสารหาย = ปัญหาจริง ไม่ใช่สำเร็จ)
        /// และไม่กลืน "Draft"/validation (อนุมัติไม่ผ่าน ต้องให้ fail เห็นสาเหตุ ไม่ mark สำเร็จลอย ๆ)
        /// </summary>
        private static bool IsAlreadyApprovedOrPosted(AccountingApiException ex)
        {
            if (ex.StatusCode != 400 && ex.StatusCode != 409) return false;
            string body = ex.ResponseBody ?? "";
            return body.Contains("Approved")
                || body.Contains("Posted")
                || body.Contains("Sent")
                || body.Contains("Paid")
                || body.Contains("Voided")
                || body.Contains("อนุมัติแล้ว")
                || body.Contains("ลงรายการแล้ว")
                || body.Contains("ยกเลิกไปแล้ว");
        }

        /// <summary>สถานะเอกสารถือว่า "โพสต์แล้วจริง" (ไม่ใช่ Draft/รออนุมัติ/ถูกปฏิเสธ)</summary>
        private static bool IsPostedStatus(int status)
        {
            return status == NexaaccDocumentStatus.Approved
                || status == NexaaccDocumentStatus.Sent
                || status == NexaaccDocumentStatus.PartiallyPaid
                || status == NexaaccDocumentStatus.Paid
                || status == NexaaccDocumentStatus.Overdue;
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

        /// <summary>
        /// กันเอกสารค้างสถานะ "ร่าง" (Draft): ถ้าเอกสารที่เพิ่งสร้าง (invoice/receipt/ใบกำกับ) ยังไม่อนุมัติ
        /// → อนุมัติทันที (auto-post GL). idempotent — ถ้าอนุมัติ/ลงรายการ/void ไปแล้วจะกลืน error.
        /// approve เป็น company endpoint → ทำได้ต่อเมื่อ CanUseCompanyEndpoints (int_ ก็ผ่าน X-Api-Key fallback ได้).
        /// status = ค่าที่ NextAcc คืนมาหลังสร้าง; ว่าง/Draft/WaitingApproval/ร่าง → อนุมัติ.
        /// </summary>
        private async Task EnsureDocumentApprovedAsync(Guid documentId, string status, string ctx)
        {
            if (documentId == Guid.Empty) return;
            if (!_config.CanUseCompanyEndpoints) return;   // ไม่มี company endpoint → อนุมัติผ่าน API ไม่ได้

            string s = (status ?? "").Trim();
            bool alreadyDone = s.IndexOf("Approved", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Posted", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Paid", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Sent", StringComparison.OrdinalIgnoreCase) >= 0
                || s.IndexOf("Void", StringComparison.OrdinalIgnoreCase) >= 0;
            if (alreadyDone) return;   // ผ่านสถานะร่างไปแล้ว ไม่ต้องทำอะไร

            try
            {
                var apr = await _apiClient.ApproveDocumentAsync(documentId, new ApproveDocumentRequest { AcknowledgeWarnings = true });
                // จับเลขเอกสารจริงหลังอนุมัติ (ก่อนอนุมัติ NextAcc คืน "DRAFT-xxxx")
                string aprNum = apr?.data?.DocumentNumber;
                if (!string.IsNullOrEmpty(aprNum) && !aprNum.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                    _lastDocNumber = aprNum;
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureDocumentApproved: อนุมัติเอกสารร่าง {documentId} ({ctx}) status='{s}' → เลข={aprNum}", "SYSTEM");
            }
            catch (AccountingApiException ex) when (IsAlreadyApprovedOrPosted(ex))
            {
                // อนุมัติ/ลงรายการไปแล้ว — ถือว่าสำเร็จ
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureDocumentApproved: อนุมัติเอกสาร {documentId} ({ctx}) ไม่สำเร็จ: {ex.Message}", "SYSTEM");
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
            DateTime voucherDate = ParseAcctDate(p["voucherDate"]?.ToString());
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
            decimal vatAmount = p.ContainsKey("vatAmount") ? Convert.ToDecimal(p["vatAmount"]) : 0m;

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
                    // Explicit vendor info from the Account_Payment voucher flow (voucherId == 0).
                    // Include the vendor Address so NextAcc creates the contact with full
                    // address (not name-only). Address comes from the payload when supplied,
                    // otherwise it is looked up from the Vendor table via the "VENDOR-{id}" id.
                    string supplierAddress = p.ContainsKey("supplierAddress") ? p["supplierAddress"]?.ToString() : null;
                    if (string.IsNullOrEmpty(supplierAddress))
                        supplierAddress = LookupVendorAddressByExternalId(supplierExternalId);
                    supplierContact = await EnsureSupplierContactAsync(new ContactInfo
                    {
                        ExternalId = supplierExternalId,
                        Name = payeeName,
                        TaxId = supplierTaxId,
                        Address = supplierAddress
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
                // ใบสำคัญจ่ายที่จ่ายเงินแล้ว → POST /api/integration/payment-vouchers ใบเดียวจบ
                // NextAcc สร้าง PV Approved + จ่ายครบ + journal (ไม่ผ่านเจ้าหนี้ 21220 — ไม่มีเจ้าหนี้หลอก)
                // + WHT 21916/21917 ตาม TaxId + ออก 50ทวิ อัตโนมัติ
                // ยกเว้น: ตั้งหนี้เครดิต (isCredit) ใช้ expense → payment สองจังหวะตามเดิม
                //        และใบเงินเดือน (ต้อง Sensitivity=Payroll ซึ่ง PV request ยังไม่รองรับ)
                // One-shot PV endpoint always credits เงินสด and can't override the credit account
                // or carry the payer signature on its internal payment. Use it only for real
                // cash/bank settlements (autoRecordPayment). Non-cash settlements such as
                // จ่ายจากเงินทดรองกรรมการ fall through to the expense + explicit payment path,
                // where AutoRecordPaymentForVoucher sends OverridePaymentAccountId (เจ้าหนี้กรรมการ)
                // and the payer signature via /api/integration/payments.
                bool pvCreated = false;

                // ✅ จ่ายจริงแบบ "ไม่ใช่เงินสด/ธนาคาร" (เช่น เจ้าหนี้กรรมการ ต้อง override บัญชีเครดิต) →
                //    ออกเป็น "ใบสำคัญจ่าย" ผ่าน company /document (PaymentVoucher type 13) บังคับ Cr แหล่งเงิน
                //    แทน Expense. เคสเงินสด/ธนาคาร (autoRecordPayment) ใช้ integration one-shot PV ด้านล่าง
                //    แทน — เพราะ company /document (CreateDocumentRequest) "ไม่มีฟิลด์ลายเซ็นผู้จัดทำ" แต่
                //    integration PV (InboundPaymentVoucherRequest) มี PreparerSignatureBase64 → ส่งลายเซ็นได้
                //    (NextAcc รองรับลายเซ็นบน integration PV + company payment เท่านั้น ไม่รองรับบน company doc)
                if (_config.CanUseCompanyEndpoints && supplierContact?.NexaaccContactId != null
                    && !isCredit && !isSalaryVoucher && !autoRecordPayment)
                {
                    var pvDoc = _mapper.MapVoucherToDocument(voucherId, expenseCategory, amount, paymentMethod,
                        voucherDate, description, payeeName, supplierContact.NexaaccContactId.Value,
                        hasInputVat, whtRate, whtAmount, paymentAccountId, expenseAccountId, expenseLines, docNumber, vatAmount);
                    Guid pvDocId = await SettleVoucherDocAsync(pvDoc, docNumber);
                    if (pvDocId != Guid.Empty)
                    {
                        nexaaccId = pvDocId.ToString();
                        nexaaccDocNumber = _lastDocNumber;
                        nexaaccDocType = "PAYMENT_VOUCHER";
                        _lastDocType = nexaaccDocType;
                        await TryAutoGenerateWhtCertAsync(pvDocId, docNumber);   // ออก 50ทวิ ถ้ามี WHT
                        pvCreated = true;
                    }
                }

                if (!pvCreated && !isCredit && !isSalaryVoucher && autoRecordPayment)
                {
                    try
                    {
                        var pv = _mapper.MapVoucherToPaymentVoucher(voucherId, expenseCategory, amount, paymentMethod,
                            voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                            paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                            expenseLines: expenseLines, documentNumber: docNumber, vatAmount: vatAmount);
                        pv.Attachments = attachments;   // PV endpoint รับ base64 attachments ใน JSON
                        ApplyContactToPaymentVoucher(pv, supplierContact);
                        ApplyPreparerSignature(pv, docNumber);

                        var pvResult = await _apiClient.CreatePaymentVoucherAsync(pv);
                        Guid pvDocId = RequireValidDocId(pvResult?.data?.Id, $"CreatePaymentVoucher doc={docNumber}");
                        nexaaccId = pvDocId.ToString();
                        nexaaccDocNumber = pvResult?.data?.DocumentNumber;
                        nexaaccDocType = "PAYMENT_VOUCHER";
                        _lastDocNumber = nexaaccDocNumber;
                        _lastDocType = nexaaccDocType;
                        pvCreated = true;
                        // ไม่ต้อง TryAutoGenerateWhtCertAsync / AutoRecordPaymentForVoucher —
                        // NextAcc จัดการครบใน ProcessPaymentVoucherAsync แล้ว
                    }
                    catch (AccountingApiException pvEx) when (pvEx.StatusCode == 404)
                    {
                        // NextAcc deployment เก่า ยังไม่มี /payment-vouchers → ใช้ expense + payment ตามเดิม
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoucherJournal: /payment-vouchers returned 404 (NextAcc เก่า) — fallback expense+payment doc={docNumber}",
                            "SYSTEM");
                    }
                }

                if (!pvCreated)
                {
                    var expense = _mapper.MapVoucherToExpense(voucherId, expenseCategory, amount, paymentMethod,
                        voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                        paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                        expenseLines: expenseLines, documentNumber: docNumber, vatAmount: vatAmount);
                    expense.Attachments = attachments;
                    if (isSalaryVoucher)
                        expense.Sensitivity = "Payroll";
                    ApplyContactToExpense(expense, supplierContact);
                    ApplyPreparerSignature(expense, docNumber);   // ส่งลายเซ็นผู้จัดทำไป NextAcc

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

                    // Record the payment in NextAcc to settle the expense for every paid (non-credit)
                    // voucher reaching this path — cash/bank fallback AND non-cash methods like
                    // จ่ายจากเงินทดรองกรรมการ (settled by crediting OverridePaymentAccountId).
                    // Salary keeps its original cash/bank-only behaviour.
                    if (!isCredit && (!isSalaryVoucher || autoRecordPayment))
                    {
                        await AutoRecordPaymentForVoucher(expDocId, amount, whtAmount, voucherDate,
                            paymentMethod, payeeName, docNumber, paymentAccountId);
                    }
                }
            }
            else
            {
                var journal = _mapper.MapVoucherToJournal(voucherId, expenseCategory, amount, paymentMethod,
                    voucherDate, description, payeeName, hasInputVat, whtRate, whtAmount,
                    paymentAccountId: paymentAccountId, expenseAccountId: expenseAccountId,
                    expenseLines: expenseLines, documentNumber: docNumber,
                    isCredit: isCredit,
                    supplierTaxId: p.ContainsKey("supplierTaxId") ? p["supplierTaxId"]?.ToString() : null);
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

        /// <summary>
        /// Process credit voucher payment: DR A/P, CR Cash/Bank
        /// </summary>
        private async Task<string> ProcessCreditVoucherPayment(Dictionary<string, object> p)
        {
            if (_config.IsVoucherLocal)
                return "SKIPPED_LOCAL_MODE";

            string origDocNum = p["originalDocumentNumber"]?.ToString();
            decimal amount = Convert.ToDecimal(p["amount"]);
            string paymentMethod = p["paymentMethod"]?.ToString();
            DateTime paymentDate = ParseAcctDate(p["paymentDate"]?.ToString());
            string vendorName = p.ContainsKey("vendorName") ? p["vendorName"]?.ToString() : "";
            string paymentAccountId = p.ContainsKey("paymentAccountId") ? p["paymentAccountId"]?.ToString() : null;

            if (string.IsNullOrEmpty(paymentAccountId))
                paymentAccountId = LookupPaidHowAccountId(paymentMethod);

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessCreditVoucherPayment: origDoc={origDocNum} amount={amount} method={paymentMethod}", "SYSTEM");

            if (_config.IsVoucherDocumentMode)
            {
                // In DOCUMENT mode: find the original expense doc and record payment against it
                string nexaaccId = LookupNexaaccId(origDocNum, "VOUCHER");
                if (!string.IsNullOrEmpty(nexaaccId))
                {
                    Guid expDocId;
                    if (Guid.TryParse(nexaaccId, out expDocId))
                    {
                        await AutoRecordPaymentForVoucher(expDocId, amount, 0, paymentDate,
                            paymentMethod, vendorName, origDocNum, paymentAccountId);
                        return nexaaccId;
                    }
                }
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessCreditVoucherPayment: original doc {origDocNum} not found in NextAcc — falling back to journal", "SYSTEM");
            }

            // JOURNAL mode or fallback: DR A/P, CR Cash/Bank
            var journal = _mapper.MapCreditPaymentToJournal(origDocNum, amount, paymentMethod,
                paymentDate, vendorName, paymentAccountId);

            var result = await _apiClient.CreateJournalAsync(journal);
            return RequireValidDocId(result?.data?.Id, $"CreditVoucherPayment doc={origDocNum}").ToString();
        }

        private async System.Threading.Tasks.Task AutoRecordPaymentForVoucher(
            Guid documentId, decimal totalAmount, decimal whtAmount,
            DateTime voucherDate, string paymentMethod, string payeeName,
            string docNumber, string paymentAccountId)
        {
            try
            {
                // M8 idempotency: ถ้าเอกสารนี้บันทึกชำระเงินสำเร็จไปแล้ว (มี Nexaacc_Payment_Id)
                // → ข้าม เพื่อกันจ่ายซ้ำเมื่อ queue retry (เช่น retry หลัง step อื่นล้มเหลว)
                string existingPayId = LookupVoucherPaymentId(docNumber);
                if (!string.IsNullOrEmpty(existingPayId))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"AutoRecordPayment: doc={docNumber} มี paymentId={existingPayId} แล้ว — ข้าม (idempotent)", "SYSTEM");
                    return;
                }

                decimal payAmount = totalAmount - whtAmount;
                if (payAmount <= 0) return;

                // M6: ปิดยอดด้วย BalanceDue จริงของเอกสาร (กัน WHT/VAT rounding mismatch → ค้างชำระเศษ)
                // ปรับเฉพาะเมื่อต่างกันแค่เศษปัด (≤ 2 บาท); ถ้าต่างมากแปลว่ามีอย่างอื่นผิด → คงยอดเดิม
                try
                {
                    var docResp = await _apiClient.GetDocumentAsync(documentId);
                    decimal bal = docResp?.data?.BalanceDue ?? 0m;
                    if (bal > 0m && Math.Abs(bal - payAmount) <= 2.00m && bal != payAmount)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"AutoRecordPayment: doc={docNumber} ปรับยอดชำระตาม BalanceDue {bal:N2} (เดิม {payAmount:N2}) กันเศษค้างชำระ", "SYSTEM");
                        payAmount = bal;
                    }
                }
                catch { /* ดึง BalanceDue ไม่ได้ → ใช้ payAmount เดิม (total − wht) */ }

                string method = AccountingDataMapper.NormalizePaymentMethod(paymentMethod);

                // Override the credit (จ่ายเงินจาก) account when an explicit NextAcc account id is
                // configured — e.g. จ่ายจากเงินทดรองกรรมการ → CR เจ้าหนี้กรรมการ instead of เงินสด.
                // Only a real account GUID (from Account_Paid_How.Nexaacc_AccountId) is used, so
                // unconfigured methods keep NextAcc's default behaviour (no regression).
                Guid? overrideAccId = null;
                if (!string.IsNullOrEmpty(paymentAccountId)
                    && Guid.TryParse(paymentAccountId, out var parsedAccId) && parsedAccId != Guid.Empty)
                {
                    overrideAccId = parsedAccId;
                }

                // Payer (ผู้จ่ายเงิน, slot 0) signature + name from the document creator.
                string payerSigName = null, payerSigBase64 = null;
                var preparer = LookupPreparerInfo(docNumber);
                if (preparer != null)
                {
                    if (!string.IsNullOrEmpty(preparer.Value.name))
                        payerSigName = preparer.Value.name;
                    // NextAcc caps the base64 at 512KB; skip oversize to avoid a 400 error.
                    string sig = preparer.Value.dataUri;
                    if (!string.IsNullOrEmpty(sig) && sig.Length <= SignatureMaxBytes)
                        payerSigBase64 = sig;
                    else if (!string.IsNullOrEmpty(sig))
                        _code.Logs(_connectionString, "AccountingSync",
                            $"AutoRecordPayment: payer signature for doc={docNumber} skipped — {sig.Length} bytes > {SignatureMaxBytes}", "SYSTEM");
                }

                string paymentId = null;
                bool paymentOk = false;
                string paymentMsg = null;

                // OverridePaymentAccountId + PayerSignature are ONLY honoured by the company
                // endpoint POST /api/companies/{id}/document/payments (acc_ key). The integration
                // endpoint /api/integration/payments silently ignores both (verified against
                // Wachira-d/Accounting: InboundPaymentRequest has neither field). So when either
                // feature is actually needed and we hold an acc_ key, route to the company endpoint;
                // otherwise keep the integration endpoint (no regression for the common case).
                // ทั้ง int_ และ acc_ เรียก company endpoint ได้ผ่าน X-Api-Key (int_ ผ่าน fallback
                // ของ NextAcc ApiKeyMiddleware) → gate ที่ CanUseCompanyEndpoints (มี CompanyId + flag)
                bool needsCompanyEndpoint = overrideAccId.HasValue || !string.IsNullOrEmpty(payerSigBase64);
                if (needsCompanyEndpoint && _config.CanUseCompanyEndpoints)
                {
                    var companyReq = new CreatePaymentRequest
                    {
                        DocumentId = documentId,
                        PaymentDate = voucherDate,
                        Amount = payAmount,
                        PaymentMethod = method,
                        Reference = docNumber,
                        Notes = $"ชำระเงินอัตโนมัติจากใบสำคัญจ่าย {docNumber}",
                        OverridePaymentAccountId = overrideAccId,
                        PayerSignatureName = payerSigName,
                        PayerSignatureBase64 = payerSigBase64
                    };
                    var companyResult = await _apiClient.CreatePaymentAsync(companyReq);
                    paymentOk = companyResult?.success == true && companyResult.data != null;
                    paymentId = paymentOk ? companyResult.data.Id.ToString() : null;
                    paymentMsg = companyResult?.message;
                }
                else
                {
                    if (needsCompanyEndpoint && !_config.CanUseCompanyEndpoints)
                        _code.Logs(_connectionString, "AccountingSync",
                            $"AutoRecordPayment: doc={docNumber} ต้องใช้ override account/ลายเซ็นผู้จ่าย แต่ company endpoint ปิดอยู่ (ไม่มี CompanyId หรือ Nexaacc_Company_Endpoints=0) — integration endpoint ไม่รองรับ จึงข้ามฟีเจอร์นี้", "SYSTEM");

                    var paymentRequest = new CreateIntegrationPaymentRequest
                    {
                        ExternalId = $"PAY-{docNumber}",
                        ExternalRef = docNumber,
                        DocumentId = documentId,
                        PaymentDate = voucherDate,
                        Amount = payAmount,
                        PaymentMethod = method,
                        Notes = $"ชำระเงินอัตโนมัติจากใบสำคัญจ่าย {docNumber}"
                    };
                    var payResult = await _apiClient.CreateIntegrationPaymentAsync(paymentRequest);
                    paymentOk = payResult?.success == true && payResult.data != null;
                    paymentId = paymentOk ? payResult.data.Id.ToString() : null;
                    paymentMsg = payResult?.message;
                }

                if (paymentOk)
                {
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
                    // M8: อย่ากลืน error เงียบ ๆ (เดิมทำให้ AP ค้างเปิดใน NextAcc โดยไม่มีใครรู้)
                    // → throw เพื่อให้ queue mark FAILED + retry (idempotent guard ด้านบนกันจ่ายซ้ำ)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"AutoRecordPayment: FAILED doc={docNumber} msg={paymentMsg ?? "null response"}",
                        "SYSTEM");
                    throw new Exception($"AutoRecordPayment failed for doc={docNumber}: {paymentMsg ?? "null response"}");
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"AutoRecordPayment: ERROR doc={docNumber} {ex.Message}", "SYSTEM");
                throw;   // M8: ให้ queue retry แทนการปล่อยให้ AP ค้างเปิดเงียบ ๆ
            }
        }

        // ──────────────────────────────────────────────
        // รับชำระฝั่งรายรับ (ปิดลูกหนี้) — DOCUMENT mode
        // ──────────────────────────────────────────────

        /// <summary>
        /// ปิดลูกหนี้ของ invoice ที่เพิ่งสร้างในโหมด DOCUMENT (NextAcc invoice ลง Dr ลูกหนี้
        /// และไม่ auto-record payment): ตัดมัดจำที่หัก (ถ้ามี) เข้าลูกหนี้ผ่าน adjustment journal
        /// แล้วบันทึกรับเงินสดจริง (= total − depositApplied) ผ่าน integration payment
        /// (Dr เงินสด / Cr ลูกหนี้). idempotent ด้วย Account_Receipt.Nexaacc_Receipt_Payment_Id
        /// (payment endpoint ของ NextAcc ไม่ dedupe → ต้องกัน double เมื่อ queue retry).
        /// </summary>
        private async System.Threading.Tasks.Task SettleReceiptInNextAcc(
            Guid invoiceDocId, string receiptNumber, decimal totalAmount, decimal depositApplied,
            string paymentMethod, DateTime receiptDate, string customerName, bool hasVat, int reservationId,
            string paymentAccountId)
        {
            if (string.IsNullOrEmpty(receiptNumber))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    "SettleReceiptInNextAcc: ข้าม — ไม่มี receiptNumber (ใช้เป็น idempotency key ไม่ได้)", "SYSTEM");
                return;
            }

            string marker = LookupReceiptPaymentMarker(receiptNumber);
            // edit = void→สร้างเลขเดิม: marker "VOIDED" จาก void ก่อนหน้า ต้องไม่ทำให้ข้าม settle
            // (ใบเสร็จใหม่ถูกสร้างแล้ว ต้องปิดลูกหนี้/บันทึกเงินสดใหม่) → รีเซ็ตเป็นเริ่มใหม่
            if (marker == "VOIDED") marker = null;
            // เฟส DOC:/APR: มาจากขั้นสร้าง/อนุมัติเอกสาร (EnsureRevenueDocCreatedApprovedAsync —
            // ใบกำกับ TaxInvoice doc-mode) = ยังไม่เริ่ม settle → มองเป็นว่าง
            if (!string.IsNullOrEmpty(marker) && (marker.StartsWith("DOC:") || marker.StartsWith("APR:")))
                marker = null;
            bool payDone = !string.IsNullOrEmpty(marker) && !marker.StartsWith("ADJ:");
            bool adjDone = !string.IsNullOrEmpty(marker);   // "ADJ:" หรือ final → adjustment ลงแล้ว
            if (payDone) return;                            // ปิดลูกหนี้ครบแล้ว

            // 1) ตัดมัดจำที่หักออกจากลูกหนี้ (ถ้ามี)
            if (depositApplied > 0 && !adjDone)
            {
                // ── GUARD ลำดับ resync: จะ Dr เงินรับล่วงหน้า ได้ก็ต่อเมื่อใบมัดจำของการจองนี้
                // ถูก booked เป็นหนี้สินบน NextAcc แล้ว (ใบมัดจำรุ่นเก่าที่เคยรับรู้เป็นรายได้ทันที
                // ต้องถูก resync เป็น "ใบเสร็จมัดจำ" ก่อน) — กัน 21712 ติดลบ + รายได้/VAT ซ้ำ
                var depState = VerifyDepositBookedOnNextAcc(reservationId);
                if (depState.PendingSync)
                    throw new Exception($"SettleReceipt: ใบมัดจำของการจอง #{reservationId} กำลังรอ sync/อนุมัติ — เลื่อนไปรอบถัดไป");
                if (depState.AnyDeposit && depState.BookedAmount + 0.01m < depositApplied)
                    throw new Exception(
                        $"SettleReceipt: มัดจำที่ booked บน NextAcc มี {depState.BookedAmount:N2} แต่ใบนี้จะตัด {depositApplied:N2} — " +
                        $"กรุณากด Retry ใบมัดจำของการจอง #{reservationId} ให้เป็น 'ใบเสร็จมัดจำ' (ตั้งเงินรับล่วงหน้า) ก่อน แล้วใบนี้จะ settle ต่ออัตโนมัติ" +
                        (depState.UnsyncedReceipts.Count > 0 ? $" [ใบมัดจำ: {string.Join(", ", depState.UnsyncedReceipts)}]" : ""));

                // ✅ ทางหลัก (company endpoints): ตัดมัดจำเป็น "document payment" ของใบกำกับ
                //    (OverridePaymentAccountId = เงินรับล่วงหน้า → Dr ADVANCE / Cr ลูกหนี้) —
                //    สำคัญ: payment ลด BalanceDue ของเอกสารด้วย → ใบกำกับปิดยอดสนิท ไม่ค้างชำระ
                //    เท่ายอดมัดจำ (journal อย่างเดียวลดแค่ GL ไม่ลดยอดค้างของเอกสาร)
                //    + journal แก้ VAT มัดจำ (ADVANCE เก็บ net) เมื่อ VAT รับรู้ตอนรับมัดจำ
                Guid advDepId = Guid.Empty;
                bool depositAsPayment = _config.CanUseCompanyEndpoints
                    && _mapper.TryGetAccountId("ADVANCE_DEPOSIT", out advDepId) && advDepId != Guid.Empty;

                if (depositAsPayment)
                {
                    var depPay = new CreatePaymentRequest
                    {
                        DocumentId = invoiceDocId,
                        PaymentDate = receiptDate,
                        Amount = depositApplied,
                        PaymentMethod = "Other",
                        // อ้างรหัสการจอง → ตามรอยได้ทันทีว่า "กลับยอดมัดจำของการจองไหน" เข้าใบกำกับนี้
                        Reference = $"RES-{reservationId}-DEPADJ",
                        Notes = $"ตัดมัดจำการจอง #{reservationId} เข้าใบกำกับ {receiptNumber}",
                        OverridePaymentAccountId = advDepId
                    };
                    var depResult = await _apiClient.CreatePaymentAsync(depPay);
                    if (depResult?.success != true || depResult.data == null)
                        throw new Exception($"SettleReceipt: ตัดมัดจำเป็น payment ไม่สำเร็จ receipt={receiptNumber}: {depResult?.message ?? "null response"}");
                    Guid depPayId = depResult.data.Id;

                    if (hasVat && _config.IsDepositVatAtReceipt
                        && !await JournalExistsByReferenceAsync($"RES-{reservationId}-DEPVAT"))
                    {
                        // ADVANCE เก็บเฉพาะ net → ย้ายส่วน VAT: Dr 21913(defer)/21911 / Cr ADVANCE
                        // guard RES-{id}-DEPVAT กัน post ซ้ำถ้า crash หลังโพสต์ก่อนเขียน marker (idempotent)
                        var vatFix = _mapper.MapDepositVatCorrection(reservationId, depositApplied,
                            receiptNumber, _config.IsDepositOutputVatDeferred);
                        var vatResult = await _apiClient.CreateJournalAsync(vatFix);
                        Guid vatFixId = RequireValidDocId(vatResult?.data?.Id, $"DepositVatCorrection receipt={receiptNumber}");
                        await SafePostJournalAsync(vatFixId);
                    }

                    SetReceiptPaymentMarker(receiptNumber, "ADJ:" + depPayId);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleReceipt: ตัดมัดจำเป็น payment สำเร็จ receipt={receiptNumber} deposit={depositApplied:N2} paymentId={depPayId} (BalanceDue ลดครบ)", "SYSTEM");
                }
                else
                {
                    // fallback (int_ ไม่มี company endpoint / ยังไม่ map ADVANCE_DEPOSIT):
                    // journal Dr เงินรับล่วงหน้า / Cr ลูกหนี้ — GL ถูก แต่ BalanceDue ของเอกสาร
                    // จะค้างเท่ายอดมัดจำ (ข้อจำกัดที่รู้จัก — log ไว้). guard RES-{id}-DEPADJ กัน post ซ้ำ
                    // MapDepositAppliedAdjustment ใช้ Reference = "{receiptNumber}-DEPADJ"
                    string adjRef = !string.IsNullOrEmpty(receiptNumber) ? $"{receiptNumber}-DEPADJ" : $"RES-{reservationId}-DEPADJ";
                    string adjId2 = "existing";
                    if (!await JournalExistsByReferenceAsync(adjRef))
                    {
                        var adj = _mapper.MapDepositAppliedAdjustment(reservationId, depositApplied, paymentMethod,
                            receiptDate, customerName, paymentAccountId, receiptNumber,
                            hasVat: hasVat, vatAtReceipt: _config.IsDepositVatAtReceipt);
                        var adjResult = await _apiClient.CreateJournalAsync(adj);
                        Guid adjId = RequireValidDocId(adjResult?.data?.Id, $"DepositAppliedAdjustment receipt={receiptNumber}");
                        await SafePostJournalAsync(adjId);
                        adjId2 = adjId.ToString();
                    }
                    SetReceiptPaymentMarker(receiptNumber, "ADJ:" + adjId2);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleReceipt: deposit adjustment (journal) posted receipt={receiptNumber} deposit={depositApplied:N2} journalId={adjId2} — BalanceDue เอกสารจะค้างเท่ามัดจำ (ไม่มี company endpoint)", "SYSTEM");
                }
            }

            // 2) บันทึกรับเงินสดจริง (= total − depositApplied) → Dr เงินสด / Cr ลูกหนี้
            decimal cashNow = totalAmount - depositApplied;
            if (cashNow <= 0.005m)
            {
                SetReceiptPaymentMarker(receiptNumber, "NOCASH");
                _code.Logs(_connectionString, "AccountingSync",
                    $"SettleReceipt: receipt={receiptNumber} ไม่มีเงินสดรับเพิ่ม (หักมัดจำหมด) — ปิดลูกหนี้ด้วยมัดจำอย่างเดียว", "SYSTEM");
                return;
            }

            string method = AccountingDataMapper.NormalizePaymentMethod(paymentMethod);

            // บังคับบัญชี "แหล่งเงิน" (ฝั่ง Dr เงินสด/ธนาคาร) ตามที่ผู้ใช้เลือกฝั่ง TakeTime
            // (Account_Paid_How.Nexaacc_AccountId → ChartOfAccount GUID). NextAcc
            // CreatePaymentJournalAsync เลือกบัญชีฝั่งเงินสดจาก OverridePaymentAccountId ก่อน
            // (verified vs Wachira-d/Accounting) → ต้องใช้ company endpoint. integration endpoint
            // ละเลย override → NextAcc เดาบัญชีจาก PaymentMethod เอง (ไม่ยึดแหล่งเงินที่เลือก).
            Guid? overrideAccId = null;
            if (!string.IsNullOrEmpty(paymentAccountId)
                && Guid.TryParse(paymentAccountId, out var parsedAccId) && parsedAccId != Guid.Empty)
                overrideAccId = parsedAccId;

            string paymentId = null;
            bool payOk = false;
            string payMsg = null;

            if (overrideAccId.HasValue && _config.CanUseCompanyEndpoints)
            {
                // company endpoint บังคับแหล่งเงินได้ (int_/acc_ ผ่าน X-Api-Key)
                var companyReq = new CreatePaymentRequest
                {
                    DocumentId = invoiceDocId,
                    PaymentDate = receiptDate,
                    Amount = cashNow,
                    PaymentMethod = method,
                    Reference = receiptNumber,
                    Notes = $"รับชำระอัตโนมัติจากใบเสร็จ {receiptNumber}",
                    OverridePaymentAccountId = overrideAccId
                };
                var companyResult = await _apiClient.CreatePaymentAsync(companyReq);
                payOk = companyResult?.success == true && companyResult.data != null;
                paymentId = payOk ? companyResult.data.Id.ToString() : null;
                payMsg = companyResult?.message;
            }
            else
            {
                if (overrideAccId.HasValue && !_config.CanUseCompanyEndpoints)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleReceipt: receipt={receiptNumber} มีแหล่งเงินที่ map ไว้ (acc={paymentAccountId}) แต่ company endpoint ปิดอยู่ — integration endpoint ไม่บังคับบัญชี NextAcc จะเลือกตาม PaymentMethod เอง", "SYSTEM");

                var payReq = new CreateIntegrationPaymentRequest
                {
                    ExternalId = $"RCPT-PAY-{receiptNumber}",
                    ExternalRef = receiptNumber,
                    InvoiceExternalRef = receiptNumber,
                    DocumentId = invoiceDocId,
                    CustomerName = customerName,
                    PaymentDate = receiptDate,
                    Amount = cashNow,
                    PaymentMethod = method,
                    ReferenceNo = receiptNumber,
                    Notes = $"รับชำระอัตโนมัติจากใบเสร็จ {receiptNumber}"
                };
                var payResult = await _apiClient.CreateIntegrationPaymentAsync(payReq);
                payOk = payResult?.success == true && payResult.data != null;
                paymentId = payOk ? payResult.data.Id.ToString() : null;
                payMsg = payResult?.message;
            }

            if (payOk)
            {
                SetReceiptPaymentMarker(receiptNumber, paymentId);
                _code.Logs(_connectionString, "AccountingSync",
                    $"SettleReceipt: รับชำระสำเร็จ receipt={receiptNumber} cash={cashNow:N2} paymentId={paymentId} แหล่งเงิน={(overrideAccId.HasValue && _config.CanUseCompanyEndpoints ? "บังคับ "+paymentAccountId : "default ตาม PaymentMethod")}", "SYSTEM");
            }
            else
            {
                // re-throw → queue retry; invoice (ExternalRef) + adjustment (marker) idempotent ไม่ซ้ำ
                throw new Exception($"SettleReceipt: บันทึกรับชำระไม่สำเร็จ receipt={receiptNumber}: {payMsg ?? "null response"}");
            }
        }

        // ──────────────────────────────────────────────
        // Receipt document (company /document endpoint, acc_ key) — DOCUMENT mode ที่ถูกต้องตามบัญชี
        //   Dr เงินสด(แหล่งเงิน) / Cr รายได้ราย line / Cr ภาษีขาย ; มัดจำ → Cr รับล่วงหน้า(หนี้สิน)
        // ไม่เปิดลูกหนี้ ไม่ต้องบันทึก payment แยก. idempotent ผ่าน Nexaacc_Receipt_Payment_Id
        // 3 เฟส: DOC:{id} (สร้างแล้ว) → APR:{id} (อนุมัติแล้ว) → {id} (ปรับมัดจำเสร็จ/จบ)
        // ──────────────────────────────────────────────
        private async System.Threading.Tasks.Task<Guid> SettleReceiptDocAsync(
            CreateDocumentRequest doc, string receiptNumber, int reservationId, decimal depositApplied,
            string paymentMethod, DateTime receiptDate, string customerName, bool hasVat, string paymentAccountId)
        {
            string marker = LookupReceiptPaymentMarker(receiptNumber);
            // marker "VOIDED" = ใบเสร็จเดิมถูก void แล้ว. ถ้ามี CREATE เข้ามาใหม่ (edit = void→สร้างเลขเดิม,
            // row ถูก reinsert) ให้เริ่มสร้างใหม่ตามปกติ — ไม่บล็อก (delete ปกติไม่ enqueue CREATE)
            if (marker == "VOIDED") marker = null;
            // เฟส: DOC:{id} (สร้างแล้ว) → APR:{id} (อนุมัติแล้ว) → ADJ:{id} (ลงปรับมัดจำแล้ว) → {id} (จบ)
            bool approved = !string.IsNullOrEmpty(marker) && (marker.StartsWith("APR:") || marker.StartsWith("ADJ:"));
            bool adjDone = !string.IsNullOrEmpty(marker) && marker.StartsWith("ADJ:");
            Guid docId = Guid.Empty;
            if (!string.IsNullOrEmpty(marker)
                && (marker.StartsWith("DOC:") || marker.StartsWith("APR:") || marker.StartsWith("ADJ:")))
                Guid.TryParse(marker.Substring(4), out docId);

            bool isFinal = !string.IsNullOrEmpty(marker)
                && !marker.StartsWith("DOC:") && !marker.StartsWith("APR:") && !marker.StartsWith("ADJ:");
            if (isFinal && Guid.TryParse(marker, out var finalId) && finalId != Guid.Empty)
            {
                // marker บอกว่า "จบแล้ว" — แต่ verify ว่าเอกสารโพสต์จริงบน NextAcc (กันเคสเก่าที่ mark สำเร็จ
                // ทั้งที่ยัง Draft/ไม่มีเอกสาร). ถ้าโพสต์แล้ว → จบ; ถ้ายัง Draft → อนุมัติซ้ำ; ถ้าไม่พบ → สร้างใหม่.
                try
                {
                    var chk = await _apiClient.GetDocumentAsync(finalId);
                    if (chk?.data != null && IsPostedStatus(chk.data.Status))
                    {
                        if (!string.IsNullOrEmpty(chk.data.DocumentNumber) && !chk.data.DocumentNumber.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                            _lastDocNumber = chk.data.DocumentNumber;
                        _lastDocType = "RECEIPT";
                        return finalId;   // โพสต์แล้วจริง — จบ
                    }
                    int chkStatus = chk?.data?.Status ?? -1;
                    if (chkStatus == NexaaccDocumentStatus.Voided || chkStatus == NexaaccDocumentStatus.Rejected)
                    {
                        // เอกสารถูก void/ปฏิเสธไปแล้ว (เช่น flow re-post void ของเก่า) → สร้างใหม่ทั้งใบ
                        SetReceiptPaymentMarker(receiptNumber, null);
                        docId = Guid.Empty; approved = false; adjDone = false;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"SettleReceiptDoc: receipt={receiptNumber} marker=final แต่เอกสารถูก void/reject (status={chkStatus}) → สร้างใหม่", "SYSTEM");
                    }
                    else
                    {
                        // ยัง Draft/รออนุมัติ → ซ่อมด้วยการอนุมัติซ้ำ docId เดิม
                        // adjDone=true: การปรับมัดจำ (ถ้ามี) ทำไปแล้วตอนถึง final — ไม่ทำซ้ำ (กัน double)
                        docId = finalId; approved = false; adjDone = true;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"SettleReceiptDoc: receipt={receiptNumber} marker=final แต่เอกสารยัง Draft (status={chkStatus}) → อนุมัติซ้ำ", "SYSTEM");
                    }
                }
                catch (AccountingApiException gx) when (gx.StatusCode == 404)
                {
                    // เอกสารไม่อยู่บน NextAcc → รีเซ็ต marker แล้วสร้างใหม่
                    SetReceiptPaymentMarker(receiptNumber, null);
                    docId = Guid.Empty; approved = false; adjDone = false;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleReceiptDoc: receipt={receiptNumber} marker=final แต่ไม่พบเอกสารบน NextAcc → สร้างใหม่", "SYSTEM");
                }
            }

            // 1) สร้างเอกสาร (company /document ไม่ dedupe → marker กันสร้างซ้ำ)
            if (docId == Guid.Empty)
            {
                var createResult = await _apiClient.CreateDocumentAsync(doc);
                docId = RequireValidDocId(createResult?.data?.Id, $"CreateDocument (receipt) receipt={receiptNumber}");
                _lastDocNumber = createResult?.data?.DocumentNumber;
                SetReceiptPaymentMarker(receiptNumber, "DOC:" + docId);
            }

            // 2) อนุมัติ (auto-post GL)
            //    ก่อนอนุมัติ NextAcc คืนเลข "DRAFT-xxxx"; หลังอนุมัติจะได้เลขจริง → ต้องจับเลขหลังอนุมัติ
            //    และตรวจว่าโพสต์จริง — ถ้ายังค้าง Draft/รออนุมัติ หรือ approve หาเอกสารไม่เจอ (404)
            //    ให้ throw เพื่อให้คิวเป็น FAILED เห็นสาเหตุ (ไม่ mark COMPLETED ทั้งที่ไม่มีเอกสารบน NextAcc)
            if (!approved)
            {
                bool alreadyPosted = false;
                try
                {
                    var apr = await _apiClient.ApproveDocumentAsync(docId, new ApproveDocumentRequest { AcknowledgeWarnings = true });

                    string aprNum = apr?.data?.DocumentNumber;
                    if (!string.IsNullOrEmpty(aprNum) && !aprNum.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                        _lastDocNumber = aprNum;   // เลขเอกสารจริงหลังอนุมัติ

                    int st = apr?.data?.Status ?? -1;
                    if (st == NexaaccDocumentStatus.Draft || st == NexaaccDocumentStatus.WaitingApproval
                        || st == NexaaccDocumentStatus.Rejected)
                    {
                        throw new Exception(
                            $"อนุมัติไม่สำเร็จ — เอกสารยังเป็นสถานะ {st} (0=Draft/1=รออนุมัติ/8=Rejected) " +
                            $"receipt={receiptNumber} docId={docId} เลข={aprNum}");
                    }
                }
                catch (AccountingApiException ex) when (IsAlreadyApprovedOrPosted(ex))
                {
                    alreadyPosted = true;
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleReceiptDoc: doc {docId} already approved/posted receipt={receiptNumber} ({ex.StatusCode})", "SYSTEM");
                }

                // เลขจริงยังไม่ได้ (เช่น already-posted path) → ดึงเอกสารมาอ่านเลข/สถานะยืนยัน
                if (string.IsNullOrEmpty(_lastDocNumber) || _lastDocNumber.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase) || alreadyPosted)
                {
                    try
                    {
                        var got = await _apiClient.GetDocumentAsync(docId);
                        if (got?.data != null)
                        {
                            if (!string.IsNullOrEmpty(got.data.DocumentNumber) && !got.data.DocumentNumber.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                                _lastDocNumber = got.data.DocumentNumber;
                            if (!IsPostedStatus(got.data.Status))
                                throw new Exception(
                                    $"อนุมัติแล้วแต่เอกสารยังไม่โพสต์ (status={got.data.Status}) receipt={receiptNumber} docId={docId}");
                        }
                    }
                    catch (AccountingApiException gx)
                    {
                        // อ่านเอกสารไม่ได้/ไม่พบ → ถือว่าไม่สำเร็จจริง (เอกสารไม่อยู่บน NextAcc)
                        throw new Exception(
                            $"ยืนยันเอกสารบน NextAcc ไม่ได้หลังอนุมัติ receipt={receiptNumber} docId={docId} ({gx.StatusCode})");
                    }
                }

                SetReceiptPaymentMarker(receiptNumber, "APR:" + docId);
            }

            // 3) หักมัดจำ (ถ้ามี): Dr รับล่วงหน้า(+VAT) / Cr เงินสด — ลดเงินสดที่ใบเสร็จ Dr เกิน
            //    CreateJournalAsync ไม่ dedupe → marker เฟส ADJ: กันโพสต์ซ้ำตอน retry
            if (depositApplied > 0 && !adjDone)
            {
                var adj = _mapper.MapDepositAppliedReceiptAdjustment(reservationId, depositApplied, paymentMethod,
                    receiptDate, customerName, paymentAccountId, receiptNumber,
                    hasVat: hasVat, vatAtReceipt: _config.IsDepositVatAtReceipt, deferOutputVat: _config.IsDepositOutputVatDeferred);
                var adjResult = await _apiClient.CreateJournalAsync(adj);
                Guid adjId = RequireValidDocId(adjResult?.data?.Id, $"DepositAppliedReceiptAdjustment receipt={receiptNumber}");
                await SafePostJournalAsync(adjId);
                SetReceiptPaymentMarker(receiptNumber, "ADJ:" + docId);
                _code.Logs(_connectionString, "AccountingSync",
                    $"SettleReceiptDoc: deposit adjustment {adjId} deposit={depositApplied:N2} receipt={receiptNumber}", "SYSTEM");
            }

            SetReceiptPaymentMarker(receiptNumber, docId.ToString());   // final
            _lastDocType = "RECEIPT";
            _code.Logs(_connectionString, "AccountingSync",
                $"SettleReceiptDoc: เสร็จ receipt={receiptNumber} docId={docId} depositApplied={depositApplied:N2} แหล่งเงิน={(string.IsNullOrEmpty(paymentAccountId) ? "default" : paymentAccountId)}", "SYSTEM");
            return docId;
        }

        /// <summary>
        /// สร้าง + อนุมัติเอกสารรายรับ (TaxInvoice ฯลฯ) แบบ idempotent ผ่าน marker เฟส
        /// DOC:{id} → APR:{id} แล้ว "หยุดแค่นั้น" — เฟส settle ต่อ (ADJ:/paymentId/NOCASH)
        /// เป็นของ SettleReceiptInNextAcc. ถ้า marker เลยเฟสอนุมัติไปแล้ว (settle เริ่ม/จบ)
        /// จะกู้ doc id จาก queue history แทน (ไม่สร้างซ้ำ). อนุมัติ verify แบบเดียวกับ
        /// SettleReceiptDocAsync: จับเลขเอกสารจริงหลังอนุมัติ + throw ถ้ายังค้าง Draft
        /// </summary>
        private async System.Threading.Tasks.Task<Guid> EnsureRevenueDocCreatedApprovedAsync(
            CreateDocumentRequest doc, string receiptNumber)
        {
            string marker = LookupReceiptPaymentMarker(receiptNumber);
            if (marker == "VOIDED") marker = null;

            Guid docId = Guid.Empty;
            bool approved = false;

            if (!string.IsNullOrEmpty(marker) && marker.StartsWith("DOC:"))
            {
                Guid.TryParse(marker.Substring(4), out docId);
            }
            else if (!string.IsNullOrEmpty(marker) && marker.StartsWith("APR:"))
            {
                Guid.TryParse(marker.Substring(4), out docId);
                approved = true;
            }
            else if (!string.IsNullOrEmpty(marker))
            {
                // เฟส settle เริ่ม/จบแล้ว (ADJ:/NOCASH/paymentId) → เอกสารมีอยู่แล้วแน่นอน
                docId = LookupNexaaccDocIdByReceipt(receiptNumber);
                if (docId != Guid.Empty) return docId;
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnsureRevenueDoc: marker={marker} แต่หา doc id จาก queue ไม่เจอ receipt={receiptNumber} → สร้างใหม่", "SYSTEM");
            }

            if (docId == Guid.Empty)
            {
                var createResult = await _apiClient.CreateDocumentAsync(doc);
                docId = RequireValidDocId(createResult?.data?.Id, $"CreateDocument (tax-invoice) receipt={receiptNumber}");
                _lastDocNumber = createResult?.data?.DocumentNumber;
                SetReceiptPaymentMarker(receiptNumber, "DOC:" + docId);
            }

            if (!approved)
            {
                try
                {
                    var apr = await _apiClient.ApproveDocumentAsync(docId, new ApproveDocumentRequest { AcknowledgeWarnings = true });
                    string aprNum = apr?.data?.DocumentNumber;
                    if (!string.IsNullOrEmpty(aprNum) && !aprNum.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                        _lastDocNumber = aprNum;
                    int st = apr?.data?.Status ?? -1;
                    if (st == NexaaccDocumentStatus.Draft || st == NexaaccDocumentStatus.WaitingApproval
                        || st == NexaaccDocumentStatus.Rejected)
                        throw new Exception($"อนุมัติใบกำกับไม่สำเร็จ — สถานะ {st} receipt={receiptNumber} docId={docId}");
                }
                catch (AccountingApiException ex) when (IsAlreadyApprovedOrPosted(ex))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"EnsureRevenueDoc: doc {docId} already approved receipt={receiptNumber} ({ex.StatusCode})", "SYSTEM");
                }
                SetReceiptPaymentMarker(receiptNumber, "APR:" + docId);
            }

            return docId;
        }

        /// <summary>มี JE ที่ Reference นี้ (ไม่ถูก void) อยู่บน NextAcc แล้วไหม — ใช้กัน post ซ้ำตอน retry</summary>
        private async Task<bool> JournalExistsByReferenceAsync(string reference)
        {
            if (string.IsNullOrEmpty(reference)) return false;
            try
            {
                var found = await _apiClient.SearchJournalsAsync(reference, 10);
                if (found?.data?.Items != null)
                    foreach (var j in found.data.Items)
                        if (string.Equals(j.Reference, reference, StringComparison.OrdinalIgnoreCase) && j.Status != 2)
                            return true;
            }
            catch { }
            return false;
        }

        /// <summary>รับรู้รายได้มัดจำ (§78/1 checkout) แบบ idempotent — ข้ามถ้า RES-{id}-DEPREV โพสต์แล้ว</summary>
        private async Task PostDepositRevenueRecognitionAsync(int reservationId, decimal depositApplied, string receiptNumber, string revenueType, bool hasVat)
        {
            string reff = $"RES-{reservationId}-DEPREV";
            if (await JournalExistsByReferenceAsync(reff))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"PostDepositRevenueRecognition: {reff} โพสต์แล้ว — ข้าม (idempotent) receipt={receiptNumber}", "SYSTEM");
                return;
            }
            var depRev = _mapper.MapDepositRevenueRecognition(reservationId, depositApplied, receiptNumber, revenueType, hasVat);
            var r = await _apiClient.CreateJournalAsync(depRev);
            Guid id = RequireValidDocId(r?.data?.Id, $"DepositRevenueRecognition receipt={receiptNumber}");
            await SafePostJournalAsync(id);
        }

        /// <summary>กลับรายการ DEPREV ตาม "บัญชีจริงที่โพสต์ไป" (แม่นกว่า rebuild) — idempotent, ใช้ตอน void ใบกำกับ §78/1</summary>
        private async Task ReverseDepositRevenueRecognitionAsync(int reservationId, string receiptNumber)
        {
            string reff = $"RES-{reservationId}-DEPREV";
            string revRef = reff + "-REV";
            if (await JournalExistsByReferenceAsync(revRef))
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ReverseDepositRevenueRecognition: {revRef} กลับรายการแล้ว — ข้าม receipt={receiptNumber}", "SYSTEM");
                return;
            }
            JournalEntryResponse orig = null;
            try
            {
                var found = await _apiClient.SearchJournalsAsync(reff, 10);
                if (found?.data?.Items != null)
                    foreach (var j in found.data.Items)
                        if (string.Equals(j.Reference, reff, StringComparison.OrdinalIgnoreCase) && j.Status != 2)
                        { orig = j; break; }
            }
            catch { }
            if (orig?.Lines == null || orig.Lines.Count < 2)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ReverseDepositRevenueRecognition: ไม่พบ DEPREV ({reff}) — ข้าม (อาจไม่ใช่โหมด §78/1) receipt={receiptNumber}", "SYSTEM");
                return;
            }
            var lines = new List<JournalEntryLineRequest>();
            foreach (var l in orig.Lines)   // สลับ Dr↔Cr ตามบัญชีจริง → กลับรายการเป๊ะทุกบัญชี
                lines.Add(new JournalEntryLineRequest
                {
                    AccountId = l.AccountId,
                    DebitAmount = l.CreditAmount,
                    CreditAmount = l.DebitAmount,
                    Description = "กลับรายการ: " + (l.Description ?? "")
                });
            var rev = new CreateJournalEntryRequest
            {
                EntryDate = DateTime.Now,
                JournalType = NexaaccJournalType.General,
                Description = $"กลับรายการรับรู้รายได้มัดจำ (void ใบกำกับ {receiptNumber})",
                Reference = revRef,
                Lines = lines
            };
            var rr = await _apiClient.CreateJournalAsync(rev);
            Guid rrId = RequireValidDocId(rr?.data?.Id, $"DepositRevenueRecognitionReverse receipt={receiptNumber}");
            await SafePostJournalAsync(rrId);
            _code.Logs(_connectionString, "AccountingSync",
                $"ReverseDepositRevenueRecognition: กลับรายการ {reff} สำเร็จ receipt={receiptNumber}", "SYSTEM");
        }

        // ──────────────────────────────────────────────
        // PaymentVoucher document (company /document, DocumentType=13) — "จ่ายจริง" = ใบสำคัญจ่าย
        //   Dr ค่าใช้จ่าย/ภาษีซื้อ / Cr แหล่งเงิน (PaymentAccountId) − WHT ; ไม่เปิดเจ้าหนี้หลอก
        // idempotent ผ่าน Account_Payment.Nexaacc_Voucher_Doc_Marker (DOC:→APR:→{id}/VOIDED)
        // ──────────────────────────────────────────────
        private async System.Threading.Tasks.Task<Guid> SettleVoucherDocAsync(CreateDocumentRequest doc, string documentNumber)
        {
            string marker = LookupVoucherDocMarker(documentNumber);
            // edit = void→สร้างใหม่เลขเดิม: marker "VOIDED" ไม่บล็อกการสร้างใหม่
            if (marker == "VOIDED") marker = null;
            bool isFinal = !string.IsNullOrEmpty(marker)
                && !marker.StartsWith("DOC:") && !marker.StartsWith("APR:");
            if (isFinal && Guid.TryParse(marker, out var finalId) && finalId != Guid.Empty)
                return finalId;   // จบแล้ว

            bool approved = !string.IsNullOrEmpty(marker) && marker.StartsWith("APR:");
            Guid docId = Guid.Empty;
            if (!string.IsNullOrEmpty(marker) && (marker.StartsWith("DOC:") || marker.StartsWith("APR:")))
                Guid.TryParse(marker.Substring(4), out docId);

            // 1) สร้างเอกสาร (company /document ไม่ dedupe → marker กันสร้างซ้ำ)
            if (docId == Guid.Empty)
            {
                var createResult = await _apiClient.CreateDocumentAsync(doc);
                docId = RequireValidDocId(createResult?.data?.Id, $"CreatePaymentVoucher (doc) doc={documentNumber}");
                _lastDocNumber = createResult?.data?.DocumentNumber;
                SetVoucherDocMarker(documentNumber, "DOC:" + docId);
            }

            // 2) อนุมัติ (auto-post GL) — idempotent
            if (!approved)
            {
                try
                {
                    await _apiClient.ApproveDocumentAsync(docId, new ApproveDocumentRequest { AcknowledgeWarnings = true });
                }
                catch (AccountingApiException ex) when (IsAlreadyPostedOrTerminal(ex))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"SettleVoucherDoc: doc {docId} already approved/posted doc={documentNumber} ({ex.StatusCode})", "SYSTEM");
                }
                SetVoucherDocMarker(documentNumber, "APR:" + docId);
            }

            SetVoucherDocMarker(documentNumber, docId.ToString());   // final
            _code.Logs(_connectionString, "AccountingSync",
                $"SettleVoucherDoc: เสร็จ doc={documentNumber} docId={docId} (ใบสำคัญจ่าย company endpoint)", "SYSTEM");
            return docId;
        }

        private string LookupVoucherDocMarker(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Voucher_Doc_Marker FROM Account_Payment WHERE ID = @num",
                    new Dictionary<string, object> { { "@num", documentNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_Voucher_Doc_Marker"] != DBNull.Value)
                    return dt.Rows[0]["Nexaacc_Voucher_Doc_Marker"].ToString();
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVoucherDocMarker: doc={documentNumber} {ex.Message}", "SYSTEM");
            }
            return null;
        }

        private void SetVoucherDocMarker(string documentNumber, string marker)
        {
            if (string.IsNullOrEmpty(documentNumber)) return;
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Account_Payment SET Nexaacc_Voucher_Doc_Marker = @m WHERE ID = @num",
                    new Dictionary<string, object> { { "@m", marker }, { "@num", documentNumber } });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"SetVoucherDocMarker: doc={documentNumber} {ex.Message}", "SYSTEM");
            }
        }

        /// <summary>คืน Nexaacc_Payment_Id ของใบสำคัญจ่าย (ถ้าบันทึกชำระเงินแล้ว) — ใช้กันจ่ายซ้ำ (M8 idempotent)</summary>
        private string LookupVoucherPaymentId(string documentNumber)
        {
            if (string.IsNullOrEmpty(documentNumber)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Nexaacc_Payment_Id FROM Account_Payment WHERE ID = @num",
                    new Dictionary<string, object> { { "@num", documentNumber } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Nexaacc_Payment_Id"] != DBNull.Value)
                {
                    string v = dt.Rows[0]["Nexaacc_Payment_Id"].ToString();
                    return string.IsNullOrWhiteSpace(v) ? null : v;
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVoucherPaymentId: doc={documentNumber} {ex.Message}", "SYSTEM");
            }
            return null;
        }

        private string LookupReceiptPaymentMarker(string receiptNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT Nexaacc_Receipt_Payment_Id FROM Account_Receipt WHERE ID = @num",
                    new Dictionary<string, object> { { "@num", receiptNumber } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return dt.Rows[0][0]?.ToString();
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupReceiptPaymentMarker: receipt={receiptNumber} {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>
        /// Void payment ฝั่งรับที่บันทึกไว้ตอน settle (กัน cash/AR เพี้ยนเมื่อ void ใบเสร็จ).
        /// alreadyCascaded=true (integration void endpoint จัดการ payment ให้แล้ว) → แค่ตั้ง marker.
        /// alreadyCascaded=false (credit-note fallback) → ต้องเรียก VoidPaymentAsync เอง.
        /// </summary>
        private async System.Threading.Tasks.Task VoidRecordedReceiptPaymentAsync(string receiptNumber, bool alreadyCascaded)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return;
            string marker = LookupReceiptPaymentMarker(receiptNumber);
            Guid pid;
            if (!alreadyCascaded && !string.IsNullOrEmpty(marker) && Guid.TryParse(marker, out pid))
            {
                try
                {
                    await _apiClient.VoidPaymentAsync(pid);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"VoidRecordedReceiptPayment: voided payment {pid} for receipt={receiptNumber}", "SYSTEM");
                }
                catch (AccountingApiException ex) when (IsAlreadyPostedOrTerminal(ex))
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"VoidRecordedReceiptPayment: payment {pid} already voided/terminal receipt={receiptNumber} ({ex.StatusCode})", "SYSTEM");
                }
            }
            SetReceiptPaymentMarker(receiptNumber, "VOIDED");
        }

        private void SetReceiptPaymentMarker(string receiptNumber, string marker)
        {
            try
            {
                _code.DatabaseInsertSafe(_connectionString,
                    "UPDATE Account_Receipt SET Nexaacc_Receipt_Payment_Id = @m WHERE ID = @num",
                    new Dictionary<string, object> { { "@m", marker }, { "@num", receiptNumber } });
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"SetReceiptPaymentMarker: receipt={receiptNumber} {ex.Message}", "SYSTEM");
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
            if (_config.IsPayrollLocal)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPayrollEntry: SKIPPED — PayrollSyncMode=LOCAL", "SYSTEM");
                return "SKIPPED_LOCAL_MODE";
            }

            decimal totalSalary = Convert.ToDecimal(p["totalSalary"]);
            if (totalSalary <= 0)
                throw new ArgumentException($"Cannot create payroll journal: totalSalary is {totalSalary}");

            DateTime payDate = ParseAcctDate(p["payDate"]?.ToString());
            string period = p.ContainsKey("period") ? p["period"]?.ToString() : "";
            decimal ssfEmployee = p.ContainsKey("socialSecurityEmployee") ? Convert.ToDecimal(p["socialSecurityEmployee"]) : 0;
            decimal ssfEmployer = p.ContainsKey("socialSecurityEmployer") ? Convert.ToDecimal(p["socialSecurityEmployer"]) : 0;
            decimal whtAmount = p.ContainsKey("whtAmount") ? Convert.ToDecimal(p["whtAmount"]) : 0;
            string docNumber = p.ContainsKey("documentNumber") ? p["documentNumber"]?.ToString() : "";
            string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() : "CASH";

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollEntry: period={period} gross={totalSalary} ssfEmp={ssfEmployee} ssfEr={ssfEmployer} wht={whtAmount} method={paymentMethod} doc={docNumber} mode={_config.PayrollSyncMode}",
                "SYSTEM");

            // เงินเดือนพนักงานประจำ (เงินได้ ม.40(1)) โพสต์เป็น "journal" เสมอ — ไม่ใช่ expense/PV
            // เหตุผลทางภาษี:
            //   • พนักงานประจำ "ไม่ต้องออก 50ทวิ รายเดือน" (ออกปีละครั้ง/ตอนลาออก) — ถ้าใช้ /expenses
            //     NextAcc จะ auto-generate 50ทวิ ทุกเดือน (ภงด.3/53 ตาม contact type) ซึ่งผิด
            //   • ภงด.1 รายเดือนใช้แค่ "ข้อมูล" (เงินได้+ภาษีหัก ต่อคน) ซึ่ง TakeTime ออกใบแนบ ภงด.1 เองได้
            //   • journal ลง GL ถูกต้องด้วยยอดจริงจาก TakeTime: DR เงินเดือน/ปกส.นายจ้าง,
            //     CR ภาษีหัก ณ ที่จ่ายค้างจ่าย (ภงด.1) + ปกส.ค้างจ่าย + เงินสด — และไม่ออก 50ทวิ
            var journal = _mapper.MapPayrollToJournal(totalSalary, payDate, period,
                ssfEmployee, ssfEmployer, whtAmount, paymentMethod);
            if (!string.IsNullOrEmpty(docNumber))
                journal.Reference = docNumber;
            var result = await _apiClient.CreateJournalAsync(journal);
            Guid payrollId = RequireValidDocId(result?.data?.Id, $"CreateJournal (payroll) period={period}");
            _lastDocNumber = result?.data?.EntryNumber;
            _lastDocType = "JOURNAL";
            await SafePostJournalAsync(payrollId);
            return payrollId.ToString();
        }

        /// <summary>
        /// ยกเลิก/กลับรายการใบจ่ายเงินเดือนบน NextAcc — โพสต์ journal กลับรายการตามยอดเดิม
        /// (อ่านยอดจาก payload เดิมของ CREATE_PAYROLL_ENTRY) → หักล้างทั้ง DOCUMENT/JOURNAL mode ที่ระดับ GL
        /// </summary>
        private async Task<string> ProcessVoidPayroll(Dictionary<string, object> p)
        {
            string nexaaccId = p.ContainsKey("nexaaccId") ? p["nexaaccId"]?.ToString() : null;
            string documentNumber = p.ContainsKey("documentNumber") ? p["documentNumber"]?.ToString() : null;

            // ถ้า nexaaccId มาจาก SYNC_PAYROLL_RUN → void PayrollRun บน NextAcc (reverse GL อัตโนมัติ)
            string docType = p.ContainsKey("documentType") ? p["documentType"]?.ToString() : null;
            if (docType == "PAYROLL_RUN" && Guid.TryParse(nexaaccId, out Guid prId))
            {
                try
                {
                    await _apiClient.VoidPayrollRunAsync(prId);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidPayroll: voided PayrollRun {nexaaccId} doc={documentNumber}", "SYSTEM");
                    return $"VOIDED_RUN:{nexaaccId}";
                }
                catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
                {
                    return $"VOIDED_RUN:{nexaaccId} (already)";
                }
                catch (AccountingApiException ex)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidPayroll: void PayrollRun failed {ex.StatusCode}: {ex.Message}", "SYSTEM");
                }
            }

            // Journal mode: ยกเลิกด้วยการกลับรายการ journal
            // วิธีที่ 1: reverse journal ตรงๆ ด้วย id เดิม (ถ้าเป็น GUID ของ journal)
            if (Guid.TryParse(nexaaccId, out Guid jid))
            {
                try
                {
                    var reverseResult = await _apiClient.ReverseJournalAsync(jid, new ReverseJournalEntryRequest
                    {
                        ReversalDate = DateTime.Now,
                        Description = $"กลับรายการจ่ายเงินเดือน {documentNumber}"
                    });
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidPayroll: reversed journal {nexaaccId} → {reverseResult.data?.Id} doc={documentNumber}",
                        "SYSTEM");
                    return $"REVERSED:{nexaaccId} → {reverseResult.data?.Id}";
                }
                catch (AccountingApiException ex) when (IsAlreadyVoided(ex))
                {
                    return $"REVERSED:{nexaaccId} (already)";
                }
                catch (AccountingApiException ex) when (ex.StatusCode == 404)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidPayroll: reverse-journal 404 for {nexaaccId}, falling back to reversal entry doc={documentNumber}",
                        "SYSTEM");
                }
            }

            // วิธีที่ 2 (fallback): สร้าง journal กลับรายการจากยอดเดิม
            var orig = LookupOriginalPayload(documentNumber, "PAYROLL", "CREATE_PAYROLL_ENTRY");
            if (orig != null)
            {
                decimal totalSalary = orig.ContainsKey("totalSalary") ? Convert.ToDecimal(orig["totalSalary"]) : 0;
                decimal ssfEmployee = orig.ContainsKey("socialSecurityEmployee") ? Convert.ToDecimal(orig["socialSecurityEmployee"]) : 0;
                decimal ssfEmployer = orig.ContainsKey("socialSecurityEmployer") ? Convert.ToDecimal(orig["socialSecurityEmployer"]) : 0;
                decimal whtAmount = orig.ContainsKey("whtAmount") ? Convert.ToDecimal(orig["whtAmount"]) : 0;
                string period = orig.ContainsKey("period") ? orig["period"]?.ToString() : "";
                string paymentMethod = orig.ContainsKey("paymentMethod") ? orig["paymentMethod"]?.ToString() : "CASH";

                if (totalSalary > 0)
                {
                    // เงินเดือนโพสต์ผ่าน MapPayrollToJournal (journal เต็ม) เสมอ → กลับรายการแบบ journal (isDocumentMode:false)
                    var reversal = _mapper.MapPayrollReversalToJournal(totalSalary, DateTime.Now, period,
                        ssfEmployee, ssfEmployer, whtAmount, paymentMethod, documentNumber, isDocumentMode: false);
                    var result = await _apiClient.CreateJournalAsync(reversal);
                    Guid revId = RequireValidDocId(result?.data?.Id, $"VoidPayroll reversal doc={documentNumber}");
                    await SafePostJournalAsync(revId);
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidPayroll: posted reversal journal {revId} for payroll {documentNumber}", "SYSTEM");
                    return $"REVERSED:{revId}";
                }
            }

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessVoidPayroll: ไม่พบยอดเดิมของ {documentNumber} — ต้องกลับรายการบน NextAcc เอง", "SYSTEM");
            return $"VOID_SKIPPED:{nexaaccId} (payroll — manual review)";
        }

        /// <summary>
        /// Full payroll run sync: Sync พนักงาน → Create PayrollRun → Calculate → Approve → Pay
        /// NextAcc จัดการ GL + ภงด.1 + สปส.1-10 + 50ทวิ + payslip ทั้งหมด
        /// </summary>
        private async Task<string> ProcessPayrollRunSync(Dictionary<string, object> p)
        {
            int periodId = Convert.ToInt32(p["payrollPeriodId"]);

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: periodId={periodId} — starting full payroll sync", "SYSTEM");

            // 1. ดึงข้อมูลงวดเงินเดือน
            var dtPeriod = _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Payroll_Periods WHERE ID = @id",
                new Dictionary<string, object> { { "@id", periodId } });
            if (dtPeriod == null || dtPeriod.Rows.Count == 0)
                throw new ArgumentException($"Payroll period {periodId} not found");

            var periodRow = dtPeriod.Rows[0];
            int year = Convert.ToInt32(periodRow["Year"]);
            int month = Convert.ToInt32(periodRow["Month"]);
            string periodName = periodRow["PeriodName"]?.ToString() ?? $"{year}-{month:D2}";
            DateTime payrollDate = periodRow["PayrollDate"] != DBNull.Value
                ? Convert.ToDateTime(periodRow["PayrollDate"])
                : new DateTime(year, month, DateTime.DaysInMonth(year, month));
            DateTime periodStart = new DateTime(year, month, 1);
            DateTime periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // 2. Sync พนักงานทั้งหมดไป NextAcc
            var employees = LoadEmployeesForPayrollSync();
            if (employees.Count == 0)
                throw new ArgumentException("No active employees found for payroll sync");

            var syncRequest = new PayrollSyncEmployeesRequest
            {
                ExternalSystem = "TakeTime",
                Rows = employees
            };

            var syncResult = await _apiClient.SyncPayrollEmployeesAsync(syncRequest);
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: employee sync done — inserted={syncResult?.data?.Inserted} updated={syncResult?.data?.Updated} skipped={syncResult?.data?.Skipped}",
                "SYSTEM");

            // 3. สร้าง PayrollRun
            var createRequest = new PayrollCreateRunRequest
            {
                Name = $"เงินเดือน {periodName}",
                Year = year,
                Month = month,
                PayDate = payrollDate,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd
            };

            var runResult = await _apiClient.CreatePayrollRunAsync(createRequest);
            Guid runId = runResult?.data?.Id ?? Guid.Empty;
            if (runId == Guid.Empty)
                throw new Exception($"CreatePayrollRun failed: {runResult?.message}");

            string payrollNumber = runResult?.data?.PayrollNumber;
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: run created id={runId} number={payrollNumber}", "SYSTEM");

            // 4. Calculate (NextAcc คำนวณ SSF/WHT จากข้อมูลพนักงานที่ sync ไป)
            var calcResult = await _apiClient.CalculatePayrollAsync(runId);
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: calculated — gross={calcResult?.data?.TotalGrossSalary} net={calcResult?.data?.TotalNetPay} employees={calcResult?.data?.EmployeeCount}",
                "SYSTEM");

            // 5. Approve
            var approveResult = await _apiClient.ApprovePayrollAsync(runId);
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: approved status={approveResult?.data?.Status}", "SYSTEM");

            // 6. Pay (สร้าง GL journal + ภงด.1 + สปส.1-10 + 50ทวิ + payslip อัตโนมัติ)
            var payResult = await _apiClient.PayPayrollAsync(runId);
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunSync: PAID — run={runId} number={payrollNumber} gross={payResult?.data?.TotalGrossSalary} tax={payResult?.data?.TotalWithholdingTax} ssfEmp={payResult?.data?.TotalSocialSecurityEmployee} ssfEr={payResult?.data?.TotalSocialSecurityEmployer} net={payResult?.data?.TotalNetPay}",
                "SYSTEM");

            _lastDocNumber = payrollNumber;
            _lastDocType = "PAYROLL_RUN";
            return runId.ToString();
        }

        /// <summary>
        /// Option A — Import ยอดเงินเดือนที่ TakeTime คำนวณเอง (ผันแปรต่องวด) เข้า NextAcc:
        /// Sync พนักงาน → POST /payroll/runs/import (Recalculate=false, ยอดจาก Payroll_Records) →
        /// approve → pay → NextAcc ออก GL + ภงด.1 + สปส.1-10 + 50ทวิ + payslip จากยอดของเรา.
        /// Idempotent: queue refKey + NextAcc ExternalRunRef (ยิงซ้ำคืน run เดิม)
        /// </summary>
        private async Task<string> ProcessPayrollRunImport(Dictionary<string, object> p)
        {
            int periodId = Convert.ToInt32(p["payrollPeriodId"]);

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunImport: periodId={periodId} — starting payroll IMPORT (Option A)", "SYSTEM");

            // 1. งวดเงินเดือน
            var dtPeriod = _code.DatabaseQuerySafe(_connectionString,
                "SELECT * FROM Payroll_Periods WHERE ID = @id",
                new Dictionary<string, object> { { "@id", periodId } });
            if (dtPeriod == null || dtPeriod.Rows.Count == 0)
                throw new ArgumentException($"Payroll period {periodId} not found");

            var periodRow = dtPeriod.Rows[0];
            int year = Convert.ToInt32(periodRow["Year"]);
            int month = Convert.ToInt32(periodRow["Month"]);
            string periodName = periodRow["PeriodName"]?.ToString() ?? $"{year}-{month:D2}";
            DateTime payrollDate = periodRow["PayrollDate"] != DBNull.Value
                ? Convert.ToDateTime(periodRow["PayrollDate"])
                : new DateTime(year, month, DateTime.DaysInMonth(year, month));
            DateTime periodStart = new DateTime(year, month, 1);
            DateTime periodEnd = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // 2. Sync พนักงาน "เฉพาะคนในงวดนี้" (จาก Payroll_Records) — ให้ EmployeeExternalId map ครบ 1:1
            //    กับ import lines เสมอ (ครอบคลุมคนที่ลาออก/ไม่มี Employee_Salary active/เงินเดือน 0 ที่
            //    LoadEmployeesForPayrollSync กรองออก แต่ยังมีในงวด) ไม่งั้น NextAcc map ไม่เจอ → reject ทั้ง run
            var employees = LoadEmployeesForPayrollPeriod(periodId);
            if (employees.Count > 0)
            {
                var syncResult = await _apiClient.SyncPayrollEmployeesAsync(
                    new PayrollSyncEmployeesRequest { ExternalSystem = "TakeTime", Rows = employees });
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPayrollRunImport: employee sync (period {periodId}) — count={employees.Count} inserted={syncResult?.data?.Inserted} updated={syncResult?.data?.Updated} skipped={syncResult?.data?.Skipped}",
                    "SYSTEM");
            }

            // 3. ยอดต่อพนักงานที่ TakeTime คำนวณไว้ (Payroll_Records) → import lines
            var dtRec = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT PR.Admin_ID, PR.EmployeeName, PR.BaseSalary, PR.OTAmount, PR.BonusAmount,
                         PR.AllowanceAmount, PR.LeaveDeduction, PR.SocialSecurity, PR.Tax,
                         PR.OtherDeductions, PR.TotalEarnings, PR.TotalDeductions, PR.NetSalary,
                         A.IDCard
                  FROM Payroll_Records PR
                  LEFT JOIN Admin A ON A.ID = PR.Admin_ID
                  WHERE PR.PayrollPeriod_ID = @id",
                new Dictionary<string, object> { { "@id", periodId } });

            if (dtRec == null || dtRec.Rows.Count == 0)
                throw new ArgumentException($"No Payroll_Records for period {periodId} — สร้างรอบเงินเดือนก่อน");

            var lines = new List<PayrollImportLine>();
            var balanceErrors = new List<string>();
            foreach (DataRow r in dtRec.Rows)
            {
                int adminId = Convert.ToInt32(r["Admin_ID"]);
                decimal baseSalary = SafeDec(r["BaseSalary"]);
                decimal ot = SafeDec(r["OTAmount"]);
                decimal bonus = SafeDec(r["BonusAmount"]);
                decimal allowance = SafeDec(r["AllowanceAmount"]);
                decimal leave = SafeDec(r["LeaveDeduction"]);
                decimal sso = SafeDec(r["SocialSecurity"]);
                decimal wht = SafeDec(r["Tax"]);
                decimal other = SafeDec(r["OtherDeductions"]);
                decimal gross = SafeDec(r["TotalEarnings"]);       // = base+OT+bonus+allowance
                decimal totalDed = SafeDec(r["TotalDeductions"]);  // = leave+sso+wht+other (ฝั่งลูกจ้าง)
                decimal net = SafeDec(r["NetSalary"]);             // = gross − totalDed

                // NextAcc validation: net == gross − (SSO emp + WHT + PVD emp + advance + other)
                // TakeTime มี "หักลา (LeaveDeduction)" ที่ไม่มีช่องตรงใน import → รวมเข้า OtherDeductions
                // เพื่อให้สมการ balance (otherImport = other + leave). ProvidentFund/advance = 0
                decimal otherImport = other + leave;

                // pre-validate ฝั่งเรา: net == gross − (sso + wht + otherImport) ก่อนส่ง
                // ถ้าไม่ตรง NextAcc จะ reject ทั้ง run ด้วย 422 แบบ opaque → ดักไว้พร้อมระบุพนักงาน
                decimal expectedNet = gross - (sso + wht + otherImport);
                if (Math.Abs(expectedNet - net) > 0.01m)
                    balanceErrors.Add($"{r["EmployeeName"]}: net {net:N2} ≠ gross {gross:N2} − หัก(SSO {sso:N2}+WHT {wht:N2}+อื่น {otherImport:N2})={expectedNet:N2}");

                lines.Add(new PayrollImportLine
                {
                    EmployeeExternalId = $"EMP-{adminId}",
                    CitizenId = r["IDCard"]?.ToString(),
                    EmployeeName = r["EmployeeName"]?.ToString(),
                    BaseSalary = baseSalary,
                    OvertimePay = ot,
                    Allowances = allowance,
                    Commission = 0,
                    Bonus = bonus,
                    OtherEarnings = 0,
                    GrossIncome = gross,
                    SocialSecurityEmployee = sso,
                    SocialSecurityEmployer = sso,   // นายจ้างสมทบเท่าลูกจ้าง (5% เท่ากัน) — ตรงกับ EnqueuePayrollJournal
                    WithholdingTax = wht,
                    ProvidentFundEmployee = 0,
                    ProvidentFundEmployer = 0,
                    SalaryAdvance = 0,
                    OtherDeductions = otherImport,
                    TotalDeductions = totalDed,
                    NetPay = net,
                    IncomeTypeCode = "01"           // ม.40(1) เงินเดือน
                });
            }

            if (balanceErrors.Count > 0)
                throw new ArgumentException(
                    "ยอดเงินเดือนไม่ balance (net ≠ gross − หักฝั่งลูกจ้าง) — NextAcc จะปฏิเสธทั้ง run. แก้ที่ Payroll_Records ก่อน:\n"
                    + string.Join("\n", balanceErrors));

            // 4. Import (Recalculate=false) — idempotent ด้วย ExternalRunRef
            var req = new PayrollImportRunRequest
            {
                Name = $"เงินเดือน {periodName}",
                Year = year,
                Month = month,
                PayDate = payrollDate,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                ExternalSystem = "TakeTime",
                ExternalRunRef = $"PAYRUN-{year}{month:D2}",
                Recalculate = false,
                Lines = lines
            };

            var imp = await _apiClient.ImportPayrollRunAsync(req);
            Guid runId = imp?.data?.Id ?? Guid.Empty;
            if (runId == Guid.Empty)
                throw new Exception($"ImportPayrollRun failed: {imp?.message}");
            string status = imp?.data?.Status ?? "";
            string payrollNumber = imp?.data?.PayrollNumber;
            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessPayrollRunImport: imported run={runId} number={payrollNumber} status={status} employees={lines.Count} gross={imp?.data?.TotalGrossSalary} net={imp?.data?.TotalNetPay}",
                "SYSTEM");

            // 5. approve → pay (gate ตามสถานะ → idempotent เมื่อ run เดิมถูก approve/pay ไปแล้ว)
            if (!status.Equals("Approved", StringComparison.OrdinalIgnoreCase)
                && !status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                var ap = await _apiClient.ApprovePayrollAsync(runId);
                status = ap?.data?.Status ?? "Approved";
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPayrollRunImport: approved run={runId} status={status}", "SYSTEM");
            }
            if (!status.Equals("Paid", StringComparison.OrdinalIgnoreCase))
            {
                var pay = await _apiClient.PayPayrollAsync(runId);
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessPayrollRunImport: PAID run={runId} number={payrollNumber} gross={pay?.data?.TotalGrossSalary} wht={pay?.data?.TotalWithholdingTax} ssfEmp={pay?.data?.TotalSocialSecurityEmployee} ssfEr={pay?.data?.TotalSocialSecurityEmployer} net={pay?.data?.TotalNetPay}",
                    "SYSTEM");
            }

            _lastDocNumber = payrollNumber;
            _lastDocType = "PAYROLL_RUN";
            return runId.ToString();
        }

        private static decimal SafeDec(object v)
        {
            if (v == null || v == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(v); } catch { return 0m; }
        }

        /// <summary>
        /// ดึงข้อมูลพนักงานทั้งหมดจาก Admin + Employee_Salary เพื่อ sync ไป NextAcc
        /// </summary>
        private List<PayrollEmployeeSyncRow> LoadEmployeesForPayrollSync()
        {
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT A.ID, A.FirstName, A.LastName, A.Title, A.IDCard,
                         A.Phone, A.Email, A.HireDate, A.Status,
                         A.BankCode, A.BankAccountNumber, A.BankAccountName,
                         ES.MonthlySalary, ES.Position
                  FROM Admin A
                  INNER JOIN Employee_Salary ES ON ES.Admin_ID = A.ID AND ES.IsActive = 1
                  WHERE A.Status = 1 AND ES.MonthlySalary > 0",
                null);

            var employees = new List<PayrollEmployeeSyncRow>();
            if (dt == null) return employees;

            foreach (DataRow row in dt.Rows)
            {
                string firstName = row["FirstName"]?.ToString() ?? "";
                string lastName = row["LastName"]?.ToString() ?? "";
                string title = row["Title"]?.ToString() ?? "";
                string idCard = row["IDCard"]?.ToString() ?? "";
                int adminId = Convert.ToInt32(row["ID"]);

                employees.Add(new PayrollEmployeeSyncRow
                {
                    ExternalId = $"EMP-{adminId}",
                    ExternalSystem = "TakeTime",
                    EmployeeCode = $"EMP-{adminId:D4}",
                    TitleTh = title,
                    FirstNameTh = firstName,
                    LastNameTh = lastName,
                    CitizenId = idCard,
                    StartDate = row["HireDate"] != DBNull.Value
                        ? Convert.ToDateTime(row["HireDate"]) : new DateTime(2020, 1, 1),
                    BaseSalary = Convert.ToDecimal(row["MonthlySalary"]),
                    SalaryType = "Monthly",
                    Position = row["Position"]?.ToString(),
                    Phone = row["Phone"]?.ToString(),
                    Email = row["Email"]?.ToString(),
                    BankName = row["BankCode"]?.ToString(),
                    BankAccountNumber = row["BankAccountNumber"]?.ToString(),
                    BankAccountName = row["BankAccountName"]?.ToString(),
                    IsSubjectToSocialSecurity = true,
                    IsActive = true
                });
            }

            return employees;
        }

        /// <summary>
        /// ดึงพนักงาน "เฉพาะที่อยู่ในงวดเงินเดือนนี้" (จาก Payroll_Records) พร้อม master จาก Admin —
        /// ใช้ก่อน import เพื่อให้ทุก import line map กับพนักงานใน NextAcc ได้ (ExternalId=EMP-{id}) ครบ
        /// ไม่ตัดคนที่ลาออก/ไม่มี Employee_Salary active/เงินเดือน 0 ออก (ต่างจาก LoadEmployeesForPayrollSync)
        /// </summary>
        private List<PayrollEmployeeSyncRow> LoadEmployeesForPayrollPeriod(int periodId)
        {
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT A.ID, A.FirstName, A.LastName, A.Title, A.IDCard,
                         A.Phone, A.Email, A.HireDate, A.Status,
                         A.BankCode, A.BankAccountNumber, A.BankAccountName,
                         PR.BaseSalary,
                         (SELECT TOP 1 Position FROM Employee_Salary WHERE Admin_ID = A.ID AND IsActive = 1) AS Position
                  FROM Payroll_Records PR
                  INNER JOIN Admin A ON A.ID = PR.Admin_ID
                  WHERE PR.PayrollPeriod_ID = @id",
                new Dictionary<string, object> { { "@id", periodId } });

            var employees = new List<PayrollEmployeeSyncRow>();
            if (dt == null) return employees;

            foreach (DataRow row in dt.Rows)
            {
                int adminId = Convert.ToInt32(row["ID"]);
                bool active = row["Status"] != DBNull.Value && Convert.ToInt32(row["Status"]) == 1;
                employees.Add(new PayrollEmployeeSyncRow
                {
                    ExternalId = $"EMP-{adminId}",
                    ExternalSystem = "TakeTime",
                    EmployeeCode = $"EMP-{adminId:D4}",
                    TitleTh = row["Title"]?.ToString() ?? "",
                    FirstNameTh = row["FirstName"]?.ToString() ?? "",
                    LastNameTh = row["LastName"]?.ToString() ?? "",
                    CitizenId = row["IDCard"]?.ToString() ?? "",
                    StartDate = row["HireDate"] != DBNull.Value
                        ? Convert.ToDateTime(row["HireDate"]) : new DateTime(2020, 1, 1),
                    BaseSalary = SafeDec(row["BaseSalary"]),
                    SalaryType = "Monthly",
                    Position = row["Position"]?.ToString(),
                    Phone = row["Phone"]?.ToString(),
                    Email = row["Email"]?.ToString(),
                    BankName = row["BankCode"]?.ToString(),
                    BankAccountNumber = row["BankAccountNumber"]?.ToString(),
                    BankAccountName = row["BankAccountName"]?.ToString(),
                    IsSubjectToSocialSecurity = true,
                    IsActive = active
                });
            }

            return employees;
        }

        /// <summary>
        /// บันทึกสินทรัพย์ถาวร — journal reclassify: DR Fixed Asset / CR Expense
        /// </summary>
        private async Task<string> ProcessAssetReclassification(Dictionary<string, object> p)
        {
            decimal assetAmount = Convert.ToDecimal(p["assetAmount"]);
            string assetName = p.ContainsKey("assetName") ? p["assetName"]?.ToString() : "สินทรัพย์ถาวร";
            DateTime purchaseDate = ParseAcctDate(p["purchaseDate"]?.ToString());
            string voucherDocNumber = p["voucherDocNumber"]?.ToString();
            string expenseAccountId = p.ContainsKey("expenseAccountId") ? p["expenseAccountId"]?.ToString() : null;
            string expenseCategory = p.ContainsKey("expenseCategory") ? p["expenseCategory"]?.ToString() : null;

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessAssetReclassification: {assetName} amount={assetAmount} voucher={voucherDocNumber}", "SYSTEM");

            var journal = _mapper.MapAssetReclassificationToJournal(
                assetAmount, assetName, purchaseDate, voucherDocNumber, expenseAccountId, expenseCategory);
            var intJournal = _mapper.ConvertJournalToIntegration(journal);
            var result = await _apiClient.CreateIntegrationJournalAsync(intJournal);
            Guid journalId = RequireValidDocId(result?.data?.Id, $"AssetReclassification voucher={voucherDocNumber}");

            _lastDocNumber = result?.data?.DocumentNumber;
            _lastDocType = "JOURNAL";
            return journalId.ToString();
        }

        /// <summary>อ่าน payload เดิมของ action ที่ระบุ จาก Accounting_Sync_Queue (COMPLETED ล่าสุด)</summary>
        private Dictionary<string, object> LookupOriginalPayload(string documentNumber, string entityType, string actionType)
        {
            if (string.IsNullOrEmpty(documentNumber)) return null;
            try
            {
                string esc = documentNumber.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Payload FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @e AND Action_Type = @a AND Status = 'COMPLETED'
                        AND Payload LIKE @p
                      ORDER BY Processed_Date DESC",
                    new Dictionary<string, object>
                    {
                        { "@e", entityType },
                        { "@a", actionType },
                        { "@p", $"%\"documentNumber\":\"{esc}\"%" }
                    });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Payload"] != DBNull.Value)
                {
                    string json = dt.Rows[0]["Payload"].ToString();
                    return _serializer.Deserialize<Dictionary<string, object>>(json);
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupOriginalPayload({actionType}) doc={documentNumber}: {ex.Message}", "SYSTEM");
            }
            return null;
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
            DateTime receiptDate = ParseAcctDate(p["receiptDate"]?.ToString());
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
            // ใบกำกับภาษี (ไม่ใช่มัดจำ) → forceRefresh: push เลขผู้เสียภาษี/ที่อยู่ล่าสุดจากระบบเข้า
            // contact ทุกครั้ง (ผ่านด่าน §86/4 ของ NextAcc — ผู้ใช้เพิ่งเติมข้อมูลแล้วกด Retry ต้องติดทันที)
            ContactInfo customerContact = null;
            if (_config.IsReceiptDocumentMode)
            {
                customerContact = await EnsureCustomerContactAsync(reservationId, forceRefresh: !isDeposit);
            }

            if (isDeposit)
            {
                // โหมด RECEIPT: แยก VAT ออกจากมัดจำตั้งแต่ตอนรับเงิน (ม.78/1 — บริการ)
                bool depositHasVat = LookupBusinessHasVat();
                bool depositVatAtReceipt = _config.IsDepositVatAtReceipt;

                if (_config.IsReceiptDocumentMode && _config.CanUseCompanyEndpoints && customerContact?.NexaaccContactId != null)
                {
                    // ✅ แนวทางถูกต้องตามบัญชี: Receipt doc + IsDeposit → Cr รับล่วงหน้า(หนี้สิน) ไม่ใช่รายได้
                    var doc = _mapper.MapReceiptToDocument(reservationId, null, totalAmount, null,
                        paymentMethod, receiptDate, customerName, customerContact.NexaaccContactId.Value,
                        paymentAccountId, depositHasVat, receiptNumber,
                        isDeposit: true, depositVatAtReceipt: depositVatAtReceipt, deferOutputVat: _config.IsDepositOutputVatDeferred);
                    Guid docId = await SettleReceiptDocAsync(doc, receiptNumber, reservationId, 0m,
                        paymentMethod, receiptDate, customerName, depositHasVat, paymentAccountId);
                    // มัดจำ = "ใบเสร็จรับเงิน" เท่านั้น — ไม่ออก e-Tax (ใบกำกับภาษีออกตอนเช็คเอาท์
                    // เต็มยอดรวมมัดจำ; ใช้คู่กับ Deposit_Defer_Output_Vat เพื่อให้จุด VAT ตรงใบกำกับ)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"Deposit receipt {receiptNumber}: ใบเสร็จรับเงิน (ไม่ออก e-Tax — ใบกำกับภาษีจะออกตอนเช็คเอาท์)", "SYSTEM");
                    return docId.ToString();
                }
                else if (_config.IsReceiptDocumentMode)
                {
                    // int_ key fallback: integration invoice + settle (deposit ถูกรับรู้เป็นรายได้ทันที — ดู caveat)
                    // ⚠ ข้อจำกัด int_: InboundInvoiceRequest ไม่มีฟิลด์ deposit-defer VAT → VAT มัดจำลง 21911
                    // ทันที (ไม่เข้า 21913) แม้ตั้ง Deposit_Defer_Output_Vat=1 — GL รวมยังถูก (21911=210)
                    // แต่ VAT เข้า ภ.พ.30 เร็วไป 1 งวด. ต้องการ defer จริง → ใช้ acc_ (company endpoint)
                    if (depositHasVat && depositVatAtReceipt && _config.IsDepositOutputVatDeferred)
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessReceiptDocument(int_ deposit): receipt={receiptNumber} ตั้ง defer VAT แต่ int_ endpoint ไม่รองรับ → VAT มัดจำลง 21911 ทันที (ใช้ acc_ เพื่อ defer เข้า 21913)", "SYSTEM");
                    var invoice = _mapper.MapDepositToInvoice(reservationId, totalAmount, paymentMethod, receiptDate, customerName,
                        paymentAccountId: paymentAccountId, hasVat: depositHasVat, vatAtReceipt: depositVatAtReceipt);
                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        invoice.Reference = receiptNumber;
                        invoice.ExternalRef = receiptNumber;
                        invoice.ReplaceExistingForSource = true;
                    }
                    invoice.Attachments = attachments;
                    // อ้างอิง = รหัสการจอง (คู่กับใบกำกับสุดท้ายชุดเดียวกัน); externalRef คงเลขใบเสร็จ (dedup)
                    if (reservationId > 0) invoice.Reference = $"RES-{reservationId}";
                    // int_ deposit = TaxInvoice บน NextAcc → โดน gate §86/4 ด้วย: ไม่มีข้อมูลภาษี
                    // → เคสไม่ประสงค์รับใบกำกับ (contact กลางลูกค้าเงินสด)
                    if (HasFullBuyerTaxData(customerContact))
                        ApplyContactToInvoice(invoice, customerContact);
                    else
                        MarkBuyerDeclinedTaxInvoice(invoice);

                    ApiResponse<IntegrationDocumentResponse> result;
                    var filePaths = ExtractFilePaths(attachments);
                    if (filePaths != null && filePaths.Count > 0)
                        result = await _apiClient.CreateInvoiceMultipartAsync(invoice, filePaths);
                    else
                        result = await _apiClient.CreateInvoiceAsync(invoice);

                    Guid invDocId = RequireValidDocId(result?.data?.Id, $"CreateInvoice (deposit) receipt={receiptNumber}");
                    _lastDocNumber = result?.data?.DocumentNumber;
                    _lastDocType = "INVOICE";
                    // กันเอกสารค้าง "ร่าง": อนุมัติทันทีถ้ายังไม่อนุมัติ (ก่อนบันทึกรับเงินปิดลูกหนี้)
                    await EnsureDocumentApprovedAsync(invDocId, result?.data?.Status, $"deposit invoice receipt={receiptNumber}");
                    // บันทึกรับเงินสดของมัดจำเพื่อปิดลูกหนี้ที่ invoice เปิดไว้ (NextAcc ไม่ auto-pay)
                    await SettleReceiptInNextAcc(invDocId, receiptNumber, totalAmount, 0m,
                        paymentMethod, receiptDate, customerName, depositHasVat, reservationId, paymentAccountId);
                    // มัดจำ = ใบเสร็จรับเงิน ไม่ออก e-Tax (นโยบายเดียวกับ doc-mode — ใบกำกับออกตอนเช็คเอาท์)
                    _code.Logs(_connectionString, "AccountingSync",
                        $"Deposit receipt {receiptNumber} (int_): ไม่ออก e-Tax — ใบกำกับภาษีจะออกตอนเช็คเอาท์", "SYSTEM");
                    return invDocId.ToString();
                }
                else
                {
                    var journal = _mapper.MapDepositToJournal(reservationId, totalAmount, paymentMethod, receiptDate, customerName,
                        paymentAccountId: paymentAccountId, documentNumber: receiptNumber,
                        hasVat: depositHasVat, vatAtReceipt: depositVatAtReceipt,
                        deferOutputVat: _config.IsDepositOutputVatDeferred);
                    var result = await _apiClient.CreateJournalAsync(journal);
                    Guid jrnlDocId = RequireValidDocId(result?.data?.Id, $"CreateJournal (deposit) receipt={receiptNumber}");
                    _lastDocNumber = result?.data?.EntryNumber;
                    _lastDocType = "JOURNAL";
                    // ตั้ง marker ก่อน post (กัน retry สร้าง JE ซ้ำ — CreateJournalAsync ไม่ dedupe;
                    // SafePostJournalAsync กลืน already-posted เอง) + ให้ VerifyDepositBookedOnNextAcc
                    // รู้ว่ามัดจำใบนี้ booked แล้ว (เดิมโหมด journal ไม่ตั้ง → ตัดมัดจำถูกข้ามถาวร)
                    SetReceiptPaymentMarker(receiptNumber, jrnlDocId.ToString());
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
                    // กันบวกซ้ำตอน queue retry: รอบแรกเรา persist Deposit_Applied_Amount (= รวม lines แล้ว)
                    // → รอบ retry LookupDepositAppliedFromReceipt คืนค่าที่รวม lines ไว้แล้ว ถ้าบวกอีกจะเบิล
                    if (depositApplied < depositFromLines)
                        depositApplied += depositFromLines;
                    // สำคัญ: ใบเช็คอินที่มี line "ส่วนลด" ติดลบ เก็บ Total_Amount เป็นยอด "สุทธิหลังหักมัดจำ"
                    // แต่ convention ปลายทาง (SettleReceiptDocAsync / SettleReceiptInNextAcc คิด
                    // cashNow = totalAmount − depositApplied และ doc/invoice สร้างจาก line บวกยอดเต็ม)
                    // คาดหวัง totalAmount = ยอดเต็ม (GROSS) → บวกมัดจำกลับคืนก่อน ไม่งั้นมัดจำถูกหักซ้ำ
                    // (เงินสดบน NextAcc ขาดเท่ายอดมัดจำ + ลูกหนี้ค้างเปิด)
                    totalAmount += depositFromLines;
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

                if (_config.IsReceiptDocumentMode && _config.CanUseCompanyEndpoints && customerContact?.NexaaccContactId != null
                    && HasFullBuyerTaxData(customerContact))
                {
                    // โหมด §78/1 เคร่ง (RECEIPT + ไม่ defer + มีการหักมัดจำ): มัดจำออกใบกำกับ+รับรู้ VAT
                    // ไปแล้วตอนรับเงิน → เช็คเอาท์ต้องออกใบกำกับ "เฉพาะยอดคงเหลือ" (ไม่ใช่เต็มยอด)
                    // เพื่อไม่ให้ลูกค้าได้เอกสารภาษี VAT ซ้อน 2 ใบ. โหมดอื่น (CHECKOUT/defer) = เต็มยอดเดิม
                    bool strictRemainingMode = hasVat && _config.IsDepositVatAtReceipt
                        && !_config.IsDepositOutputVatDeferred && depositApplied > 0;

                    if (strictRemainingMode)
                    {
                        decimal remaining = totalAmount - depositApplied;   // ยอดคงเหลือที่รับตอนนี้
                        // ใบกำกับ "ยอดคงเหลือ" (single line net remaining) → Dr ลูกหนี้/Cr รายได้/Cr VAT
                        var docR = _mapper.MapReceiptToDocument(reservationId, null, remaining, revenueType,
                            paymentMethod, receiptDate, customerName, customerContact.NexaaccContactId.Value,
                            paymentAccountId, hasVat, receiptNumber, isDeposit: false,
                            documentType: NexaaccDocumentType.TaxInvoice);
                        Guid docRId = Guid.Empty;
                        if (remaining > 0.01m)
                        {
                            docRId = await EnsureRevenueDocCreatedApprovedAsync(docR, receiptNumber);
                            // ปิดลูกหนี้เฉพาะยอดคงเหลือ (ไม่มีการตัดมัดจำในใบนี้ — มัดจำไม่ได้อยู่ในใบกำกับนี้)
                            await SettleReceiptInNextAcc(docRId, receiptNumber, remaining, 0m,
                                paymentMethod, receiptDate, customerName, hasVat, reservationId, paymentAccountId);
                        }
                        else
                        {
                            // มัดจำครอบคลุมเต็มยอด → ไม่มียอดคงเหลือให้ออกใบกำกับ (ใบมัดจำ = เอกสารภาษีเต็มยอด)
                            _code.Logs(_connectionString, "AccountingSync",
                                $"ProcessReceiptDocument(§78/1): receipt={receiptNumber} มัดจำครอบคลุมเต็มยอด — ไม่ออกใบกำกับคงเหลือ, รับรู้รายได้มัดจำอย่างเดียว", "SYSTEM");
                        }
                        // ย้ายมัดจำ (net) จาก 21712 → รายได้ (VAT รับรู้ไปแล้วตอนรับมัดจำ) — idempotent กัน retry ซ้ำ
                        await PostDepositRevenueRecognitionAsync(reservationId, depositApplied, receiptNumber, revenueType, hasVat);
                        _lastDocType = "RECEIPT";
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessReceiptDocument(§78/1): receipt={receiptNumber} ใบกำกับยอดคงเหลือ {remaining:N2} + รับรู้รายได้มัดจำ (มัดจำ {depositApplied:N2} ออกใบกำกับ+VAT ตอนรับเงินแล้ว)", "SYSTEM");
                        if (docRId != Guid.Empty)
                            await TryAutoGenerateEtaxAsync(docRId, receiptNumber, reservationId, remaining, customerName);
                        return docRId != Guid.Empty ? docRId.ToString() : "DEPREV_ONLY";
                    }

                    // ✅ รับชำระ/เช็คเอาท์ = "ใบกำกับภาษี" (TaxInvoice type 4) ไม่ใช่ใบเสร็จรับเงิน:
                    //    doc → Dr ลูกหนี้ / Cr รายได้ราย line / Cr ภาษีขาย แล้วปิดลูกหนี้ด้วย
                    //    SettleReceiptInNextAcc (ตัดมัดจำ Dr รับล่วงหน้า/Cr ลูกหนี้ + รับเงินสดจริง
                    //    Dr เงินสดตามแหล่งเงิน/Cr ลูกหนี้) — รองรับยอดส่วนต่าง/อัพเกรด/โอนตรง
                    //    (ใบเสร็จที่สร้างด้วยยอดใดก็ได้ จะออกใบกำกับเท่ายอดนั้น). ใบกำกับจริงบน
                    //    NextAcc → /etax/generate TAX_INVOICE ผ่าน (เดิมเป็น Receipt doc → e-Tax ไม่ออก)
                    var doc = _mapper.MapReceiptToDocument(reservationId, useMultiLine ? lines : null, totalAmount, revenueType,
                        paymentMethod, receiptDate, customerName, customerContact.NexaaccContactId.Value,
                        paymentAccountId, hasVat, receiptNumber, isDeposit: false,
                        documentType: NexaaccDocumentType.TaxInvoice);
                    // B2B TaxInvoice: มัดจำแสดง/ตัดผ่าน document payment ใน SettleReceiptInNextAcc (ลด BalanceDue)
                    // — ไม่ตั้ง field §9 (display-only นั้นเพื่อ Receipt; บน AR doc จะซ้อนกับ payment section)
                    Guid docId = await EnsureRevenueDocCreatedApprovedAsync(doc, receiptNumber);
                    await SettleReceiptInNextAcc(docId, receiptNumber, totalAmount, depositApplied,
                        paymentMethod, receiptDate, customerName, hasVat, reservationId, paymentAccountId);
                    _lastDocType = "RECEIPT";   // company doc → deep link /documents; repost ใช้เส้น void→สร้างใหม่
                    await TryAutoGenerateEtaxAsync(docId, receiptNumber, reservationId, totalAmount, customerName);
                    return docId.ToString();
                }
                else if (_config.IsReceiptDocumentMode && _config.CanUseCompanyEndpoints
                    && customerContact?.NexaaccContactId != null)
                {
                    // ✅ ลูกค้า walk-in / B2C (ไม่มีเลขผู้เสียภาษีครบ §86/4) แต่มี company endpoints:
                    //    เช็คเอาท์ = company Receipt(3) + VAT = "ใบกำกับภาษี/ใบเสร็จรับเงิน" จ่ายจบในใบ
                    //    (Dr เงินสดตามแหล่งเงิน / Cr รายได้ราย line / Cr ภาษีขาย 21911). ใช้ Receipt(3)
                    //    ไม่ใช่ TaxInvoice(4) เพราะ walk-in ไม่ผ่าน §86/4 (ไม่มีเลขภาษี+ที่อยู่) → TaxInvoice
                    //    ถูก NextAcc ปฏิเสธ; Receipt/ใบกำกับ-ใบเสร็จเงินสดไม่ติด gate นั้น. ได้ครบ: จ่ายแล้ว
                    //    (ไม่เปิดลูกหนี้), ลายเซ็นผู้จัดทำ (Receipt ดึงลายเซ็น NextAcc user), อ้างอิง RES-{id},
                    //    VAT-inclusive. หักมัดจำผ่าน SettleReceiptDocAsync (Dr 21510(+21913) / Cr เงินสด —
                    //    GL ถูก; ยอดหักมัดจำแสดงใน Notes) → void ใช้ Receipt-doc branch เดิม
                    //    (MapDepositAppliedReceiptAdjustmentReverse). ไม่ออก e-Tax XML (walk-in ไม่มีเลขภาษี).
                    var doc = _mapper.MapReceiptToDocument(reservationId, useMultiLine ? lines : null, totalAmount, revenueType,
                        paymentMethod, receiptDate, customerName, customerContact.NexaaccContactId.Value,
                        paymentAccountId, hasVat, receiptNumber, isDeposit: false,
                        documentType: NexaaccDocumentType.Receipt);
                    // NextAcc spec §9: field ระดับเอกสาร → แสดง "หักเงินมัดจำ (REC...) (500.00) / ยอดชำระสุทธิ 2,700"
                    // spec §9.1 "โหมดขับ JE": flag → NextAcc ลง JE self-contained ในใบ (กลับ 217xx/21913 จาก
                    // ใบมัดจำที่ depositAppliedRef ชี้) → เลิกส่ง JV แยกพร้อมกัน (depositForJv=0) กัน double-reverse.
                    //
                    // ⚠ GUARD + AUTO-BACKFILL (legacy มัดจำยังไม่ขึ้น NextAcc):
                    //   ตรวจก่อนว่าใบมัดจำของการจองนี้ถูก book บน NextAcc แล้วหรือยัง (marker-based).
                    //   ถ้ายัง (legacy/รับมัดจำก่อนมี integration หรือคิวยังไม่ทัน) → auto-enqueue ใบมัดจำ
                    //   ให้ sync ก่อน แล้ว defer เช็คเอาท์ (queue รอบถัดไป: มัดจำขึ้น → settle หักมัดจำต่อ).
                    //   กัน 404 'ไม่พบเอกสาร' (depositAppliedRef ชี้เอกสารที่ยังไม่มี) + กัน JV กลับมัดจำที่
                    //   ไม่มีจริง (21510/21913 ติดลบ). booked แล้ว → ส่ง field/drives ถ้า resolve เลขเอกสารได้.
                    // 3 เคส (ออกแบบให้ "ไม่มีวันค้างคิว"):
                    //   (a) มีใบมัดจำใน TakeTime (IsDeposit=1) แต่ยังไม่ book บน NextAcc → auto-backfill + defer
                    //   (b) ใบมัดจำ book แล้ว → ส่ง field/drives (ถ้า resolve เลขเอกสาร) ไม่งั้น JV กลับมัดจำจริง
                    //   (c) ไม่มีใบมัดจำเอกสารเลย (legacy: มัดจำเป็นยอดหักในใบ/บันทึกที่อื่น) → JV กลับกับ
                    //       "ยอดยกมา" 21510/21913 + ไม่ส่ง field (กัน 404) + ไม่ defer (ไม่มีอะไรให้ sync)
                    decimal depositForJv = depositApplied;
                    if (depositApplied > 0.005m)
                    {
                        var depState = VerifyDepositBookedOnNextAcc(reservationId);
                        bool booked = depState.AnyDeposit && !depState.PendingSync
                            && depState.BookedAmount + 0.01m >= depositApplied;

                        if (depState.AnyDeposit && !booked)
                        {
                            // (a) มีเอกสารใบมัดจำแต่ยังไม่ขึ้น NextAcc → backfill ให้ sync ก่อน แล้ว defer
                            int enq = EnqueueUnsyncedDeposits(reservationId, customerName);
                            throw new Exception(
                                $"เช็คเอาท์การจอง #{reservationId}: ใบมัดจำยังไม่ขึ้น NextAcc (booked {depState.BookedAmount:N2}, " +
                                $"ต้องหักมัดจำ {depositApplied:N2}) → auto-enqueue ใบมัดจำ {enq} ใบให้ sync ก่อน — settle หักมัดจำรอบถัดไป" +
                                (depState.UnsyncedReceipts.Count > 0 ? $" [ใบมัดจำ: {string.Join(", ", depState.UnsyncedReceipts)}]" : "") +
                                $" receipt={receiptNumber}");
                        }

                        if (booked && DepositRefsResolvedToNextAcc(reservationId))
                        {
                            // (b) book แล้ว + resolve เลขเอกสาร → field/drives (โชว์หักมัดจำ + self-contained JE)
                            ApplyDepositAppliedFields(doc, reservationId, depositApplied);   // ref = เลข NextAcc เสมอ
                            if (_config.IsDepositAppliedDrivesJournal)
                            {
                                doc.DepositAppliedDrivesJournal = true;
                                depositForJv = 0m;   // NextAcc ลงการหักมัดจำใน JE ของเอกสารเอง → ห้ามยิง JV แยกซ้ำ
                            }
                        }
                        else if (!depState.AnyDeposit)
                        {
                            // (c) legacy ไม่มีเอกสารใบมัดจำ → ไม่ส่ง field (ไม่มี doc ref → กัน 404) แต่ยัง JV
                            //     กลับมัดจำ (Dr 21510/21913 / Cr เงินสด) กับ "ยอดยกมา" หนี้สินมัดจำ (opening balance).
                            //     ถ้ายอดยกมาไม่มีหนี้สินนี้ → 21510/21913 ติดลบ (มองเห็น → ผู้ทำบัญชีปรับยอดยกมา).
                            //     ไม่ค้างคิว. depositForJv คงเดิม → JV ยิง.
                            _code.Logs(_connectionString, "AccountingSync",
                                $"⚠ เช็คเอาท์ #{reservationId}: depositApplied {depositApplied:N2} ไม่มีใบมัดจำเอกสาร (IsDeposit=1) — " +
                                $"หักมัดจำผ่าน JV กับยอดยกมา 21510/21913 (ไม่โชว์บรรทัดหักมัดจำบนใบ). โปรดตรวจว่ายอดยกมามีหนี้สินมัดจำก้อนนี้. receipt={receiptNumber}", "SYSTEM");
                        }
                        // else: book แล้วแต่ resolve เลขไม่ได้ (sync เก่าไม่เก็บเลข) → depositForJv คงเดิม → JV กลับมัดจำจริง
                    }
                    Guid docId = await SettleReceiptDocAsync(doc, receiptNumber, reservationId, depositForJv,
                        paymentMethod, receiptDate, customerName, hasVat, paymentAccountId);
                    _lastDocType = "RECEIPT";
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessReceiptDocument(B2C checkout): receipt={receiptNumber} → Receipt(3)+VAT (ใบกำกับ/ใบเสร็จ) docId={docId} depositApplied={depositApplied:N2} drivesJE={(doc.DepositAppliedDrivesJournal ? "yes(no JV)" : "no(JV แยก)")}", "SYSTEM");
                    return docId.ToString();
                }
                else if (_config.IsReceiptDocumentMode)
                {
                    // int_ key fallback: integration invoice (revenue ยุบบัญชีเดียว) + settle ปิดลูกหนี้
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
                    // อ้างอิง = รหัสการจอง (จับคู่กับใบมัดจำ RES-{id} ชุดเดียวกัน) —
                    // externalRef คงเป็นเลขใบเสร็จ (คีย์ dedup ราย "ใบ" ห้ามใช้รหัสจอง:
                    // การจองเดียวอาจมีหลายใบ เช่น มัดจำ+ส่วนต่าง จะชนกันเอง)
                    if (reservationId > 0) invoice.Reference = $"RES-{reservationId}";
                    // ลูกค้ามีข้อมูลภาษีครบ → ใบกำกับเต็มรูปผูก contact จริง
                    // ไม่ครบ (B2C ทั่วไป) → "ไม่ประสงค์รับใบกำกับภาษี": NextAcc ผูก contact กลาง
                    // ลูกค้าเงินสด (IsWalkInCustomer ยกเว้น §86/4) — VAT ขายเข้า ภ.พ.30 ครบ
                    if (HasFullBuyerTaxData(customerContact))
                        ApplyContactToInvoice(invoice, customerContact);
                    else
                        MarkBuyerDeclinedTaxInvoice(invoice);

                    ApiResponse<IntegrationDocumentResponse> result;
                    var fpay = ExtractFilePaths(attachments);
                    if (fpay != null && fpay.Count > 0)
                        result = await _apiClient.CreateInvoiceMultipartAsync(invoice, fpay);
                    else
                        result = await _apiClient.CreateInvoiceAsync(invoice);

                    Guid invDocId = RequireValidDocId(result?.data?.Id, $"CreateInvoice (payment) receipt={receiptNumber}");

                    // กันเอกสารค้าง "ร่าง": อนุมัติทันทีถ้ายังไม่อนุมัติ (ก่อนบันทึกรับเงินปิดลูกหนี้)
                    await EnsureDocumentApprovedAsync(invDocId, result?.data?.Status, $"payment invoice receipt={receiptNumber}");

                    // ปิดลูกหนี้ที่ invoice เปิดไว้ (NextAcc ไม่ auto-pay): ตัดมัดจำที่หัก (ถ้ามี)
                    // เข้าลูกหนี้ + บันทึกรับเงินสดจริง (= total − depositApplied). idempotent ภายใน.
                    await SettleReceiptInNextAcc(invDocId, receiptNumber, totalAmount, depositApplied,
                        paymentMethod, receiptDate, customerName, hasVat, reservationId, paymentAccountId);

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
                            vatAtReceipt: _config.IsDepositVatAtReceipt,
                            deferOutputVat: _config.IsDepositOutputVatDeferred);
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
                    if (!string.IsNullOrEmpty(receiptNumber))
                        SetReceiptPaymentMarker(receiptNumber, jrnlDocId.ToString());   // consistency กับ deposit branch
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

        /// <summary>เลขเอกสาร NextAcc ของใบมัดจำ (เช่น REC-20260702-0001) จาก Accounting_Sync_Queue
        /// — map จาก local receipt id (Account_Receipt.ID เช่น REC260702001). คืน null ถ้ายังไม่ sync/
        /// ยังเป็น DRAFT.</summary>
        private string LookupNexaaccDocNumberForReceipt(string localReceiptId)
        {
            if (string.IsNullOrEmpty(localReceiptId)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Document_Number FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'RECEIPT' AND Status = 'COMPLETED'
                        AND Nexaacc_Document_Number IS NOT NULL
                        AND Payload LIKE @p
                      ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + localReceiptId + "\"%" } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                {
                    string n = dt.Rows[0][0].ToString();
                    if (!string.IsNullOrWhiteSpace(n) && !n.StartsWith("DRAFT", StringComparison.OrdinalIgnoreCase))
                        return n;
                }
            }
            catch { }
            return null;
        }

        /// <summary>เลขใบมัดจำที่อ้างอิง (Account_Receipt.IsDeposit=1) ของการจอง — ใช้ "เลขเอกสาร NextAcc"
        /// (เช่น REC-20260702-0001) ถ้า sync แล้ว, fallback เป็น local id ถ้ายังไม่มี. หลายใบ → join ", ".</summary>
        private string LookupDepositReceiptRefs(int reservationId)
        {
            if (reservationId <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Account_Receipt
                      WHERE Reservation_ID = @rid AND IsDeposit = 1 AND (Status='Normal' OR Status IS NULL)
                      ORDER BY Created_Date",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                var refs = new List<string>();
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    string localId = r["ID"]?.ToString();
                    if (string.IsNullOrEmpty(localId)) continue;
                    // แสดงเลขเอกสาร NextAcc ของใบมัดจำ (ไม่ใช่ local id) → ตรงกับที่ลูกค้าเห็นบนใบเสร็จมัดจำ
                    string nexNum = LookupNexaaccDocNumberForReceipt(localId);
                    refs.Add(!string.IsNullOrEmpty(nexNum) ? nexNum : localId);
                }
                return refs.Count > 0 ? string.Join(", ", refs) : null;
            }
            catch { return null; }
        }

        /// <summary>Auto-backfill: enqueue ใบมัดจำของการจองที่ "ยังไม่ถูก book บน NextAcc" ให้ sync
        /// (legacy/รับมัดจำก่อนมี integration). อ่านยอด/วันที่จาก Account_Receipt เดิม, ข้ามใบที่ book แล้ว
        /// (marker APR:/ADJ:/GUID/NOCASH). EnqueueReceipt มี anti-dup กันซ้ำอยู่แล้ว. คืนจำนวนใบที่ enqueue.</summary>
        private int EnqueueUnsyncedDeposits(int reservationId, string customerName)
        {
            if (reservationId <= 0) return 0;
            int enq = 0;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ISNULL(Total_Amount,0) AS Amt, ISNULL(Vat,0) AS Vat, Created_Date,
                             Nexaacc_Receipt_Payment_Id AS Marker
                      FROM Account_Receipt
                      WHERE Reservation_ID = @rid AND IsDeposit = 1 AND (Status='Normal' OR Status IS NULL)
                      ORDER BY Created_Date",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                if (dt == null) return 0;
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    string depId = r["ID"]?.ToString();
                    if (string.IsNullOrEmpty(depId)) continue;
                    string marker = r["Marker"] == DBNull.Value ? null : r["Marker"]?.ToString();
                    // book แล้ว (APR:/ADJ:/GUID final/NOCASH) → ข้าม; ยังไม่ book (null/DOC:) → enqueue
                    bool booked = !string.IsNullOrEmpty(marker) && marker != "VOIDED"
                        && (marker.StartsWith("APR:") || marker.StartsWith("ADJ:") || marker == "NOCASH"
                            || (!marker.StartsWith("DOC:") && Guid.TryParse(marker, out _)));
                    if (booked) continue;
                    decimal amt = r["Amt"] != DBNull.Value ? Convert.ToDecimal(r["Amt"]) : 0m;
                    decimal vat = r["Vat"] != DBNull.Value ? Convert.ToDecimal(r["Vat"]) : 0m;
                    DateTime dd = r["Created_Date"] != DBNull.Value ? Convert.ToDateTime(r["Created_Date"]) : DateTime.Now;
                    if (amt <= 0) continue;
                    long qid = EnqueueReceipt(reservationId, depId, amt, vat, dd, customerName, isDeposit: true);
                    if (qid > 0)
                    {
                        enq++;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"Auto-backfill: enqueue ใบมัดจำ {depId} ({amt:N2}) การจอง #{reservationId} ให้ sync (queueId={qid})", "SYSTEM");
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"EnqueueUnsyncedDeposits failed resId={reservationId}: {ex.Message}", "SYSTEM");
            }
            return enq;
        }

        /// <summary>ใบมัดจำ "ทุกใบ" ของการจอง resolve เป็นเลขเอกสาร NextAcc ได้หรือยัง (sync + COMPLETED).
        /// ใช้เป็น gate ก่อนส่ง depositAppliedRef/drives — ถ้ายังมีใบที่ยังไม่ resolve จะส่ง ref ที่ NextAcc
        /// หาไม่เจอ → 404 "ไม่พบเอกสาร". คืน false ถ้าไม่มีใบมัดจำ/มีใบยังไม่ resolve.</summary>
        private bool DepositRefsResolvedToNextAcc(int reservationId)
        {
            if (reservationId <= 0) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID FROM Account_Receipt
                      WHERE Reservation_ID = @rid AND IsDeposit = 1 AND (Status='Normal' OR Status IS NULL)",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                if (dt == null || dt.Rows.Count == 0) return false;
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    string localId = r["ID"]?.ToString();
                    if (string.IsNullOrEmpty(localId)) return false;
                    if (string.IsNullOrEmpty(LookupNexaaccDocNumberForReceipt(localId))) return false;
                }
                return true;
            }
            catch { return false; }
        }

        /// <summary>ตั้ง field ระดับเอกสาร DepositAppliedAmount + DepositAppliedRef (NextAcc spec §9)
        /// เพื่อให้ NextAcc แสดง "หักเงินมัดจำ (ref) (amount) / ยอดชำระสุทธิ" บนใบ (display-only, ไม่กระทบ JE).
        /// แทนวิธีเดิมที่ยัดใน Notes. ไม่มีมัดจำ → ไม่ตั้ง (แสดงยอดรวมสุทธิแบบเดิม).</summary>
        private void ApplyDepositAppliedFields(CreateDocumentRequest doc, int reservationId, decimal depositApplied)
        {
            if (doc == null || depositApplied <= 0.005m) return;
            doc.DepositAppliedAmount = depositApplied;
            doc.DepositAppliedRef = LookupDepositReceiptRefs(reservationId);
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
                if (_config.IsReceiptDocumentMode && _config.CanUseCompanyEndpoints)
                {
                    // อ่านชนิดเอกสารก่อน void — ใช้เลือกวิธีกลับรายการหักมัดจำให้ตรงกับที่เคยโพสต์:
                    //   Receipt (3, ใบเก่า): เคยลง adjustment "Dr ADVANCE / Cr เงินสด" → reverse ด้วย journal เงินสด
                    //   TaxInvoice (4, ใบใหม่): ตัดมัดจำเป็น document payment → void doc cascade กลับให้เอง
                    //     เหลือแค่กลับ journal แก้ VAT มัดจำ (ถ้ามี)
                    int voidedDocType = 0;
                    bool docAlreadyGone = false;
                    try
                    {
                        var docInfo = await _apiClient.GetDocumentAsync(docId);
                        voidedDocType = docInfo?.data?.DocumentType ?? 0;
                        if (docInfo?.data != null && docInfo.data.Status == NexaaccDocumentStatus.Voided)
                            docAlreadyGone = true;   // ถูก void/ลบมือบน NextAcc ไปแล้ว
                    }
                    catch (AccountingApiException gx) when (gx.StatusCode == 404)
                    {
                        docAlreadyGone = true;       // ถูกลบบน NextAcc ไปแล้ว (workflow เคลียร์มือ)
                    }
                    catch { }

                    try
                    {
                        await _apiClient.VoidDocumentAsync(docId);
                    }
                    catch (AccountingApiException ex) when (IsAlreadyPostedOrTerminal(ex))
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(doc): doc {docId} already voided/terminal receipt={receiptNumber} ({ex.StatusCode})", "SYSTEM");
                    }

                    // เอกสารถูกลบ/void มือบน NextAcc แล้ว → ผู้ใช้เคลียร์เอง (รวม journal ปรับปรุง)
                    // ห้ามโพสต์กลับรายการซ้ำ — ไม่งั้นได้ reversal ลอยไม่มีคู่
                    if (docAlreadyGone)
                    {
                        SetReceiptPaymentMarker(receiptNumber, "VOIDED");
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt: doc {docId} ถูกลบ/void บน NextAcc แล้ว receipt={receiptNumber} — ข้ามการกลับรายการ (เคลียร์มือ)", "SYSTEM");
                        return $"VOIDED:{nexaaccId}";
                    }

                    if (!string.IsNullOrEmpty(receiptNumber))
                    {
                        decimal applied = LookupDepositAppliedFromReceipt(receiptNumber);
                        if (applied > 0)
                        {
                            if (voidedDocType == NexaaccDocumentType.TaxInvoice)
                            {
                                int resIdV = LookupReceiptHeaderInfo(receiptNumber)?.reservationId ?? 0;
                                if (_config.IsDepositVatAtReceipt && !_config.IsDepositOutputVatDeferred)
                                {
                                    // โหมด §78/1 เคร่ง: ใบกำกับออกเฉพาะยอดคงเหลือ + มี journal รับรู้รายได้มัดจำ
                                    // (Dr 21712/Cr รายได้) แยกจาก doc → void doc ไม่ cascade → กลับตามบัญชีจริง
                                    // ที่โพสต์ไป (แม่นแม้ revenueType ต่าง) + idempotent
                                    await ReverseDepositRevenueRecognitionAsync(resIdV, receiptNumber);
                                }
                                else if (LookupBusinessHasVat() && _config.IsDepositVatAtReceipt)
                                {
                                    // โหมดเต็มยอด: payment ตัดมัดจำถูก cascade-reverse โดย void doc แล้ว (Cr ADVANCE คืน)
                                    // → กลับเฉพาะ journal แก้ VAT มัดจำ (Dr ADVANCE / Cr 21913-21911)
                                    var vatRev = _mapper.MapDepositVatCorrection(resIdV,
                                        applied, receiptNumber, _config.IsDepositOutputVatDeferred, reverse: true);
                                    var vatRevResult = await _apiClient.CreateJournalAsync(vatRev);
                                    Guid vatRevId = RequireValidDocId(vatRevResult?.data?.Id, $"VoidReceipt vat-correction-rev receipt={receiptNumber}");
                                    await SafePostJournalAsync(vatRevId);
                                    _code.Logs(_connectionString, "AccountingSync",
                                        $"ProcessVoidReceipt(TAXINV doc): reversed deposit VAT correction applied={applied} receipt={receiptNumber}", "SYSTEM");
                                }
                            }
                            else
                            {
                                var info = LookupReceiptHeaderInfo(receiptNumber);
                                if (info != null)
                                {
                                    // GUARD double-reverse: กลับ JV หักมัดจำแยก "ก็ต่อเมื่อ JV นั้นมีอยู่จริง".
                                    // display-only mode → มี JV (ref "{receipt}-DEPADJ") → กลับ. โหมดขับ JE
                                    // (spec §9.1) → ไม่มี JV (การหักมัดจำอยู่ใน JE ของเอกสาร) → void doc
                                    // cascade กลับให้เอง → ข้าม. เช็คของจริงแทน config → ครอบคลุมช่วง transition
                                    // (เอกสารเก่าสร้างแบบ display-only แล้ว void หลังสลับ flag ก็ยังกลับ JV เดิมถูก)
                                    string depadjRef = !string.IsNullOrEmpty(receiptNumber)
                                        ? $"{receiptNumber}-DEPADJ" : $"RES-{info.Value.reservationId}-DEPADJ";
                                    if (await JournalExistsByReferenceAsync(depadjRef))
                                    {
                                        var counterAdj = _mapper.MapDepositAppliedReceiptAdjustmentReverse(
                                            info.Value.reservationId, applied, info.Value.paymentMethod, DateTime.Now,
                                            info.Value.customerName ?? "", info.Value.paymentAccountId, receiptNumber,
                                            hasVat: LookupBusinessHasVat(), vatAtReceipt: _config.IsDepositVatAtReceipt,
                                            deferOutputVat: _config.IsDepositOutputVatDeferred);
                                        var counterResult = await _apiClient.CreateJournalAsync(counterAdj);
                                        Guid revId = RequireValidDocId(counterResult?.data?.Id, $"VoidReceipt(RECEIPT doc) counter-adj receipt={receiptNumber}");
                                        await SafePostJournalAsync(revId);
                                        _code.Logs(_connectionString, "AccountingSync",
                                            $"ProcessVoidReceipt(RECEIPT doc): reversed deposit adj applied={applied} receipt={receiptNumber} journalId={revId}", "SYSTEM");
                                    }
                                    else
                                    {
                                        _code.Logs(_connectionString, "AccountingSync",
                                            $"ProcessVoidReceipt(RECEIPT doc): ไม่มี JV หักมัดจำแยก ({depadjRef}) — โหมดขับ JE/ไม่มีมัดจำ → ข้ามการกลับ (void doc cascade JE เอง) receipt={receiptNumber}", "SYSTEM");
                                    }
                                }
                            }
                        }
                    }

                    SetReceiptPaymentMarker(receiptNumber, "VOIDED");
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ProcessVoidReceipt(RECEIPT doc): voided receipt={receiptNumber} docId={docId}", "SYSTEM");
                    return $"VOIDED:{nexaaccId}";
                }
                else if (_config.IsReceiptDocumentMode)
                {
                    // int_ fallback: integration invoice path — ลองใช้ /api/integration/documents/void ก่อน
                    // ยกเลิกเอกสาร + journal ในคำสั่งเดียว
                    // fallback → credit note ถ้า void endpoint ยังไม่พร้อม (404)
                    try
                    {
                        var voidReq = new InboundVoidDocumentRequest
                        {
                            DocumentId = docId,
                            ExternalRef = receiptNumber,
                            Reason = reason ?? $"ยกเลิกใบเสร็จ {receiptNumber}"
                        };
                        var voidResult = await _apiClient.VoidDocumentViaIntegrationAsync(voidReq);

                        // ถ้าใบเสร็จเดิมมี deposit applied → ต้อง restore ADVANCE_DEPOSIT
                        if (!string.IsNullOrEmpty(receiptNumber))
                        {
                            decimal applied = LookupDepositAppliedFromReceipt(receiptNumber);
                            if (applied > 0)
                            {
                                var info = LookupReceiptHeaderInfo(receiptNumber);
                                if (info != null)
                                {
                                    int resId = info.Value.reservationId;
                                    string custName = info.Value.customerName ?? "";
                                    var counterAdj = _mapper.MapDepositAppliedAdjustmentReverse(
                                        resId, applied, info.Value.paymentMethod, DateTime.Now,
                                        custName, info.Value.paymentAccountId, receiptNumber,
                                        hasVat: LookupBusinessHasVat(), vatAtReceipt: _config.IsDepositVatAtReceipt);
                                    var counterResult = await _apiClient.CreateJournalAsync(counterAdj);
                                    RequireValidDocId(counterResult?.data?.Id, $"VoidReceipt counter-adj receipt={receiptNumber}");
                                    _code.Logs(_connectionString, "AccountingSync",
                                        $"ProcessVoidReceipt: counter-adj for depositApplied={applied} on receipt={receiptNumber}",
                                        "SYSTEM");
                                }
                            }
                        }

                        // integration void cascade ได้ void payment ที่บันทึกรับชำระให้แล้ว — แค่ mark marker
                        await VoidRecordedReceiptPaymentAsync(receiptNumber, alreadyCascaded: true);

                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(DOCUMENT): voided via integration endpoint receipt={receiptNumber} nexaaccId={nexaaccId}",
                            "SYSTEM");
                        return $"VOIDED:{nexaaccId}";
                    }
                    catch (AccountingApiException voidEx) when (voidEx.StatusCode == 404)
                    {
                        // Void endpoint ยังไม่มี — fallback เป็น credit note
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(DOCUMENT): void endpoint returned 404, falling back to credit note for {receiptNumber}",
                            "SYSTEM");

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

                                decimal applied = LookupDepositAppliedFromReceipt(receiptNumber);
                                if (applied > 0 && info != null)
                                {
                                    var counterAdj = _mapper.MapDepositAppliedAdjustmentReverse(
                                        resId, applied, info.Value.paymentMethod, DateTime.Now,
                                        custName, info.Value.paymentAccountId, receiptNumber,
                                        hasVat: LookupBusinessHasVat(), vatAtReceipt: _config.IsDepositVatAtReceipt);
                                    var counterResult = await _apiClient.CreateJournalAsync(counterAdj);
                                    RequireValidDocId(counterResult?.data?.Id, $"VoidReceipt counter-adj receipt={receiptNumber}");
                                    _code.Logs(_connectionString, "AccountingSync",
                                        $"ProcessVoidReceipt: counter-adj for depositApplied={applied} on receipt={receiptNumber}",
                                        "SYSTEM");
                                }

                                // credit-note fallback ไม่ cascade payment → ต้อง void payment ที่บันทึกรับชำระเอง
                                await VoidRecordedReceiptPaymentAsync(receiptNumber, alreadyCascaded: false);

                                return $"CREDIT_NOTE:{cnId}";
                            }
                        }

                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidReceipt(DOCUMENT): missing receiptNumber/total for nexaaccId={nexaaccId} — manual review required",
                            "SYSTEM");
                        return $"VOID_SKIPPED:{nexaaccId} (manual review needed)";
                    }
                }
                else
                {
                    // JOURNAL mode: ใบเสร็จเป็น compound journal ที่รวม deposit-debit อยู่แล้ว
                    // → reverse เพียงพอ (NextAcc auto-flip DR↔CR ทุก line รวมทั้ง Advance Deposit)
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
                    // DOCUMENT mode: ใช้ /api/integration/documents/void (Integration Key)
                    // ยกเลิกเอกสาร + journal ที่เกี่ยวข้องในคำสั่งเดียว
                    // fallback → debit note ถ้า void endpoint ไม่รองรับ
                    try
                    {
                        var voidReq = new InboundVoidDocumentRequest
                        {
                            DocumentId = docId,
                            ExternalRef = documentNumber,
                            Reason = reason ?? $"ยกเลิกใบสำคัญจ่าย {documentNumber}"
                        };
                        var voidResult = await _apiClient.VoidDocumentViaIntegrationAsync(voidReq);
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher(DOCUMENT): voided via integration endpoint doc={documentNumber} nexaaccId={nexaaccId}",
                            "SYSTEM");
                        return $"VOIDED:{nexaaccId}";
                    }
                    catch (AccountingApiException voidEx) when (voidEx.StatusCode == 404)
                    {
                        // Void endpoint ยังไม่มีบน NextAcc version นี้ — fallback เป็น debit note
                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher(DOCUMENT): void endpoint returned 404, falling back to debit note for {documentNumber}",
                            "SYSTEM");

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

                        _code.Logs(_connectionString, "AccountingSync",
                            $"ProcessVoidVoucher(DOCUMENT): missing documentNumber/total for nexaaccId={nexaaccId} — manual review",
                            "SYSTEM");
                        return $"VOID_SKIPPED:{nexaaccId} (DOCUMENT mode — manual review needed)";
                    }
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
            // WHT cert endpoint ({company}/withholding-tax-certs/*) เรียกผ่าน X-Api-Key
            // (int_/acc_) ได้ — ข้ามเฉพาะเมื่อ company endpoint ปิด (ไม่มี CompanyId / flag=0)
            if (!_config.CanUseCompanyEndpoints)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"WHT cert auto-generate ข้าม doc={documentNumber}: company endpoint ปิดอยู่ " +
                    "(ตั้ง CompanyId + Nexaacc_Company_Endpoints=1 เพื่อใช้งาน)", "SYSTEM");
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
            DateTime checkoutDate = ParseAcctDate(p["checkoutDate"]?.ToString());
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

            // ── SAFEGUARD: ตรวจผ่าน Booking ID ว่ามัดจำถูกบันทึกเป็นหนี้สิน (เงินรับล่วงหน้า) บน NextAcc จริง ──
            // กันเคส "TakeTime มีมัดจำ แต่ใบมัดจำไม่เคยขึ้น/ไม่อนุมัติบน NextAcc" → ถ้า Dr ADVANCE_DEPOSIT
            // ทั้งที่ไม่มี Cr ตั้งไว้ บัญชีเงินรับล่วงหน้าจะติดลบ. ตัดได้ไม่เกินยอดที่ booked จริง.
            var depChk = VerifyDepositBookedOnNextAcc(reservationId);
            if (depChk.PendingSync)
            {
                // ใบมัดจำยังรอ sync/รออนุมัติบน NextAcc → ยังไม่ตัด ให้ queue retry (FIFO จะสร้าง/อนุมัติก่อน)
                throw new Exception(
                    $"ProcessDepositClearing: มัดจำของ Booking #{reservationId} ยังรอ sync/อนุมัติบน NextAcc — เลื่อนไปตัดรอบถัดไป");
            }
            if (depChk.AnyDeposit && depChk.BookedAmount + 0.01m < depositAmount)
            {
                // ส่วนที่ยังไม่ booked บน NextAcc → ไม่ตัดส่วนนั้น (กันติดลบ). แจ้งเลขใบที่ค้าง sync ให้เห็น.
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositClearing: Booking #{reservationId} ตัดเฉพาะมัดจำที่ booked บน NextAcc {depChk.BookedAmount:N2} " +
                    $"(ขอตัด {depositAmount:N2}); ใบมัดจำที่ยังไม่ขึ้น NextAcc: [{string.Join(", ", depChk.UnsyncedReceipts)}] — โปรด re-sync ใบเหล่านี้",
                    "SYSTEM");
                depositAmount = depChk.BookedAmount;
            }
            if (depositAmount <= 0.01m)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositClearing: SKIPPED — Booking #{reservationId} ยังไม่พบมัดจำที่บันทึกบน NextAcc " +
                    $"(กัน ADVANCE_DEPOSIT ติดลบ). ใบมัดจำที่ต้อง re-sync: [{string.Join(", ", depChk.UnsyncedReceipts)}]", "SYSTEM");
                return "SKIPPED_DEPOSIT_NOT_ON_NEXTACC";
            }

            bool hasVat = LookupBusinessHasVat();

            _code.Logs(_connectionString, "AccountingSync",
                $"ProcessDepositClearing: ref={reservationRef} resId={reservationId} deposit={depositAmount} damage={damageAmount} vat={hasVat}", "SYSTEM");

            var journal = _mapper.MapCheckoutToJournal(reservationId, depositAmount, customerName, checkoutDate, damageAmount, reservationRef, hasVat, _config.IsDepositVatAtReceipt, _config.IsDepositOutputVatDeferred);
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
            DateTime refundDate = ParseAcctDate(p["refundDate"]?.ToString());

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
            DateTime forfeitDate = ParseAcctDate(p["forfeitDate"]?.ToString());
            string reason = p.ContainsKey("reason") ? p["reason"]?.ToString() : null;

            if (forfeitAmount <= 0)
                throw new ArgumentException($"ProcessDepositForfeit: forfeitAmount ต้อง > 0 (ได้ {forfeitAmount}) reservation #{reservationId}");

            // RE-COMPUTE ณ เวลา process (mirror ProcessDepositClearing): ริบได้ไม่เกิน
            // "มัดจำคงค้าง" = มัดจำที่จ่ายจริง − ส่วนที่ถูกหักในใบเสร็จไปแล้ว (Deposit_Applied_Amount)
            // ไม่งั้นเคสมัดจำ 1,070 ใช้ไป 500 แล้วยกเลิกไม่คืนเงิน จะ Dr 21712 ซ้ำ 500 (over-clear)
            decimal fActualDeposit = LookupActualDepositPaid(reservationId);
            decimal fAlreadyApplied = LookupDepositAppliedForReservation(reservationId);
            decimal outstanding = fActualDeposit - fAlreadyApplied;
            if (forfeitAmount > outstanding)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositForfeit: payload={forfeitAmount} แต่มัดจำคงค้าง={outstanding} " +
                    $"(จ่ายจริง {fActualDeposit} − หักในใบเสร็จแล้ว {fAlreadyApplied}) — ใช้ค่าคงค้าง", "SYSTEM");
                forfeitAmount = outstanding;
            }
            if (forfeitAmount <= 0.01m)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ProcessDepositForfeit: ไม่มีมัดจำคงค้างให้ริบ — skip reservation #{reservationId}", "SYSTEM");
                return "SKIPPED_NO_BALANCE";
            }

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
            DateTime receiveDate = ParseAcctDate(p["receiveDate"]?.ToString());
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
            DateTime outDate = ParseAcctDate(p["outDate"]?.ToString());
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
            DateTime reverseDate = ParseAcctDate(p["reverseDate"]?.ToString());
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
            DateTime adjustDate = ParseAcctDate(p["adjustDate"]?.ToString());
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
            DateTime writeOffDate = ParseAcctDate(p["writeOffDate"]?.ToString());
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

        /// <summary>ผลตรวจว่ามัดจำของ Booking ถูกบันทึกเป็นหนี้สินบน NextAcc แล้วหรือยัง</summary>
        private struct DepositBookedState
        {
            public bool AnyDeposit;                  // มีใบมัดจำใน TakeTime ไหม
            public decimal BookedAmount;             // ยอดมัดจำที่ sync + อนุมัติบน NextAcc แล้ว
            public bool PendingSync;                 // ยังมีใบมัดจำรอ sync/รออนุมัติ → ควร retry
            public System.Collections.Generic.List<string> UnsyncedReceipts;  // ใบที่ sync ไม่สำเร็จ/ไม่เคยเข้าคิว
        }

        /// <summary>
        /// SAFEGUARD ตรวจผ่าน Booking ID (Reservation_ID): มัดจำถูกบันทึกเป็นหนี้สิน (เงินรับล่วงหน้า)
        /// บน NextAcc จริงแค่ไหน — อ่านจาก marker Account_Receipt.Nexaacc_Receipt_Payment_Id
        ///   APR:/ADJ:/GUID สุดท้าย = อนุมัติ+โพสต์แล้ว (booked) | DOC: = สร้างแล้วรออนุมัติ (pending)
        ///   null + ยังมีคิวค้าง = pending | null + ไม่มีคิว = ยัง sync ไม่สำเร็จ (unsynced) | VOIDED = ข้าม
        /// </summary>
        private DepositBookedState VerifyDepositBookedOnNextAcc(int reservationId)
        {
            var state = new DepositBookedState { UnsyncedReceipts = new System.Collections.Generic.List<string>() };
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT ID, ISNULL(Total_Amount, 0) AS Amt, Nexaacc_Receipt_Payment_Id AS Marker
                      FROM Account_Receipt
                      WHERE Reservation_ID = @rid AND IsDeposit = 1 AND (Status = 'Normal' OR Status IS NULL)",
                    new Dictionary<string, object> { { "@rid", reservationId } });
                if (dt != null)
                {
                    foreach (System.Data.DataRow r in dt.Rows)
                    {
                        state.AnyDeposit = true;
                        string num = r["ID"]?.ToString();
                        decimal amt = r["Amt"] != DBNull.Value ? Convert.ToDecimal(r["Amt"]) : 0m;
                        string marker = r["Marker"] == DBNull.Value ? null : r["Marker"]?.ToString();

                        if (marker == "VOIDED")
                        {
                            // ยกเลิกแล้ว — ไม่มีหนี้สินบน NextAcc
                        }
                        else if (!string.IsNullOrEmpty(marker)
                                 && (marker.StartsWith("APR:") || marker.StartsWith("ADJ:") || marker == "NOCASH"
                                     || (!marker.StartsWith("DOC:") && Guid.TryParse(marker, out _))))
                        {
                            // ใบมัดจำ "รุ่นเก่า" ที่ sync เป็น integration invoice: NextAcc (int_) ยุบเข้า
                            // บัญชีรายได้ทันที — ไม่มีหนี้สิน 21712 ให้ตัด แม้ marker จะสำเร็จ
                            // → ต้อง resync เป็น "ใบเสร็จมัดจำ" ก่อน (เฉพาะ deployment ที่มี company endpoint
                            // ซึ่งใบใหม่ตั้ง 21712 จริง; ร้าน int_ ล้วนคงพฤติกรรมเดิม)
                            if (_config.CanUseCompanyEndpoints && LookupReceiptQueueDocType(num) == "INVOICE")
                                state.UnsyncedReceipts.Add(num + " (ใบกำกับแบบเก่า — กด Retry ให้เป็นใบเสร็จมัดจำ)");
                            else
                                state.BookedAmount += amt;    // อนุมัติ/โพสต์บน NextAcc แล้ว (NOCASH = settle ครบด้วยมัดจำ)
                        }
                        else if (!string.IsNullOrEmpty(marker) && marker.StartsWith("DOC:"))
                        {
                            state.PendingSync = true;     // สร้างแล้วรออนุมัติ
                        }
                        else
                        {
                            // marker ว่าง → ยังมีคิว sync ค้างไหม
                            if (HasActiveReceiptQueue(num)) state.PendingSync = true;
                            // ไม่มีคิวค้าง แต่เคย sync สำเร็จ (คิว COMPLETED) = booked
                            // ครอบคลุมใบเก่าก่อนมี marker และโหมด journal รุ่นก่อน fix
                            else if (HasCompletedReceiptQueue(num)) state.BookedAmount += amt;
                            else state.UnsyncedReceipts.Add(num);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"VerifyDepositBookedOnNextAcc: Booking #{reservationId} {ex.Message}", "SYSTEM");
            }
            return state;
        }

        /// <summary>ยังมี queue สร้างใบเสร็จ (RECEIPT) ที่รอ/กำลังทำ สำหรับเลขนี้อยู่ไหม (retry ยังไม่หมด)</summary>
        private bool HasActiveReceiptQueue(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'RECEIPT' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT'
                        AND Status IN ('PENDING', 'PROCESSING') AND Retry_Count < Max_Retries
                        AND Payload LIKE @p",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%" } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
        }

        /// <summary>ชนิดเอกสาร NextAcc ของใบเสร็จนี้จากคิวล่าสุดที่ COMPLETED ("RECEIPT"/"INVOICE"/"JOURNAL"/null)
        /// — ใช้แยกใบมัดจำรุ่นเก่า (INVOICE = int_ ยุบเป็นรายได้ ไม่มี 21712) ออกจากใบเสร็จมัดจำจริง</summary>
        private string LookupReceiptQueueDocType(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 Nexaacc_Document_Type FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'RECEIPT' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT'
                        AND Status = 'COMPLETED'
                        AND Payload LIKE @p
                      ORDER BY ID DESC",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%" } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0][0] != DBNull.Value)
                    return dt.Rows[0][0].ToString();
            }
            catch { }
            return null;
        }

        /// <summary>เคย sync ใบเสร็จนี้สำเร็จ (คิว COMPLETED) ไหม — ใช้เป็น booked-fallback เมื่อไม่มี marker</summary>
        private bool HasCompletedReceiptQueue(string receiptNumber)
        {
            if (string.IsNullOrEmpty(receiptNumber)) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'RECEIPT' AND Action_Type = 'CREATE_RECEIPT_DOCUMENT'
                        AND Status = 'COMPLETED'
                        AND Payload LIKE @p",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%" } });
                return dt != null && dt.Rows.Count > 0;
            }
            catch { return false; }
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

            // E-Tax endpoint ({company}/etax/*) เรียกผ่าน X-Api-Key (int_/acc_) ได้
            // ข้ามเฉพาะเมื่อ company endpoint ปิด (ไม่มี CompanyId / flag=0)
            if (!_config.CanUseCompanyEndpoints)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"E-Tax auto-generate ข้าม receipt={receiptNumber}: company endpoint ปิดอยู่ " +
                    "(ตั้ง CompanyId + Nexaacc_Company_Endpoints=1 เพื่อใช้งาน) หรือสร้าง E-Tax เองในหน้าจัดการ", "SYSTEM");
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
        private async Task<ContactInfo> EnsureCustomerContactAsync(int reservationId, bool forceRefresh = false)
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

            // Try cache — ข้ามเมื่อ forceRefresh (เช่นก่อนออกใบกำกับภาษี §86/4: ผู้ใช้เพิ่งเติม
            // เลขผู้เสียภาษี/ที่อยู่ในระบบ ต้อง push ไปที่ contact บน NextAcc ทันที ไม่รอ cache 30 วัน)
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
                    if (!forceRefresh
                        && cached.Rows[0]["Nexaacc_Contact_Id"] != DBNull.Value
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

        /// <summary>ผู้ซื้อมีข้อมูลใบกำกับเต็มรูป §86/4 ครบไหม (เลขภาษี 13 หลัก + ที่อยู่) —
        /// ครบ → ออกใบกำกับเต็มรูปผูก contact จริง / ไม่ครบ → เคส "ไม่ประสงค์รับใบกำกับ"
        /// (BuyerDeclinedTaxInvoice → contact กลางลูกค้าเงินสด, VAT ลง ภ.พ.30 ครบ)</summary>
        private static bool HasFullBuyerTaxData(ContactInfo c)
        {
            return c != null
                && !string.IsNullOrWhiteSpace(c.TaxId)
                && System.Text.RegularExpressions.Regex.IsMatch(c.TaxId.Trim(), @"^\d{13}$")
                && !string.IsNullOrWhiteSpace(c.Address);
        }

        /// <summary>ตั้งค่า invoice เป็นเคส "ลูกค้าไม่ประสงค์รับใบกำกับภาษี" ตามสัญญา NextAcc:
        /// flag + ล้างข้อมูลลูกค้าทั้ง 3 field (NextAcc ผูก contact กลางให้เอง)</summary>
        private static void MarkBuyerDeclinedTaxInvoice(CreateIntegrationInvoiceRequest invoice)
        {
            if (invoice == null) return;
            invoice.BuyerDeclinedTaxInvoice = true;
            invoice.CustomerName = null;
            invoice.CustomerTaxId = null;
            invoice.CustomerExternalId = null;
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

        private static void ApplyContactToPaymentVoucher(CreateIntegrationPaymentVoucherRequest voucher, ContactInfo info)
        {
            if (voucher == null || info == null) return;
            voucher.SupplierExternalId = info.ExternalId;
            if (string.IsNullOrEmpty(voucher.SupplierName)) voucher.SupplierName = info.Name;
            if (!string.IsNullOrEmpty(info.TaxId)) voucher.SupplierTaxId = info.TaxId;
        }

        /// <summary>
        /// แนบลายเซ็น + ชื่อ "ผู้จัดทำ" ลงใน request ที่จะส่งไป NextAcc.
        /// ดึงผู้จัดทำจาก Account_Payment.Created_By_ID ของใบสำคัญจ่าย → Admin (ชื่อ + SignaturePath)
        /// → อ่านไฟล์รูปลายเซ็นเป็น base64 data URI. ไม่ throw ถ้าไม่มีลายเซ็น (เป็น optional)
        /// </summary>
        // NextAcc cap ลายเซ็น (PreparerSignatureMaxBytes) — เกินกว่านี้ NextAcc throw → เอกสารไม่ถูกสร้าง.
        // จึง skip + log แทนที่จะส่งไปให้ล้มทั้งใบ (แนะนำบีบรูปลายเซ็น < 100KB)
        private const int SignatureMaxBytes = 256 * 1024;

        private void ApplyPreparerSignature(CreateIntegrationExpenseRequest expense, string documentNumber)
        {
            if (expense == null || string.IsNullOrEmpty(documentNumber)) return;
            var info = LookupPreparerInfo(documentNumber);
            if (info == null) return;
            if (!string.IsNullOrEmpty(info.Value.name)) expense.PreparerName = info.Value.name;
            if (!string.IsNullOrEmpty(info.Value.dataUri))
            {
                if (info.Value.dataUri.Length <= SignatureMaxBytes)
                    expense.PreparerSignatureBase64 = info.Value.dataUri;
                else
                    _code.Logs(_connectionString, "AccountingSync",
                        $"ApplyPreparerSignature(expense): doc={documentNumber} ลายเซ็น {info.Value.dataUri.Length} bytes > {SignatureMaxBytes} — ข้าม (บีบรูปให้เล็กลง)", "SYSTEM");
            }
        }

        private void ApplyPreparerSignature(CreateIntegrationPaymentVoucherRequest voucher, string documentNumber)
        {
            if (voucher == null || string.IsNullOrEmpty(documentNumber)) return;
            var info = LookupPreparerInfo(documentNumber);
            if (info == null) return;
            if (!string.IsNullOrEmpty(info.Value.name))
            {
                voucher.PreparerName = info.Value.name;
                voucher.PayerSignatureName = info.Value.name;   // slot 0 "ผู้จ่ายเงิน"
            }
            if (!string.IsNullOrEmpty(info.Value.dataUri) && info.Value.dataUri.Length <= SignatureMaxBytes)
            {
                voucher.PreparerSignatureBase64 = info.Value.dataUri;
                voucher.PayerSignatureBase64 = info.Value.dataUri;
            }
        }

        private (string name, string dataUri)? LookupPreparerInfo(string documentNumber)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT a.ID AS AdminId, a.FirstName, a.LastName, a.SignaturePath
                      FROM Account_Payment ap
                      LEFT JOIN Admin a ON ap.Created_By_ID = a.ID
                      WHERE ap.ID = @ID",
                    new Dictionary<string, object> { { "@ID", documentNumber } });
                if (dt == null || dt.Rows.Count == 0 || dt.Rows[0]["AdminId"] == DBNull.Value) return null;

                var row = dt.Rows[0];
                string first = row["FirstName"] == DBNull.Value ? "" : row["FirstName"].ToString();
                string last = row["LastName"] == DBNull.Value ? "" : row["LastName"].ToString();
                string name = (first + " " + last).Trim();

                short adminId = Convert.ToInt16(row["AdminId"]);
                string dataUri = LoadSignatureDataUri(adminId);

                _code.Logs(_connectionString, "AccountingSync",
                    $"ApplyPreparerSignature: doc={documentNumber} preparer='{name}' signature={(dataUri != null ? "แนบแล้ว" : "ไม่พบไฟล์ลายเซ็น")}", "SYSTEM");
                return (name, dataUri);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"ApplyPreparerSignature: doc={documentNumber} ล้มเหลว: {ex.Message}", "SYSTEM");
                return null;
            }
        }

        /// <summary>อ่านไฟล์ลายเซ็นของ admin → base64 data URI ("data:image/png;base64,..."). คืน null ถ้าไม่มี.</summary>
        private string LoadSignatureDataUri(short adminId)
        {
            try
            {
                var sigService = new SignatureService();
                string virtualPath = sigService.GetSignaturePath(adminId);   // เช่น ~/Documents/Staff/Signature/sig_1_xxx.png
                if (string.IsNullOrEmpty(virtualPath)) return null;

                string physical = System.Web.Hosting.HostingEnvironment.MapPath(virtualPath);
                if (string.IsNullOrEmpty(physical) || !File.Exists(physical)) return null;

                byte[] bytes = File.ReadAllBytes(physical);
                if (bytes.Length == 0) return null;

                string ext = Path.GetExtension(physical).ToLowerInvariant();
                string mime = ext == ".jpg" || ext == ".jpeg" ? "image/jpeg" : "image/png";
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch { return null; }
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

        /// <summary>
        /// Look up the Vendor address from an ExternalId of the form "VENDOR-{id}".
        /// Used by the Account_Payment voucher flow (voucherId == 0) where the vendor is
        /// referenced only by its external id, so the contact upsert can include the address
        /// instead of creating a name-only contact in NextAcc.
        /// </summary>
        private string LookupVendorAddressByExternalId(string externalId)
        {
            if (string.IsNullOrEmpty(externalId) || !externalId.StartsWith("VENDOR-")) return null;
            string vendorId = externalId.Substring("VENDOR-".Length);
            if (string.IsNullOrEmpty(vendorId)) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 Address FROM Vendor WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", vendorId } });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["Address"] != DBNull.Value)
                {
                    string addr = dt.Rows[0]["Address"].ToString();
                    return string.IsNullOrWhiteSpace(addr) ? null : addr;
                }
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"LookupVendorAddressByExternalId failed for {externalId}: {ex.Message}", "SYSTEM");
            }
            return null;
        }

        /// <summary>ผูกผู้ขายจากระบบ TakeTime (Vendor) เป็น contact ใน NextAcc แล้วคืน ContactId —
        /// ใช้เมื่อ OCR ไม่เจอชื่อผู้ขาย ผู้ใช้เลือกผู้ขายจากระบบเอง (set ContactId ของเอกสาร)</summary>
        public async System.Threading.Tasks.Task<Guid?> EnsureVendorContactAsync(int vendorId)
        {
            if (vendorId <= 0) return null;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT TOP 1 ID, Name, IDNumber, Address FROM Vendor WHERE ID = @id",
                    new Dictionary<string, object> { { "@id", vendorId } });
                if (dt == null || dt.Rows.Count == 0) return null;
                var row = dt.Rows[0];
                var info = await EnsureSupplierContactAsync(new ContactInfo
                {
                    ExternalId = "VENDOR-" + row["ID"],
                    Name = row["Name"]?.ToString(),
                    TaxId = row["IDNumber"]?.ToString(),
                    Address = row["Address"]?.ToString()
                });
                return info?.NexaaccContactId;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync", $"EnsureVendorContactAsync({vendorId}) failed: {ex.Message}", "SYSTEM");
                return null;
            }
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
                    // นิติบุคคล (taxId 13 หลักขึ้นต้น 0) → JuristicPerson เพื่อให้ NextAcc หัก ภ.ง.ด.53
                    // / บุคคลธรรมดา → Individual (ภ.ง.ด.3). อย่าใช้ความยาว 13 หลักตัดสิน (เท่ากันทั้งคู่)
                    ContactType = AccountingDataMapper.IsJuristicPerson(info.TaxId) ? "JuristicPerson" : "Individual"
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
            // 404 = เอกสาร/รายการถูกลบ/ไม่พบแล้ว, 409 = conflict (มักอยู่สถานะ terminal แล้ว)
            // → การ void/reverse ซ้ำถือว่าสำเร็จแบบ idempotent (ไม่ใช่ error ที่ต้อง retry วนไป)
            if (ex.StatusCode == 404 || ex.StatusCode == 409) return true;
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

        /// <summary>
        /// มี void entry ของเอกสารเดียวกันที่ enqueue หลัง queue entry ที่ระบุหรือไม่ (ทุกสถานะ).
        /// ใช้แยก "edit flow (void → สร้างใหม่เลขเดิม)" ออกจาก "submit ซ้ำ" ใน anti-duplicate check —
        /// ถ้ามี void ที่ใหม่กว่า แปลว่าเอกสารบน NextAcc ถูก/กำลังถูกยกเลิก ต้องปล่อยให้สร้างใหม่
        /// </summary>
        private bool HasNewerVoidEntry(string entityType, string voidActionType, string payloadKey, string payloadValue, long afterQueueId)
        {
            if (string.IsNullOrEmpty(payloadValue)) return false;
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT TOP 1 ID FROM Accounting_Sync_Queue
                      WHERE Entity_Type = @entityType AND Action_Type = @actionType
                        AND ID > @afterId
                        AND Payload LIKE @pattern",
                    new Dictionary<string, object>
                    {
                        { "@entityType", entityType },
                        { "@actionType", voidActionType },
                        { "@afterId", afterQueueId },
                        { "@pattern", $"%\"{payloadKey}\":\"{payloadValue}\"%"}
                    });
                return dt?.Rows.Count > 0;
            }
            catch { return false; }
        }

        private long InsertQueue(string entityType, int entityId, string actionType, Dictionary<string, object> payload)
        {
            // Capture operator user from HttpContext for X-Acting-User attribution on NextAcc
            if (!payload.ContainsKey("operatorUser"))
            {
                try
                {
                    var ctx = System.Web.HttpContext.Current;
                    if (ctx?.Session != null)
                    {
                        string user = ctx.Session["User"]?.ToString();
                        if (!string.IsNullOrEmpty(user))
                            payload["operatorUser"] = user;
                    }
                }
                catch { /* background thread — no HttpContext */ }
            }

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
        /// Re-post ใบเสร็จที่ COMPLETED แล้วด้วย "หลักการบันทึกบัญชีปัจจุบัน" ใน click เดียว:
        /// NextAcc ไม่อนุญาตแก้เอกสาร/JE ที่โพสต์แล้ว (แก้ได้เฉพาะ Draft) → การ "แก้ JE" ที่ทำได้จริง
        /// คือ void ของเก่า + สร้างใหม่เลขเดิม — เมธอดนี้ทำให้ครบอัตโนมัติ ผู้ใช้ไม่ต้องไป void เอง:
        ///   1) enqueue VOID_RECEIPT ของเอกสารเก่า (ทำก่อนเสมอ — ไม่ข้ามแม้ DOCUMENT mode
        ///      เพราะ Receipt doc ผ่าน company /document ไม่มี ReplaceExistingForSource)
        ///   2) mark คิวเก่า SUPERSEDED + ล้าง PDF cache (ผ่าน PrepareResync)
        ///   3) reset marker Account_Receipt.Nexaacc_Receipt_Payment_Id → CREATE ใหม่ไม่ถูกบล็อก
        ///   4) enqueue CREATE ใหม่จาก payload เดิม → processor ยิง JE ตามโค้ด/หลักการล่าสุด
        /// FIFO ของคิวการันตี VOID ประมวลก่อน CREATE. คืน queueId ของ CREATE ใหม่ (-1 = ไม่สำเร็จ)
        /// </summary>
        /// <summary>ข้อความผลลัพธ์ล่าสุดจาก RepostReceiptWithCurrentLogic (in-place/reversal/เหตุผล guard) — ให้หน้า UI แสดง</summary>
        public string LastRepostMessage { get; private set; }

        public long RepostReceiptWithCurrentLogic(long queueId)
        {
            LastRepostMessage = null;
            var dt = _code.DatabaseQuerySafe(_connectionString,
                @"SELECT ID, Entity_Type, Action_Type, Status, Payload, Nexaacc_Response_Id, Nexaacc_Document_Type
                  FROM Accounting_Sync_Queue WHERE ID = @id",
                new Dictionary<string, object> { { "@id", queueId } });
            if (dt == null || dt.Rows.Count == 0) return -1;

            var row = dt.Rows[0];
            if ((row["Action_Type"]?.ToString()) != "CREATE_RECEIPT_DOCUMENT") return -1;

            Dictionary<string, object> p;
            try { p = _serializer.Deserialize<Dictionary<string, object>>(row["Payload"]?.ToString() ?? "{}"); }
            catch { return -1; }
            string receiptNumber = p.ContainsKey("receiptNumber") ? p["receiptNumber"]?.ToString() : null;
            if (string.IsNullOrEmpty(receiptNumber)) return -1;

            string docType = row.Table.Columns.Contains("Nexaacc_Document_Type")
                ? row["Nexaacc_Document_Type"]?.ToString() ?? "" : "";

            // ── ทางที่ 0: Official resync contract (INTEGRATION_RESYNC.md) — เอกสารเดิมเป็น
            // integration invoice → ส่ง /integration/invoices ซ้ำด้วย externalRef เดิม + resyncUpdate:true
            // NextAcc จัดการเอง: งวดเปิด+JE เดียว = in-place (เลข JE คงเดิม) / งวดปิด = reversal+post ใหม่
            // เลขเอกสารคงเดิมเสมอ ไม่มี void. guard (ชำระแล้ว/มี CN-DN/ภ.พ.30 ยื่นแล้ว) → success:false
            if (docType == "INVOICE")
            {
                try
                {
                    var invoice = BuildCorrectedReceiptInvoice(p, receiptNumber);
                    if (invoice != null)
                    {
                        ApiResponse<IntegrationDocumentResponse> resp = null;
                        string guardMsg = null;
                        try
                        {
                            resp = System.Threading.Tasks.Task.Run(() => _apiClient.CreateInvoiceAsync(invoice))
                                .GetAwaiter().GetResult();
                        }
                        catch (AccountingApiException apiEx)
                        {
                            // guard อาจตอบเป็น HTTP error — เอาข้อความจริงจาก NextAcc มาแสดง
                            guardMsg = string.IsNullOrEmpty(apiEx.ResponseBody) ? apiEx.Message : apiEx.ResponseBody;
                        }

                        string msg = resp?.message ?? "";
                        if (resp != null && resp.success
                            && msg.StartsWith("Resync updated", StringComparison.OrdinalIgnoreCase))
                        {
                            bool inPlace = msg.IndexOf("(in-place)", StringComparison.OrdinalIgnoreCase) >= 0;
                            LastRepostMessage = inPlace
                                ? "✅ แก้ JE เดิม (in-place) — เลข JE/เลขเอกสารคงเดิม"
                                : "✅ ปรับด้วย reversal (งวดเดิมปิด/มีหลาย JE) — เลขเอกสารคงเดิม";
                            _code.DatabaseInsertSafe(_connectionString,
                                "UPDATE Accounting_Sync_Queue SET Error_Message = @m WHERE ID = @id",
                                new Dictionary<string, object> { { "@m", "Resync: " + msg }, { "@id", queueId } });
                            ClearReceiptPdfCache(receiptNumber);
                            _code.Logs(_connectionString, "AccountingSync",
                                $"RepostReceipt: resyncUpdate สำเร็จ receipt={receiptNumber} → {msg}", "SYSTEM");
                            return 0;
                        }
                        if (resp != null && !resp.success)
                        {
                            // ติด guard — NextAcc บอกทางแก้ในข้อความ (เช่น ชำระแล้ว → ต้อง void/CN)
                            LastRepostMessage = "NextAcc ปฏิเสธการแก้: " + (resp.message ?? "ไม่ทราบสาเหตุ");
                            _code.Logs(_connectionString, "AccountingSync",
                                $"RepostReceipt: resyncUpdate ถูก guard ปฏิเสธ receipt={receiptNumber}: {resp.message}", "SYSTEM");
                            return -1;
                        }
                        if (guardMsg != null)
                        {
                            LastRepostMessage = "NextAcc ปฏิเสธการแก้: " + guardMsg;
                            _code.Logs(_connectionString, "AccountingSync",
                                $"RepostReceipt: resyncUpdate error receipt={receiptNumber}: {guardMsg}", "SYSTEM");
                            return -1;
                        }
                        // success + "Already synced" = NextAcc รุ่นเก่ายังไม่รู้จัก resyncUpdate → fallback void→สร้างใหม่
                        _code.Logs(_connectionString, "AccountingSync",
                            $"RepostReceipt: NextAcc ตอบ '{msg}' (รุ่นเก่า/ไม่เข้าเงื่อนไข resync) receipt={receiptNumber} → fallback void→สร้างใหม่", "SYSTEM");
                    }
                }
                catch (Exception rex)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"RepostReceipt: resyncUpdate ล้มเหลว receipt={receiptNumber} ({rex.Message}) → fallback", "SYSTEM");
                }
            }

            // ── ทางที่ 1: แก้ JE เดิม in-place (ไม่ void) — เฉพาะเอกสารเดิมแบบ JOURNAL เท่านั้น ──
            // journal-mode receipt = JE เดี่ยว self-contained → แทนที่ Lines ทั้งชุดได้ปลอดภัย.
            // RECEIPT/INVOICE มี JE คู่หู (adjustment หักมัดจำ / integration payment) ที่จะรอดจากการ
            // แทนที่ → ยอดเบิล — พวกนั้นใช้ resyncUpdate (INVOICE ด้านบน) หรือ fallback void→สร้างใหม่
            if (docType == "JOURNAL")
            try
            {
                bool inPlaceOk = System.Threading.Tasks.Task.Run(async () =>
                {
                    var corrected = BuildCorrectedReceiptJournal(p);
                    if (corrected == null || corrected.Lines == null || corrected.Lines.Count < 2) return false;

                    var found = await _apiClient.SearchJournalsAsync(receiptNumber, 10);
                    var je = found?.data?.Items?.FirstOrDefault(j =>
                        string.Equals(j.Reference, receiptNumber, StringComparison.OrdinalIgnoreCase)
                        && j.Status != 2 /* Voided */ && j.OriginalEntryId == null);
                    if (je == null) return false;

                    var upd = await _apiClient.UpdateJournalEntryAsync(je.Id, new UpdateJournalEntryRequest
                    {
                        EntryDate = corrected.EntryDate,
                        Description = corrected.Description,
                        Reference = corrected.Reference,
                        Lines = corrected.Lines
                    });
                    if (upd?.success != true) return false;

                    _code.Logs(_connectionString, "AccountingSync",
                        $"RepostReceipt: แก้ JE {je.Id} ({je.EntryNumber}) in-place สำเร็จ receipt={receiptNumber} (ไม่ void)", "SYSTEM");
                    return true;
                }).GetAwaiter().GetResult();

                if (inPlaceOk)
                {
                    // สำเร็จแบบไม่ void: คงคิวเดิม COMPLETED + จดบันทึก, ล้าง PDF cache ให้ดึงยอดใหม่
                    LastRepostMessage = "✅ แก้ JE เดิม (in-place) — เลข JE คงเดิม";
                    _code.DatabaseInsertSafe(_connectionString,
                        "UPDATE Accounting_Sync_Queue SET Error_Message = N'JE updated in-place (no void)' WHERE ID = @id",
                        new Dictionary<string, object> { { "@id", queueId } });
                    ClearReceiptPdfCache(receiptNumber);
                    return 0;   // 0 = แก้ in-place แล้ว ไม่มีคิวใหม่
                }
            }
            catch (Exception ipEx)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"RepostReceipt: in-place update ไม่สำเร็จ receipt={receiptNumber} ({ipEx.Message}) → fallback void→สร้างใหม่", "SYSTEM");
            }

            // ── ทางที่ 2 (fallback): void เอกสารเก่า + สร้างใหม่เลขเดิม ──
            // เก็บ id เอกสารเก่าก่อน (PrepareResync จะ mark แถว SUPERSEDED)
            string oldNexaaccId = row["Nexaacc_Response_Id"]?.ToString();
            if (string.IsNullOrEmpty(oldNexaaccId) || !Guid.TryParse(oldNexaaccId, out _))
            {
                // fallback: marker บน Account_Receipt (เฟส final เป็น GUID เอกสาร)
                string mk = LookupReceiptPaymentMarker(receiptNumber);
                if (!string.IsNullOrEmpty(mk) && Guid.TryParse(mk, out var mg) && mg != Guid.Empty)
                    oldNexaaccId = mk;
            }

            // 1) void เอกสารเก่า (ถ้ามี) — ProcessVoidReceipt กลืน already-gone เอง + กลับรายการหักมัดจำให้
            if (!string.IsNullOrEmpty(oldNexaaccId) && Guid.TryParse(oldNexaaccId, out _))
            {
                // สำคัญ: ProcessVoidReceipt อ่าน "receiptNumber" — ถ้าส่งแต่ documentNumber
                // void จะวิ่งแบบ null → ข้ามกลับรายการหักมัดจำ → CREATE ใหม่โพสต์ซ้ำ (เบิล)
                InsertQueue("RECEIPT", 0, "VOID_RECEIPT", new Dictionary<string, object>
                {
                    { "receiptNumber", receiptNumber },
                    { "documentNumber", receiptNumber },
                    { "nexaaccId", oldNexaaccId },
                    { "reason", "Re-post ตามหลักการบัญชีปัจจุบัน" }
                });
            }

            // 2) เคลียร์คิวเก่า + ล้าง PDF cache (PrepareResync ในโหมด journal จะ enqueue void ซ้ำ —
            //    ไม่เป็นไร ProcessVoidReceipt idempotent/กลืนเอกสารที่ void แล้ว)
            PrepareResync(receiptNumber);
            ClearReceiptPdfCache(receiptNumber);   // PrepareResync ล้างเฉพาะฝั่งจ่าย — ล้างฝั่งรับด้วย

            // 3) reset marker → SettleReceiptDocAsync/SettleReceiptInNextAcc เริ่ม state machine ใหม่
            SetReceiptPaymentMarker(receiptNumber, null);

            // 4) สร้าง CREATE ใหม่จาก payload เดิม (ตัวเลขต้นทางเดิม — processor ตีความตามหลักการใหม่)
            return InsertQueue("RECEIPT",
                p.ContainsKey("reservationId") ? Convert.ToInt32(p["reservationId"]) : 0,
                "CREATE_RECEIPT_DOCUMENT", p);
        }

        /// <summary>ล้าง PDF cache ฝั่ง TakeTime ของเอกสาร → ครั้งต่อไป re-download ยอดใหม่จาก NextAcc
        /// (ทั้งโฟลเดอร์ฝั่งจ่าย PaymentFolderPath และฝั่งรับ ReceiptFolderPath)</summary>
        private void ClearReceiptPdfCache(string receiptNumber)
        {
            foreach (string key in new[] { "PaymentFolderPath", "ReceiptFolderPath" })
            {
                try
                {
                    string basePath = ConfigurationManager.AppSettings[key];
                    if (!string.IsNullOrEmpty(basePath))
                    {
                        string naFolder = Path.Combine(basePath, "NextAcc", MakeSafeFileName(receiptNumber));
                        if (Directory.Exists(naFolder)) Directory.Delete(naFolder, true);
                    }
                }
                catch { }
            }
        }

        /// <summary>
        /// สร้าง integration invoice ที่ "ถูกต้องตามหลักการปัจจุบัน" จาก payload คิวเดิม สำหรับ
        /// official resync (INTEGRATION_RESYNC.md): mapper ชุดเดียวกับ ProcessReceiptDocument
        /// (int_ invoice branch) + ExternalRef = เลขใบเสร็จ (คีย์ dedup ของ NextAcc) + ResyncUpdate=true
        /// </summary>
        private CreateIntegrationInvoiceRequest BuildCorrectedReceiptInvoice(Dictionary<string, object> p, string receiptNumber)
        {
            try
            {
                int reservationId = p.ContainsKey("reservationId") ? Convert.ToInt32(p["reservationId"]) : 0;
                decimal totalAmount = p.ContainsKey("totalAmount") ? Convert.ToDecimal(p["totalAmount"]) : 0m;
                DateTime receiptDate = ParseAcctDate(p.ContainsKey("receiptDate") ? p["receiptDate"]?.ToString() : null);
                string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() ?? "" : "";
                bool isDeposit = p.ContainsKey("isDeposit") && Convert.ToBoolean(p["isDeposit"]);
                string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() ?? "CASH" : "CASH";
                string revenueType = p.ContainsKey("revenueType") ? p["revenueType"]?.ToString() : null;
                string paymentAccountId = p.ContainsKey("paymentAccountId") ? p["paymentAccountId"]?.ToString() : null;
                if (totalAmount <= 0) return null;

                bool hasVat = LookupBusinessHasVat();
                CreateIntegrationInvoiceRequest invoice;

                if (isDeposit)
                {
                    invoice = _mapper.MapDepositToInvoice(reservationId, totalAmount, paymentMethod, receiptDate,
                        customerName, paymentAccountId: paymentAccountId, hasVat: hasVat,
                        vatAtReceipt: _config.IsDepositVatAtReceipt);
                }
                else
                {
                    decimal depositApplied = p.ContainsKey("depositApplied") ? Convert.ToDecimal(p["depositApplied"]) : 0m;
                    if (depositApplied <= 0)
                        depositApplied = LookupDepositAppliedFromReceipt(receiptNumber);

                    decimal depositFromLines;
                    var lines = LookupReceiptLinesEx(receiptNumber, reservationId, totalAmount, revenueType, out depositFromLines);
                    if (depositFromLines > 0)
                    {
                        // กันบวกซ้ำ (Deposit_Applied_Amount ที่ persist ไว้รวม lines แล้ว)
                        if (depositApplied < depositFromLines)
                            depositApplied += depositFromLines;
                        totalAmount += depositFromLines;   // คืน GROSS (fix มัดจำหักซ้ำ)
                    }

                    bool useMultiLine = lines != null && (lines.Count > 1 || depositApplied > 0);
                    if (useMultiLine)
                        invoice = _mapper.MapMultiLinePaymentToInvoice(reservationId, lines, paymentMethod, receiptDate,
                            customerName, hasVat, paymentAccountId, depositApplied, receiptNumber);
                    else
                        invoice = _mapper.MapPaymentToInvoice(reservationId, totalAmount, paymentMethod, receiptDate,
                            customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId);
                }

                // Reference = รหัสการจอง (นโยบายเดียวกับ sync ปกติ); externalRef = เลขใบเสร็จ (คีย์ dedup)
                invoice.Reference = reservationId > 0 ? $"RES-{reservationId}" : receiptNumber;
                invoice.ExternalRef = receiptNumber;      // 🔑 คีย์ dedup ที่ NextAcc ใช้หาเอกสารเดิม
                invoice.ExternalId = receiptNumber;
                invoice.ReplaceExistingForSource = false; // ใช้กลไก resyncUpdate แทน (ไม่ void)
                invoice.ResyncUpdate = true;

                // นโยบายผู้ซื้อเดียวกับ sync ปกติ: ข้อมูลภาษีครบ → ใบเต็มรูป / ไม่ครบ → ไม่ประสงค์รับใบกำกับ
                var repostContact = LookupCustomerFromReservation(reservationId);
                if (HasFullBuyerTaxData(repostContact))
                {
                    invoice.CustomerExternalId = repostContact.ExternalId;
                    invoice.CustomerTaxId = repostContact.TaxId;
                    if (string.IsNullOrEmpty(invoice.CustomerName)) invoice.CustomerName = repostContact.Name;
                }
                else
                {
                    MarkBuyerDeclinedTaxInvoice(invoice);
                }
                return invoice;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"BuildCorrectedReceiptInvoice error: {ex.Message}", "SYSTEM");
                return null;
            }
        }

        /// <summary>
        /// สร้าง JE ที่ "ถูกต้องตามหลักการปัจจุบัน" ของใบเสร็จ จาก payload คิวเดิม —
        /// ใช้ mapper ชุดเดียวกับ ProcessReceiptDocument (journal branch) เพื่อให้ GL เหมือน
        /// การ sync ใหม่ทุกประการ: มัดจำ → MapDepositToJournal; รับเงิน → MapMultiLine/MapPayment
        /// (รวม fix มัดจำ NET→GROSS). คืน null ถ้าข้อมูลไม่พอ
        /// </summary>
        private CreateJournalEntryRequest BuildCorrectedReceiptJournal(Dictionary<string, object> p)
        {
            try
            {
                int reservationId = p.ContainsKey("reservationId") ? Convert.ToInt32(p["reservationId"]) : 0;
                string receiptNumber = p.ContainsKey("receiptNumber") ? p["receiptNumber"]?.ToString() : null;
                decimal totalAmount = p.ContainsKey("totalAmount") ? Convert.ToDecimal(p["totalAmount"]) : 0m;
                DateTime receiptDate = ParseAcctDate(p.ContainsKey("receiptDate") ? p["receiptDate"]?.ToString() : null);
                string customerName = p.ContainsKey("customerName") ? p["customerName"]?.ToString() ?? "" : "";
                bool isDeposit = p.ContainsKey("isDeposit") && Convert.ToBoolean(p["isDeposit"]);
                string paymentMethod = p.ContainsKey("paymentMethod") ? p["paymentMethod"]?.ToString() ?? "CASH" : "CASH";
                string revenueType = p.ContainsKey("revenueType") ? p["revenueType"]?.ToString() : null;
                string paymentAccountId = p.ContainsKey("paymentAccountId") ? p["paymentAccountId"]?.ToString() : null;
                if (string.IsNullOrEmpty(receiptNumber) || totalAmount <= 0) return null;

                bool hasVat = LookupBusinessHasVat();

                if (isDeposit)
                {
                    return _mapper.MapDepositToJournal(reservationId, totalAmount, paymentMethod, receiptDate,
                        customerName, paymentAccountId: paymentAccountId, documentNumber: receiptNumber,
                        hasVat: hasVat, vatAtReceipt: _config.IsDepositVatAtReceipt,
                        deferOutputVat: _config.IsDepositOutputVatDeferred);
                }

                decimal depositApplied = p.ContainsKey("depositApplied") ? Convert.ToDecimal(p["depositApplied"]) : 0m;
                if (depositApplied <= 0)
                    depositApplied = LookupDepositAppliedFromReceipt(receiptNumber);

                decimal depositFromLines;
                var lines = LookupReceiptLinesEx(receiptNumber, reservationId, totalAmount, revenueType, out depositFromLines);
                if (depositFromLines > 0)
                {
                    // กันบวกซ้ำ (Deposit_Applied_Amount ที่ persist ไว้รวม lines แล้ว)
                    if (depositApplied < depositFromLines)
                        depositApplied += depositFromLines;
                    totalAmount += depositFromLines;   // คืน GROSS (fix มัดจำหักซ้ำ)
                }

                bool useMultiLine = lines != null && (lines.Count > 1 || depositApplied > 0);
                if (useMultiLine)
                {
                    return _mapper.MapMultiLinePaymentToJournal(reservationId, lines, paymentMethod, receiptDate,
                        customerName, hasVat, paymentAccountId, depositApplied, receiptNumber,
                        vatAtReceipt: _config.IsDepositVatAtReceipt,
                        deferOutputVat: _config.IsDepositOutputVatDeferred);
                }
                return _mapper.MapPaymentToJournal(reservationId, totalAmount, paymentMethod, receiptDate,
                    customerName, hasVat, revenueType: revenueType, paymentAccountId: paymentAccountId,
                    documentNumber: receiptNumber);
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"BuildCorrectedReceiptJournal error: {ex.Message}", "SYSTEM");
                return null;
            }
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
                        // ProcessVoidReceipt อ่าน "receiptNumber" (ProcessVoidVoucher อ่าน "documentNumber")
                        // → ใส่ทั้งคู่ ไม่งั้น void ฝั่ง RECEIPT วิ่งแบบ null ข้ามกลับรายการหักมัดจำ
                        var voidPayload = new Dictionary<string, object>
                        {
                            { "documentNumber", documentNumber },
                            { "receiptNumber", documentNumber },
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

            // ล้าง PDF cache บนดิสก์ของเอกสารนี้ (NextAcc cache + markers _att.done/_nopdf) →
            // ครั้งต่อไปจะ re-download ยอดใหม่จาก NextAcc ไม่ค้างยอดเก่าหลัง edit/re-sync
            try
            {
                string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
                if (!string.IsNullOrEmpty(basePath))
                {
                    string naFolder = Path.Combine(basePath, "NextAcc", MakeSafeFileName(documentNumber));
                    if (Directory.Exists(naFolder)) Directory.Delete(naFolder, true);
                }
            }
            catch { }

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

        /// <summary>true ถ้า PDF cache บนดิสก์ "เก่ากว่า" การ sync ล่าสุดของเอกสาร (ถูกแก้/re-sync หลัง cache)
        /// → ควรดึงใหม่. ปกติ PDF ถูกโหลดหลัง sync เสร็จ (ไฟล์ใหม่กว่า) จึงคืน false (ใช้ cache, เร็ว)</summary>
        private bool IsVoucherPdfCacheStale(string documentNumber, string pdfPath)
        {
            try
            {
                DateTime pdfTime = File.GetLastWriteTime(pdfPath);
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT MAX(ISNULL(Processed_Date, Created_Date)) AS LastSync
                      FROM Accounting_Sync_Queue
                      WHERE (Action_Type = 'CREATE_VOUCHER_JOURNAL' OR Action_Type = 'CREATE_RECEIPT_DOCUMENT')
                        AND Status IN ('COMPLETED','SUPERSEDED')
                        AND (Payload LIKE @p1 OR Payload LIKE @p2)",
                    new Dictionary<string, object>
                    {
                        { "@p1", $"%\"documentNumber\":\"{documentNumber}\"%" },
                        { "@p2", $"%\"receiptNumber\":\"{documentNumber}\"%" }
                    });
                if (dt?.Rows.Count > 0 && dt.Rows[0]["LastSync"] != DBNull.Value)
                {
                    DateTime lastSync = Convert.ToDateTime(dt.Rows[0]["LastSync"]);
                    return lastSync > pdfTime.AddSeconds(1);   // เผื่อ clock skew 1 วิ
                }
            }
            catch { }
            return false;   // หาไม่ได้ → ถือว่าไม่ stale (ใช้ cache, ไม่ download มั่ว)
        }

        private static void WritePdfAmtMarker(string amtMarker, decimal total)
        {
            try { File.WriteAllText(amtMarker, total.ToString("0.0000")); } catch { }
        }

        /// <summary>true ก็ต่อเมื่อ "มี baseline ยอด (marker) + ตรงกับยอด NextAcc ปัจจุบัน" → cache ใช้ได้.
        /// ไม่มี marker (ไฟล์เก่า/สร้างจาก OCR ที่ไม่ผ่านคิว) หรือยอดต่าง → false → ดึง PDF ใหม่ 1 รอบ
        /// (ตั้ง/อัปเดต baseline) → self-heal ทุกใบไม่ว่ามาจากไหน (แก้ในระบบเรา/OCR/แก้ตรงบน NextAcc)</summary>
        private static bool AmtMarkerFresh(string amtMarker, decimal currentTotal)
        {
            try
            {
                if (!File.Exists(amtMarker)) return false;
                if (decimal.TryParse(File.ReadAllText(amtMarker).Trim(), out var stored))
                    return Math.Abs(stored - currentTotal) <= 0.005m;
            }
            catch { }
            return false;
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

            // ── Fast path: ถ้า PDF cache อยู่แล้วบนดิสก์ + ยังใหม่ → คืนทันที ไม่ต้อง query DB/ยิง API ──
            // (ทำให้การค้นหาซ้ำช่วงเดิมเร็วมาก — ดึงจริงเฉพาะครั้งแรก/หลังเอกสารถูกแก้)
            {
                string safeEarly = MakeSafeFileName(voucherDocNumber);
                string suffixEarly = isCancelled ? "_Cancel" : "";
                string folderEarly = Path.Combine(basePath, "NextAcc", safeEarly);
                string pdfEarly = Path.Combine(folderEarly, safeEarly + suffixEarly + ".pdf");
                string relEarly = "/Documents/Payment/NextAcc/" + safeEarly;
                // ใช้ cache ก็ต่อเมื่อ "ยังใหม่" (ไม่มีการ re-sync เอกสารหลังเวลาที่ cache ไฟล์)
                // → ปกติเร็ว (ไม่ยิง API); ดึงใหม่เฉพาะตอนเอกสารถูกแก้/re-sync เท่านั้น (กันค้างยอดเก่า)
                if (!forceRefresh && File.Exists(pdfEarly) && new FileInfo(pdfEarly).Length > 0
                    && !IsVoucherPdfCacheStale(voucherDocNumber, pdfEarly))
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

        /// <summary>
        /// ดึง PDF + ไฟล์แนบของ "เอกสารที่สร้างบน NextAcc โดยตรง" (NextAcc-only) ตาม NextAcc document id (GUID)
        /// มาเก็บฝั่ง TakeTime แล้วคืน relative url ของไฟล์ local — ใช้ตอนกด "ดู PDF" ในตาราง เพื่อเปิดไฟล์
        /// จริงแทนการเด้งไปหน้า NextAcc. เอกสารพวกนี้ไม่มี entry ใน sync queue จึงหา docId ผ่าน queue ไม่ได้
        /// (ต่างจาก DownloadVoucherDocumentFromNextAccAsync) — รับ GUID ที่ได้จากรายการเอกสารโดยตรง.
        /// smart-cache: ถ้ามีไฟล์อยู่แล้ว + ไม่ force → คืนเลย (ไม่ยิง API ซ้ำ ไม่ช้า).
        /// </summary>
        public async System.Threading.Tasks.Task<NextAccCachedDocument> DownloadNextAccDocumentByIdAsync(
            Guid nextAccId, string docNumber, bool forceRefresh = false)
        {
            var result = new NextAccCachedDocument();
            if (nextAccId == Guid.Empty) { result.Message = "ไม่มี NextAcc document id"; return result; }
            if (!_config.IsConfigured || !_config.Enabled) { result.Message = "ยังไม่ได้ตั้งค่า NextAcc"; return result; }

            string basePath = ConfigurationManager.AppSettings["PaymentFolderPath"];
            if (string.IsNullOrEmpty(basePath)) { result.Message = "ไม่ได้ตั้งค่า PaymentFolderPath"; return result; }

            string safeDoc = MakeSafeFileName(string.IsNullOrEmpty(docNumber) ? nextAccId.ToString() : docNumber);
            string folder = Path.Combine(basePath, "NextAcc", safeDoc);
            string pdfPath = Path.Combine(folder, safeDoc + ".pdf");
            string relPrefix = "/Documents/Payment/NextAcc/" + safeDoc;

            // fast path: มี cache อยู่แล้ว
            if (!forceRefresh && File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0)
            {
                result.Found = true;
                result.PdfLocalPath = pdfPath;
                result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                try
                {
                    foreach (var f in Directory.GetFiles(folder, "att*"))
                    {
                        string fn = Path.GetFileName(f);
                        if (fn.StartsWith("att_Cancel")) continue;
                        result.AttachmentCount++;
                        result.AttachmentRelativeUrls.Add(relPrefix + "/" + fn);
                    }
                }
                catch { }
                return result;
            }

            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // 1) PDF อย่างเป็นทางการจาก NextAcc (ตาม document id)
                if (forceRefresh || !File.Exists(pdfPath) || new FileInfo(pdfPath).Length == 0)
                {
                    byte[] pdf = await _apiClient.GenerateDocumentPdfAsync(nextAccId);
                    if (pdf != null && pdf.Length > 0)
                        File.WriteAllBytes(pdfPath, pdf);
                }
                if (File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                }
                else
                {
                    result.Message = "NextAcc ไม่มี PDF/template สำหรับเอกสารนี้";
                }

                // 2) ไฟล์แนบ
                try
                {
                    var attResp = await _apiClient.GetAttachmentsAsync("Document", nextAccId);
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
                            string attName = $"att{idx}{ext}";
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
                        $"DownloadNextAccDocumentById: attachments doc={docNumber} ล้มเหลว: {exAtt.Message}", "SYSTEM");
                }

                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadNextAccDocumentById: doc={docNumber} id={nextAccId} pdf={result.Found} att={result.AttachmentCount}", "SYSTEM");
            }
            catch (AuthenticationFailedException exAuth)
            {
                result.Message = "NextAcc auth ล้มเหลว: " + exAuth.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadNextAccDocumentById: doc={docNumber} {exAuth.Message}", "SYSTEM");
            }
            catch (Exception ex)
            {
                result.Message = "ดาวน์โหลดเอกสารล้มเหลว: " + ex.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadNextAccDocumentById: doc={docNumber} {ex.Message}", "SYSTEM");
            }

            return result;
        }

        /// <summary>
        /// ดึง PDF เอกสารฝั่งรับ (ใบเสร็จ/ใบกำกับที่ sync เป็นเอกสาร NextAcc) มา cache ฝั่ง TakeTime
        /// แล้วคืน relative url — ใช้กับปุ่ม "ดู PDF" หน้า CheckDocument ให้เปิดเอกสารจริงจาก NextAcc
        /// แทน PDF ที่ระบบเรา render เอง (mirror วิธีของฝั่งจ่าย DownloadVoucherDocumentFromNextAccAsync)
        /// smart-cache: ใช้ไฟล์เดิมถ้ายังใหม่กว่า sync ล่าสุดของใบนี้ — ดึงใหม่เฉพาะหลัง edit/re-sync
        /// </summary>
        public async System.Threading.Tasks.Task<NextAccCachedDocument> DownloadReceiptPdfFromNextAccAsync(
            string receiptNumber, bool isCancelled = false)
        {
            var result = new NextAccCachedDocument();
            if (string.IsNullOrEmpty(receiptNumber)) { result.Message = "ไม่มีเลขที่ใบเสร็จ"; return result; }
            if (!_config.IsConfigured || !_config.Enabled) { result.Message = "ยังไม่ได้ตั้งค่า NextAcc"; return result; }

            string basePath = ConfigurationManager.AppSettings["ReceiptFolderPath"];
            if (string.IsNullOrEmpty(basePath)) { result.Message = "ไม่ได้ตั้งค่า ReceiptFolderPath"; return result; }

            string safeDoc = MakeSafeFileName(receiptNumber);
            string suffix = isCancelled ? "_Cancel" : "";
            string folder = Path.Combine(basePath, "NextAcc", safeDoc);
            string pdfPath = Path.Combine(folder, safeDoc + suffix + ".pdf");
            string relPrefix = "/Documents/Receipt/NextAcc/" + safeDoc;

            // fast path: cache ยังใหม่ (ไฟล์ใหม่กว่ารายการ sync ล่าสุดของใบนี้)
            if (File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0
                && !IsReceiptPdfCacheStale(receiptNumber, pdfPath))
            {
                result.Found = true;
                result.PdfLocalPath = pdfPath;
                result.PdfRelativeUrl = relPrefix + "/" + safeDoc + suffix + ".pdf";
                return result;
            }

            Guid docId = LookupNexaaccDocIdByReceipt(receiptNumber);
            if (docId == Guid.Empty) { result.Message = "ใบเสร็จนี้ยังไม่ได้ sync เป็นเอกสาร NextAcc"; return result; }

            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                byte[] pdf = await _apiClient.GenerateDocumentPdfAsync(docId,
                    watermark: isCancelled ? "ยกเลิก" : null);
                if (pdf != null && pdf.Length > 0)
                {
                    File.WriteAllBytes(pdfPath, pdf);
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + suffix + ".pdf";
                }
                else
                {
                    result.Message = "NextAcc ไม่มี PDF/template สำหรับเอกสารนี้";
                }
            }
            catch (Exception ex)
            {
                result.Message = "ดาวน์โหลดเอกสารล้มเหลว: " + ex.Message;
                _code.Logs(_connectionString, "AccountingSync",
                    $"DownloadReceiptPdf: receipt={receiptNumber} {ex.Message}", "SYSTEM");
            }
            return result;
        }

        /// <summary>cache PDF ใบเสร็จเก่ากว่ารายการ sync (CREATE/VOID) ล่าสุดของใบนี้ไหม → stale = ดึงใหม่</summary>
        private bool IsReceiptPdfCacheStale(string receiptNumber, string pdfPath)
        {
            try
            {
                var dt = _code.DatabaseQuerySafe(_connectionString,
                    @"SELECT MAX(Processed_Date) AS LastDone FROM Accounting_Sync_Queue
                      WHERE Entity_Type = 'RECEIPT' AND Status = 'COMPLETED'
                        AND Payload LIKE @p",
                    new Dictionary<string, object> { { "@p", "%\"receiptNumber\":\"" + receiptNumber + "\"%" } });
                if (dt != null && dt.Rows.Count > 0 && dt.Rows[0]["LastDone"] != DBNull.Value)
                {
                    DateTime lastDone = Convert.ToDateTime(dt.Rows[0]["LastDone"]);
                    return File.GetLastWriteTime(pdfPath) < lastDone;
                }
            }
            catch { }
            return false;   // ไม่รู้ → ใช้ cache (เร็ว); PrepareResync/repost ล้างโฟลเดอร์ให้อยู่แล้ว
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
            DateTime fromDate, DateTime toDate, bool includeAttachments = true, bool cacheFiles = true)
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

                        // cacheFiles=false → แสดงผลเร็ว (metadata + อ่านไฟล์จากดิสก์ที่เคย cache ไว้ ไม่ยิง API ต่อเอกสาร)
                        // cacheFiles=true  → โหลด PDF/ไฟล์แนบ/WHT จาก NextAcc มาเก็บ (ใช้รันเบื้องหลัง)
                        var cached = cacheFiles
                            ? await CacheNextAccDocumentAsync(d, basePath, baseUrl, includeAttachments)
                            : BuildDiskOnlyCachedDoc(d, basePath);
                        list.Add(cached);
                    }

                    if (resp.Items.Count < 50 || page >= resp.TotalPages) break;
                    page++;
                }
            }

            _code.Logs(_connectionString, "AccountingSync",
                $"DownloadVoucherDocumentsForRange: {fromDate:yyyy-MM-dd}..{toDate:yyyy-MM-dd} พบ {list.Count} เอกสาร (cacheFiles={cacheFiles})", "SYSTEM");
            return list;
        }

        /// <summary>
        /// สร้าง NextAccCachedDocument จาก metadata + ไฟล์ที่ cache ไว้บนดิสก์เท่านั้น (ไม่ยิง API ต่อเอกสาร)
        /// ใช้สำหรับแสดงผลเร็ว — เอกสารที่สร้างบน NextAcc โดยตรงจะโผล่ในตารางทันทีพร้อมลิงก์เปิดดู
        /// </summary>
        private NextAccCachedDocument BuildDiskOnlyCachedDoc(OutboundDocumentResponse d, string basePath)
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
            try
            {
                string safeDoc = MakeSafeFileName(!string.IsNullOrEmpty(d.Reference) ? d.Reference : d.DocumentNumber);
                string folder = Path.Combine(basePath, "NextAcc", safeDoc);
                string relPrefix = "/Documents/Payment/NextAcc/" + safeDoc;

                string pdfPath = Path.Combine(folder, safeDoc + ".pdf");
                if (File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                }

                string whtPath = Path.Combine(folder, "wht.pdf");
                if (File.Exists(whtPath) && new FileInfo(whtPath).Length > 0)
                    result.WhtCertPdfRelativeUrl = relPrefix + "/wht.pdf";

                if (Directory.Exists(folder))
                    GlobCachedAttachments(folder, relPrefix, result);
            }
            catch { }
            return result;
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
            string amtMarker = Path.Combine(folder, "_amt.marker");   // ยอดรวม ณ ตอน cache (ตรวจยอดเปลี่ยน)
            string whtPath = Path.Combine(folder, "wht.pdf");
            string noWhtMarker = Path.Combine(folder, "_nowht.marker");
            string relPrefix = "/Documents/Payment/NextAcc/" + safeDoc;

            // re-check ไฟล์แนบ/WHT ใหม่ ถ้า marker เก่ากว่า TTL — เพื่อให้ไฟล์ที่เพิ่งเพิ่มบน NextAcc แสดงได้
            TimeSpan recheckTtl = TimeSpan.FromMinutes(10);

            try
            {
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                // ── 1) PDF เอกสารหลัก (ถ้า NextAcc มี template) ──
                // ใช้ไฟล์ cache เฉพาะที่ "ยังใหม่" — ถ้าเอกสารถูกแก้/re-sync หลัง cache → ถือว่าไม่ cached
                // เพื่อ re-download ทับยอดใหม่ (กันลิงก์ "เอกสาร NextAcc" ค้างยอดเก่า เช่น 630 ทั้งที่แก้เป็น 530 แล้ว)
                string ttDocRef = !string.IsNullOrEmpty(d.Reference) ? d.Reference : d.DocumentNumber;
                // stale ถ้า: (ก) re-sync ใน TakeTime หลัง cache  หรือ  (ข) ยอดรวมจาก NextAcc ปัจจุบัน ≠ ที่ cache ไว้
                // (ข) ครอบคลุมการแก้ตรงบน NextAcc ด้วย (ไม่มี record ในคิวฝั่งเรา)
                bool pdfCached = File.Exists(pdfPath) && new FileInfo(pdfPath).Length > 0
                                 && !IsVoucherPdfCacheStale(ttDocRef, pdfPath)
                                 && AmtMarkerFresh(amtMarker, d.TotalAmount);
                if (pdfCached)
                {
                    result.Found = true;
                    result.PdfLocalPath = pdfPath;
                    result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                }
                else if (!File.Exists(noPdfMarker))
                {
                    byte[] pdf = await _apiClient.GenerateDocumentPdfAsync(d.Id);
                    if (pdf != null && pdf.Length > 0)
                    {
                        File.WriteAllBytes(pdfPath, pdf);
                        WritePdfAmtMarker(amtMarker, d.TotalAmount);   // บันทึกยอด baseline ไว้ตรวจรอบหน้า
                        result.Found = true;
                        result.PdfLocalPath = pdfPath;
                        result.PdfRelativeUrl = relPrefix + "/" + safeDoc + ".pdf";
                    }
                    else
                    {
                        try { File.WriteAllText(noPdfMarker, DateTime.Now.ToString("o")); } catch { }
                    }
                }

                // ── 2) ไฟล์แนบ — ดึงรายการใหม่เมื่อ marker หาย/เก่ากว่า TTL (ไฟล์ที่มีบนดิสก์แล้วไม่โหลดซ้ำ) ──
                if (includeAttachments && IsMarkerStale(attDoneMarker, recheckTtl))
                {
                    bool definitive = await FetchAndCacheNextAccAttachmentsAsync(d, folder, baseUrl);
                    // เขียน marker เฉพาะเมื่อได้ผลแน่ชัด (โหลดได้/ยืนยันว่าไม่มีไฟล์) — ถ้าล้มเหลวชั่วคราวจะลองใหม่รอบหน้า
                    if (definitive) { try { File.WriteAllText(attDoneMarker, DateTime.Now.ToString("o")); } catch { } }
                }
                // เก็บ URL ไฟล์แนบที่มีบนดิสก์ (ทั้งที่โหลดใหม่และที่ cache ไว้แล้ว)
                GlobCachedAttachments(folder, relPrefix, result);

                // ── 3) ใบหัก ณ ที่จ่าย (50 ทวิ) — ลองดึงเสมอ (int_ key ใช้กับ company endpoint ได้ผ่าน fallback) ──
                bool whtCached = File.Exists(whtPath) && new FileInfo(whtPath).Length > 0;
                if (whtCached)
                {
                    result.WhtCertPdfRelativeUrl = relPrefix + "/wht.pdf";
                }
                else if (IsMarkerStale(noWhtMarker, recheckTtl))
                {
                    try
                    {
                        var certs = await _apiClient.GetWhtCertsByDocumentAsync(d.Id);
                        var certItems = certs?.data?.Items;
                        // กรองฝั่ง client ด้วย DocumentId เสมอ เผื่อ endpoint คืนทั้งหมด (ไม่กรองตาม query param)
                        var cert = certItems?.FirstOrDefault(c => c != null && c.Id != Guid.Empty && c.DocumentId == d.Id)
                                   ?? certItems?.FirstOrDefault(c => c != null && c.Id != Guid.Empty);
                        if (cert != null)
                        {
                            byte[] whtPdf = await _apiClient.GetWhtCertPdfAsync(cert.Id);
                            if (whtPdf == null || whtPdf.Length == 0)
                                whtPdf = await _apiClient.GenerateDocumentPdfAsync(cert.Id); // fallback ผ่าน template engine
                            if (whtPdf != null && whtPdf.Length > 0)
                            {
                                File.WriteAllBytes(whtPath, whtPdf);
                                result.WhtCertPdfRelativeUrl = relPrefix + "/wht.pdf";
                                _code.Logs(_connectionString, "AccountingSync",
                                    $"CacheNextAccDocument: WHT cert doc={d.DocumentNumber} certId={cert.Id} แนบแล้ว", "SYSTEM");
                            }
                        }
                        else
                        {
                            try { File.WriteAllText(noWhtMarker, DateTime.Now.ToString("o")); } catch { }
                        }
                    }
                    catch (Exception exWht)
                    {
                        // 401/403/404 = ไม่มี WHT / endpoint ใช้ไม่ได้ → ทำ marker กันลองถี่ๆ
                        try { File.WriteAllText(noWhtMarker, DateTime.Now.ToString("o")); } catch { }
                        _code.Logs(_connectionString, "AccountingSync",
                            $"CacheNextAccDocument: WHT cert doc={d.DocumentNumber} ({d.Reference}) ล้มเหลว: {exWht.Message}", "SYSTEM");
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

        /// <summary>true ถ้า marker ไม่มี หรือเก่ากว่า ttl — ใช้ตัดสินใจว่าควร re-fetch จาก NextAcc ใหม่หรือไม่</summary>
        private static bool IsMarkerStale(string markerPath, TimeSpan ttl)
        {
            try
            {
                if (!File.Exists(markerPath)) return true;
                return (DateTime.Now - File.GetLastWriteTime(markerPath)) > ttl;
            }
            catch { return true; }
        }

        /// <summary>
        /// ดึงรายการไฟล์แนบของเอกสาร NextAcc แล้วดาวน์โหลดมาเก็บที่ folder (ชื่อ att{n}{ext}).
        /// แข็งแรงขึ้น: ลอง entity type "Document" ก่อน ถ้าว่างลองตามชนิดเอกสารจริง (เช่น PaymentVoucher);
        /// ดาวน์โหลดผ่าน storagePath ตรงๆ ก่อน ถ้าไม่ได้ลองผ่าน attachment Id endpoint.
        /// บันทึก log วินิจฉัยทุกขั้นเพื่อให้ตรวจสอบได้ว่าติดที่ขั้นไหน.
        /// </summary>
        /// <returns>true = ได้ผลแน่ชัด (โหลดไฟล์ได้ หรือยืนยันว่าไม่มีไฟล์); false = ล้มเหลวชั่วคราว ควรลองใหม่</returns>
        private async System.Threading.Tasks.Task<bool> FetchAndCacheNextAccAttachmentsAsync(
            OutboundDocumentResponse d, string folder, string baseUrl)
        {
            try
            {
                // ── Fast path: ไฟล์แนบฝังมากับ integration list response แล้ว (NextAcc มิ.ย. 2026+) ──
                // โหลดผ่าน DownloadUrl ตรง ๆ → ไม่ต้องยิง GET /attachments ต่อเอกสาร (กัน N+1)
                // (DownloadUrl = /api/companies/{cid}/attachments/{fileId}/download → ต่อกับ host + X-Api-Key)
                if (d.Attachments != null && d.Attachments.Count > 0)
                {
                    int eidx = 0, eok = 0, efail = 0;
                    foreach (var a in d.Attachments)
                    {
                        eidx++;
                        string ext = Path.GetExtension(a.FileName ?? "");
                        if (string.IsNullOrEmpty(ext)) ext = ExtFromContentType(a.ContentType);
                        string attName = $"att{eidx}{ext}";
                        string attLocal = Path.Combine(folder, attName);
                        if (File.Exists(attLocal) && new FileInfo(attLocal).Length > 0) { eok++; continue; }

                        byte[] bytes = null;
                        string dl = a.DownloadUrl;
                        if (!string.IsNullOrEmpty(dl))
                        {
                            string url = dl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? dl : baseUrl + (dl.StartsWith("/") ? dl : "/" + dl);
                            bytes = await _apiClient.DownloadFileAsync(url);
                        }
                        // fallback: ผ่าน attachment Id endpoint (เผื่อ DownloadUrl ใช้ไม่ได้)
                        if ((bytes == null || bytes.Length == 0) && a.Id != Guid.Empty)
                            bytes = await _apiClient.DownloadAttachmentByIdAsync(a.Id);

                        if (bytes != null && bytes.Length > 0) { File.WriteAllBytes(attLocal, bytes); eok++; }
                        else
                        {
                            efail++;
                            _code.Logs(_connectionString, "AccountingSync",
                                $"FetchAttachments: doc={d.DocumentNumber} ไฟล์ฝัง '{a.FileName}' โหลดไม่สำเร็จ (url='{dl}', id={a.Id})", "SYSTEM");
                        }
                    }
                    _code.Logs(_connectionString, "AccountingSync",
                        $"FetchAttachments: doc={d.DocumentNumber} (embedded) พบ {d.Attachments.Count} ไฟล์ โหลดสำเร็จ {eok} ล้มเหลว {efail}", "SYSTEM");
                    return true;   // integration list ส่งไฟล์แนบมาแล้ว = แน่ชัด (รวมกรณี repair ฝั่ง NextAcc)
                }

                // ลองหลาย entity type — NextAcc อาจผูกไฟล์แนบกับ "Document" หรือชนิดเอกสารจริง
                var entityTypes = new List<string> { "Document" };
                if (!string.IsNullOrEmpty(d.DocumentType) && d.DocumentType != "Document")
                    entityTypes.Add(d.DocumentType);

                List<FileAttachmentResponse> atts = null;
                string usedType = null;
                bool anyCallSucceeded = false;
                foreach (var et in entityTypes)
                {
                    try
                    {
                        var resp = await _apiClient.GetAttachmentsAsync(et, d.Id);
                        anyCallSucceeded = true;
                        if (resp?.data != null && resp.data.Count > 0)
                        {
                            atts = resp.data;
                            usedType = et;
                            break;
                        }
                    }
                    catch (Exception exType)
                    {
                        _code.Logs(_connectionString, "AccountingSync",
                            $"FetchAttachments: doc={d.DocumentNumber} entityType={et} error: {exType.Message}", "SYSTEM");
                    }
                }

                if (atts == null || atts.Count == 0)
                {
                    _code.Logs(_connectionString, "AccountingSync",
                        $"FetchAttachments: doc={d.DocumentNumber} ({d.Reference}) ไม่พบไฟล์แนบจาก NextAcc (ลอง entityType: {string.Join(",", entityTypes)})", "SYSTEM");
                    // ถ้า call สำเร็จแต่ไม่มีไฟล์ = แน่ชัด; ถ้าทุก call ล้มเหลว = ชั่วคราว
                    return anyCallSucceeded;
                }

                int idx = 0, ok = 0, fail = 0;
                foreach (var a in atts)
                {
                    idx++;
                    string ext = Path.GetExtension(a.OriginalFileName ?? a.FileName ?? "");
                    if (string.IsNullOrEmpty(ext)) ext = ExtFromContentType(a.ContentType);
                    string attName = $"att{idx}{ext}";
                    string attLocal = Path.Combine(folder, attName);

                    if (File.Exists(attLocal) && new FileInfo(attLocal).Length > 0) { ok++; continue; }

                    byte[] bytes = null;
                    // 1) ลองผ่าน storage path ตรงๆ (ถ้า NextAcc serve static)
                    string storage = (a.StoragePath ?? "").Replace("\\", "/").TrimStart('/');
                    if (!string.IsNullOrEmpty(storage))
                        bytes = await _apiClient.DownloadFileAsync($"{baseUrl}/{storage}");
                    // 2) ถ้าไม่ได้ ลองผ่าน attachment Id endpoint
                    if ((bytes == null || bytes.Length == 0) && a.Id != Guid.Empty)
                        bytes = await _apiClient.DownloadAttachmentByIdAsync(a.Id);

                    if (bytes != null && bytes.Length > 0)
                    {
                        File.WriteAllBytes(attLocal, bytes);
                        ok++;
                    }
                    else
                    {
                        fail++;
                        _code.Logs(_connectionString, "AccountingSync",
                            $"FetchAttachments: doc={d.DocumentNumber} ไฟล์ '{a.OriginalFileName ?? a.FileName}' โหลดไม่สำเร็จ (storage='{storage}', id={a.Id})", "SYSTEM");
                    }
                }

                _code.Logs(_connectionString, "AccountingSync",
                    $"FetchAttachments: doc={d.DocumentNumber} entityType={usedType} พบ {atts.Count} ไฟล์ โหลดสำเร็จ {ok} ล้มเหลว {fail}", "SYSTEM");

                // ดึง list สำเร็จแล้ว = แน่ชัด (เขียน marker กันยิงซ้ำทุก search) แม้บางไฟล์โหลดไม่ได้
                // (ถ้า download endpoint ผิด การ retry ทุกครั้งไม่ช่วย แค่ทำให้หน้าค้าง — ดู log เพื่อปรับ endpoint)
                return true;
            }
            catch (Exception ex)
            {
                _code.Logs(_connectionString, "AccountingSync",
                    $"FetchAttachments: doc={d.DocumentNumber} ({d.Reference}) ล้มเหลว: {ex.Message}", "SYSTEM");
                return false;
            }
        }
    }
}
