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
        /// <summary>เป็นการคืนสินค้าที่บันทึกผ่านหน้า "คืนสินค้าให้ผู้ขาย" (PRT-) แล้ว — เก็บเป็นหลักฐาน ไม่สร้างเอกสารซ้ำ.
        /// ใช้เฉพาะเมื่อปิดนโยบาย Vendor_Cn_Primary (=0) / ยังไม่รัน PHASE19_39 — เปิดอยู่ = ใบลดหนี้เป็นตัวจริง ผูกผ่าน <see cref="OcrReviewState.ReturnRef"/></summary>
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

    /// <summary>
    /// เหตุผลใบลด/เพิ่มหนี้ผู้ขาย (รหัสของเรา) → NextAcc enum (CreditNoteReason 1-4 / DebitNoteReason 1-4 — AllEnums.cs:1636-1662).
    /// NextAcc พิมพ์ได้แค่ข้อความมาตรฐานของ enum (PdfGenerationService.cs:3198-3205 + DocumentLabels.cs:244-247) และไม่มีช่องข้อความเหตุผลอิสระ
    /// ⇒ ข้อความของเรา (+ รายละเอียด) ไปอยู่ใน Notes ของเอกสาร. Return (1) กระทบสต๊อกฝั่ง NextAcc เฉพาะบรรทัดที่มี ProductCode
    /// (DocumentService.cs:17828-17860) — หน้านี้ไม่ส่ง ProductCode ⇒ สต๊อกเดินตาม TakeTime (การคืนสินค้า PRT-) เท่านั้น
    /// </summary>
    public static class OcrNoteReasons
    {
        public sealed class Reason
        {
            public string Code;
            public string Label;
            public int NextAcc;
            /// <summary>มีของส่งกลับจริง (ผูกการคืนสินค้า PRT- ได้)</summary>
            public bool PhysicalReturn;
            public bool NeedsText;
            public string Hint;
        }

        public static readonly Reason[] Credit =
        {
            new Reason { Code = "RETURN", Label = "คืนสินค้า", NextAcc = 1, PhysicalReturn = true,
                Hint = "ส่งของกลับผู้ขาย — ผูกการคืนสินค้า (PRT-) ถ้าบันทึกที่หน้า \"คืนสินค้าให้ผู้ขาย\" ไว้แล้ว" },
            new Reason { Code = "DEFECT", Label = "สินค้าชำรุด/เสียหาย/ผิดสเปก (ส่งคืน)", NextAcc = 1, PhysicalReturn = true,
                Hint = "ส่งของที่ชำรุด/ผิดสเปกกลับ — เหมือนคืนสินค้า" },
            new Reason { Code = "SHORT", Label = "ผู้ขายไม่มีสินค้า/ส่งของไม่ครบ", NextAcc = 3,
                Hint = "ไม่มีของส่งกลับ ⇒ ไม่ต้องผูกการคืนสินค้า · ถ้ารับเข้าสต๊อก (Product/In) เต็มจำนวนไปแล้ว ให้แก้จำนวนรับเข้าให้ตรงของที่ได้จริง และให้บรรทัดลงบัญชีเดียวกับใบเดิม (สินค้าคงเหลือ/GRNI) เพื่อลดต้นทุนของที่ไม่ได้รับ" },
            new Reason { Code = "DISCOUNT", Label = "ส่วนลด/ลดราคาหลังขาย", NextAcc = 2,
                Hint = "ไม่แตะสต๊อก — แนะนำบัญชีส่วนลดรับ/ส่งคืน หรือบัญชีเดียวกับใบเดิม" },
            new Reason { Code = "PRICE_ERR", Label = "คิดราคาผิด/คำนวณผิด (สูงกว่าจริง)", NextAcc = 3,
                Hint = "ไม่แตะสต๊อก — ใช้บัญชีเดียวกับใบเดิม" },
            new Reason { Code = "OTHER", Label = "อื่น ๆ (ระบุ)", NextAcc = 3, NeedsText = true,
                Hint = "พิมพ์รายละเอียดเหตุผล (§86/10 บังคับระบุเหตุผลบนใบ)" },
        };

        public static readonly Reason[] Debit =
        {
            new Reason { Code = "PRICE_UP", Label = "ราคาเพิ่มจากที่ตกลง/คิดราคาต่ำไป", NextAcc = 1 },
            new Reason { Code = "EXTRA", Label = "ส่งสินค้าเกิน/บริการเพิ่ม", NextAcc = 2 },
            new Reason { Code = "CHARGE", Label = "ค่าใช้จ่ายเพิ่มเติมภายหลัง (ขนส่ง/ติดตั้ง)", NextAcc = 3 },
            new Reason { Code = "CALC_ERR", Label = "คำนวณยอด/ภาษีต่ำไป", NextAcc = 4 },
            new Reason { Code = "OTHER", Label = "อื่น ๆ (ระบุ)", NextAcc = 4, NeedsText = true },
        };

        public static Reason Find(string docKind, string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var list = docKind == OcrDocKind.VendorDebitNote ? Debit : Credit;
            foreach (var r in list) if (string.Equals(r.Code, code.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }

        /// <summary>ร่างเก่า (มีแค่ NoteReason 1-4) → รหัสของเรา</summary>
        public static string FromLegacy(string docKind, int nextAcc)
        {
            if (docKind == OcrDocKind.VendorDebitNote)
                return nextAcc == 1 ? "PRICE_UP" : nextAcc == 2 ? "EXTRA" : nextAcc == 3 ? "CHARGE" : nextAcc == 4 ? "CALC_ERR" : null;
            return nextAcc == 1 ? "RETURN" : nextAcc == 2 ? "DISCOUNT" : nextAcc == 3 ? "PRICE_ERR" : nextAcc == 4 ? "OTHER" : null;
        }
    }

    /// <summary>บรรทัดของใบต้นทางใน NextAcc (อ่านอย่างเดียว — ใช้แสดงข้างฟอร์มและจับคู่บัญชีของบรรทัดใบลด/เพิ่มหนี้)</summary>
    public sealed class OcrSourceLine
    {
        public string Description { get; set; }
        public string ProductCode { get; set; }
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        /// <summary>ยอดบรรทัดก่อน VAT (NextAcc DocumentLineResponse.Amount)</summary>
        public decimal Amount { get; set; }
        public decimal VatRate { get; set; }
        public string AccountCode { get; set; }
        public string AccountName { get; set; }
    }

    /// <summary>บรรทัดที่ผู้ใช้ยืนยัน — ราคา/ส่วนลดอยู่ในฐานเดียวกับ <see cref="OcrReviewState.PricesIncludeVat"/>
    /// (false = ก่อน VAT · true = รวม VAT ตามกระดาษ) ส่ง NextAcc ด้วย PricesIncludeVat ค่าเดียวกัน + VatAmountOverride ทุกบรรทัดที่มีภาษี</summary>
    public sealed class OcrReviewLine
    {
        public string Description { get; set; }
        public decimal Qty { get; set; } = 1m;
        public decimal UnitPrice { get; set; }
        /// <summary>ส่วนลดเป็นบาทของบรรทัด (ฐานเดียวกับราคา) — NextAcc DocumentLineRequest.DiscountAmount (DocumentDtos.cs:234)</summary>
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
        /// <summary>รหัสสินค้าบนกระดาษ (เช่น Makro article no.) — ใช้จับคู่บรรทัดใบต้นทาง ไม่ส่ง NextAcc (กันสต๊อกฝั่ง NextAcc ขยับ)</summary>
        public string ProductCode { get; set; }
        /// <summary>บัญชีของบรรทัดที่จับคู่ได้ในใบต้นทาง (<see cref="OcrReviewMath.AssignAccountsFromSource"/>)</summary>
        public string SourceAccountCode { get; set; }
        /// <summary>เหตุผลการจับคู่ (รหัสสินค้า / รายละเอียด / ราคา / บัญชีหลักของใบเดิม)</summary>
        public string SourceMatch { get; set; }
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
        /// <summary>ราคาในรายการ "รวม VAT แล้ว" (ตามกระดาษ เช่น Makro) — VAT ทั้งใบ = ยอดรวม − round(ยอดรวม × 100/107) ต่ออัตรา ·
        /// ส่ง NextAcc PricesIncludeVat=true (DocumentDtos.cs:109, :290; สูตร DocumentService.cs:642-684)</summary>
        public bool PricesIncludeVat { get; set; }
        /// <summary>ที่มาของ <see cref="PricesIncludeVat"/>: "AUTO" = ระบบตัดสินจากยอดบนกระดาษ · "OCR" = OCR อ่านว่าราคารวม VAT · "USER" = ผู้ใช้สลับเอง</summary>
        public string PriceBasisSource { get; set; }

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
        public decimal SourceSubTotal { get; set; }
        public string SourceDocDate { get; set; }
        /// <summary>เลขใบกำกับของผู้ขายที่ใบต้นทางบันทึกไว้ (SupplierInvoiceNumber)</summary>
        public string SourceSupplierDocNo { get; set; }
        /// <summary>บรรทัดของใบต้นทาง (GET document) — แสดงข้างฟอร์ม + จับคู่บัญชี</summary>
        public List<OcrSourceLine> SourceLines { get; set; } = new List<OcrSourceLine>();
        /// <summary>บัญชีเงินที่ใบต้นทางจ่ายออก (PV.PaymentAccountId หรือขาเครดิตเงินของ JE การจ่าย) — ค่าเริ่มต้นของบัญชีรับเงินคืน</summary>
        public string SourcePaidAccountId { get; set; }
        public string SourcePaidAccountLabel { get; set; }
        /// <summary>CN: CreditNoteReason 1-4 (AllEnums.cs:1636-1646) · DN: DebitNoteReason 1-4 (AllEnums.cs:1652-1662) — ตั้งจาก <see cref="NoteReasonCode"/></summary>
        public int NoteReason { get; set; }
        /// <summary>เหตุผลของเรา (<see cref="OcrNoteReasons"/>) เช่น SHORT = ผู้ขายไม่มีสินค้า/ส่งไม่ครบ</summary>
        public string NoteReasonCode { get; set; }
        public string NoteReasonText { get; set; }
        // ── §86/10 บนกระดาษใบลด/เพิ่มหนี้: อ้างใบกำกับเดิม + มูลค่าเดิม/ที่ถูกต้อง/ผลต่าง ──
        public string OrigTaxInvoiceNo { get; set; }
        /// <summary>yyyy-MM-dd</summary>
        public string OrigTaxInvoiceDate { get; set; }
        public decimal? OrigAmount { get; set; }
        public decimal? CorrectAmount { get; set; }
        public decimal? DiffAmount { get; set; }
        /// <summary>วิธีชำระที่พิมพ์บนใบลดหนี้ (เช่น "Credit Card/Debit Card") — คำใบ้เลือกบัญชีรับเงินคืน</summary>
        public string PaperPaymentType { get; set; }
        public string Settlement { get; set; } = OcrNoteSettlement.Auto;
        /// <summary>ผู้ใช้เลือกขาเงินเอง — ระบบไม่ตั้งค่าเริ่มต้นทับ</summary>
        public bool SettlementUserSet { get; set; }
        /// <summary>บัญชีรับคืน/จ่าย (ChartOfAccount id) เมื่อ Settlement = BANK</summary>
        public string RefundAccountId { get; set; }
        /// <summary>เลขอ้างอิงการคืนสินค้า PRT- — SourceMode = PRT (หลักฐาน, นโยบายเดิม) หรือใบลดหนี้ NEXTACC/OUTSIDE ที่ผูกการคืน
        /// (PHASE19_39: ใบลดหนี้เป็นตัวจริง → หลังอนุมัติกลับ JE PRT-…-REV + ปรับต้นทุน PRT-…-DIF)</summary>
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
        public decimal Gross;           // round(จำนวน × ราคา) — ฐานเดียวกับราคา (รวม/ไม่รวม VAT)
        public decimal Amount;          // Gross − ส่วนลด (ยอดตามกระดาษของบรรทัด)
        public decimal Net;             // ฐานก่อน VAT
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
    /// <summary>ปุ่มแก้ด่วน 1 แตะ (ตัวเดียวกับหน้าเว็บ — OcrReviewMath.SuggestFixes / OcrMath.suggestFixes)</summary>
    public sealed class OcrFix
    {
        /// <summary>SWITCH_BASIS · FIT_VAT · FILL_MISSING · ADD_GAP</summary>
        public string Code;
        public string Label;
        public string Hint;
    }

    public sealed class OcrValidation
    {
        public List<string> Errors = new List<string>();
        public List<string> Warnings = new List<string>();
        public List<OcrFix> Fixes = new List<OcrFix>();
        public List<OcrReconItem> Recon = new List<OcrReconItem>();
        public OcrTotals Totals;
        /// <summary>§86/4 ที่ยังขาด (ว่าง = ครบ) — ขาด + เคลม VAT ⇒ NextAcc ลง 11640 (TaxInvoiceCompletenessChecker.cs:5-10)</summary>
        public List<string> MissingTaxInvoiceFields = new List<string>();
        public bool CanSubmit => Errors.Count == 0;
    }
}
