-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 05: Deposit deferred output VAT (ภาษีขายรอเรียกเก็บ 21913)
-- ════════════════════════════════════════════════════════════════════════════
-- ฟีเจอร์ (opt-in): ในโหมดรับรู้ VAT มัดจำแบบ RECEIPT ปกติระบบจะ Cr "ภาษีขาย"
-- (OUTPUT_VAT, 21911) ทันทีตอนรับมัดจำ ซึ่งทำให้ VAT ขึ้น ภ.พ.30 ทันที
--
-- เมื่อเปิด Deposit_Defer_Output_Vat = true ระบบจะ:
--   • ตอนรับมัดจำ : Cr "ภาษีขายรอเรียกเก็บ/รอรับรู้" (OUTPUT_VAT_DEFERRED, 21913) แทน
--   • ตอน check-out: โอนกลับ Dr 21913 / Cr 21911 (รับรู้เข้า ภ.พ.30 ตอนรับรู้รายได้)
--
-- เป็น opt-in 100% — ถ้าไม่เปิด config นี้ พฤติกรรมเดิมไม่เปลี่ยน และถ้าเปิดแต่ยัง
-- ไม่ map OUTPUT_VAT_DEFERRED ระบบจะ fallback กลับไปใช้ OUTPUT_VAT (กันบัญชีไม่บาลานซ์)
--
-- หมายเหตุสำคัญ (ต้องทำเพิ่มหลังรัน migration นี้ ถ้าจะเปิดใช้):
--   1. ตั้ง Deposit_Defer_Output_Vat = 'true'
--   2. ให้ผู้ดูแล "Sync ผังบัญชี" หรือกรอก Nexaacc_AccountId (GUID ของบัญชี 21913
--      จาก NextAcc) ลงในแถว OUTPUT_VAT_DEFERRED มิฉะนั้นจะ resolve ไม่ได้
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- 1. Config flag (default false — ไม่กระทบของเดิม)
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Deposit_Defer_Output_Vat')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description) VALUES
    ('Deposit_Defer_Output_Vat', 'false',
     N'(โหมด RECEIPT) true = พัก VAT มัดจำที่ภาษีขายรอเรียกเก็บ (21913) แล้วโอนเข้าภาษีขาย (21911) ตอน check-out');
    PRINT 'Inserted config Deposit_Defer_Output_Vat = false';
END
ELSE
    PRINT 'Config Deposit_Defer_Output_Vat already exists';
GO

-- 2. Account mapping row OUTPUT_VAT_DEFERRED (ภาษีขายรอเรียกเก็บ/รอรับรู้ 21913)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'OUTPUT_VAT_DEFERRED')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('OUTPUT_VAT_DEFERRED', N'ภาษีขายรอเรียกเก็บ/รอรับรู้ (มัดจำ)', '21913', 'LIABILITY', 1);
    PRINT 'Inserted OUTPUT_VAT_DEFERRED (21913 ภาษีขายรอเรียกเก็บ)';
END
ELSE
BEGIN
    UPDATE Accounting_Account_Mapping
        SET Is_Active = 1,
            Mapping_Type = 'LIABILITY',
            Nexaacc_AccountCode = CASE
                WHEN Nexaacc_AccountCode IS NULL OR Nexaacc_AccountCode = '' THEN '21913'
                ELSE Nexaacc_AccountCode END,
            TakeTime_Description = CASE
                WHEN TakeTime_Description IS NULL OR TakeTime_Description = ''
                THEN N'ภาษีขายรอเรียกเก็บ/รอรับรู้ (มัดจำ)'
                ELSE TakeTime_Description END
    WHERE TakeTime_Code = 'OUTPUT_VAT_DEFERRED';
    PRINT 'Ensured OUTPUT_VAT_DEFERRED is active (21913 ภาษีขายรอเรียกเก็บ)';
END
GO

-- 3. ตรวจสอบ
SELECT TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Mapping_Type, Is_Active
FROM Accounting_Account_Mapping
WHERE TakeTime_Code IN ('OUTPUT_VAT', 'OUTPUT_VAT_DEFERRED');
GO
