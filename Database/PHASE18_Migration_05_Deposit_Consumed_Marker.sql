-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 05: Deposit-consumed marker (กันเรียกใช้มัดจำซ้ำ / double-use)
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท: ใบมัดจำเก็บเป็น Account_Receipt (IsDeposit=1) ผูกกับการจองด้วย Reservation_ID.
-- ตอนเช็คเอาท์ ใบเสร็จ/ใบกำกับ (อีก Account_Receipt) จะ "หักมัดจำ" (depositApplied) →
-- กลับหนี้สินมัดจำ (Dr 21510/21712 + Dr 21913 VAT) ในเอกสารเช็คเอาท์.
--
-- ปัญหาเดิม: idempotency มีเฉพาะ "ฝั่งเอกสารเช็คเอาท์" (Nexaacc_Receipt_Payment_Id) แต่
-- "ตัวใบมัดจำ" ไม่มีเครื่องหมายบอกว่าถูกเรียกใช้ไปแล้วโดยใบไหน → ถ้ามีเอกสารเช็คเอาท์คนละใบ
-- (คนละ receiptNumber) วิ่งมาหักมัดจำก้อนเดิมซ้ำ จะกลับหนี้สินมัดจำเกิน (21510/21913 ติดลบ,
-- เงินสดหาย 2 เท่า) โดยไม่มีตัวกันที่ระดับ "ใบมัดจำ".
--
-- Fix: มาร์คบน "แถวใบมัดจำ" (IsDeposit=1) ว่าถูกเรียกใช้โดยเอกสารเช็คเอาท์ใบไหน + เมื่อไร +
-- ยอดเท่าไร. ตั้งตอนหักมัดจำสำเร็จ, ล้างตอน void เอกสารเช็คเอาท์ (edit=void→สร้างใหม่เลขเดิม
-- ก็ล้าง+มาร์คใหม่ได้). ก่อนหัก เช็คว่าถูกเรียกใช้โดย "ใบอื่น" อยู่แล้วรึยัง → ถ้าใช่ บล็อกการหักซ้ำ.
--
--   Deposit_Consumed_By_Receipt : เลขเอกสารเช็คเอาท์ (local receiptNumber) ที่เรียกใช้มัดจำใบนี้
--                                 (NULL = ยังไม่ถูกเรียกใช้)
--   Deposit_Consumed_Date       : วันเวลาที่เรียกใช้
--   Deposit_Consumed_Amount     : ยอดมัดจำที่ถูกหักไป (audit)
--
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Deposit_Consumed_By_Receipt'
)
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD Deposit_Consumed_By_Receipt NVARCHAR(50) NULL;
    PRINT 'Added Account_Receipt.Deposit_Consumed_By_Receipt';
END
ELSE
    PRINT 'Account_Receipt.Deposit_Consumed_By_Receipt already exists';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Deposit_Consumed_Date'
)
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD Deposit_Consumed_Date DATETIME NULL;
    PRINT 'Added Account_Receipt.Deposit_Consumed_Date';
END
ELSE
    PRINT 'Account_Receipt.Deposit_Consumed_Date already exists';
GO

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Receipt' AND COLUMN_NAME = 'Deposit_Consumed_Amount'
)
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD Deposit_Consumed_Amount DECIMAL(18,2) NULL;
    PRINT 'Added Account_Receipt.Deposit_Consumed_Amount';
END
ELSE
    PRINT 'Account_Receipt.Deposit_Consumed_Amount already exists';
GO

-- Index ช่วย GetDepositConsumedByOther / ClearDepositConsumed (ค้นตาม Reservation_ID + IsDeposit)
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Account_Receipt_Deposit_Consumed' AND object_id = OBJECT_ID('dbo.Account_Receipt')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Account_Receipt_Deposit_Consumed
        ON [dbo].[Account_Receipt] (Reservation_ID, IsDeposit)
        INCLUDE (Deposit_Consumed_By_Receipt);
    PRINT 'Created index IX_Account_Receipt_Deposit_Consumed';
END
ELSE
    PRINT 'Index IX_Account_Receipt_Deposit_Consumed already exists';
GO
