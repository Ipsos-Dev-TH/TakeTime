using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Take_Time_BangPhra.Integration
{
    // ════════════════════════════════════════════════════════════════════════
    //  DTO สำหรับใบลดหนี้/คืนเงิน (PHASE19_33) — ตรงตาม Wachira-d/Accounting
    //  (Accounting/Models/DTOs/Document/DocumentDtos.cs) ใช้ผ่าน AccountingApiClient.GetAsync/PostAsync
    //  (company endpoint /api/companies/{cid}/document/* → X-Api-Key)
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// สร้างใบลดหนี้ (DocumentType = 6) — CreateDocumentRequest + ฟิลด์ของใบลดหนี้:
    /// RelatedDocumentId (DocumentDtos.cs:23 — เอกสารต้นทาง ใช้ cap §86/10, ฝั่งภาษี, ขาเงินสด/ลูกหนี้),
    /// CreditNoteReason (DocumentDtos.cs:77, AllEnums.cs:1636-1646 — Return=1 · Discount=2 · Adjustment=3 · Writeoff=4;
    /// บังคับตอนอนุมัติ DocumentService.cs:6518)
    /// </summary>
    public class CreditNoteDocumentRequest : CreateDocumentRequest
    {
        // RelatedDocumentId / CreditNoteReason อยู่ใน CreateDocumentRequest แล้ว (ชนิดเดียวกัน) — ไม่ประกาศซ้ำ (เดิมซ่อนของ base = CS0108)
    }

    /// <summary>POST /document/{depositId}/refund-deposit — DocumentDtos.cs:406-414
    /// (Amount = ยอดรวม VAT ที่คืน · MoneyAccountId = ผังบัญชีที่เงินออกจริง null = 111)</summary>
    public class NxRefundDepositRequest
    {
        public decimal Amount { get; set; }
        public DateTime? RefundDate { get; set; }
        public string Reason { get; set; }
        public Guid? MoneyAccountId { get; set; }
    }

    /// <summary>POST /document/{depositId}/realize-deposit — DocumentDtos.cs:395-404
    /// (Amount = ฐานไม่รวม VAT ของมัดจำ · ForfeitAs ไม่ว่าง = "ริบ": PriceOrFee=1 / Compensation=2, AllEnums.cs:1836-1848)</summary>
    public class NxRealizeDepositRequest
    {
        public decimal Amount { get; set; }
        public DateTime? RealizeDate { get; set; }
        public string RevenueAccountCode { get; set; }
        public Guid? FinalInvoiceId { get; set; }
        public int? ForfeitAs { get; set; }
    }

    /// <summary>GET /document/{id} — เฉพาะฟิลด์ที่ใบลดหนี้ใช้ (DocumentDtos.cs:625-809, 1169-1200, 1268)</summary>
    public class NxDocumentDetail
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; }
        [JsonConverter(typeof(DocumentTypeConverter))]
        public int DocumentType { get; set; }
        [JsonConverter(typeof(DocumentStatusConverter))]
        public int Status { get; set; }
        public DateTime DocumentDate { get; set; }
        public NxContactBrief Contact { get; set; }
        public decimal SubTotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceDue { get; set; }
        public string Reference { get; set; }
        public List<NxDocumentLine> Lines { get; set; }
        public bool IsDeposit { get; set; }
        public decimal DepositRealizedAmount { get; set; }
        public decimal DepositRefundedAmount { get; set; }
        public bool DepositOutputVatDeferred { get; set; }
        public DateTime? DepositOutputVatRecognizedAt { get; set; }
        public List<NxDocumentBrief> ConvertedToDocuments { get; set; }
    }

    public class NxContactBrief
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string TaxId { get; set; }
    }

    public class NxDocumentLine
    {
        public Guid Id { get; set; }
        public int LineOrder { get; set; }
        public string Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public decimal VatRate { get; set; }
        public decimal VatAmount { get; set; }
        public Guid? AccountId { get; set; }
        public string AccountCode { get; set; }
        public string ProductCode { get; set; }
    }

    public class NxDocumentBrief
    {
        public Guid Id { get; set; }
        public string DocumentNumber { get; set; }
        [JsonConverter(typeof(DocumentTypeConverter))]
        public int DocumentType { get; set; }
        [JsonConverter(typeof(DocumentStatusConverter))]
        public int Status { get; set; }
        public DateTime DocumentDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
