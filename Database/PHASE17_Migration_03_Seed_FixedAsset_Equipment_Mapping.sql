-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 03: Seed missing FIXED_ASSET / EQUIPMENT account mappings
-- ════════════════════════════════════════════════════════════════════════════
-- ปัญหา: AccountingDataMapper (asset reclassification — DR สินทรัพย์ถาวร / CR ค่าใช้จ่าย)
--        เรียก TryGetAccountId("FIXED_ASSET") ?? GetAccountId("EQUIPMENT") แต่ทั้งสอง code
--        ไม่เคยถูก seed ใน Accounting_Account_Mapping → GetAccountId โยน exception เมื่อ
--        ผู้ใช้ติ๊ก "บันทึกเป็นสินทรัพย์" ในหน้าใบสำคัญจ่าย
--
-- แก้: seed ทั้งสอง code (Mapping_Type = ASSET, Active) โดย Nexaacc_AccountCode = '' (ว่าง)
--      เพื่อให้แสดงในหน้า Admin > Accounting Integration > Account Mapping เป็น "ยังไม่ map"
--      ผู้ดูแลกด "ดึง Chart of Accounts" แล้วเลือก/แก้บัญชี NextAcc ให้ตรงเอง
--      (ไม่ใส่รหัสเดาเพื่อกันการ auto-match ไปผิดบัญชีโดยไม่ตั้งใจ)
--
-- idempotent — รันซ้ำได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'FIXED_ASSET')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('FIXED_ASSET', N'ที่ดิน อาคารและอุปกรณ์ (สินทรัพย์ถาวร)', '', 'ASSET', 1);
    PRINT 'Inserted FIXED_ASSET (unmapped — set Nexaacc account via Admin UI)';
END
ELSE
    PRINT 'FIXED_ASSET mapping already exists';
GO

IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'EQUIPMENT')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('EQUIPMENT', N'เครื่องตกแต่งและอุปกรณ์', '', 'ASSET', 1);
    PRINT 'Inserted EQUIPMENT (unmapped — set Nexaacc account via Admin UI)';
END
ELSE
    PRINT 'EQUIPMENT mapping already exists';
GO

-- ตรวจสอบบัญชีที่ยังไม่ได้ map (Nexaacc_AccountCode ว่าง และ Nexaacc_AccountId ว่าง)
-- ผู้ดูแลควรกด "ดึง Chart of Accounts" แล้ว map รายการเหล่านี้ผ่านหน้า Admin
SELECT TakeTime_Code, TakeTime_Description, Mapping_Type
FROM Accounting_Account_Mapping
WHERE Is_Active = 1
  AND (Nexaacc_AccountCode IS NULL OR Nexaacc_AccountCode = '')
  AND Nexaacc_AccountId IS NULL
ORDER BY Mapping_Type, TakeTime_Code;
GO
