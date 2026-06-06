-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 17 Migration 01: Payment Voucher - Credit Flag & NextAcc Reference
-- ════════════════════════════════════════════════════════════════════════════
-- 1. Add IsCredit column to Account_Payment
--    เครดิต = ยังไม่จ่ายเงินจริง → NextAcc จะไม่บันทึกชำระเงินอัตโนมัติ
-- 2. Add Nexaacc_Document_Number column to Account_Payment
--    เก็บเลขที่เอกสาร NextAcc เพื่อแสดงในระบบภายใน
-- 3. Add Nexaacc_Response_Id column to Account_Payment
--    เก็บ Document ID จาก NextAcc เพื่อ link กลับ
-- 4. Add Nexaacc_Payment_Id column to Account_Payment
--    เก็บ Payment ID (การชำระเงิน) จาก NextAcc
-- ════════════════════════════════════════════════════════════════════════════

-- 1. IsCredit: เครดิต (ยังไม่จ่ายจริง)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'IsCredit'
)
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD IsCredit BIT NOT NULL CONSTRAINT DF_Account_Payment_IsCredit DEFAULT (0);
    PRINT 'Added Account_Payment.IsCredit (default 0 = จ่ายเงินสด/โอนทันที)';
END
ELSE
    PRINT 'Account_Payment.IsCredit already exists';
GO

-- 2. Nexaacc_Document_Number: เลขที่เอกสาร NextAcc
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Nexaacc_Document_Number'
)
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD Nexaacc_Document_Number NVARCHAR(50) NULL;
    PRINT 'Added Account_Payment.Nexaacc_Document_Number';
END
ELSE
    PRINT 'Account_Payment.Nexaacc_Document_Number already exists';
GO

-- 3. Nexaacc_Response_Id: Document ID จาก NextAcc
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Nexaacc_Response_Id'
)
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD Nexaacc_Response_Id NVARCHAR(100) NULL;
    PRINT 'Added Account_Payment.Nexaacc_Response_Id';
END
ELSE
    PRINT 'Account_Payment.Nexaacc_Response_Id already exists';
GO

-- 4. Nexaacc_Payment_Id: Payment ID จาก NextAcc (บันทึกชำระเงิน)
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'Nexaacc_Payment_Id'
)
BEGIN
    ALTER TABLE [dbo].[Account_Payment]
        ADD Nexaacc_Payment_Id NVARCHAR(100) NULL;
    PRINT 'Added Account_Payment.Nexaacc_Payment_Id';
END
ELSE
    PRINT 'Account_Payment.Nexaacc_Payment_Id already exists';
GO

-- 5. Add IsCashOrBankAccount flag to Account_Paid_How
--    ใช้ระบุว่าวิธีจ่ายเงินนี้เป็นเงินสดหรือบัญชีธนาคาร
--    เพื่อให้ระบบตัดสินใจบันทึกชำระเงินอัตโนมัติใน NextAcc
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'Account_Paid_How' AND COLUMN_NAME = 'IsCashOrBank'
)
BEGIN
    ALTER TABLE [dbo].[Account_Paid_How]
        ADD IsCashOrBank BIT NOT NULL CONSTRAINT DF_Account_Paid_How_IsCashOrBank DEFAULT (0);
    PRINT 'Added Account_Paid_How.IsCashOrBank';
END
ELSE
    PRINT 'Account_Paid_How.IsCashOrBank already exists';
GO

-- Auto-detect cash/bank payment methods by name pattern
UPDATE [dbo].[Account_Paid_How]
SET IsCashOrBank = 1
WHERE (
    Paid_How LIKE N'%เงินสด%'
    OR Paid_How LIKE N'%ธนาคาร%'
    OR Paid_How LIKE N'%โอน%'
    OR Paid_How LIKE N'%บัญชี%'
    OR Paid_How LIKE N'%Cash%'
    OR Paid_How LIKE N'%Bank%'
    OR Paid_How LIKE N'%Transfer%'
)
AND IsCashOrBank = 0;

DECLARE @affected INT = @@ROWCOUNT;
PRINT 'Marked ' + CAST(@affected AS NVARCHAR(10)) + ' Account_Paid_How rows as IsCashOrBank=1';
GO

-- Backfill: Update existing Account_Payment records with NextAcc info from sync queue
-- (for vouchers that were already synced before this migration)
UPDATE ap
SET ap.Nexaacc_Document_Number = q.Nexaacc_Document_Number,
    ap.Nexaacc_Response_Id = q.Nexaacc_Response_Id
FROM Account_Payment ap
CROSS APPLY (
    SELECT TOP 1 q2.Nexaacc_Document_Number, q2.Nexaacc_Response_Id
    FROM Accounting_Sync_Queue q2
    WHERE q2.Status = 'COMPLETED'
      AND q2.Nexaacc_Response_Id IS NOT NULL
      AND q2.Nexaacc_Response_Id NOT LIKE 'SKIPPED%'
      AND q2.Action_Type = 'CREATE_VOUCHER_JOURNAL'
      AND q2.Payload LIKE '%"documentNumber":"' + REPLACE(REPLACE(REPLACE(ap.ID, '[', '[[]'), '%', '[%]'), '_', '[_]') + '"%'
    ORDER BY q2.Processed_Date DESC
) q
WHERE ap.Nexaacc_Response_Id IS NULL;

DECLARE @backfilled INT = @@ROWCOUNT;
PRINT 'Backfilled ' + CAST(@backfilled AS NVARCHAR(10)) + ' existing payment vouchers with NextAcc references';
GO

PRINT '════════════════════════════════════════════════════════════════════════════';
PRINT 'PHASE 17 Migration 01 completed.';
PRINT 'Changes:';
PRINT '  - Account_Payment: IsCredit, Nexaacc_Document_Number, Nexaacc_Response_Id, Nexaacc_Payment_Id';
PRINT '  - Account_Paid_How: IsCashOrBank';
PRINT '  - Backfilled existing vouchers with NextAcc references from sync queue';
PRINT '════════════════════════════════════════════════════════════════════════════';
GO
