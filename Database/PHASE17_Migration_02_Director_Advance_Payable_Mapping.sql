-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 02: Director Advance → เจ้าหนี้กรรมการ (Payable) mapping
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: ใบสำคัญจ่ายที่เลือก "จ่ายจากเงินทดรองกรรมการ" ถูกบันทึกบัญชีเป็น
--        Cr เงินสด (11111) ใน NextAcc แทนที่จะเป็น Cr เจ้าหนี้กรรมการ
--
-- การแก้ฝั่งโค้ด (AccountingDataMapper.GetPaymentMethodAccountId) ได้เปลี่ยน
-- ให้วิธีจ่าย "กรรมการ/ทดรอง/DIRECTOR" map ไปที่ TakeTime_Code = DIRECTOR_ADVANCE_REPAY
-- (เจ้าหนี้กรรมการ 21230, หนี้สิน) แทน DIRECTOR_ADVANCE (ลูกหนี้กรรมการ 11330, สินทรัพย์)
--
-- Migration นี้ทำให้แน่ใจว่า mapping row DIRECTOR_ADVANCE_REPAY มีอยู่และ Active
-- เพื่อให้ resolve บัญชีได้ (idempotent — รันซ้ำได้)
--
-- หมายเหตุสำคัญ (ต้องทำเพิ่มหลังรัน migration นี้):
--   เพื่อให้ NextAcc รับรู้บัญชีจริง ต้องให้ผู้ดูแล "Sync ผังบัญชี" หรือกรอก
--   Nexaacc_AccountId (GUID ของบัญชี 21230 จาก NextAcc) ลงในแถวนี้
--   มิฉะนั้นระบบจะ resolve เป็น GUID สังเคราะห์จากรหัส 21230 ซึ่ง NextAcc อาจไม่รู้จัก
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- 1. สร้างแถว DIRECTOR_ADVANCE_REPAY ถ้ายังไม่มี (เจ้าหนี้กรรมการ 21230)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'DIRECTOR_ADVANCE_REPAY')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('DIRECTOR_ADVANCE_REPAY', N'เจ้าหนี้กรรมการ/เงินทดรองรับจากกรรมการ', '21230', 'LIABILITY', 1);
    PRINT 'Inserted DIRECTOR_ADVANCE_REPAY (21230 เจ้าหนี้กรรมการ)';
END
ELSE
BEGIN
    -- มีอยู่แล้ว → ทำให้ Active และตั้งรหัส/ประเภทให้ถูกต้อง (เผื่อถูกปิดไว้)
    UPDATE Accounting_Account_Mapping
        SET Is_Active = 1,
            Mapping_Type = 'LIABILITY',
            Nexaacc_AccountCode = CASE
                WHEN Nexaacc_AccountCode IS NULL OR Nexaacc_AccountCode = '' THEN '21230'
                ELSE Nexaacc_AccountCode END,
            TakeTime_Description = CASE
                WHEN TakeTime_Description IS NULL OR TakeTime_Description = ''
                THEN N'เจ้าหนี้กรรมการ/เงินทดรองรับจากกรรมการ'
                ELSE TakeTime_Description END
    WHERE TakeTime_Code = 'DIRECTOR_ADVANCE_REPAY';
    PRINT 'Ensured DIRECTOR_ADVANCE_REPAY is active (21230 เจ้าหนี้กรรมการ)';
END
GO

-- 2. แสดงผลเพื่อตรวจสอบ (ผู้ดูแลควรเติม Nexaacc_AccountId ถ้ายังว่าง)
SELECT TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active
FROM Accounting_Account_Mapping
WHERE TakeTime_Code IN ('DIRECTOR_ADVANCE', 'DIRECTOR_ADVANCE_REPAY');
GO
