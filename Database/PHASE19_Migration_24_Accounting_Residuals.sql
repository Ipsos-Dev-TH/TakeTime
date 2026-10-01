-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 24: Accounting residuals (NextAcc)
-- ════════════════════════════════════════════════════════════════════════════
-- 1) เงินประกันความเสียหายแบบโอน (SecurityHoldService, Security_Hold_Mode = TRANSFER) → ลงบัญชีหนี้สิน (opt-in)
--    ค่าตั้ง Nexaacc_SecurityDeposit_Journal (0 = ปิด = พฤติกรรมเดิม ไม่ส่งอะไรเข้า NextAcc)
--      รับโอน      : Dr ธนาคาร (แหล่งเงินของช่องทางรับโอน) / Cr SECURITY_DEPOSIT_LIABILITY     ref SECDEP-{holdId}-IN
--      โอนคืน      : Dr SECURITY_DEPOSIT_LIABILITY / Cr ธนาคาร                                 ref SECDEP-{holdId}-OUT
--      หักค่าเสียหาย : Dr SECURITY_DEPOSIT_LIABILITY / Cr ธนาคาร (เฉพาะส่วนที่หัก)                ref SECDEP-{holdId}-DMG
--      รายได้ค่าเสียหาย + VAT มาจาก "ใบเสร็จค่าเสียหาย" ที่พนักงานออกตามเดิม (แหล่งเงิน = บัญชีรับโอน) — ไม่ลงซ้ำ
--    mapping ใหม่ SECURITY_DEPOSIT_LIABILITY (Nexaacc_AccountCode ว่าง = ให้ผู้ดูแลผูกเองในหน้า Admin กัน auto-match ผิด)
--      ผังโรงแรมมาตรฐาน NextAcc: 21530 "เงินประกันความเสียหาย" (Wachira-d/Accounting Services/ChartOfAccountTemplates.cs:441)
--      หรือ 21620 "เงินค้ำประกัน" (:130)
--
-- 2) ไม่มีการเปลี่ยนโครงสร้างสำหรับ: การยกเลิกเอกสาร OTA (OTADOC) / กัน OTA_CASH_RECLASS ซ้ำกับเอกสาร OTA /
--    การแสดงแหล่งเงินรายช่องทาง / ผู้รับเงินบนใบรับรองแทนใบเสร็จ — เป็นโค้ดล้วน (ใช้คอลัมน์เดิม Reservation.Ota_Revenue_Ref
--    NVARCHAR(60) จาก PHASE18_20: ค่าใหม่ 'VOIDED-OTADOC-{id}' = ยกเลิกเอกสาร OTA แล้วและล็อกไม่ให้สร้างใหม่อัตโนมัติ)
--
-- idempotent — รันซ้ำได้, ไม่แตะค่าที่ตั้งไว้แล้ว, ไม่ลบข้อมูล
-- ════════════════════════════════════════════════════════════════════════════
SET NOCOUNT ON;

-- 1a) ค่าตั้ง (default ปิด)
IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NULL
BEGIN
    PRINT N'ยังไม่มีตาราง Accounting_Integration_Config (รัน PHASE9_Migration_01 ก่อน) — ข้ามการเพิ่มค่าตั้ง';
END
ELSE IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_SecurityDeposit_Journal')
BEGIN
    INSERT INTO dbo.Accounting_Integration_Config (ConfigKey, ConfigValue)
    VALUES ('Nexaacc_SecurityDeposit_Journal', '0');
    PRINT N'เพิ่มค่า Nexaacc_SecurityDeposit_Journal = 0 (ปิด — พฤติกรรมเดิม)';
END
ELSE
BEGIN
    DECLARE @cur NVARCHAR(200);
    SELECT @cur = ConfigValue FROM dbo.Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_SecurityDeposit_Journal';
    PRINT N'มีค่า Nexaacc_SecurityDeposit_Journal = ' + ISNULL(@cur, N'(ว่าง)') + N' อยู่แล้ว — ไม่แตะ';
END
GO

-- 1b) mapping บัญชีหนี้สินเงินประกัน
IF OBJECT_ID('dbo.Accounting_Account_Mapping', 'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Accounting_Account_Mapping WHERE TakeTime_Code = 'SECURITY_DEPOSIT_LIABILITY')
    BEGIN
        INSERT INTO dbo.Accounting_Account_Mapping (TakeTime_Code, TakeTime_Description, Nexaacc_AccountCode, Mapping_Type, Is_Active)
        VALUES ('SECURITY_DEPOSIT_LIABILITY', N'เงินประกันความเสียหายรอคืน (หนี้สิน — ผังโรงแรม NextAcc 21530)', '', 'LIABILITY', 1);
        PRINT N'เพิ่ม mapping SECURITY_DEPOSIT_LIABILITY (ยังไม่ผูกรหัสบัญชี — ผูกที่ Admin → Accounting Integration → ผังบัญชี)';
    END
    ELSE
        PRINT N'มี mapping SECURITY_DEPOSIT_LIABILITY อยู่แล้ว — ไม่แตะ';
END
ELSE
    PRINT N'ไม่มีตาราง Accounting_Account_Mapping — ข้ามการเพิ่ม mapping';
GO

PRINT '';
PRINT N'วิธีเปิดใช้เงินประกันโอน → บัญชี:';
PRINT N'  1) Admin → Accounting Integration → ดึง Chart of Accounts → ผูก SECURITY_DEPOSIT_LIABILITY กับ 21530 (หรือบัญชีหนี้สินที่ผู้ทำบัญชีเลือก)';
PRINT N'  2) ผูกบัญชีธนาคารของแหล่งเงินช่องทางรับโอนเงินประกัน (วิธีจ่ายเงิน → บัญชี NextAcc)';
PRINT N'  3) Sync Settings → "เงินประกันความเสียหาย (รับโอน) → ลงบัญชีหนี้สิน" = เปิด';
PRINT N'  ⚠ ออกใบเสร็จค่าเสียหายด้วยแหล่งเงินเดียวกับบัญชีรับโอน (JE หักค่าเสียหาย Cr ธนาคารคู่กับใบเสร็จที่ Dr ธนาคาร)';
PRINT N'  ⚠ เงินประกันที่รับก่อนเปิดสวิตช์ จะไม่ถูกลงขาโอนคืน/หัก (ไม่มีหนี้สินให้ล้าง)';
GO
