-- =============================================
-- PHASE 3 - Accounting System Improvements
-- Migration 01: Core Accounting Enhancements
-- Date: 2025-11-06
-- Description:
--   1. Create standardized PaymentMethod lookup table
--   2. Extend Payment_History to support non-reservation payments
--   3. Add Payment_Amount_Per_Method JSON to Account_Receipt
--   4. Add indexes for performance optimization
--   5. Add data integrity checks and constraints
-- =============================================

BEGIN TRANSACTION;

PRINT 'Starting PHASE3_Migration_01: Accounting System Improvements...'

-- =============================================
-- 1. CREATE PAYMENT METHOD LOOKUP TABLE
-- =============================================

PRINT '1. Creating Account_PaymentMethod lookup table...'

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Account_PaymentMethod]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Account_PaymentMethod] (
        [ID] INT IDENTITY(1,1) NOT NULL,
        [Code] NVARCHAR(50) NOT NULL,           -- Short code (KBANK, CASH, DIRECTOR, KTB, etc.)
        [Name_TH] NVARCHAR(255) NOT NULL,       -- Thai display name
        [Name_EN] NVARCHAR(255) NULL,           -- English display name
        [BankName] NVARCHAR(255) NULL,          -- Bank name if applicable
        [AccountNumber] NVARCHAR(50) NULL,      -- Bank account number if applicable
        [IsActive] BIT NOT NULL DEFAULT 1,      -- Active status
        [DisplayOrder] INT NOT NULL DEFAULT 0,  -- Display order in UI
        [RequiresSlip] BIT NOT NULL DEFAULT 0,  -- Requires payment slip upload
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [UpdatedDate] DATETIME NOT NULL DEFAULT GETDATE(),

        CONSTRAINT [PK_Account_PaymentMethod] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [UQ_Account_PaymentMethod_Code] UNIQUE NONCLUSTERED ([Code] ASC)
    );

    PRINT '   ✓ Account_PaymentMethod table created successfully'
END
ELSE
BEGIN
    PRINT '   → Account_PaymentMethod table already exists'
END
GO

-- =============================================
-- 2. SEED PAYMENT METHOD DATA
-- =============================================

PRINT '2. Seeding Account_PaymentMethod with initial data...'

-- Insert standard payment methods based on current system usage
IF NOT EXISTS (SELECT 1 FROM [dbo].[Account_PaymentMethod] WHERE Code = 'KBANK')
BEGIN
    INSERT INTO [dbo].[Account_PaymentMethod]
        (Code, Name_TH, Name_EN, BankName, IsActive, DisplayOrder, RequiresSlip)
    VALUES
        ('KBANK', N'เงินโอน บัญชี ธ.กสิกรไทย', 'KBANK Transfer', N'ธนาคารกสิกรไทย', 1, 1, 1),
        ('CASH', N'เงินสด', 'Cash', NULL, 1, 2, 0),
        ('DIRECTOR', N'กรรมการ', 'Director Money', NULL, 1, 3, 0),
        ('KTB', N'เงินโอน บัญชี ธ.กรุงไทย', 'KTB Transfer', N'ธนาคารกรุงไทย', 1, 4, 1),
        ('CARD', N'บัตรเครดิต/เดบิต', 'Credit/Debit Card', NULL, 1, 5, 0),
        ('PROMPTPAY', N'พร้อมเพย์', 'PromptPay', NULL, 1, 6, 1),
        ('OTHER', N'อื่นๆ', 'Other', NULL, 1, 99, 0);

    PRINT '   ✓ Payment methods seeded successfully'
END
ELSE
BEGIN
    PRINT '   → Payment methods already exist'
END
GO

-- =============================================
-- 3. ADD PAYMENTMETHOD_ID TO PAYMENT_HISTORY
-- =============================================

PRINT '3. Extending Payment_History table...'

-- Add PaymentMethod_ID foreign key
IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Payment_History]')
               AND name = 'PaymentMethod_ID')
BEGIN
    ALTER TABLE [dbo].[Payment_History]
        ADD [PaymentMethod_ID] INT NULL;

    PRINT '   ✓ PaymentMethod_ID column added to Payment_History'
END
ELSE
BEGIN
    PRINT '   → PaymentMethod_ID column already exists in Payment_History'
END
GO

-- Add foreign key constraint
IF NOT EXISTS (SELECT * FROM sys.foreign_keys
               WHERE object_id = OBJECT_ID(N'[dbo].[FK_Payment_History_PaymentMethod]')
               AND parent_object_id = OBJECT_ID(N'[dbo].[Payment_History]'))
BEGIN
    ALTER TABLE [dbo].[Payment_History]
        ADD CONSTRAINT [FK_Payment_History_PaymentMethod]
        FOREIGN KEY ([PaymentMethod_ID])
        REFERENCES [dbo].[Account_PaymentMethod]([ID]);

    PRINT '   ✓ Foreign key constraint added to Payment_History'
END
ELSE
BEGIN
    PRINT '   → Foreign key constraint already exists'
END
GO

-- =============================================
-- 4. MIGRATE EXISTING PAYMENT_HISTORY DATA
-- =============================================

PRINT '4. Migrating existing Payment_History PaymentMethod data...'

-- Update existing records to use PaymentMethod_ID
UPDATE ph
SET ph.PaymentMethod_ID = CASE
    WHEN ph.PaymentMethod LIKE N'%กสิกรไทย%' OR ph.PaymentMethod = 'KBANK' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'KBANK')
    WHEN ph.PaymentMethod LIKE N'%เงินสด%' OR ph.PaymentMethod = 'CASH' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'CASH')
    WHEN ph.PaymentMethod LIKE N'%กรรมการ%' OR ph.PaymentMethod = 'DIRECTOR' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'DIRECTOR')
    WHEN ph.PaymentMethod LIKE N'%กรุงไทย%' OR ph.PaymentMethod = 'KTB' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'KTB')
    WHEN ph.PaymentMethod LIKE N'%บัตร%' OR ph.PaymentMethod = 'CARD' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'CARD')
    WHEN ph.PaymentMethod LIKE N'%พร้อมเพย์%' OR ph.PaymentMethod = 'PROMPTPAY' THEN (SELECT ID FROM Account_PaymentMethod WHERE Code = 'PROMPTPAY')
    ELSE (SELECT ID FROM Account_PaymentMethod WHERE Code = 'OTHER')
END
FROM [dbo].[Payment_History] ph
WHERE ph.PaymentMethod_ID IS NULL;

PRINT '   ✓ Existing Payment_History records migrated to use PaymentMethod_ID'
GO

-- =============================================
-- 5. ALLOW NULL RESERVATION_ID IN PAYMENT_HISTORY
-- =============================================

PRINT '5. Modifying Payment_History to support non-reservation payments...'

-- Make Reservation_ID nullable to support non-reservation payments
IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID(N'[dbo].[Payment_History]')
           AND name = 'Reservation_ID'
           AND is_nullable = 0)
BEGIN
    -- First, we need to drop the existing foreign key constraint
    DECLARE @ConstraintName NVARCHAR(255);
    SELECT @ConstraintName = name
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[Payment_History]')
    AND referenced_object_id = OBJECT_ID(N'[dbo].[Reservation]');

    IF @ConstraintName IS NOT NULL
    BEGIN
        DECLARE @SQL NVARCHAR(MAX);
        SET @SQL = N'ALTER TABLE [dbo].[Payment_History] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName);
        EXEC sp_executesql @SQL;
        PRINT '   ✓ Dropped existing Reservation_ID foreign key constraint'
    END

    -- Alter column to allow NULL
    ALTER TABLE [dbo].[Payment_History]
        ALTER COLUMN [Reservation_ID] BIGINT NULL;

    -- Re-add foreign key constraint (allowing NULL)
    ALTER TABLE [dbo].[Payment_History]
        ADD CONSTRAINT [FK_Payment_History_Reservation]
        FOREIGN KEY ([Reservation_ID])
        REFERENCES [dbo].[Reservation]([ID]);

    PRINT '   ✓ Reservation_ID modified to allow NULL (for non-reservation payments)'
END
ELSE
BEGIN
    PRINT '   → Reservation_ID already allows NULL'
END
GO

-- Add check constraint to ensure either Reservation_ID or Receipt_ID exists
IF NOT EXISTS (SELECT * FROM sys.check_constraints
               WHERE object_id = OBJECT_ID(N'[dbo].[CK_Payment_History_Reference]'))
BEGIN
    ALTER TABLE [dbo].[Payment_History]
        ADD CONSTRAINT [CK_Payment_History_Reference]
        CHECK ([Reservation_ID] IS NOT NULL OR [Receipt_ID] IS NOT NULL);

    PRINT '   ✓ Check constraint added to ensure payment has a reference'
END
ELSE
BEGIN
    PRINT '   → Check constraint already exists'
END
GO

-- =============================================
-- 6. ADD PAYMENT_AMOUNT_DETAILS TO ACCOUNT_RECEIPT
-- =============================================

PRINT '6. Adding Payment_Amount_Details to Account_Receipt...'

IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]')
               AND name = 'Payment_Amount_Details')
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD [Payment_Amount_Details] NVARCHAR(MAX) NULL;

    PRINT '   ✓ Payment_Amount_Details column added (will store JSON)'
    PRINT '     Format: [{"PaymentMethodCode":"KBANK","Amount":1000},{"PaymentMethodCode":"CASH","Amount":500}]'
END
ELSE
BEGIN
    PRINT '   → Payment_Amount_Details column already exists'
END
GO

-- =============================================
-- 7. CREATE INDEXES FOR PERFORMANCE
-- =============================================

PRINT '7. Creating performance indexes...'

-- Index on Payment_History.PaymentMethod_ID
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[Payment_History]')
               AND name = N'IX_Payment_History_PaymentMethod')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_History_PaymentMethod]
        ON [dbo].[Payment_History] ([PaymentMethod_ID])
        INCLUDE ([PaymentAmount], [Status]);

    PRINT '   ✓ Index IX_Payment_History_PaymentMethod created'
END
ELSE
BEGIN
    PRINT '   → Index IX_Payment_History_PaymentMethod already exists'
END
GO

-- Index on Payment_History for revenue queries
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[Payment_History]')
               AND name = N'IX_Payment_History_Revenue')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_History_Revenue]
        ON [dbo].[Payment_History] ([Status], [Receipt_ID])
        INCLUDE ([PaymentAmount], [PaymentMethod_ID], [Reservation_ID]);

    PRINT '   ✓ Index IX_Payment_History_Revenue created'
END
ELSE
BEGIN
    PRINT '   → Index IX_Payment_History_Revenue already exists'
END
GO

-- =============================================
-- 8. CREATE REVENUE RECONCILIATION VIEW
-- =============================================

PRINT '8. Creating revenue reconciliation view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Revenue_Reconciliation]'))
BEGIN
    DROP VIEW [dbo].[vw_Revenue_Reconciliation];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Revenue_Reconciliation]
AS
SELECT
    ar.Created_Date,
    CONVERT(DATE, ar.Created_Date) AS Receipt_Date,
    ar.ID AS Receipt_ID,
    ar.Reservation_ID,
    ar.Status AS Receipt_Status,
    ar.Total_Amount AS Receipt_Total_Amount,
    ar.Paid_Type AS Receipt_Paid_Type,

    -- Sum from Payment_History (for reservation payments)
    ISNULL((
        SELECT SUM(ph.PaymentAmount)
        FROM Payment_History ph
        WHERE ph.Receipt_ID = ar.ID
        AND ph.Status = 'COMPLETED'
    ), 0) AS Payment_History_Total,

    -- Calculate difference
    ar.Total_Amount - ISNULL((
        SELECT SUM(ph.PaymentAmount)
        FROM Payment_History ph
        WHERE ph.Receipt_ID = ar.ID
        AND ph.Status = 'COMPLETED'
    ), 0) AS Difference,

    -- Flag reconciliation status
    CASE
        WHEN ar.Reservation_ID IS NULL OR ar.Reservation_ID = 0 THEN 'NON_RESERVATION'
        WHEN ABS(ar.Total_Amount - ISNULL((
            SELECT SUM(ph.PaymentAmount)
            FROM Payment_History ph
            WHERE ph.Receipt_ID = ar.ID
            AND ph.Status = 'COMPLETED'
        ), 0)) < 0.01 THEN 'MATCHED'
        WHEN NOT EXISTS (
            SELECT 1 FROM Payment_History ph
            WHERE ph.Receipt_ID = ar.ID
        ) THEN 'NO_PAYMENT_HISTORY'
        ELSE 'MISMATCH'
    END AS Reconciliation_Status

FROM Account_Receipt ar
WHERE ar.Status = 'Normal';
GO

PRINT '   ✓ Revenue reconciliation view created'
GO

-- =============================================
-- 9. CREATE PAYMENT METHOD SUMMARY FUNCTION
-- =============================================

PRINT '9. Creating payment method summary function...'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_GetPaymentMethodSummary]') AND type in (N'FN', N'IF', N'TF'))
BEGIN
    DROP FUNCTION [dbo].[fn_GetPaymentMethodSummary];
    PRINT '   → Dropped existing function'
END
GO

CREATE FUNCTION [dbo].[fn_GetPaymentMethodSummary]
(
    @StartDate DATETIME,
    @EndDate DATETIME,
    @PaymentMethodCode NVARCHAR(50)
)
RETURNS @Result TABLE
(
    PaymentMethod NVARCHAR(255),
    TotalAmount DECIMAL(10,2),
    TransactionCount INT,
    Category NVARCHAR(50)
)
AS
BEGIN
    -- Category 1: Reservation payments with check-in in range
    INSERT INTO @Result
    SELECT
        pm.Name_TH AS PaymentMethod,
        SUM(ph.PaymentAmount) AS TotalAmount,
        COUNT(DISTINCT ph.ID) AS TransactionCount,
        'Category1_CheckIn' AS Category
    FROM Payment_History ph
    INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
    INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
    INNER JOIN Account_PaymentMethod pm ON ph.PaymentMethod_ID = pm.ID
    WHERE r.CheckinDate >= @StartDate
      AND r.CheckinDate <= @EndDate
      AND ar.Status = 'Normal'
      AND ph.Status = 'COMPLETED'
      AND ph.Receipt_ID IS NOT NULL
      AND (pm.Code = @PaymentMethodCode OR @PaymentMethodCode IS NULL)
    GROUP BY pm.Name_TH;

    -- Category 2: Reservation payments with receipt created in range (check-in outside)
    INSERT INTO @Result
    SELECT
        pm.Name_TH AS PaymentMethod,
        SUM(ph.PaymentAmount) AS TotalAmount,
        COUNT(DISTINCT ph.ID) AS TransactionCount,
        'Category2_Receipt' AS Category
    FROM Payment_History ph
    INNER JOIN Reservation r ON ph.Reservation_ID = r.ID
    INNER JOIN Account_Receipt ar ON ph.Receipt_ID = ar.ID
    INNER JOIN Account_PaymentMethod pm ON ph.PaymentMethod_ID = pm.ID
    WHERE ar.Created_Date >= @StartDate
      AND ar.Created_Date < DATEADD(DAY, 1, @EndDate)
      AND (r.CheckinDate < @StartDate OR r.CheckinDate > @EndDate OR r.CheckinDate IS NULL)
      AND ar.Status = 'Normal'
      AND ph.Status = 'COMPLETED'
      AND ph.Receipt_ID IS NOT NULL
      AND (pm.Code = @PaymentMethodCode OR @PaymentMethodCode IS NULL)
    GROUP BY pm.Name_TH;

    RETURN;
END
GO

PRINT '   ✓ Payment method summary function created'
GO

-- =============================================
-- 10. ADD AUDIT COLUMNS TO ACCOUNT_RECEIPT
-- =============================================

PRINT '10. Adding audit columns to Account_Receipt...'

IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]')
               AND name = 'UpdatedDate')
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD [UpdatedDate] DATETIME NULL DEFAULT GETDATE();

    PRINT '   ✓ UpdatedDate column added to Account_Receipt'
END
ELSE
BEGIN
    PRINT '   → UpdatedDate column already exists'
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]')
               AND name = 'UpdatedBy_ID')
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
        ADD [UpdatedBy_ID] SMALLINT NULL;

    PRINT '   ✓ UpdatedBy_ID column added to Account_Receipt'
END
ELSE
BEGIN
    PRINT '   → UpdatedBy_ID column already exists'
END
GO

-- =============================================
-- COMMIT TRANSACTION
-- =============================================

COMMIT TRANSACTION;

PRINT ''
PRINT '========================================='
PRINT 'PHASE3_Migration_01 completed successfully!'
PRINT '========================================='
PRINT ''
PRINT 'Summary of changes:'
PRINT '  ✓ Account_PaymentMethod lookup table created with 7 standard payment methods'
PRINT '  ✓ Payment_History extended with PaymentMethod_ID foreign key'
PRINT '  ✓ Existing Payment_History data migrated to use PaymentMethod_ID'
PRINT '  ✓ Payment_History now supports non-reservation payments (Reservation_ID nullable)'
PRINT '  ✓ Account_Receipt extended with Payment_Amount_Details JSON column'
PRINT '  ✓ Performance indexes created for faster queries'
PRINT '  ✓ Revenue reconciliation view created for automated validation'
PRINT '  ✓ Payment method summary function created for reporting'
PRINT '  ✓ Audit columns added to Account_Receipt'
PRINT ''
PRINT 'Next steps:'
PRINT '  1. Update C# code to use Account_PaymentMethod lookup table'
PRINT '  2. Update receipt creation logic to populate Payment_Amount_Details JSON'
PRINT '  3. Update revenue calculation to use new PaymentMethod_ID'
PRINT '  4. Create UI for payment method management'
PRINT '  5. Add unit tests for new functionality'
PRINT ''
GO
