-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 04: แยกบัญชีภาษีหัก ณ ที่จ่ายค้างจ่ายตามแบบ (ภ.ง.ด.1/3/53)
-- ════════════════════════════════════════════════════════════════════════════
-- เดิมระบบใช้ WHT_PAYABLE บัญชีเดียวกับ WHT ทุกแบบ ทำให้ภาษีหัก ณ ที่จ่ายของ
-- นิติบุคคล (ภ.ง.ด.53) และเงินเดือน (ภ.ง.ด.1) ไปลงบัญชีเดียวกับบุคคลธรรมดา (ภ.ง.ด.3)
--
-- โครงสร้างใหม่ (เลือกบัญชีอัตโนมัติตามผู้ถูกหัก — ดู AccountingDataMapper):
--   WHT_PAYABLE        = ภ.ง.ด.3  (บุคคลธรรมดา)   เช่น 21916
--   WHT_PAYABLE_PND53  = ภ.ง.ด.53 (นิติบุคคล)     เช่น 21917
--   WHT_PAYABLE_PND1   = ภ.ง.ด.1  (เงินเดือนพนักงาน)
--
-- การตัดสินบุคคล/นิติบุคคล: เลขผู้เสียภาษี 13 หลักขึ้นต้น '0' = นิติบุคคล (ภ.ง.ด.53)
-- ฝั่ง NextAcc DOCUMENT mode จะแยก 21916/21917 ให้เองตาม TaxId/ContactType ที่ส่งไป
-- ส่วน JOURNAL mode + เงินเดือน ใช้บัญชีจากตารางนี้
--
-- idempotent — รันซ้ำได้. โค้ดบัญชี NextAcc จริงให้ตรวจ/แก้ผ่านหน้า Admin > Account Mapping
-- (กด 'ดึง Chart of Accounts' แล้ว map) — ที่ seed เป็นค่าเริ่มต้นที่เปลี่ยนได้
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

-- 1. ชี้แจง WHT_PAYABLE ว่าเป็น ภ.ง.ด.3 (ไม่แตะบัญชี/รหัสที่ผูกไว้แล้ว)
UPDATE Accounting_Account_Mapping
    SET TakeTime_Description = N'ภาษีหัก ณ ที่จ่าย ค้างจ่าย (ภ.ง.ด.3 บุคคลธรรมดา)'
WHERE TakeTime_Code = 'WHT_PAYABLE'
  AND (TakeTime_Description IS NULL OR TakeTime_Description NOT LIKE N'%ภ.ง.ด.3%');
GO

-- 2. ภ.ง.ด.53 (นิติบุคคล) → 21917
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'WHT_PAYABLE_PND53')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('WHT_PAYABLE_PND53', N'ภาษีหัก ณ ที่จ่าย ค้างจ่าย (ภ.ง.ด.53 นิติบุคคล)', '21917', 'LIABILITY', 1);
    PRINT 'Inserted WHT_PAYABLE_PND53 (21917 ภ.ง.ด.53)';
END
ELSE
    PRINT 'WHT_PAYABLE_PND53 already exists';
GO

-- 3. ภ.ง.ด.1 (เงินเดือน) — โค้ดบัญชีให้ map เองผ่าน Admin (seed ว่าง กัน auto-match ผิด)
IF NOT EXISTS (SELECT 1 FROM Accounting_Account_Mapping WHERE TakeTime_Code = 'WHT_PAYABLE_PND1')
BEGIN
    INSERT INTO Accounting_Account_Mapping
        (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
    VALUES
        ('WHT_PAYABLE_PND1', N'ภาษีหัก ณ ที่จ่าย ค้างจ่าย (ภ.ง.ด.1 เงินเดือน)', '', 'LIABILITY', 1);
    PRINT 'Inserted WHT_PAYABLE_PND1 (unmapped — set via Admin UI)';
END
ELSE
    PRINT 'WHT_PAYABLE_PND1 already exists';
GO

-- ตรวจสอบผล
SELECT TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Nexaacc_AccountId, Is_Active
FROM Accounting_Account_Mapping
WHERE TakeTime_Code IN ('WHT_PAYABLE', 'WHT_PAYABLE_PND53', 'WHT_PAYABLE_PND1')
ORDER BY TakeTime_Code;
GO
