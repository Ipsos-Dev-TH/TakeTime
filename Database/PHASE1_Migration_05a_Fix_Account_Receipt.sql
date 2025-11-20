-- =============================================
-- Pre-Migration 05: Fix Account_Receipt Table
-- Purpose: Ensure Account_Receipt has required columns before Migration 05
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Pre-Migration 05: Fix Account_Receipt';
PRINT '========================================';
PRINT '';

-- ===================================================================
-- Check if Account_Receipt table exists
-- ===================================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Account_Receipt')
BEGIN
    PRINT '❌ ERROR: Account_Receipt table does not exist!';
    PRINT '   Migration 05 requires this table.';
    PRINT '';
    PRINT 'SOLUTION: You need to run the migration that creates Account_Receipt first.';
    GOTO ScriptEnd
END

PRINT '✅ Account_Receipt table exists';
PRINT '';

-- ===================================================================
-- Add HasPaymentSlip column if not exists
-- ===================================================================

PRINT 'Step 1: Checking HasPaymentSlip column...';

IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Account_Receipt')
               AND name = 'HasPaymentSlip')
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
    ADD [HasPaymentSlip] [bit] NOT NULL DEFAULT 0;

    PRINT '  ✅ Added HasPaymentSlip column';
END
ELSE
BEGIN
    PRINT '  ⚠️  HasPaymentSlip column already exists';
END

PRINT '';

-- ===================================================================
-- Add PaymentSlipRequired column if not exists
-- ===================================================================

PRINT 'Step 2: Checking PaymentSlipRequired column...';

IF NOT EXISTS (SELECT * FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.Account_Receipt')
               AND name = 'PaymentSlipRequired')
BEGIN
    ALTER TABLE [dbo].[Account_Receipt]
    ADD [PaymentSlipRequired] [bit] NOT NULL DEFAULT 0;

    PRINT '  ✅ Added PaymentSlipRequired column';
END
ELSE
BEGIN
    PRINT '  ⚠️  PaymentSlipRequired column already exists';
END

PRINT '';

-- ===================================================================
-- Verification
-- ===================================================================

PRINT '========================================';
PRINT 'Verification';
PRINT '========================================';

DECLARE @HasPaymentSlipExists bit = 0;
DECLARE @PaymentSlipRequiredExists bit = 0;

IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Account_Receipt')
           AND name = 'HasPaymentSlip')
    SET @HasPaymentSlipExists = 1;

IF EXISTS (SELECT * FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.Account_Receipt')
           AND name = 'PaymentSlipRequired')
    SET @PaymentSlipRequiredExists = 1;

IF @HasPaymentSlipExists = 1
    PRINT '✅ HasPaymentSlip column exists';
ELSE
    PRINT '❌ HasPaymentSlip column MISSING';

IF @PaymentSlipRequiredExists = 1
    PRINT '✅ PaymentSlipRequired column exists';
ELSE
    PRINT '❌ PaymentSlipRequired column MISSING';

PRINT '';

IF @HasPaymentSlipExists = 1 AND @PaymentSlipRequiredExists = 1
BEGIN
    PRINT '========================================';
    PRINT '✅ Account_Receipt is ready!';
    PRINT '========================================';
    PRINT '';
    PRINT 'You can now run:';
    PRINT '  PHASE1_Migration_05_Payment_Slips.sql';
END
ELSE
BEGIN
    PRINT '========================================';
    PRINT '⚠️  Warning: Some columns are missing';
    PRINT '========================================';
    PRINT '';
    PRINT 'Check if there were any errors above.';
    PRINT 'You may need to manually add the columns.';
END

ScriptEnd:
PRINT '';
PRINT 'Script completed.';
GO
