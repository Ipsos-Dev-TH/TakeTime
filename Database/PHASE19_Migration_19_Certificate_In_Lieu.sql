-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 19 Migration 19: ใบรับรองแทนใบเสร็จรับเงิน (Certificate in Lieu of Receipt)
-- ════════════════════════════════════════════════════════════════════════════
-- บริบท: โรงแรมจ่ายเงินให้ผู้ที่ออกใบเสร็จไม่ได้ (แท็กซี่ / แผงลอย / ลูกจ้างรายวัน) → กิจการ
-- รับรองรายจ่ายเอง. หน้าใบสำคัญจ่าย (Account/PaymentVoucher.aspx) มีตัวเลือก
-- "ผู้รับเงินออกใบเสร็จไม่ได้ — ออกใบรับรองแทนใบเสร็จรับเงิน" → sync เป็นเอกสาร NextAcc
-- DocumentType=15 (CertificateInLieu) ผ่าน company /document แทน PV/Expense (GL ลงครั้งเดียว:
-- Dr ค่าใช้จ่าย / Cr แหล่งเงิน, ไม่มีภาษีซื้อ).
--
-- คอลัมน์ใหม่ใน Account_Payment (เก็บไว้ให้หน้าแก้ไขโหลดกลับ + เป็นหลักฐานฝั่ง TakeTime;
-- การ sync อ่านจาก payload คิว ไม่ได้อ่านคอลัมน์เหล่านี้):
--   Is_Certificate_In_Lieu   BIT  (0 = ใบสำคัญจ่ายปกติ)
--   Cil_Reason               เหตุผลที่ไม่ได้รับใบเสร็จ (NextAcc บังคับ)
--   Cil_Payee_Name/Address   ผู้รับเงินจริง (ถ้าไม่ตรงกับผู้ขายที่เลือก)
--   Cil_Certifier_Name/Position   ผู้รับรอง (NextAcc บังคับชื่อ)
--   Cil_Witness_Name/Position     พยาน/ผู้อนุมัติ
--
-- ค่าตั้ง (ไม่บังคับ): Nexaacc_Cil_Certifier_Position = ตำแหน่งตั้งต้นของผู้รับรอง เมื่อพนักงาน
-- ไม่มีตำแหน่งใน Employee_Salary (เว้นว่าง = "ผู้มีอำนาจอนุมัติ")
--
-- idempotent — รันซ้ำได้, ไม่ลบข้อมูลเดิม
-- ════════════════════════════════════════════════════════════════════════════

SET NOCOUNT ON;

IF COL_LENGTH('dbo.Account_Payment', 'Is_Certificate_In_Lieu') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD Is_Certificate_In_Lieu BIT NOT NULL
            CONSTRAINT DF_Account_Payment_Is_Certificate_In_Lieu DEFAULT (0);
    PRINT 'Added Account_Payment.Is_Certificate_In_Lieu';
END
ELSE
    PRINT 'Account_Payment.Is_Certificate_In_Lieu already exists';
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Reason') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Reason NVARCHAR(500) NULL;
    PRINT 'Added Account_Payment.Cil_Reason';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Payee_Name') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Payee_Name NVARCHAR(200) NULL;
    PRINT 'Added Account_Payment.Cil_Payee_Name';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Payee_Address') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Payee_Address NVARCHAR(500) NULL;
    PRINT 'Added Account_Payment.Cil_Payee_Address';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Certifier_Name') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Certifier_Name NVARCHAR(200) NULL;
    PRINT 'Added Account_Payment.Cil_Certifier_Name';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Certifier_Position') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Certifier_Position NVARCHAR(200) NULL;
    PRINT 'Added Account_Payment.Cil_Certifier_Position';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Witness_Name') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Witness_Name NVARCHAR(200) NULL;
    PRINT 'Added Account_Payment.Cil_Witness_Name';
END
GO

IF COL_LENGTH('dbo.Account_Payment', 'Cil_Witness_Position') IS NULL
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD Cil_Witness_Position NVARCHAR(200) NULL;
    PRINT 'Added Account_Payment.Cil_Witness_Position';
END
GO

IF OBJECT_ID('dbo.Accounting_Integration_Config', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = N'Nexaacc_Cil_Certifier_Position')
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description, Updated_Date)
    VALUES (N'Nexaacc_Cil_Certifier_Position', N'',
            N'ใบรับรองแทนใบเสร็จรับเงิน: ตำแหน่งตั้งต้นของผู้รับรอง เมื่อพนักงานไม่มีตำแหน่งใน Employee_Salary (ว่าง = ผู้มีอำนาจอนุมัติ)', GETDATE());
GO

-- ── ตรวจสอบ ─────────────────────────────────────────────────────────────────
SELECT COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'Account_Payment'
  AND (COLUMN_NAME = 'Is_Certificate_In_Lieu' OR COLUMN_NAME LIKE 'Cil[_]%')
ORDER BY COLUMN_NAME;
