-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 07: Voucher-side PaymentVoucher document marker
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท: ฝั่งจ่าย เมื่อ "จ่ายเงินจริง" (เงินสด/ธนาคาร หรือแหล่งอื่นที่ไม่ใช่เครดิต)
-- ควรออกเป็น "ใบสำคัญจ่าย (PaymentVoucher)" ใน NextAcc ไม่ใช่ "ใบบันทึกค่าใช้จ่าย (Expense)".
-- เดิมกรณีจ่ายแบบไม่ใช่เงินสด (เช่น เจ้าหนี้กรรมการ) ตกไปสร้าง Expense เพราะ
-- /api/integration/payment-vouchers บังคับ Cr เงินสดเสมอ (override บัญชีไม่ได้).
--
-- การแก้: เมื่อ CanUseCompanyEndpoints จะสร้าง PaymentVoucher ผ่าน company /document
-- (DocumentType=13) + PaymentAccountId = แหล่งเงิน (Cr บัญชีนั้นตรง ๆ) → approve.
--
-- ปัญหา idempotency: company create-document ไม่ dedupe → queue retry จะสร้างซ้ำ.
-- คอลัมน์นี้เป็น marker 3 เฟส กัน double (keyed by Account_Payment.ID = เลขเอกสาร):
--   NULL        = ยังไม่ทำ
--   'DOC:{id}'  = สร้างเอกสารแล้ว (รออนุมัติ)
--   'APR:{id}'  = อนุมัติแล้ว
--   '{id}'      = จบ (final, = NextAcc document id)
--   'VOIDED'    = ถูก void แล้ว (edit = void→สร้างใหม่ จะ reset เป็น null)
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Nexaacc_Voucher_Doc_Marker'
)
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD Nexaacc_Voucher_Doc_Marker NVARCHAR(100) NULL;
    PRINT 'Added Account_Payment.Nexaacc_Voucher_Doc_Marker';
END
ELSE
    PRINT 'Account_Payment.Nexaacc_Voucher_Doc_Marker already exists';
GO
