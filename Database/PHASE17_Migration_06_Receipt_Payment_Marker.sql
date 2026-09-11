-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 06: Receipt-side payment marker (close AR in DOCUMENT mode)
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท: ในโหมด DOCUMENT ฝั่งรับ ระบบสร้าง invoice (Dr ลูกหนี้การค้า / Cr รายได้ /
-- Cr ภาษีขาย) แต่ "ไม่เคยบันทึกรับชำระ" → ลูกหนี้การค้าค้างถาวร เงินสดไม่เข้า
-- (NextAcc InboundInvoiceRequest ไม่ auto-record payment; PaymentMethod ถูกละเลย)
--
-- การแก้ฝั่งโค้ด: หลังสร้าง invoice ให้เรียก /api/integration/payments บันทึกรับเงินสด
-- จริง (= ยอดรวม − มัดจำที่หัก) → NextAcc ลง Dr เงินสด / Cr ลูกหนี้ ("ตัดลูกหนี้")
-- ปิด AR + บันทึกเงินสด. ส่วนมัดจำที่หักตัดลูกหนี้ผ่าน adjustment journal (Cr ลูกหนี้).
--
-- ปัญหา idempotency: payment endpoint ของ NextAcc ไม่ dedupe ตาม ExternalId →
-- ถ้า queue retry จะ double-pay. คอลัมน์นี้เป็น marker กัน double:
--   NULL          = ยังไม่ทำ
--   'ADJ:{jid}'   = ลง adjustment มัดจำแล้ว รอบันทึกรับเงินสด
--   '{paymentId}' หรือ 'NOCASH' = บันทึกรับชำระครบแล้ว (final)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Nexaacc_Receipt_Payment_Id'
)
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD Nexaacc_Receipt_Payment_Id NVARCHAR(100) NULL;
    PRINT 'Added Account_Receipt.Nexaacc_Receipt_Payment_Id';
END
ELSE
    PRINT 'Account_Receipt.Nexaacc_Receipt_Payment_Id already exists';
GO
