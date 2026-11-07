using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Take_Time_BangPhra.Integration
{
    // ══════════════════════════════════════════════════════════════════════════════════════════
    //  CLAUDE.md ข้อ 25 — ใช้ความสามารถ NextAcc ที่ยังไม่เคยเรียก (ต.ค. 2026) · ส่วน "บริสุทธิ์" (ไม่แตะ DB/HTTP — mono test ได้)
    //   ① ออกใบกำกับภาษีเต็มรูป "แทน" ใบเสร็จ/ใบกำกับอย่างย่อ — POST document/{id}/issue-full-tax-invoice
    //   ② เปลี่ยนแหล่งเงินหลังอนุมัติ (ไม่ void) — POST document/{id}/reclassify-payment-source
    //   ③ ภาษีซื้อยังไม่ถึงกำหนด 11640 — GET document/undue-input-vat + POST document/{id}/complete-tax-invoice
    //   ④ e-Tax ใบลดหนี้ (TypeCode 81) + ส่งอีเมลเอกสารจาก NextAcc
    //  สัญญา (Wachira-d/Accounting HEAD 9c5680d) อ้างบรรทัดไว้ที่แต่ละคลาส · ตัวตัดสินจริงอยู่ฝั่ง NextAcc เสมอ —
    //  ตัวตรวจฝั่งเรามีไว้ "บอกก่อนยิง" เป็นภาษาไทย ไม่ใช่ทดแทนด่านของ NextAcc
    // ══════════════════════════════════════════════════════════════════════════════════════════

    // ── DTO (ตรงตาม NextAcc — ส่งผ่าน AccountingApiClient: camelCase + ละ null) ─────────────────────

    /// <summary>NextAcc <c>IssueFullTaxInvoiceRequest(string? Reason)</c> — DocumentController.cs:1125</summary>
    public class NxIssueFullTaxInvoiceRequest
    {
        public string Reason { get; set; }
    }

    /// <summary>NextAcc <c>ReclassifyPaymentSourceRequest(Guid? NewBankAccountId, Guid? NewPaymentAccountId, string? Reason)</c>
    /// — DocumentDtos.cs:1309-1310 (ระบุทางใดทางหนึ่ง; TakeTime ส่ง NewPaymentAccountId = ผังบัญชีของแหล่งเงิน)</summary>
    public class NxReclassifyPaymentSourceRequest
    {
        public Guid? NewBankAccountId { get; set; }
        public Guid? NewPaymentAccountId { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>NextAcc <c>UndueInputVatSummary</c> — DocumentDtos.cs:626-640</summary>
    public class NxUndueInputVatSummary
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; }
        public DateTime DocumentDate { get; set; }
        public string SupplierName { get; set; }
        public string SupplierTaxId { get; set; }
        public decimal VatAmount { get; set; }
        public int AgeDays { get; set; }
        public int MonthsLeft { get; set; }
        public bool IsExpired { get; set; }
        public List<string> MissingFields { get; set; }
        public bool IsForeignService { get; set; }
    }

    /// <summary>NextAcc <c>CompleteSupplierTaxInvoiceRequest</c> — DocumentDtos.cs:395-410 (ทุกช่อง null = คงค่าเดิม).
    /// เลขผู้เสียภาษี/ที่อยู่ของผู้ขายอยู่ที่ "ผู้ติดต่อ" (TaxInvoiceCompletenessChecker.Evaluate :40-47) ⇒ แก้ผ่าน PUT document/contacts/{id}</summary>
    public class NxCompleteSupplierTaxInvoiceRequest
    {
        public string SupplierInvoiceNumber { get; set; }
        public DateTime? SupplierTaxInvoiceDate { get; set; }
        public string SupplierBranchCode { get; set; }
    }

    /// <summary>NextAcc <c>SendDocumentEmailRequest(To, Cc, Bcc, Subject, Body, AttachPdf=true, AttachXml=false)</c> — Email/EmailDtos.cs:6-13</summary>
    public class NxSendDocumentEmailRequest
    {
        public string To { get; set; }
        public string Cc { get; set; }
        public string Bcc { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool AttachPdf { get; set; } = true;
        public bool AttachXml { get; set; }
    }

    /// <summary>NextAcc <c>DocumentEmailLogResponse</c> (เฉพาะช่องที่ใช้) — คำตอบของ send-email</summary>
    public class NxDocumentEmailLog
    {
        public Guid Id { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
    }

    // ── ① ใบกำกับภาษีเต็มรูปทีหลัง ─────────────────────────────────────────────────────────────

    /// <summary>ข้อมูลผู้ซื้อ §86/4 ที่ผ่านการตรวจแล้ว</summary>
    public sealed class FullTaxInvoiceBuyer
    {
        public string Name;
        public string TaxId;        // 13 หลัก (ตัวเลขล้วน)
        public string BranchCode;   // 5 หลัก ("00000" = สำนักงานใหญ่) — ว่างได้สำหรับบุคคลธรรมดา
        public string Address;
        public bool IsJuristic;
        public string Phone;        // เบอร์ (คีย์ลูกค้าในระบบ) — ว่างได้ถ้าใบมีผู้ซื้ออยู่แล้ว
    }

    public enum FullTaxInvoicePath
    {
        /// <summary>ทำต่อไม่ได้ — ดูข้อความ</summary>
        Blocked = 0,
        /// <summary>เคยออกใบแทนแล้ว (ReplacedByDocumentId) — ไม่ออกซ้ำ</summary>
        AlreadyReplaced = 1,
        /// <summary>มีใบแทนที่ค้างเป็นร่าง (อนุมัติไม่ผ่านรอบก่อน) — อนุมัติใบนั้นแทนการออกใบใหม่</summary>
        ApproveDraftReplacement = 2,
        /// <summary>แก้ผู้ติดต่อของเอกสารให้ครบ §86/4 แล้วตัดสินใหม่จากค่าที่ NextAcc คำนวณ (PlanAfterContact)</summary>
        UpdateContact = 3,
        /// <summary>ผู้ซื้อบนเอกสารเป็นผู้ติดต่อกลาง/คนละราย หรือใบกำกับที่ติดธงไม่ประสงค์รับใบกำกับ — ต้องยกเลิกแล้วออกใหม่ (เลขเปลี่ยน)</summary>
        NeedRecreate = 4,
        /// <summary>ออกใบแทนได้ — เรียก issue-full-tax-invoice</summary>
        IssueReplacement = 5,
        /// <summary>เอกสารเดิมพิมพ์เป็นใบกำกับเต็มรูปแล้วหลังแก้ผู้ติดต่อ (เลขเดิม) — ไม่ต้องออกใบใหม่</summary>
        ContactUpgrade = 6,
    }

    /// <summary>ข้อเท็จจริงของเอกสาร NextAcc ที่ใช้ตัดสิน (จาก GET document/{id} — DocumentDtos.cs:646-973)</summary>
    public sealed class FullTaxInvoiceFacts
    {
        public int DocumentType;          // 3 Receipt · 4 TaxInvoice · 14 ReceiptVoucher (AllEnums DocumentType)
        public int Status;                // NexaaccDocumentStatus
        public decimal VatAmount;
        public bool BuyerDeclined;        // BuyerDeclinedTaxInvoice (DocumentDtos.cs:906)
        public string ContactName;
        public string ContactTaxId;
        public bool SharedWalkInContact;  // ผู้ติดต่อกลาง "ลูกค้าเงินสด…" — ห้ามแก้ (กระทบทุกใบ)
        public bool EtaxReachedRd;        // e-Tax Submitted/Accepted
        public Guid? ReplacedByDocumentId;
        public string ReplacedByDocumentNumber;
        public bool HasActiveCreditNote;  // ใบลดหนี้ของ TakeTime อ้างใบนี้อยู่
        public Guid? DraftReplacementId;  // ใบแทนที่ค้างร่าง (RelatedDocumentId = ใบนี้, TaxInvoice, Draft)
    }

    public sealed class FullTaxInvoiceDecision
    {
        public FullTaxInvoicePath Path;
        public string Message;
        public FullTaxInvoiceDecision(FullTaxInvoicePath p, string m) { Path = p; Message = m; }
    }

    /// <summary>
    /// ตัดสินเส้นทาง "ลูกค้าขอใบกำกับภาษีเต็มรูปทีหลัง"
    /// สัญญา NextAcc: <c>POST document/{id}/issue-full-tax-invoice</c> (DocumentController.cs:1138-1157, สิทธิ์ระดับ Approve) →
    /// <c>DocumentService.IssueFullTaxInvoiceForReceiptAsync</c> (:13109-13173): ต้นทางต้องเป็น Receipt/ReceiptVoucher (:13116-13120
    /// RD-86/4-REPLACE-SOURCE-TYPE) · ผ่าน <c>FullTaxInvoiceReplacement.Check</c> (Helpers/FullTaxInvoiceReplacement.cs:56-90: จด VAT ·
    /// อนุมัติแล้ว · ไม่ใช่เต็มรูปอยู่แล้ว · มี VAT · ยังไม่เคยออกใบแทน · ผู้ซื้อครบ §86/4 ที่ "ผู้ติดต่อของใบต้นทาง") · ใบแทน = TaxInvoice
    /// วันที่เดิม ไม่มี JE ใหม่ (:7258-7273) อนุมัติทันที — อนุมัติไม่ผ่าน (งวดปิด ฯลฯ) = ใบแทนค้างร่าง + RD-86/4-REPLACE-APPROVE-FAILED (:13156-13173)
    /// · "เต็มรูปอยู่แล้ว" = TaxInvoice หรือ ผู้ซื้อครบ+ไม่ declined+ไม่ใช่ walk-in (:13095-13097) ⇒ ใบเสร็จของผู้ซื้อตัวจริงแค่แก้ผู้ติดต่อ
    /// ก็พิมพ์เต็มรูป (หัวคำนวณสดจากผู้ติดต่อ PdfGenerationService.cs:1498 ComputeDocumentTitle; ชื่อพิมพ์จาก doc.Contact :2154 — ไม่มี snapshot)
    /// · เปลี่ยน ContactId ของเอกสารที่ลงบัญชีแล้วไม่ได้ (UpdateDocumentAsync :2658-2662) ⇒ ผู้ติดต่อกลาง/คนละราย = ยกเลิกแล้วออกใหม่
    /// </summary>
    public static class FullTaxInvoicePlanner
    {
        public const int TypeReceipt = 3, TypeTaxInvoice = 4, TypeReceiptVoucher = 14;

        public static FullTaxInvoiceDecision PlanBeforeContact(FullTaxInvoiceFacts f, string formTaxId)
        {
            if (f == null) return Block("ไม่พบเอกสารบน NextAcc ของใบนี้ — ส่งใบเสร็จเข้า NextAcc ให้สำเร็จก่อน");
            if (f.Status == NexaaccDocumentStatus.Voided)
                return Block("เอกสารบน NextAcc ถูกยกเลิกแล้ว — ส่งใบเสร็จใหม่ก่อน แล้วค่อยออกใบกำกับเต็มรูป");
            if (f.Status == NexaaccDocumentStatus.Draft || f.Status == NexaaccDocumentStatus.WaitingApproval || f.Status == NexaaccDocumentStatus.Rejected)
                return Block("เอกสารบน NextAcc ยังไม่อนุมัติ — แก้ข้อมูลผู้ซื้อในใบเสร็จแล้วส่งใหม่ได้เลย (ยังไม่ใช่เอกสารที่ออกไปแล้ว)");
            if (f.ReplacedByDocumentId.HasValue && f.ReplacedByDocumentId.Value != Guid.Empty)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.AlreadyReplaced,
                    "ใบนี้ออกใบกำกับภาษีเต็มรูปแทนไปแล้ว" + (string.IsNullOrEmpty(f.ReplacedByDocumentNumber) ? "" : " (" + f.ReplacedByDocumentNumber + ")")
                    + " — การขายครั้งเดียวมีใบกำกับได้ใบเดียว");
            if (f.HasActiveCreditNote)
                return Block("ใบนี้มีใบลดหนี้อ้างอยู่ — ออกใบกำกับเต็มรูปแทนไม่ได้ (ยอดใบแทนต้องเท่าใบเดิมทุกสตางค์) · ยกเลิกใบลดหนี้ก่อน หรือออกใบกำกับใหม่ผ่านใบลดหนี้");
            if (f.EtaxReachedRd)
                return Block("ใบนี้ส่ง e-Tax ถึงกรมสรรพากรแล้ว — ข้อมูลผู้ซื้อใน e-Tax ที่ยื่นไปเปลี่ยนไม่ได้ · ออกใบลดหนี้เต็มจำนวนแล้วออกใบใหม่ในนามผู้ซื้อ");
            if (f.DocumentType != TypeReceipt && f.DocumentType != TypeTaxInvoice && f.DocumentType != TypeReceiptVoucher)
                return Block("เอกสารชนิดนี้บน NextAcc ไม่ใช่ใบเสร็จ/ใบกำกับภาษี — ออกใบกำกับเต็มรูปแทนไม่ได้");
            if (f.VatAmount <= 0.005m)
                return Block("เอกสารนี้ไม่มีภาษีมูลค่าเพิ่ม (เช่น ใบรับมัดจำที่ VAT รับรู้ตอนเช็คเอาท์) — ไม่มีภาษีขายให้ใบกำกับรับรอง · "
                    + "ใบกำกับภาษีจะออกที่ใบเช็คเอาท์ (ทำที่ใบนั้นแทน)");
            if (f.SharedWalkInContact)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.NeedRecreate,
                    "เอกสารผูกผู้ติดต่อกลาง \"" + (f.ContactName ?? "") + "\" (ลูกค้าไม่ประสงค์รับใบกำกับ) — NextAcc เปลี่ยนผู้ซื้อของเอกสารที่ลงบัญชีแล้วไม่ได้ "
                    + "และห้ามแก้ผู้ติดต่อกลาง ⇒ ต้องยกเลิกเอกสารเดิมแล้วออกใหม่ในนามผู้ซื้อ (เลขเอกสาร NextAcc เปลี่ยน)");
            string onDoc = Digits(f.ContactTaxId);
            string want = Digits(formTaxId);
            if (onDoc.Length == 13 && want.Length == 13 && onDoc != want)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.NeedRecreate,
                    "ผู้ติดต่อบนเอกสารมีเลขผู้เสียภาษี " + onDoc + " ≠ ที่ลูกค้าขอ " + want + " (คนละราย) — ต้องยกเลิกเอกสารเดิมแล้วออกใหม่ในนามผู้ซื้อ "
                    + "(แก้ผู้ติดต่อเดิมจะไปเปลี่ยนเอกสารทุกใบของรายนั้น)");
            if (f.DocumentType == TypeTaxInvoice && f.BuyerDeclined)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.NeedRecreate,
                    "ใบกำกับภาษีนี้ออกแบบ \"ผู้ซื้อไม่ประสงค์รับใบกำกับภาษี\" — ธงนี้แก้หลังอนุมัติไม่ได้ และ NextAcc ออกใบแทนได้เฉพาะใบเสร็จ "
                    + "⇒ ต้องยกเลิกเอกสารเดิมแล้วออกใหม่ (เลขเอกสาร NextAcc เปลี่ยน)");
            if ((f.DocumentType == TypeReceipt || f.DocumentType == TypeReceiptVoucher) && f.DraftReplacementId.HasValue
                && f.DraftReplacementId.Value != Guid.Empty)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.ApproveDraftReplacement,
                    "พบใบกำกับเต็มรูป (ร่าง) ที่ออกแทนใบนี้ค้างจากครั้งก่อน — อนุมัติใบนั้นแทนการออกใบใหม่ (กันใบแทนซ้ำ)");
            return new FullTaxInvoiceDecision(FullTaxInvoicePath.UpdateContact, null);
        }

        /// <summary>หลังแก้ผู้ติดต่อ: อ่านค่าที่ NextAcc คำนวณเอง (CanIssueFullTaxInvoice/FullTaxInvoiceBlockedReason DocumentService.cs:2075-2083,
        /// DocumentTitle :940) — ห้ามเดากติกาซ้ำฝั่งเรา</summary>
        public static FullTaxInvoiceDecision PlanAfterContact(int documentType, bool? canIssue, string blockedReason,
            string documentTitle, string titleNotice)
        {
            bool receiptLike = documentType == TypeReceipt || documentType == TypeReceiptVoucher;
            if (receiptLike && canIssue == true)
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.IssueReplacement, null);
            if (TitleIsFull(documentTitle))
                return new FullTaxInvoiceDecision(FullTaxInvoicePath.ContactUpgrade,
                    "เอกสารเดิมพิมพ์เป็น \"" + documentTitle.Trim() + "\" ในนามผู้ซื้อแล้ว (เลขเดิม) — ไม่ต้องออกใบใหม่ ส่ง PDF ล่าสุดให้ลูกค้า");
            string why = !string.IsNullOrWhiteSpace(blockedReason) ? blockedReason.Trim()
                : !string.IsNullOrWhiteSpace(titleNotice) ? titleNotice.Trim()
                : "NextAcc ยังพิมพ์หัวเอกสารเป็น \"" + (documentTitle ?? "-") + "\"";
            return Block("ออกใบกำกับเต็มรูปแทนไม่ได้: " + why);
        }

        /// <summary>หัวกระดาษเป็นใบกำกับภาษีเต็มรูปหรือไม่ (มีคำว่าใบกำกับภาษี และไม่ใช่ "อย่างย่อ")</summary>
        public static bool TitleIsFull(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return false;
            return title.IndexOf("ใบกำกับภาษี", StringComparison.Ordinal) >= 0
                && title.IndexOf("อย่างย่อ", StringComparison.Ordinal) < 0;
        }

        private static FullTaxInvoiceDecision Block(string m) { return new FullTaxInvoiceDecision(FullTaxInvoicePath.Blocked, m); }

        internal static string Digits(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s) if (c >= '0' && c <= '9') sb.Append(c);
            return sb.ToString();
        }
    }

    /// <summary>ตรวจข้อมูลผู้ซื้อ/ผู้ขาย §86/4 จากฟอร์ม</summary>
    public static class TaxPartyValidator
    {
        /// <summary>เลขผู้เสียภาษี 13 หลัก + mod-11 — สูตรเดียวกับ NextAcc TaxInvoiceCompletenessChecker.IsValidThaiTaxId (:157)
        /// (Σ d[i]×(13−i), i=0..11 · check = (11 − sum%11) % 10) และ ReceiptPreflight.IsValidThaiTaxId</summary>
        public static bool IsValidThaiTaxId(string raw)
        {
            string d = FullTaxInvoicePlanner.Digits(raw);
            if (d.Length != 13) return false;
            int sum = 0;
            for (int i = 0; i < 12; i++) sum += (d[i] - '0') * (13 - i);
            return (11 - sum % 11) % 10 == d[12] - '0';
        }

        /// <summary>ผู้ซื้อของใบกำกับเต็มรูป: ชื่อ + ที่อยู่ (§86/4(3)) + เลขผู้เสียภาษี 13 หลัก mod-11 (ผู้ขอใบเต็มรูปเพื่อเคลมภาษีซื้อ — ประกาศ
        /// อธิบดีฯ VAT 194/2556; บุคคลธรรมดาใช้เลขบัตรประชาชน) + สาขา 5 หลัก (นิติบุคคล: ว่าง = 00000 สำนักงานใหญ่ ตาม NextAcc
        /// MissingBuyerFields TaxInvoiceCompletenessChecker.cs:97-120). คืนรายการข้อผิดพลาด (ว่าง = ผ่าน) และค่าที่ทำความสะอาดแล้ว</summary>
        public static List<string> ValidateBuyer(string name, string taxId, string branch, string address, bool? juristic,
            string phone, out FullTaxInvoiceBuyer buyer)
        {
            var err = new List<string>();
            buyer = new FullTaxInvoiceBuyer();
            string n = Clean(name);
            if (n.Length == 0) err.Add("กรอกชื่อผู้ซื้อ (ชื่อบริษัท/ชื่อ-นามสกุล ตามที่ต้องการให้พิมพ์บนใบกำกับ)");
            else if (n.Length > 200) err.Add("ชื่อผู้ซื้อยาวเกิน 200 ตัวอักษร");
            string t = FullTaxInvoicePlanner.Digits(taxId);
            if (t.Length == 0) err.Add("กรอกเลขประจำตัวผู้เสียภาษี 13 หลัก (บุคคลธรรมดา = เลขบัตรประชาชน)");
            else if (t.Length != 13) err.Add("เลขประจำตัวผู้เสียภาษีต้องมี 13 หลัก (กรอกมา " + t.Length + " หลัก)");
            else if (!IsValidThaiTaxId(t)) err.Add("เลขประจำตัวผู้เสียภาษี " + t + " ไม่ถูกต้อง (หลักตรวจสอบไม่ตรง) — ตรวจเลขอีกครั้ง");
            bool isJ = juristic ?? (t.Length == 13 && t[0] == '0');
            string b = FullTaxInvoicePlanner.Digits(branch);
            if (b.Length == 0 && isJ) b = "00000";
            if (b.Length > 0 && b.Length != 5) err.Add("รหัสสาขาต้องเป็นตัวเลข 5 หลัก (00000 = สำนักงานใหญ่)");
            string a = Clean(address);
            if (a.Length == 0) err.Add("กรอกที่อยู่ผู้ซื้อ (§86/4 บังคับชื่อและที่อยู่)");
            else if (a.Length < 10) err.Add("ที่อยู่ผู้ซื้อสั้นเกินไป — กรอกเลขที่ ถนน/ตำบล อำเภอ จังหวัด รหัสไปรษณีย์");
            else if (a.Length > 500) err.Add("ที่อยู่ยาวเกิน 500 ตัวอักษร");
            string p = FullTaxInvoicePlanner.Digits(phone);
            if (p.Length > 0 && (p.Length < 9 || p.Length > 10)) err.Add("เบอร์โทรต้องมี 9-10 หลัก");
            buyer.Name = n; buyer.TaxId = t; buyer.BranchCode = b; buyer.Address = a; buyer.IsJuristic = isJ; buyer.Phone = p;
            return err;
        }

        /// <summary>ฟอร์มเติมใบกำกับภาษีซื้อ (ภาษีซื้อพัก 11640): เลขที่ + วันที่ใบกำกับของผู้ขาย + สาขา (บังคับ — TaxInvoiceCompletenessChecker.cs:49-59) และ
        /// เลขผู้เสียภาษี/ที่อยู่ของผู้ขายเมื่อผู้ติดต่อยังขาด (:40-47). วันที่ใบกำกับต้องไม่เกินวันนี้</summary>
        public static List<string> ValidateSupplierInvoice(string invoiceNo, DateTime? invoiceDate, string branch, string taxId,
            string address, bool contactTaxIdOk, bool contactAddressOk, DateTime today)
        {
            var err = new List<string>();
            string no = Clean(invoiceNo);
            if (no.Length == 0) err.Add("กรอกเลขที่ใบกำกับภาษีของผู้ขาย");
            else if (no.Length > 60) err.Add("เลขที่ใบกำกับยาวเกิน 60 ตัวอักษร");
            if (!invoiceDate.HasValue) err.Add("กรอกวันที่ในใบกำกับภาษีของผู้ขาย");
            else if (invoiceDate.Value.Date > today.Date) err.Add("วันที่ใบกำกับภาษีเป็นวันในอนาคต");
            else if (invoiceDate.Value.Year < 2000) err.Add("วันที่ใบกำกับภาษีไม่ถูกต้อง (ใช้ปี ค.ศ.)");
            string b = FullTaxInvoicePlanner.Digits(branch);
            if (b.Length != 5) err.Add("รหัสสาขาผู้ขายต้องเป็นตัวเลข 5 หลัก (00000 = สำนักงานใหญ่)");
            string t = FullTaxInvoicePlanner.Digits(taxId);
            if (t.Length > 0 && !IsValidThaiTaxId(t)) err.Add("เลขผู้เสียภาษีผู้ขาย " + t + " ไม่ถูกต้อง (13 หลัก + หลักตรวจสอบ)");
            else if (t.Length == 0 && !contactTaxIdOk) err.Add("ผู้ติดต่อของผู้ขายยังไม่มีเลขผู้เสียภาษีที่ถูกต้อง — กรอกเลข 13 หลัก");
            if (Clean(address).Length == 0 && !contactAddressOk) err.Add("ผู้ติดต่อของผู้ขายยังไม่มีที่อยู่ — กรอกที่อยู่ตามใบกำกับ");
            return err;
        }

        internal static string Clean(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            return Regex.Replace(s.Trim(), @"\s+", " ");
        }
    }

    // ── ② เปลี่ยนแหล่งเงิน ────────────────────────────────────────────────────────────────────────

    public enum PaymentSourceReclassVerdict { Blocked = 0, LocalOnly = 1, CallNextAcc = 2 }

    public sealed class PaymentSourceReclassFacts
    {
        public bool HasNextAccDoc;
        public int DocumentType;
        public int Status;
        public Guid? CurrentPaymentAccountId;   // DocumentResponse.PaymentAccountId (DocumentDtos.cs:678)
        public Guid? CurrentBankAccountId;      // DocumentResponse.BankAccountId (:676)
        public Guid? NewAccountId;              // Account_Paid_How.Nexaacc_AccountId ของแหล่งเงินใหม่
        public decimal DepositAppliedAmount;    // ใบที่หักมัดจำในใบ (JE ปรับปรุงของ TakeTime Cr เงินสดเดิม)
        public bool EtaxReachedRd;
        public bool HasActiveCreditNote;
        public bool ReplacedByFullTaxInvoice;   // มีใบแทนอ้างใบนี้ (= เอกสารปลายทาง)
        public bool IsCompanyDocMode;           // company endpoint ใช้ได้
        public bool PeriodClosedKnown;          // cache งวดบัญชี (Accounting_Nexaacc_Period_Status ข้อ 21) บอกว่างวดของวันที่เอกสารไม่ Open
    }

    /// <summary>
    /// ตรวจก่อนยิง <c>POST document/{id}/reclassify-payment-source</c> (DocumentController.cs:739-757, สิทธิ์ Approve) →
    /// <c>DocumentService.ReclassifyPaymentSourceAsync</c> (:9067-9240): ต้องมีแหล่งเงินบนหัวเอกสาร (BankAccountId/PaymentAccountId :9082-9085) ·
    /// สถานะอนุมัติแล้ว (:9088-9092) · งวดบัญชี Open (:9095-9100) · ไม่มีเอกสารปลายทาง RelatedDocumentId (:9102-9107) · ไม่มีรายการชำระแยก (:9111-9116) ·
    /// ไม่อยู่ในรายงานภาษีที่ยื่นแล้ว (:9118-9124) · ไม่ได้ส่ง e-Tax (:9126-9129) · แหล่งเงินใหม่ผ่าน DocumentMoneyChannel (:9134-9139) ·
    /// ยอด = ขาเงินตาม JE ของเอกสาร (:9160-9190) · บัญชีเดียวกัน = แก้ช่องอย่างเดียว (:9195-9205) · JE แก้ (เงินเข้า: Dr ใหม่/Cr เก่า) ลงวันที่เอกสารเดิม (:9207-9225).
    /// ตัวตรวจนี้กันกรณีที่ NextAcc "ผ่าน" แต่บัญชีฝั่งเราจะเพี้ยน (ใบหักมัดจำ: JE ปรับปรุงของ TakeTime Cr บัญชีเงินเดิมไว้ — ย้ายแค่ขาเอกสาร
    /// = บัญชีเดิมติดลบเท่ายอดหัก) และแปลข้อจำกัดที่รู้ล่วงหน้าเป็นภาษาไทย; งวดปิด/ยื่นภาษีแล้ว ให้ NextAcc ตัดสิน (ข้อความไทยจาก NextAcc)
    /// </summary>
    public static class PaymentSourceReclassPolicy
    {
        public static PaymentSourceReclassVerdict Check(PaymentSourceReclassFacts f, out string message)
        {
            message = null;
            if (f == null || !f.HasNextAccDoc)
            {
                message = "ใบนี้ยังไม่มีเอกสารบน NextAcc — แก้แหล่งเงินในระบบได้เลย (จะส่งด้วยแหล่งเงินใหม่ตอน sync)";
                return PaymentSourceReclassVerdict.LocalOnly;
            }
            if (!f.NewAccountId.HasValue || f.NewAccountId.Value == Guid.Empty)
            {
                message = "แหล่งเงินที่เลือกยังไม่ได้ผูกบัญชี NextAcc — ผูกที่ Admin → Accounting Integration → แหล่งเงิน ก่อน";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (!f.IsCompanyDocMode)
            {
                message = "การเชื่อมต่อนี้เรียก company endpoint ไม่ได้ (Nexaacc_Company_Endpoints/CompanyId) — เปลี่ยนแหล่งเงินโดยไม่ยกเลิกเอกสารไม่ได้";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (f.Status == NexaaccDocumentStatus.Voided)
            {
                message = "เอกสารบน NextAcc ถูกยกเลิกแล้ว — แก้แหล่งเงินในระบบแล้วส่งใบใหม่";
                return PaymentSourceReclassVerdict.LocalOnly;
            }
            if (f.Status == NexaaccDocumentStatus.Draft || f.Status == NexaaccDocumentStatus.WaitingApproval || f.Status == NexaaccDocumentStatus.Rejected)
            {
                message = "เอกสารบน NextAcc ยังไม่อนุมัติ — แก้แหล่งเงินที่หน้าใบเสร็จตามปกติ (ยังไม่มีรายการบัญชีให้ย้าย)";
                return PaymentSourceReclassVerdict.Blocked;
            }
            bool hasMoney = (f.CurrentPaymentAccountId.HasValue && f.CurrentPaymentAccountId.Value != Guid.Empty)
                         || (f.CurrentBankAccountId.HasValue && f.CurrentBankAccountId.Value != Guid.Empty);
            if (!hasMoney)
            {
                message = f.DocumentType == FullTaxInvoicePlanner.TypeTaxInvoice
                    ? "ใบกำกับภาษีใบนี้รับเงินด้วย \"รายการรับชำระ\" แยก (ไม่มีแหล่งเงินบนหัวเอกสาร) — NextAcc เปลี่ยนแหล่งเงินด้วยวิธีนี้ไม่ได้ "
                      + "(ต้องแก้ที่รายการชำระ ซึ่งยังไม่มี API) · ใช้การแก้ใบเสร็จตามปกติ (ยกเลิก→ออกใหม่)"
                    : "เอกสารนี้ไม่มีแหล่งเงินบนหัวเอกสาร (เช่น ใบตั้งหนี้/ลูกหนี้) — เปลี่ยนด้วยวิธีนี้ไม่ได้ · ใช้การแก้ใบเสร็จตามปกติ";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (f.CurrentPaymentAccountId.HasValue && f.CurrentPaymentAccountId.Value == f.NewAccountId.Value)
            {
                message = "บัญชี NextAcc ของแหล่งเงินใหม่เป็นบัญชีเดียวกับที่เอกสารใช้อยู่ — ไม่ต้องแก้บน NextAcc (ปรับเฉพาะในระบบ)";
                return PaymentSourceReclassVerdict.LocalOnly;
            }
            if (f.PeriodClosedKnown)
            {
                message = "งวดบัญชีของวันที่เอกสารปิดแล้วบน NextAcc — แก้แหล่งเงินย้อนหลังไม่ได้ (NextAcc บังคับงวด Open) · บันทึกสมุดรายวันย้ายเงินในงวดปัจจุบันแทน";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (f.DepositAppliedAmount > 0.005m)
            {
                message = "ใบนี้หักมัดจำ " + f.DepositAppliedAmount.ToString("#,##0.00", CultureInfo.InvariantCulture)
                    + " บาทในใบ (รายการปรับปรุงของระบบเครดิตบัญชีเงินเดิมไว้) — ย้ายแหล่งเงินเฉพาะเอกสารจะทำให้บัญชีเดิมติดลบ · ใช้การแก้ใบเสร็จตามปกติ (ยกเลิก→ออกใหม่)";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (f.EtaxReachedRd)
            {
                message = "ใบนี้ส่ง e-Tax ถึงกรมสรรพากรแล้ว — NextAcc ไม่ให้เปลี่ยนแหล่งเงิน (DocumentService ด่าน e-Tax) · บันทึกสมุดรายวันย้ายเงินระหว่างบัญชีแทน";
                return PaymentSourceReclassVerdict.Blocked;
            }
            if (f.HasActiveCreditNote || f.ReplacedByFullTaxInvoice)
            {
                message = "มีเอกสารอ้างใบนี้อยู่แล้ว (" + (f.HasActiveCreditNote ? "ใบลดหนี้" : "ใบกำกับเต็มรูปที่ออกแทน")
                    + ") — NextAcc ไม่ให้เปลี่ยนแหล่งเงินของเอกสารที่มีเอกสารปลายทาง · บันทึกสมุดรายวันย้ายเงินระหว่างบัญชีแทน";
                return PaymentSourceReclassVerdict.Blocked;
            }
            return PaymentSourceReclassVerdict.CallNextAcc;
        }

        /// <summary>เหตุผลการเปลี่ยนแหล่งเงิน (บันทึกให้ผู้สอบบัญชี) — อย่างน้อย 10 ตัวอักษร (เหมือนเหตุผลยืนยันข้ามข้อ 21)</summary>
        public static string ValidateReason(string reason)
        {
            string r = TaxPartyValidator.Clean(reason);
            if (r.Length < 10) return "ระบุเหตุผลอย่างน้อย 10 ตัวอักษร (เช่น \"พนักงานเลือกโอนกสิกรผิด เงินเข้ากรุงไทย\")";
            if (r.Length > 450) return "เหตุผลยาวเกิน 450 ตัวอักษร";
            return null;
        }
    }

    // ── ④ e-Tax ใบลดหนี้ ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// e-Tax ของใบลดหนี้ลูกค้า (TypeCode 81 — Helpers/EtaxDocumentTypeMap.cs:25-35): NextAcc <c>POST etax/generate {DocumentId, SignDigitally}</c>
    /// (EtaxController.cs:61-68, สิทธิ์ EtaxIssue; DocumentTemplateDtos.cs:388-390) → ด่านใน EtaxInvoiceService.GenerateAsync: อนุมัติแล้ว ·
    /// ไม่ซ้ำ (ยกเว้นแถว Error) · CN ต้องมี RelatedDocumentId และต้นทางเป็นฝั่งขาย (ใบลดหนี้ฝั่งซื้อออก e-Tax แทนผู้ขายไม่ได้)
    /// · ส่งกรมสรรพากร: <c>etax/{id}/submit</c> (EtaxController.cs:100-106, สิทธิ์ EtaxSubmit; ต้อง Signed) หรือ <c>sign-and-submit</c> (:210-220)
    /// </summary>
    public static class CreditNoteEtaxPolicy
    {
        /// <summary>ส่ง e-Tax ใบลดหนี้ได้หรือไม่ (ฝั่งเรา): ต้องมีเอกสาร NextAcc ชนิดใบลดหนี้ — เส้นทาง JOURNAL/LOCAL ไม่มีใบให้ส่ง</summary>
        public static string CanGenerate(string cnStatus, string path, bool hasDocId, string etaxStatus)
        {
            if (!string.Equals(cnStatus, "SYNCED", StringComparison.OrdinalIgnoreCase))
                return "ใบลดหนี้ยังไม่ได้ส่งเข้า NextAcc";
            if (!hasDocId) return "ไม่มีเอกสารใบลดหนี้บน NextAcc (เส้นทางคืนมัดจำที่ NextAcc ไม่ออกใบลดหนี้ / ลงสมุดรายวัน)";
            if (string.Equals(path, "JOURNAL", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "LOCAL", StringComparison.OrdinalIgnoreCase))
                return "ใบลดหนี้นี้ลงเป็นสมุดรายวัน — ไม่มีเอกสารให้ออก e-Tax";
            if (!string.IsNullOrEmpty(etaxStatus) && !IsRetryable(etaxStatus))
                return "ออก e-Tax ไปแล้ว (" + StatusLabel(etaxStatus) + ")";
            return null;
        }

        /// <summary>ส่งอัตโนมัติเมื่อเปิด <c>CreditNote_Etax_Auto=1</c> และใบต้นทางเคยส่ง e-Tax ถึงกรมสรรพากรแล้ว
        /// (ผู้ซื้อถือ e-Tax ใบเดิม ⇒ ใบลดหนี้ควรเป็น e-Tax คู่กัน)</summary>
        public static bool ShouldAuto(string configValue, bool sourceEtaxReachedRd)
        {
            return string.Equals((configValue ?? "").Trim(), "1", StringComparison.Ordinal) && sourceEtaxReachedRd;
        }

        public static bool IsRetryable(string s)
        {
            return string.Equals(s, "Error", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "FAILED", StringComparison.OrdinalIgnoreCase)
                || string.Equals(s, "Rejected", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>สร้างแล้วแต่ยังไม่ถึงกรมสรรพากร → มีปุ่ม "ส่งกรมสรรพากร"</summary>
        public static bool CanSubmit(string s)
        {
            return string.Equals(s, "Generated", StringComparison.OrdinalIgnoreCase) || string.Equals(s, "Signed", StringComparison.OrdinalIgnoreCase);
        }

        public static string StatusLabel(string s)
        {
            switch ((s ?? "").Trim().ToLowerInvariant())
            {
                case "": return "ยังไม่ส่ง";
                case "pending": return "กำลังสร้าง";
                case "generated": return "สร้างแล้ว (ยังไม่ลงนาม)";
                case "signed": return "ลงนามแล้ว (ยังไม่ส่งกรมสรรพากร)";
                case "submitted": return "ส่งกรมสรรพากรแล้ว";
                case "accepted": return "กรมสรรพากรรับแล้ว";
                case "rejected": return "กรมสรรพากรปฏิเสธ";
                case "error": case "failed": return "ไม่สำเร็จ";
                case "voided": return "ยกเลิกแล้ว";
                default: return s;
            }
        }
    }

    // ── อ่านข้อความ error ของ NextAcc ──────────────────────────────────────────────────────────────

    /// <summary>ข้อความ + ruleCode จาก body error ของ NextAcc ({ success:false, data:{ ruleCode }, message } — ExceptionMiddleware.cs:191-198)</summary>
    public static class NextAccErrorText
    {
        public static string Message(string body, out string ruleCode)
        {
            ruleCode = null;
            if (string.IsNullOrWhiteSpace(body)) return null;
            string b = body.Trim();
            if (!b.StartsWith("{", StringComparison.Ordinal)) return Cut(b, 400);
            try
            {
                var root = new System.Web.Script.Serialization.JavaScriptSerializer().DeserializeObject(b) as Dictionary<string, object>;
                if (root == null) return Cut(b, 400);
                object data;
                var dd = Get(root, "data", out data) ? data as Dictionary<string, object> : null;
                object rc;
                if (dd != null && Get(dd, "ruleCode", out rc) && rc != null) ruleCode = Convert.ToString(rc, CultureInfo.InvariantCulture);
                object m;
                if (Get(root, "message", out m) && m != null && Convert.ToString(m).Trim().Length > 0) return Cut(Convert.ToString(m).Trim(), 600);
                object errs;
                if (Get(root, "errors", out errs) && errs != null)
                    return Cut(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(errs), 600);
            }
            catch { }
            return Cut(b, 400);
        }

        private static bool Get(Dictionary<string, object> d, string key, out object v)
        {
            foreach (var kv in d)
                if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)) { v = kv.Value; return true; }
            v = null;
            return false;
        }

        private static string Cut(string s, int n) { return s == null ? null : (s.Length > n ? s.Substring(0, n) + "…" : s); }
    }
}
