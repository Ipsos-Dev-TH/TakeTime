-- ════════════════════════════════════════════════════════════════════════════
-- PHASE 10 Migration 01: Voucher → Tax Invoice Matching
-- ════════════════════════════════════════════════════════════════════════════
-- Adds RequiresTaxInvoice flag to Account_Paid_How so payment methods that
-- transfer funds into the company bank account can force a tax invoice to be
-- issued at the time of voucher sale.
-- ════════════════════════════════════════════════════════════════════════════

-- 1. Add RequiresTaxInvoice column to Account_Paid_How
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[dbo].[Account_Paid_How]')
      AND name = 'RequiresTaxInvoice'
)
BEGIN
    ALTER TABLE [dbo].[Account_Paid_How]
        ADD RequiresTaxInvoice BIT NOT NULL CONSTRAINT DF_Account_Paid_How_RequiresTaxInvoice DEFAULT (0);
    PRINT 'Added Account_Paid_How.RequiresTaxInvoice';
END
ELSE
    PRINT 'Account_Paid_How.RequiresTaxInvoice already exists';
GO

-- 2. Mark bank-transfer payment methods as requiring tax invoice
-- Match by name pattern: contains "โอน", "ธนาคาร", "Bank", "Transfer", "บัญชี"
UPDATE [dbo].[Account_Paid_How]
SET RequiresTaxInvoice = 1
WHERE
    (
        Name LIKE N'%โอน%'
     OR Name LIKE N'%ธนาคาร%'
     OR Name LIKE N'%บัญชี%'
     OR Name LIKE N'%Bank%'
     OR Name LIKE N'%Transfer%'
     OR Name LIKE N'%บริษัท%'
    )
    AND RequiresTaxInvoice = 0;

DECLARE @affected INT = @@ROWCOUNT;
PRINT 'Marked ' + CAST(@affected AS NVARCHAR(10)) + ' Account_Paid_How rows as RequiresTaxInvoice=1';
GO

-- 3. Show result for verification
SELECT ID, Name, Status, RequiresTaxInvoice
FROM [dbo].[Account_Paid_How]
WHERE Status = 'True'
ORDER BY RequiresTaxInvoice DESC, Name;
GO

PRINT '════════════════════════════════════════════════════════════════════════════';
PRINT 'PHASE 10 Migration 01 completed.';
PRINT 'Review the result above. If a payment method should require tax invoice but';
PRINT 'is not marked, run: UPDATE Account_Paid_How SET RequiresTaxInvoice=1 WHERE ID=...';
PRINT '════════════════════════════════════════════════════════════════════════════';
GO
