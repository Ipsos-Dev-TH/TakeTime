namespace Take_Time_BangPhra.Integration
{
    /// <summary>
    /// ตัดสินว่าใบลดหนี้/ใบเพิ่มหนี้บน NextAcc เป็น "ฝั่งซื้อ" (ของผู้ขาย — แสดงในหน้าตรวจสอบใบสำคัญจ่าย) หรือฝั่งขาย —
    /// กติกาเดียวกับที่ NextAcc ใช้ลงบัญชี (DocumentService.cs:18158-18187):
    /// (1) ผู้ใช้สั่งย้ายฝั่งเอง (CnDnPurchaseSideOverride) ชนะทุกอย่าง → (2) มีใบต้นทาง = ซื้อเมื่อใบต้นทางเป็น ใบแจ้งหนี้ซื้อ/ค่าใช้จ่าย/
    /// ใบรับรองแทนใบเสร็จ/ใบสำคัญจ่าย ไม่งั้นขาย → (3) ไม่มีใบต้นทาง = ซื้อเมื่อคู่ค้าเป็น "ผู้ขายอย่างเดียว" (ไม่ใช่ลูกค้า).
    /// บริสุทธิ์ (ไม่แตะ DB/HTTP)
    /// </summary>
    public static class VendorNoteSide
    {
        public static bool IsPurchaseSourceType(int documentType)
        {
            return documentType == NexaaccDocumentType.PurchaseInvoice
                || documentType == NexaaccDocumentType.Expense
                || documentType == NexaaccDocumentType.CertificateInLieu
                || documentType == NexaaccDocumentType.PaymentVoucher;
        }

        /// <summary>true = ฝั่งซื้อ · false = ฝั่งขาย · null = ไม่มีใบต้นทางให้ตัดสิน ต้องดูบทบาทคู่ค้า (<see cref="FromContactRole"/>)</summary>
        public static bool? Decide(bool? sideOverride, int? sourceDocumentType)
        {
            if (sideOverride.HasValue) return sideOverride.Value;
            if (sourceDocumentType.HasValue) return IsPurchaseSourceType(sourceDocumentType.Value);
            return null;
        }

        /// <summary>ขั้นสุดท้ายเมื่อไม่มีใบต้นทาง: คู่ค้าเป็นผู้ขายและไม่ใช่ลูกค้า = ฝั่งซื้อ</summary>
        public static bool FromContactRole(bool isSupplier, bool isCustomer)
        {
            return isSupplier && !isCustomer;
        }
    }
}
