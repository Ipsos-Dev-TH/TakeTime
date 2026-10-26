using System;
using System.Collections.Generic;

namespace Take_Time_BangPhra.Integration.OcrReview
{
    /// <summary>ชนิดเอกสารที่หน้าตรวจทาน OCR สร้างบน NextAcc (ค่าใน <see cref="OcrReviewState.DocKind"/>)</summary>
    public static class OcrDocKind
    {
        /// <summary>ใบสำคัญจ่าย — จ่ายแล้ว (NextAcc PaymentVoucher=13, PaymentType Cash=1 — AllEnums.cs:518, :575)</summary>
        public const string PaymentVoucher = "PV";
        /// <summary>ค่าใช้จ่ายตั้งหนี้ — ยังไม่จ่าย (Expense=9, PaymentType Credit=2 — AllEnums.cs:517, :576)</summary>
        public const string Expense = "EXP";
        /// <summary>ใบลดหนี้จากผู้ขาย (CreditNote=6 + CnDnPurchaseSideOverride=true — AllEnums.cs:507, DocumentDtos.cs:26)</summary>
        public const string VendorCreditNote = "VCN";
        /// <summary>ใบเพิ่มหนี้จากผู้ขาย (DebitNote=5 + CnDnPurchaseSideOverride=true — AllEnums.cs:506)</summary>
        public const string VendorDebitNote = "VDN";

        public static bool IsNote(string kind) => kind == VendorCreditNote || kind == VendorDebitNote;
        public static bool IsKnown(string kind) =>
            kind == PaymentVoucher || kind == Expense || kind == VendorCreditNote || kind == VendorDebitNote;

        /// <summary>NextAcc DocumentType (int) ของชนิดนี้</summary>
        public static int NextAccType(string kind)
        {
            switch (kind)
            {
                case Expense: return 9;
                case VendorCreditNote: return 6;
                case VendorDebitNote: return 5;
                default: return 13;
            }
        }

        /// <summary>ชื่อ targetType ของ <c>ocr/{id}/create-document</c> (Enum.TryParse — OcrService.cs:6707-6710)</summary>
        public static string OcrTargetType(string kind)
        {
            switch (kind)
            {
                case Expense: return "Expense";
                case VendorCreditNote: return "CreditNote";
                case VendorDebitNote: return "DebitNote";
                default: return "PaymentVoucher";
            }
        }

        public static string Label(string kind)
        {
            switch (kind)
            {
                case Expense: return "ค่าใช้จ่ายตั้งหนี้";
                case VendorCreditNote: return "ใบลดหนี้จากผู้ขาย";
                case VendorDebitNote: return "ใบเพิ่มหนี้จากผู้ขาย";
                default: return "ใบสำคัญจ่าย";
            }
        }
    }

    /// <summary>แหล่งใบต้นทางของใบลด/เพิ่มหนี้</summary>
    public static class OcrNoteSource
    {
        /// <summary>เลือกเอกสารใน NextAcc (PI/Expense/PV — DocumentService.cs:12136-12160)</summary>
        public const string NextAcc = "NEXTACC";
        /// <summary>ใบเดิมอยู่นอกระบบ — ตรวจเพดาน §86/10 อัตโนมัติไม่ได้ ผู้ใช้ยืนยันเอง</summary>
        public const string Outside = "OUTSIDE";
        /// <summary>เป็นการคืนสินค้าที่บันทึกผ่านหน้า "คืนสินค้าให้ผู้ขาย" (PRT-) แล้ว — เก็บเป็นหลักฐาน ไม่สร้างเอกสารซ้ำ</summary>
        public const string PurchaseReturn = "PRT";
    }

    /// <summary>วิธีชำระของใบลด/เพิ่มหนี้ (ขาเงิน)</summary>
    public static class OcrNoteSettlement
    {
        /// <summary>ตามที่ NextAcc ลง: ใบต้นทางค้าง = เจ้าหนี้ · ชำระครบ = เงินสด 111 (DocumentService.cs:17899, :17920-17924)</summary>
        public const string Auto = "AUTO";
        /// <summary>ผู้ขายโอน/คืนเงินเข้าบัญชีที่เลือก (หรือเราจ่ายเพิ่มจากบัญชีที่เลือก สำหรับใบเพิ่มหนี้)</summary>
        public const string Bank = "BANK";
        /// <summary>เก็บเป็นยอดกับผู้ขาย (หักการซื้อครั้งถัดไป / ตั้งเจ้าหนี้รอจ่าย) — ย้ายขาเงินสดไปเจ้าหนี้</summary>
        public const string VendorAccount = "VENDOR";
    }

    /// <summary>บรรทัดที่ผู้ใช้ยืนยัน (ราคาไม่รวม VAT — ส่ง NextAcc ด้วย PricesIncludeVat=false)</summary>
    public sealed class OcrReviewLine
    {
        public string Description { get; set; }
        public decimal Qty { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        /// <summary>ส่วนลดเป็นบาทของบรรทัด (ก่อน VAT) — NextAcc DocumentLineRequest.DiscountAmount (DocumentDtos.cs:234)</summary>
        public decimal Discount { get; set; }
        /// <summary>รหัสบัญชี NextAcc (ค่าใช้จ่าย/สินค้า/GRNI) — DocumentLineRequest.AccountCode (DocumentDtos.cs:207)</summary>
        public string AccountCode { get; set; }
        /// <summary>"7" = 7% · "0" = อัตราศูนย์ · "EX" = ยกเว้น §81 (NextAcc VatRate -1 — OcrDtos.cs:279-285)</summary>
        public string VatType { get; set; } = "7";
        /// <summary>เคลมภาษีซื้อได้ (false = §82/5 รวม VAT เข้าค่าใช้จ่าย — DocumentDtos.cs:226-227)</summary>
        public bool VatClaimable { get; set; } = true;
        /// <summary>ยอด VAT ที่ผู้ใช้แก้เอง (เศษสตางค์ตามกระดาษ) — null = คำนวณ 7% · ส่งเป็น VatAmountOverride (DocumentDtos.cs:240)</summary>
        public decimal? VatOverride { get; set; }
        /// <summary>หัก ณ ที่จ่าย % ของบรรทัด (ฐาน = ยอดก่อน VAT)</summary>
        public decimal WhtRate { get; set; }
        /// <summary>ประเภทเงินได้ ม.40 (IncomeTypeCode — DocumentDtos.cs:243) · ว่าง = ให้ NextAcc ตัดสิน</summary>
        public string IncomeType { get; set; }
        /// <summary>บัญชีที่ OCR แนะนำ (เก็บไว้ทำ audit การแก้)</summary>
        public string OcrAccountCode { get; set; }
    }

    /// <summary>สถานะหน้าตรวจทานทั้งใบ — เก็บเป็น JSON ใน Ocr_Document_Log.Review_Json (draft) และส่งไปมาระหว่างหน้าเว็บ</summary>
    public sealed class OcrReviewState
    {
        public string ScanId { get; set; }
        public string DocKind { get; set; } = OcrDocKind.PaymentVoucher;

        // ── ผู้ขาย / เอกสาร ──
        public string SupplierName { get; set; }
        public string SupplierTaxId { get; set; }
        public string SupplierBranch { get; set; }
        public string SupplierAddress { get; set; }
        public string SupplierDocNo { get; set; }
        /// <summary>วันที่บนเอกสารผู้ขาย (yyyy-MM-dd)</summary>
        public string SupplierDocDate { get; set; }
        /// <summary>วันที่ใบกำกับภาษี (yyyy-MM-dd) — SupplierTaxInvoiceDate คุมงวดเคลม §82/4</summary>
        public string TaxInvoiceDate { get; set; }
        /// <summary>วันที่ลงบัญชีของเรา (DocumentDate)</summary>
        public string DocDate { get; set; }
        /// <summary>เอกสารเป็นใบกำกับภาษีเต็มรูป (HasTaxInvoiceReference — DocumentDtos.cs:97)</summary>
        public bool FullTaxInvoice { get; set; }
        /// <summary>แหล่งจ่ายเงิน (ChartOfAccount id ของ NextAcc) — PV เท่านั้น</summary>
        public string PaidHowAccountId { get; set; }
        /// <summary>ผู้ขายในระบบ TakeTime ที่เลือกผูก (Vendor.ID) — 0 = ใช้จาก OCR</summary>
        public int VendorId { get; set; }
        public string Notes { get; set; }

        public List<OcrReviewLine> Lines { get; set; } = new List<OcrReviewLine>();

        // ── ยอดตามกระดาษ (ผู้ใช้แก้ได้เมื่อ OCR อ่านผิด) ──
        public decimal? PrintedSubTotal { get; set; }
        public decimal? PrintedVat { get; set; }
        public decimal? PrintedTotal { get; set; }
        /// <summary>WHT ตามเอกสาร — ว่าง = ไม่เทียบ (ใบผู้ขายส่วนใหญ่ไม่พิมพ์ภาษีที่เราหัก)</summary>
        public decimal? PrintedWht { get; set; }

        // ── ใบลด/เพิ่มหนี้ ──
        public string SourceMode { get; set; } = OcrNoteSource.NextAcc;
        public string SourceDocId { get; set; }
        public string SourceDocNo { get; set; }
        public string SourceDocType { get; set; }
        public string SourceContactId { get; set; }
        public string SourceContactName { get; set; }
        /// <summary>ยอดใบต้นทาง (NextAcc TotalAmount = ก่อน VAT + VAT − WHT — DocumentService.cs:1791)</summary>
        public decimal SourceTotal { get; set; }
        /// <summary>ผลรวมใบลดหนี้ที่อนุมัติแล้วซึ่งอ้างใบต้นทางนี้ (ไม่นับร่าง/ยกเลิก — DocumentService.cs:11922-11927)</summary>
        public decimal SourceCredited { get; set; }
        public decimal SourceBalanceDue { get; set; }
        /// <summary>CN: CreditNoteReason 1-4 (AllEnums.cs:1636-1646) · DN: DebitNoteReason 1-4 (AllEnums.cs:1652-1662)</summary>
        public int NoteReason { get; set; }
        public string NoteReasonText { get; set; }
        public string Settlement { get; set; } = OcrNoteSettlement.Auto;
        /// <summary>บัญชีรับคืน/จ่าย (ChartOfAccount id) เมื่อ Settlement = BANK</summary>
        public string RefundAccountId { get; set; }
        /// <summary>เลขอ้างอิงการคืนสินค้า PRT- (SourceMode = PRT)</summary>
        public string ReturnRef { get; set; }
        /// <summary>ผู้ใช้ยืนยันว่าใบเดิมอยู่นอกระบบและยอดไม่เกินยอดเดิม (SourceMode = OUTSIDE)</summary>
        public bool OutsideCapConfirmed { get; set; }
        public string SearchText { get; set; }

        /// <summary>ผู้ใช้ยืนยันว่าไม่ใช่ใบซ้ำ หลัง NextAcc เตือนซ้ำ (create-document allowDuplicate — OcrController.cs:383)</summary>
        public bool AllowDuplicate { get; set; }

        // ── ข้อมูลจาก OCR (อ่านอย่างเดียว) ──
        public List<string> LowConfidence { get; set; } = new List<string>();
        public string OcrTargetType { get; set; }
        public string OcrRole { get; set; }
    }

    /// <summary>ผลคำนวณบรรทัด</summary>
    public sealed class OcrLineCalc
    {
        public decimal Gross;           // round(จำนวน × ราคา)
        public decimal Net;             // Gross − ส่วนลด (ฐานก่อน VAT)
        public decimal Vat;             // VAT ของบรรทัด (รวมส่วนที่เคลมไม่ได้)
        public decimal Wht;             // ภาษีหัก ณ ที่จ่ายของบรรทัด
        public decimal NextAccVatRate;  // 7 / 0 / -1
    }

    /// <summary>ยอดรวมทั้งใบที่คำนวณจากบรรทัด</summary>
    public sealed class OcrTotals
    {
        public decimal SubTotal;
        public decimal Vat;
        public decimal VatClaimable;
        public decimal VatNonClaimable;
        public decimal Total;           // SubTotal + Vat (ยอดตามใบ)
        public decimal Wht;
        public decimal NetPay;          // Total − Wht (= NextAcc TotalAmount)
    }

    /// <summary>ผลเทียบยอดหนึ่งช่อง</summary>
    public sealed class OcrReconItem
    {
        public string Key;
        public string Label;
        public decimal Computed;
        public decimal? Printed;
        public decimal Diff;
        public bool Ok;
    }

    /// <summary>ผลตรวจทั้งใบ — Errors ต้องว่างจึงส่งได้</summary>
    public sealed class OcrValidation
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<OcrReconItem> Recon = new List<OcrReconItem>();
        public OcrTotals Totals;
        /// <summary>§86/4 ที่ยังขาด (ว่าง = ครบ) — ขาด + เคลม VAT ⇒ NextAcc ลง 11640 (TaxInvoiceCompletenessChecker.cs:5-10)</summary>
        public List<string> MissingTaxInvoiceFields = new List<string>();
        public bool CanSubmit => Errors.Count == 0;
    }
}
