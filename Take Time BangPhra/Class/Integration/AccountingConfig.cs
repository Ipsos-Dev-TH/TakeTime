using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;

namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// Configuration for Nexaacc Accounting System integration.
    /// Reads from Accounting_Integration_Config table and Web.config.
    /// </summary>
    public class AccountingConfig
    {
        private readonly code _code = new code();
        private readonly string _connectionString;
        private Dictionary<string, string> _configCache;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public AccountingConfig()
        {
            _connectionString = ConfigurationManager.ConnectionStrings["TaketimeConnectionString"].ConnectionString;
        }

        public AccountingConfig(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Core settings
        public string BaseUrl => SanitizeBaseUrl(GetConfig("Nexaacc_BaseUrl", ""));
        public string RawBaseUrl => GetConfig("Nexaacc_BaseUrl", "");
        public string ApiKey => _code.Derypt(GetConfig("Nexaacc_ApiKey_Encrypted", ""));

        /// <summary>acc_ API Key สำหรับ company endpoints (/api/companies/* — document, OCR, override
        /// แหล่งเงิน, E-Tax, WHT, chart) โดยเฉพาะ ส่งผ่าน header X-Api-Key. ถ้าเว้นว่าง → ใช้ ApiKey (int_)
        /// ตัวเดียวกันผ่าน X-Api-Key fallback ของ NextAcc. แนะนำให้ตั้งคู่กับ int_ เพื่อให้แต่ละ surface
        /// auth ด้วย key ที่ถูกประเภท (robust สุด ไม่ต้องพึ่ง fallback).</summary>
        public string CompanyApiKey
        {
            get
            {
                var k = _code.Derypt(GetConfig("Nexaacc_CompanyApiKey_Encrypted", ""));
                return string.IsNullOrEmpty(k) ? ApiKey : k;
            }
        }

        /// <summary>true = ตั้ง acc_ key แยกสำหรับ company endpoints ไว้แล้ว (ไม่พึ่ง X-Api-Key fallback)</summary>
        public bool HasDedicatedCompanyKey =>
            !string.IsNullOrEmpty(_code.Derypt(GetConfig("Nexaacc_CompanyApiKey_Encrypted", "")));

        /// <summary>true = key เป็น Integration Key (ขึ้นต้น "int_").
        /// ใช้กับ /api/integration/* (X-Integration-Key) ได้ และ — ตั้งแต่ NextAcc เพิ่ม
        /// fallback ใน ApiKeyMiddleware — ยังใช้กับ {company}/* ผ่าน header X-Api-Key ได้ด้วย
        /// (middleware หา acc_ ไม่เจอ แล้ว fallback ไปตาราง integration). ดังนั้น int_ ครอบคลุม
        /// ทั้งสอง surface; ส่วน acc_ ใช้ /api/integration/* ไม่ได้ (auth ด้วย X-Integration-Key
        /// → ตาราง ExternalIntegration เท่านั้น) → ระบบควรตั้งค่าด้วย int_ เป็นหลัก.</summary>
        public bool IsIntegrationKey => (ApiKey ?? "").StartsWith("int_", StringComparison.OrdinalIgnoreCase);
        public Guid CompanyId => Guid.TryParse(GetConfig("Nexaacc_CompanyId", ""), out var id) ? id : Guid.Empty;

        /// <summary>company endpoints (/api/companies/{id}/* — document, OCR, payment override,
        /// แหล่งเงิน forcing, deposit docs) เรียกได้หรือไม่. ต้องมี CompanyId; ทั้ง int_ และ acc_
        /// auth ผ่าน X-Api-Key ได้ (acc_ ตรง ๆ, int_ ผ่าน fallback). ปิดด้วย flag
        /// Nexaacc_Company_Endpoints=0 ถ้า NextAcc รุ่นเก่ายังไม่รับ int_ บน company route
        /// (ระบบจะ fallback ไป /api/integration/* แทน).</summary>
        public bool CanUseCompanyEndpoints =>
            CompanyId != Guid.Empty
            && GetConfig("Nexaacc_Company_Endpoints", "1").Equals("1", StringComparison.OrdinalIgnoreCase);
        public bool Enabled => GetConfig("Nexaacc_Enabled", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
        public int SyncIntervalSeconds => int.TryParse(GetConfig("Nexaacc_SyncInterval_Sec", "30"), out var v) ? v : 30;
        public int MaxRetries => int.TryParse(GetConfig("Nexaacc_MaxRetries", "5"), out var v) ? v : 5;
        public int TimeoutSeconds => int.TryParse(GetConfig("Nexaacc_TimeoutSec", "30"), out var v) ? v : 30;
        /// <summary>timeout สำหรับ OCR upload โดยเฉพาะ — OCR ประมวลผลรูป/PDF นานกว่า call ปกติมาก (default 180s)</summary>
        public int OcrTimeoutSeconds => int.TryParse(GetConfig("Nexaacc_OcrTimeoutSec", "180"), out var v) && v > 0 ? v : 180;

        // ── ความทนทานของคิว (resilience) ────────────────────────────────────────
        /// <summary>NextAcc ล่ม (5xx/หน้า error page) → พักไม่ยิงกี่นาที ก่อนลองใหม่ (default 5)
        /// กัน retry storm: แอปล่มทีนึงเดิมยิงซ้ำ 5 ครั้ง × ทุกรายการในคิว</summary>
        public int ServerDownCooldownMinutes =>
            int.TryParse(GetConfig("Nexaacc_ServerDown_Cooldown_Min", "5"), out var v) && v > 0 ? v : 5;

        /// <summary>รายการที่ค้างสถานะ PROCESSING เกินกี่นาที ถือว่า worker ตายกลางคัน → คืนเป็น PENDING
        /// (default 15) ต้องมากกว่าเวลาที่ยิง 1 รายการนานสุดจริง ๆ ไม่งั้นเสี่ยงโพสต์ซ้ำ</summary>
        public int StuckProcessingMinutes =>
            int.TryParse(GetConfig("Nexaacc_Stuck_Processing_Min", "15"), out var v) && v >= 5 ? v : 15;

        /// <summary>เก็บ Accounting_Sync_Log ย้อนหลังกี่วัน (default 90; 0 = ไม่ลบ)</summary>
        public int SyncLogRetentionDays =>
            int.TryParse(GetConfig("Nexaacc_SyncLog_Retention_Days", "90"), out var v) && v >= 0 ? v : 90;

        /// <summary>แจ้งเตือน Telegram เมื่อคิวมีรายการล้มเหลวค้าง / NextAcc ล่ม (default เปิด)</summary>
        public bool QueueAlertEnabled => GetConfig("Nexaacc_Queue_Alert", "1").Equals("1", StringComparison.OrdinalIgnoreCase);

        /// <summary>เตือนซ้ำได้ทุกกี่ชั่วโมง (default 6) — กันสแปมกลุ่ม</summary>
        public int QueueAlertHours =>
            int.TryParse(GetConfig("Nexaacc_Queue_Alert_Hours", "6"), out var v) && v > 0 ? v : 6;

        /// <summary>
        /// JOURNAL_ONLY = บันทึกสมุดบัญชีอย่างเดียว (debit/credit journal entries)
        /// DOCUMENT = สร้างเอกสาร (ใบกำกับภาษี/ใบสำคัญจ่าย) + ระบบสร้าง journal ให้อัตโนมัติ
        /// </summary>
        public string SyncMode => GetConfig("Nexaacc_SyncMode", "DOCUMENT");
        public bool IsDocumentMode => SyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        // Per-document-type sync mode
        // LOCAL = ไม่ส่ง NextAcc (ใช้ระบบของ TakeTime)
        // JOURNAL_ONLY = สร้าง journal entry ใน NextAcc
        // DOCUMENT = สร้างเอกสารเต็มรูปแบบใน NextAcc
        // ──────────────────────────────────────────────

        public string ReceiptSyncMode => GetConfig("Nexaacc_SyncMode_Receipt", SyncMode);
        public string VoucherSyncMode => GetConfig("Nexaacc_SyncMode_Voucher", SyncMode);
        // Payroll default = JOURNAL_ONLY (ไม่ inherit DOCUMENT) — เพราะ NextAcc payroll DOCUMENT mode
        // "คำนวณใหม่ server-side" จาก master พนักงาน ไม่รับยอดต่องวดที่ TakeTime คำนวณ (OT/โบนัส/หักพิเศษ
        // ผันแปร) → JOURNAL mode โพสต์ GL ครบด้วยตัวเลขจริงของเรา. ตั้ง DOCUMENT เองได้เฉพาะกรณีเงินเดือน
        // คงที่ + ต้องการให้ NextAcc ออก ภงด.1/สปส/payslip native (ยอมรับว่า NextAcc คำนวณเอง)
        public string PayrollSyncMode => GetConfig("Nexaacc_SyncMode_Payroll", "JOURNAL_ONLY");

        public bool IsReceiptLocal => ReceiptSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);
        public bool IsReceiptDocumentMode => ReceiptSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsVoucherDocumentMode => VoucherSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsVoucherLocal => VoucherSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);
        public bool IsPayrollDocumentMode => PayrollSyncMode.Equals("DOCUMENT", StringComparison.OrdinalIgnoreCase);
        public bool IsPayrollLocal => PayrollSyncMode.Equals("LOCAL", StringComparison.OrdinalIgnoreCase);
        // DOCUMENT_IMPORT: ส่ง "ยอดเงินเดือนสำเร็จรูปต่อพนักงาน" ที่ TakeTime คำนวณเอง เข้า NextAcc
        // ผ่าน POST /payroll/runs/import (Recalculate=false) → NextAcc สร้าง run Calculated ตามยอดที่ส่ง
        // แล้ว approve→pay ออก GL + ภงด.1 + สปส.1-10 + 50ทวิ + payslip "จากยอดของเรา" (ไม่คำนวณใหม่).
        // ต่างจาก DOCUMENT (native run) ที่ NextAcc คำนวณใหม่ server-side → ใช้กับยอดผันแปรไม่ได้.
        public bool IsPayrollImportMode => PayrollSyncMode.Equals("DOCUMENT_IMPORT", StringComparison.OrdinalIgnoreCase);

        public bool AttachFiles => GetConfig("Nexaacc_AttachFiles", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>รวบยอดขายหน้าร้านที่ "ไม่ออกใบกำกับในระบบ" เป็นใบรับเงินสดสรุปรายวัน (1 ใบ/วัน/แหล่งรับเงิน)
        /// → sync รายได้+VAT ขาย + COGS ตัดสต๊อก อัตโนมัติผ่าน background timer. default ปิด</summary>
        public bool IsPosDailyRollupEnabled => GetConfig("Nexaacc_PosDailyRollup", "0") == "1";

        /// <summary>
        /// ลงบันทึกรายได้รูมเซอร์วิส (สั่งอาหารผ่าน Guest Portal) — ออเดอร์ที่ลูกค้าจ่ายเอง (โอน/เงินสด)
        /// รวบเป็นใบรับเงินสด 1 ใบ/วัน/วิธีจ่าย, ออเดอร์ที่ลงบิลห้องโพสต์เฉพาะต้นทุน (COGS) กันรายได้ซ้ำ.
        /// default ปิด
        /// </summary>
        public bool IsRoomServiceRevenueEnabled => GetConfig("Nexaacc_RoomServiceRevenue", "0") == "1";

        /// <summary>
        /// ลงบันทึกรายได้ค่าห้องของการจองจาก OTA (ที่ไม่ออกใบเสร็จในระบบ) —
        /// Dr ลูกหนี้ OTA / Cr รายได้ห้อง / Cr ภาษีขาย ต่อการจอง เมื่อเลยวันเช็คเอาท์.
        /// ต้อง map บัญชี OTA_RECEIVABLE + ใช้ acc_ key (company endpoints) ถึงจะบังคับบัญชีลูกหนี้ได้.
        /// default ปิด
        /// </summary>
        public bool IsOtaRoomRevenueEnabled => GetConfig("Nexaacc_OtaRoomRevenue", "0") == "1";

        /// <summary>อ่านอีเมลตอบกลับจากกรมสรรพากรแล้วมาร์ค e-Tax ว่านำส่งสำเร็จ (PHASE18_28). default ปิด</summary>
        public bool IsEtaxRdWatchEnabled => GetConfig("Etax_Rd_Watch_Enabled", "0") == "1";
        /// <summary>โดเมน/คำในผู้ส่งที่ถือว่าเป็นอีเมลจากกรมสรรพากร (คั่นจุลภาค)</summary>
        public string EtaxRdFromContains => GetConfig("Etax_Rd_FromContains", "rd.go.th, etax, teda.th");

        /// <summary>ดันจำนวนสต๊อก (ขาออก) TakeTime → NextAcc /product/stock/adjust (qty-only). default ปิด</summary>
        public bool IsStockQtySyncEnabled => GetConfig("Nexaacc_StockQtySync", "0") == "1";
        /// <summary>ดึงจำนวนสต๊อก (ขากลับ) NextAcc → TakeTime (ปรับสต๊อกฝั่ง NextAcc เอง). default ปิด</summary>
        public bool IsStockQtyPullEnabled => GetConfig("Nexaacc_StockQtyPull", "0") == "1";

        // ──────────────────────────────────────────────
        // Deposit VAT recognition timing (จุดความรับผิด VAT ของเงินมัดจำ)
        // ──────────────────────────────────────────────

        /// <summary>
        /// เมื่อไหร่ที่ VAT ของเงินมัดจำถูกรับรู้และยิงเข้าบัญชีภาษีขาย (OUTPUT_VAT):
        ///   CHECKOUT (default) — รับรู้ VAT ตอนตัดมัดจำเป็นรายได้ (เช็คเอาท์)
        ///   RECEIPT  — รับรู้ VAT ทันทีที่รับเงินมัดจำ (ตาม ป.รัษฎากร ม.78/1:
        ///              บริการ จุดความรับผิด VAT = วันรับชำระเงิน)
        /// </summary>
        public string DepositVatRecognition => GetConfig("Deposit_Vat_Recognition", "CHECKOUT");

        /// <summary>true = แยก VAT ออกจากมัดจำตั้งแต่ตอนรับเงิน (ยิงเข้า OUTPUT_VAT ทันที)</summary>
        public bool IsDepositVatAtReceipt => DepositVatRecognition.Equals("RECEIPT", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// (โหมด RECEIPT เท่านั้น) true = พัก VAT ของมัดจำไว้ที่ "ภาษีขายรอเรียกเก็บ/รอรับรู้"
        /// (mapping <c>OUTPUT_VAT_DEFERRED</c>, ปกติ 21913) ตอนรับเงิน แล้วโอนกลับเข้า
        /// "ภาษีขาย" (<c>OUTPUT_VAT</c>, 21911) ตอน check-out → VAT จะไม่ขึ้น ภ.พ.30 จนกว่า
        /// จะ realize ตอนรับรู้รายได้. false (ค่าเริ่มต้น) = เข้า OUTPUT_VAT ทันที (พฤติกรรมเดิม).
        /// ⚠ ต้อง map บัญชี OUTPUT_VAT_DEFERRED ก่อนเปิด ไม่งั้นระบบจะ fallback กลับไป OUTPUT_VAT.
        /// </summary>
        public bool IsDepositOutputVatDeferred => GetConfig("Deposit_Defer_Output_Vat", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Deposit_Defer_Output_Vat", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>GR/IR: รับสินค้าเข้าสต๊อก (Product/In) เครดิต GRNI 21240 แทนเจ้าหนี้การค้า (default เปิด)
        /// → ใบกำกับ/วางบิลที่ OCR ทีหลังล้าง GRNI + เพิ่มภาษีซื้อ (เคลมได้) กันโพสต์ซ้อน. ปิด = พฤติกรรมเดิม.</summary>
        public bool IsStockInUseGRNI => GetConfig("Nexaacc_StockIn_UseGRNI", "1").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_StockIn_UseGRNI", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>true = ไม่ยิง JE ตอนรับสินค้าเข้าสต๊อก (Product/In) เลย — ให้ใบกำกับซื้อที่ OCR โพสต์
        /// บัญชีทั้งหมด (invoice-only). ทับ GR/IR. ⚠ ใบกำกับต้องเดบิต "สินค้าคงเหลือ" ไม่งั้น COGS ตอนขาย
        /// ทำสต๊อกติดลบ (perpetual ต้องมี Dr สินค้าคงเหลือก่อน). default ปิด.</summary>
        public bool IsStockInSkipJournal => GetConfig("Nexaacc_StockIn_SkipJournal", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_StockIn_SkipJournal", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = เช็คเอาท์/รับชำระ B2B (ลูกค้ามีเลขภาษี) ออก "ใบเดียว" ใบกำกับภาษี/ใบเสร็จรับเงิน
        /// (ขายสด isCashSale) แทนการเปิดลูกหนี้ + ใบเสร็จรับชำระแยก (เดิมได้ 3 ใบต่อการขาย).
        /// ต้องใช้ NextAcc รุ่นที่รองรับ isCashSale (Option B) — default off เพื่อ backward-compatible;
        /// เปิดหลัง deploy + ทดสอบเช็คเอาท์ 1 รายการ. ปัจจุบันใช้กับ "ขายสดยอดเต็มไม่มีหักมัดจำ" เท่านั้น
        /// (เคสหักมัดจำยังใช้เส้นเดิมจนกว่า NextAcc ยืนยัน contract หักมัดจำบน cash-sale invoice).
        /// </summary>
        public bool IsTaxReceiptSingleDoc => GetConfig("Nexaacc_TaxReceipt_SingleDoc", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_TaxReceipt_SingleDoc", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = ใบเดียวขายสด (IsTaxReceiptSingleDoc) รองรับ "เคสหักมัดจำ" ด้วย — ส่ง
        /// depositAppliedAmount/Ref บน cash-sale invoice ให้ NextAcc ลง Dr แหล่งเงิน(สุทธิ) + Dr 21510(มัดจำ)
        /// ในใบเดียว. ⚠ เปิดได้เมื่อ NextAcc deploy รุ่นรองรับ deposit fields บน isCashSale แล้วเท่านั้น —
        /// เปิดก่อน NextAcc พร้อม = NextAcc ignore field → เงินสดลงเต็มยอด มัดจำไม่ถูกล้าง GL พัง.
        /// default off; ปิดอยู่ = เคสมัดจำใช้เส้นเดิม (TIV + settle แยก, GL ถูก).
        /// </summary>
        public bool IsCashSaleDepositEnabled => GetConfig("Nexaacc_CashSale_Deposit", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_CashSale_Deposit", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// โหมดหักมัดจำบนใบขายสด (เมื่อ IsCashSaleDepositEnabled):
        ///   true (Option A, NextAcc native) = ส่ง depositAppliedDrivesJournal=true → NextAcc ลง Dr 21510
        ///     ใน JE ของใบเอง (JE เดียว ไม่มี JV แยก สะอาดกว่า). ⚠ ต้องใช้ NextAcc รุ่นที่ต่อสาย reverse
        ///     21510/21913 ใน AutoPost ของ integration invoice แล้วเท่านั้น — ไม่งั้น 21510 ไม่ล้าง GL พัง.
        ///   false (Option B, default) = TakeTime โพสต์ JV กลับมัดจำเอง (Dr 21510 / Cr แหล่งเงิน) —
        ///     ทำงานได้เลยไม่ต้องรอ NextAcc.
        /// </summary>
        public bool IsCashSaleDepositNativeA => GetConfig("Nexaacc_CashSale_Deposit_NativeA", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_CashSale_Deposit_NativeA", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = เช็คเอาท์ลูกค้ามีเลขภาษี ให้ออกเป็น **Receipt (type 3)** แทน TaxInvoice+isCashSale.
        /// NextAcc AutoPost Receipt branch = **Dr เงินสดตรง / Cr รายได้ราย line / Cr ภาษีขาย — ไม่มีลูกหนี้**
        /// (ทำงานบน production ปัจจุบันเลย ไม่ต้องรอ isCashSale deploy) + หัวเอกสาร "ใบกำกับภาษี/ใบเสร็จรับเงิน"
        /// + มัดจำหักในใบ (Dr 21510/21913) ผ่าน SettleReceiptDocAsync.
        /// ⚠ ข้อแลก: Receipt(3) **ไม่ออก e-Tax XML** (ใบกระดาษ/PDF ใช้ได้ปกติ). เปิดชั่วคราวระหว่างรอ
        /// NextAcc deploy isCashSale — deploy แล้วปิด flag นี้กลับไปใช้ TaxInvoice+e-Tax.
        /// false (default) = ใช้ TaxInvoice/isCashSale (ได้ e-Tax แต่ต้องรอ NextAcc รองรับ ไม่งั้น fallback AR).
        /// </summary>
        public bool IsCashSaleUseReceipt => GetConfig("Nexaacc_CashSale_UseReceipt", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Nexaacc_CashSale_UseReceipt", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = ใช้ NextAcc "โหมดขับ JE" (spec §9.1): ใบกำกับ/ใบเสร็จเช็คเอาท์ที่หักมัดจำ ให้ NextAcc ลง
        /// JE self-contained ในใบเดียว (ส่ง <c>depositAppliedDrivesJournal=true</c>) + TakeTime **เลิกส่ง
        /// JV หักมัดจำแยก** — GL การกลับ 217xx/21913 อยู่ในใบเดียวจบ. false (default) = display-only:
        /// NextAcc โชว์ "หักเงินมัดจำ/สุทธิ" แต่ JV หักมัดจำยังยิงแยกฝั่ง TakeTime (พฤติกรรมปัจจุบัน).
        /// ⚠ เปิด flag นี้ได้ก็ต่อเมื่อ NextAcc deploy รองรับแล้ว — เปิดพร้อมกันในดีพลอยเดียว
        /// (flag on + เลิก JV) มิฉะนั้น double-reverse (GL พัง).
        /// </summary>
        public bool IsDepositAppliedDrivesJournal => GetConfig("Nexaacc_Deposit_Drives_Journal", "0").Equals("1", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = ส่ง "JV-INT EntryNumber" เป็น depositAppliedRef + drives สำหรับมัดจำที่เป็น journal (NextAcc
        /// cb55e3b resolve journal ref → กลับ deferred ในใบเดียว, self-contained). **เปิดได้เมื่อ NextAcc
        /// deploy cb55e3b แล้วเท่านั้น** — ถ้าเปิดก่อน NextAcc พร้อม เอกสารจะค้าง draft (approve ไม่ผ่าน).
        /// false (default) = มัดจำ JV-INT ใช้ reverse-JE แยก (GL ถูก, ปลอดภัย ไม่ค้าง draft). ต้องใช้คู่กับ
        /// Nexaacc_Deposit_Drives_Journal=1. (มี safety-net auto-fallback ถ้าเผลอเปิดก่อน NextAcc พร้อม)
        /// </summary>
        public bool IsDrivesJournalRefEnabled => GetConfig("Nexaacc_Drives_Journal_Ref", "0").Equals("1", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = AUTO-RECOVER มัดจำ legacy: ถ้าใบมัดจำถูก reverse ค้าง (จากเช็คเอาท์รอบก่อน drives ปิด แล้ว
        /// void+sync ใหม่หลายรอบ) → ตอนเช็คเอาท์ใหม่จะ "un-reverse" (กลับตัว reversal) คืนหนี้สินมัดจำให้ active
        /// → drives ทำ single-JE (Dr เงินสดสุทธิ) ได้. idempotent (เคย recover แล้วข้าม). false (default) = ใช้
        /// guard เดิม (มัดจำ reverse แล้ว → ไม่ drives/ไม่กลับซ้ำ, book Dr เต็ม, net ถูก). เปิดเมื่อต้องการ
        /// single-JE กับ booking เก่าที่ผ่าน churn — ควร test บน Windows + ตรวจ GL 1-2 ใบก่อนเปิดกว้าง.
        /// </summary>
        public bool IsAutoRecoverDeposit => GetConfig("Nexaacc_Auto_Recover_Deposit", "0").Equals("1", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true (default) = หลัง sync ใบเสร็จ/เช็คเอาท์สำเร็จ อ่านเอกสาร+JE+ไฟล์แนบกลับจาก NextAcc มาเทียบ
        /// กับความจริงฝั่งเรา (ยอดรับจริง+สลิป) → เก็บผล Verify_Status/Verify_Detail บนคิว. ดัก: ยอดไม่ตรง,
        /// JE ไม่บาลานซ์, บัญชีมัดจำ 21510 ติดลบ (double-reverse), สลิปไม่แนบ, เอกสารไม่โพสต์. read-only (ไม่แก้
        /// อะไรบน NextAcc). false = ปิด (ลด API call ต่อการ sync). migration PHASE18_08.
        /// </summary>
        public bool IsPostSyncVerifyEnabled => GetConfig("Nexaacc_Post_Sync_Verify", "1").Equals("1", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// true = AUTO-RECONCILE บัญชีมัดจำ 21510 ที่ "ติดลบ" จาก adjustment ค้าง (orphaned -DEPADJ ที่เหลือจาก
        /// เช็คเอาท์รอบเก่าที่ drives fail แล้ว void ไม่สมบูรณ์). ทำเฉพาะเมื่อเช็คเอาท์รอบนี้ใช้ drives สำเร็จ
        /// (การหักมัดจำอยู่ใน JE เดียว → -DEPADJ แยกทุกตัว = orphaned แน่นอน) → reverse -DEPADJ ที่ค้าง
        /// "เท่าที่จำเป็น" (self-limiting: หยุดเมื่อ net 21510 กลับ ~0 ไม่ over-correct) → re-verify ผลจริง.
        /// false (default) = ไม่แตะ (booking เสียเคลียร์มือ). opt-in — ควร test + ตรวจ GL ก่อนเปิด.
        /// </summary>
        public bool IsAutoReconcileDeposit => GetConfig("Nexaacc_Auto_Reconcile_Deposit", "0").Equals("1", StringComparison.OrdinalIgnoreCase);

        // ──────────────────────────────────────────────
        // E-Tax Invoice automation
        // ──────────────────────────────────────────────

        /// <summary>สร้าง E-Tax Invoice อัตโนมัติเมื่อสร้างใบเสร็จในระบบ NextAcc สำเร็จ</summary>
        public bool IsEtaxAutoGenerate => GetConfig("Etax_AutoGenerate", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoGenerate", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ลงนาม E-Tax อัตโนมัติหลังสร้าง</summary>
        public bool IsEtaxAutoSign => GetConfig("Etax_AutoSign", "1").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSign", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ส่งให้กรมสรรพากรอัตโนมัติหลังลงนาม</summary>
        public bool IsEtaxAutoSubmit => GetConfig("Etax_AutoSubmit", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSubmit", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>ส่งอีเมลใบกำกับภาษีอิเล็กทรอนิกส์ให้ลูกค้าอัตโนมัติเมื่อ E-Tax พร้อมแล้ว</summary>
        public bool IsEtaxAutoSendEmail => GetConfig("Etax_AutoSendEmail", "0").Equals("1", StringComparison.OrdinalIgnoreCase)
            || GetConfig("Etax_AutoSendEmail", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>หัวข้ออีเมล E-Tax (รองรับ {ReceiptNumber}, {GuestName}, {CompanyName})</summary>
        public string EtaxEmailSubject => GetConfig("Etax_EmailSubject",
            "ใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber}");

        /// <summary>เนื้อหาอีเมล E-Tax (รองรับ {ReceiptNumber}, {GuestName}, {Amount}, {Date})</summary>
        public string EtaxEmailBody => GetConfig("Etax_EmailBody",
            "เรียน {GuestName}\n\nกรุณาดาวน์โหลดใบกำกับภาษีอิเล็กทรอนิกส์ {ReceiptNumber} จากเอกสารแนบ\n\nขอบคุณที่ใช้บริการ");

        /// <summary>แนบ PDF E-Tax ในอีเมล</summary>
        public bool EtaxEmailAttachPdf => GetConfig("Etax_EmailAttachPdf", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>แนบ XML E-Tax ในอีเมล (ลูกค้าธุรกิจอาจต้องการ)</summary>
        public bool EtaxEmailAttachXml => GetConfig("Etax_EmailAttachXml", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>สำเนา (CC) เริ่มต้นของอีเมล E-Tax (คั่นหลายอีเมลด้วย , หรือ ;) — เติมในหน้าส่งให้ผู้ใช้แก้ได้.
        /// ว่าง → fallback ไป Email_CC ใน AppSettings (CC กลางของระบบ)</summary>
        public string EtaxEmailCc => GetConfig("Etax_EmailCc",
            AppCfg.Get("Email_CC") ?? "");

        /// <summary>
        /// ถ้าส่งอีเมลผ่าน NextAcc ไม่สำเร็จ → ดาวน์โหลด PDF/XML จาก URL ของ NextAcc แล้วส่งผ่าน SMTP ของ TakeTime
        /// </summary>
        public bool EtaxEmailFallback => GetConfig("Etax_EmailFallback", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// บังคับให้ใช้ SMTP ของ TakeTime เท่านั้น ข้าม NextAcc — สำหรับกรณี NextAcc email service ปิดอยู่
        /// </summary>
        public bool EtaxEmailLocalOnly => GetConfig("Etax_EmailLocalOnly", "false").Equals("true", StringComparison.OrdinalIgnoreCase);

        // ── Email reservation intake (STAAH) ─────────────────────────────────────
        /// <summary>เปิดอ่านอีเมลจอง OTA (STAAH) ในระบบเอง</summary>
        public bool IsEmailReservationEnabled => GetConfig("Email_Rsv_Enabled", "0") == "1";
        public string EmailRsvImapServer => GetConfig("Email_Rsv_ImapServer", "imap.gmail.com");
        public int EmailRsvImapPort => int.TryParse(GetConfig("Email_Rsv_ImapPort", "993"), out var v) ? v : 993;
        public string EmailRsvUsername => GetConfig("Email_Rsv_Username", "");
        public bool EmailRsvHasPassword => !string.IsNullOrEmpty(GetConfig("Email_Rsv_Password_Encrypted", ""));
        public int EmailRsvPollMinutes => int.TryParse(GetConfig("Email_Rsv_PollMinutes", "5"), out var v) ? v : 5;
        public string EmailRsvProcessedLabel => GetConfig("Email_Rsv_ProcessedLabel", "STAAH-Processed");
        public string EmailRsvFailedLabel => GetConfig("Email_Rsv_FailedLabel", "STAAH-Failed");
        /// <summary>folder เก็บอีเมล STAAH ที่ไม่เกี่ยวกับการจอง (จดหมายข่าว/รายงาน) — แยกจาก Failed</summary>
        public string EmailRsvIgnoredLabel => GetConfig("Email_Rsv_IgnoredLabel", "STAAH-Other");
        public int EmailRsvMaxStayDays => int.TryParse(GetConfig("Email_Rsv_MaxStayDays", "30"), out var v) ? v : 30;
        public int EmailRsvMaxDaysFuture => int.TryParse(GetConfig("Email_Rsv_MaxDaysFuture", "365"), out var v) ? v : 365;
        public bool EmailRsvNotifyTelegram => GetConfig("Email_Rsv_NotifyTelegram", "1") == "1";
        public bool EmailRsvCreateDocument => GetConfig("Email_Rsv_CreateDocument", "0") == "1";
        public bool EmailRsvMoveFailed => GetConfig("Email_Rsv_MoveFailed", "1") == "1";
        public string EmailRsvFromContains => GetConfig("Email_Rsv_FromContains", "staah");
        /// <summary>วนกลับมาลองลงจองอีเมลใน folder Failed ให้อัตโนมัติ (หลังแก้ mapping/ปล่อยห้อง)</summary>
        public bool EmailRsvRetryFailed => GetConfig("Email_Rsv_RetryFailed", "1") == "1";
        public int EmailRsvRetryHours => int.TryParse(GetConfig("Email_Rsv_RetryHours", "72"), out var v) ? v : 72;
        /// <summary>ถ้า Agency ในอีเมลไม่ตรงตาราง map ให้ยอม match ด้วยชื่อห้องอย่างเดียว (กันจองหล่น)</summary>
        public bool EmailRsvMapAnyChannel => GetConfig("Email_Rsv_MapAnyChannel", "1") == "1";
        /// <summary>ลำดับห้องที่จัดให้ก่อน (Accommodation ID คั่นจุลภาค) — เดิม hard-code 16,15,3,1,2,4,5</summary>
        public string EmailRsvRoomPriority => GetConfig("Email_Rsv_RoomPriority", "");
        public string EmailRsvDefaultPhone => GetConfig("Email_Rsv_DefaultPhone", "");
        /// <summary>สถานะที่ตั้งเมื่อยกเลิกจากอีเมล OTA (ยกเลิก/ยกเลิกคืนเงิน/ยกเลิกไม่คืนเงิน)</summary>
        public string EmailRsvCancelStatus => GetConfig("Email_Rsv_CancelStatus", "ยกเลิก");

        // ── Daily reservation board → LINE ───────────────────────────────────────
        public bool IsDailyLineReportEnabled => GetConfig("Line_DailyReport_Enabled", "0") == "1";
        public string LineDailyRecipients => GetConfig("Line_DailyReport_Recipients", "");
        public string LineDailySendTime => GetConfig("Line_DailyReport_SendTime", "08:00");
        public string LineDailySourceUrl => GetConfig("Line_DailyReport_SourceUrl", "https://taketimebangphra.com/displaytoday");
        public int LineDailyImageWidth => int.TryParse(GetConfig("Line_DailyReport_ImageWidth", "1600"), out var v) ? v : 1600;
        public int LineDailyImageHeight => int.TryParse(GetConfig("Line_DailyReport_ImageHeight", "700"), out var v) ? v : 700;
        public bool LineDailyAutoHeight => GetConfig("Line_DailyReport_AutoHeight", "1") == "1";
        public string LineDailyCaption => GetConfig("Line_DailyReport_Caption", "");
        public string LineDailyPublicBaseUrl => GetConfig("Line_DailyReport_PublicBaseUrl", "https://taketimebangphra.com/Images/Reservation");
        public string LineDailyImageFolder => GetConfig("Line_DailyReport_ImageFolder", "~/Images/Reservation");
        public bool LineDailyHasTokenOverride => !string.IsNullOrEmpty(GetConfig("Line_DailyReport_TokenOverride_Encrypted", ""));
        public int LineDailyJpegQuality => int.TryParse(GetConfig("Line_DailyReport_JpegQuality", "90"), out var v) ? v : 90;
        public int LineDailyFontScale => int.TryParse(GetConfig("Line_DailyReport_FontScale", "100"), out var v) ? v : 100;
        public string LineDailyLastSent => GetConfig("Line_DailyReport_LastSent", "");

        /// <summary>
        /// ตั้งค่า API ครบแล้วหรือยัง (Base URL, API Key, Company ID)
        /// ไม่รวม Enabled — เพราะ "ตั้งค่าครบ" กับ "เปิด sync" เป็นคนละเรื่อง
        /// </summary>
        public bool IsConfigured => !string.IsNullOrEmpty(BaseUrl) && IsValidUrl(BaseUrl) && !string.IsNullOrEmpty(ApiKey) && CompanyId != Guid.Empty;

        /// <summary>
        /// พร้อม sync อัตโนมัติ = ตั้งค่าครบ + เปิดใช้งาน
        /// </summary>
        public bool IsReadyToSync => IsConfigured && Enabled;

        /// <summary>
        /// Validate that a URL is well-formed and uses HTTPS.
        /// </summary>
        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            Uri uri;
            return Uri.TryCreate(url, UriKind.Absolute, out uri)
                && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
        }

        /// <summary>
        /// Validate the Base URL and return diagnostic messages.
        /// Returns null if valid; error message if invalid.
        /// </summary>
        public string ValidateBaseUrl()
        {
            string raw = RawBaseUrl;
            if (string.IsNullOrWhiteSpace(raw))
                return "Nexaacc_BaseUrl ยังไม่ได้ตั้งค่า";

            Uri uri;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out uri))
                return $"Nexaacc_BaseUrl format ไม่ถูกต้อง: '{raw}'";

            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                return $"Nexaacc_BaseUrl ต้องเป็น http:// หรือ https:// — ปัจจุบันเป็น: '{uri.Scheme}'";

            if (string.IsNullOrEmpty(uri.Host) || uri.Host == "localhost")
                return $"Nexaacc_BaseUrl host ไม่ถูกต้อง: '{uri.Host}'";

            return null;
        }

        /// <summary>
        /// Strip any trailing API path segments from the base URL to prevent
        /// path duplication when CompanyPath is appended by AccountingApiClient.
        /// Handles: /api/companies/{guid}..., /api/, /api
        /// </summary>
        private static string SanitizeBaseUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return url;

            // Strip /api/companies/... first (most specific)
            int idx = url.IndexOf("/api/companies", StringComparison.OrdinalIgnoreCase);
            if (idx > 0)
            {
                url = url.Substring(0, idx);
            }
            else
            {
                // Strip trailing /api to prevent /api/api/companies duplication
                string trimmed = url.TrimEnd('/');
                if (trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
                    url = trimmed.Substring(0, trimmed.Length - 4);
            }

            return url.TrimEnd('/');
        }

        private string GetConfig(string key, string defaultValue)
        {
            EnsureCache();
            return _configCache.ContainsKey(key) ? _configCache[key] : defaultValue;
        }

        private void EnsureCache()
        {
            if (_configCache != null && DateTime.Now < _cacheExpiry)
                return;

            _configCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                DataTable dt = _code.DatabaseQuerySafe(_connectionString,
                    "SELECT ConfigKey, ConfigValue FROM Accounting_Integration_Config WHERE ConfigValue IS NOT NULL",
                    null);

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string configKey = row["ConfigKey"]?.ToString();
                        string configValue = row["ConfigValue"]?.ToString();
                        if (!string.IsNullOrEmpty(configKey))
                        {
                            _configCache[configKey] = configValue ?? "";
                        }
                    }
                }
            }
            catch
            {
                // Config table may not exist yet; use defaults
            }

            _cacheExpiry = DateTime.Now.Add(CacheDuration);
        }

        /// <summary>
        /// Save or update a config value.
        /// </summary>
        public void SetConfig(string key, string value)
        {
            var parameters = new Dictionary<string, object>
            {
                { "@key", key },
                { "@value", value }
            };

            _code.DatabaseInsertSafe(_connectionString,
                @"IF EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = @key)
                    UPDATE Accounting_Integration_Config SET ConfigValue = @value, Updated_Date = GETDATE() WHERE ConfigKey = @key
                  ELSE
                    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue) VALUES (@key, @value)",
                parameters);

            // Invalidate cache
            _cacheExpiry = DateTime.MinValue;
        }
    }
}
