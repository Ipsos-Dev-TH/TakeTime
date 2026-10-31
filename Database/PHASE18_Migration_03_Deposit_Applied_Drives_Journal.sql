-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 18 Migration 03: Deposit-applied "drives JE" mode (NextAcc spec §9.1)
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหาเดิม: ใบกำกับ/ใบเสร็จเช็คเอาท์ที่หักมัดจำ — JE ในเอกสาร Dr เงินสด "เต็มยอด"
-- แล้วการกลับมัดจำ (Dr 217xx/21913) อยู่ใน JV แยกอีกใบ → JE บนหน้าเอกสารไม่ self-contained
-- (โชว์ Dr ธนาคาร 3,200 ขัดกับ "ยอดชำระสุทธิ 2,700")
--
-- NextAcc เพิ่ม flag depositAppliedDrivesJournal (spec §9.1): เมื่อ true → ตอน post Receipt
-- NextAcc ลง JE self-contained ในใบเดียว:
--   Dr เงินสด (Total − Applied)  /  Dr 217xx + Dr 21913 (กลับใบมัดจำที่ depositAppliedRef ชี้)
--   Cr รายได้  /  Cr 21911
-- → ไม่ต้องมี JV หักมัดจำแยกอีก
--
-- ⚠️ COORDINATION (สำคัญ): เปิด flag นี้ = TakeTime ส่ง depositAppliedDrivesJournal=true
-- และ "เลิกส่ง JV หักมัดจำแยก" พร้อมกัน (โค้ด ProcessReceiptDocument จัดการให้อัตโนมัติ
-- เมื่ออ่าน config นี้เป็น true). ถ้า NextAcc ยังไม่ deploy รองรับ flag นี้ → อย่าเปิด
-- (จะได้ display-only เฉยๆ แต่ JV แยกจะถูกตัดออก → 217xx/21913 ไม่ถูกกลับ = GL พัง).
--
-- ลำดับเปิดใช้:
--   1. NextAcc rebuild + deploy (รองรับ depositAppliedDrivesJournal + migration ฝั่งเขา) ให้เสร็จก่อน
--   2. TakeTime rebuild + deploy (มีโค้ดอ่าน config นี้)
--   3. ค่อยตั้ง Nexaacc_Deposit_Drives_Journal = '1' (สลับในจังหวะเดียว)
--
-- default = '0' (display-only + JV แยกเดิม) → ไม่กระทบของปัจจุบัน. idempotent — รันซ้ำได้.
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Deposit_Drives_Journal')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Nexaacc_Deposit_Drives_Journal', '0',
     N'1 = ให้ NextAcc ลง JE หักมัดจำ self-contained ในใบ (depositAppliedDrivesJournal) + เลิกส่ง JV แยก; 0 = display-only + JV แยกเดิม. เปิดได้เมื่อ NextAcc deploy รองรับแล้วเท่านั้น');
    PRINT 'Inserted config Nexaacc_Deposit_Drives_Journal = 0';
END
ELSE
    PRINT 'Config Nexaacc_Deposit_Drives_Journal already exists';
GO

SELECT ConfigKey, ConfigValue, Description
FROM Accounting_Integration_Config
WHERE ConfigKey = 'Nexaacc_Deposit_Drives_Journal';
GO
