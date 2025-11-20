-- =============================================
-- Migration 05b: Payment Slips System (Simplified - No GO statements)
-- Purpose: Create Payment_Slips table without updating Account_Receipt
-- Date: 2025-11-05
-- =============================================

USE [Taketime];

PRINT '========================================';
PRINT 'Migration 05b: Payment Slips (Simple)';
PRINT '========================================';
PRINT '';

-- ===================================================================
-- Pre-flight checks
-- ===================================================================

PRINT 'Running pre-flight checks...';
PRINT '';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservation')
BEGIN
    PRINT '❌ ERROR: Reservation table does not exist!';
    PRINT '   Cannot create Payment_Slips without Reservation table.';
    PRINT '';
    -- Don't proceed but don't error out
    SET NOEXEC ON;
END
ELSE
BEGIN
    PRINT '✅ Reservation table exists';
    PRINT '';
END

-- ===================================================================
-- Step 1: Create Payment_Slips table
-- ===================================================================

PRINT 'Step 1: Creating Payment_Slips table...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    CREATE TABLE [dbo].[Payment_Slips](
        [ID] [bigint] IDENTITY(1,1) NOT NULL,
        [Account_Receipt_ID] [bigint] NULL,
        [Reservation_ID] [bigint] NOT NULL,
        [SlipFileURL] [nvarchar](500) NOT NULL,
        [FileName] [nvarchar](255) NOT NULL,
        [FileType] [nvarchar](50) NULL,
        [FileSize] [int] NULL,
        [UploadedDate] [datetime] NOT NULL DEFAULT GETDATE(),
        [UploadedBy_ID] [smallint] NULL,
        [UploadedBy_CustomerPhone] [nvarchar](20) NULL,
        [IsVerified] [bit] NOT NULL DEFAULT 0,
        [VerifiedBy_ID] [smallint] NULL,
        [VerifiedDate] [datetime] NULL,
        [VerificationStatus] [nvarchar](20) NOT NULL DEFAULT 'PENDING',
        [RejectionReason] [nvarchar](500) NULL,
        [Notes] [nvarchar](1000) NULL,
        [IsActive] [bit] NOT NULL DEFAULT 1,
        [Status] [tinyint] NOT NULL DEFAULT 1,
        CONSTRAINT [PK_Payment_Slips] PRIMARY KEY CLUSTERED ([ID] ASC)
    );

    PRINT '  ✅ Payment_Slips table created';
END
ELSE
BEGIN
    PRINT '  ⚠️  Payment_Slips table already exists';
END

PRINT '';

-- ===================================================================
-- Step 2: Add foreign keys
-- ===================================================================

PRINT 'Step 2: Creating foreign keys...';

-- FK to Reservation (REQUIRED)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Reservation')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Reservation]
        FOREIGN KEY ([Reservation_ID]) REFERENCES [dbo].[Reservation]([ID]) ON DELETE CASCADE;

        PRINT '  ✅ FK_Payment_Slips_Reservation created';
    END
    ELSE
    BEGIN
        PRINT '  ⚠️  FK_Payment_Slips_Reservation already exists';
    END
END

-- FK to Account_Receipt (OPTIONAL)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Account_Receipt')
   AND EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_AccountReceipt')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_AccountReceipt]
        FOREIGN KEY ([Account_Receipt_ID]) REFERENCES [dbo].[Account_Receipt]([ID]);

        PRINT '  ✅ FK_Payment_Slips_AccountReceipt created';
    END
END
ELSE
BEGIN
    PRINT '  ⚠️  Account_Receipt table not found - skipping FK';
END

-- FK to Admin (OPTIONAL)
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Admin')
   AND EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_UploadedBy')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_UploadedBy]
        FOREIGN KEY ([UploadedBy_ID]) REFERENCES [dbo].[Admin]([ID]);

        PRINT '  ✅ FK_Payment_Slips_UploadedBy created';
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_VerifiedBy')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_VerifiedBy]
        FOREIGN KEY ([VerifiedBy_ID]) REFERENCES [dbo].[Admin]([ID]);

        PRINT '  ✅ FK_Payment_Slips_VerifiedBy created';
    END
END

PRINT '';

-- ===================================================================
-- Step 3: Create indexes
-- ===================================================================

PRINT 'Step 3: Creating indexes...';

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_ReservationID')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_ReservationID]
        ON [dbo].[Payment_Slips] ([Reservation_ID], [UploadedDate] DESC);

        PRINT '  ✅ IX_Payment_Slips_ReservationID created';
    END

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_PendingVerification')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_PendingVerification]
        ON [dbo].[Payment_Slips] ([VerificationStatus], [UploadedDate] DESC)
        WHERE [IsActive] = 1 AND [Status] = 1;

        PRINT '  ✅ IX_Payment_Slips_PendingVerification created';
    END

    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Account_Receipt')
    BEGIN
        IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_ReceiptID')
        BEGIN
            CREATE NONCLUSTERED INDEX [IX_Payment_Slips_ReceiptID]
            ON [dbo].[Payment_Slips] ([Account_Receipt_ID])
            INCLUDE ([VerificationStatus], [IsActive]);

            PRINT '  ✅ IX_Payment_Slips_ReceiptID created';
        END
    END
END

PRINT '';

-- ===================================================================
-- Verification
-- ===================================================================

PRINT '========================================';
PRINT 'Verification';
PRINT '========================================';

DECLARE @TableExists bit = 0;
DECLARE @IndexCount int = 0;
DECLARE @FKCount int = 0;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    SET @TableExists = 1;
    PRINT '✅ Payment_Slips table exists';

    SELECT @IndexCount = COUNT(*)
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Payment_Slips')
    AND name LIKE 'IX_%';

    SELECT @FKCount = COUNT(*)
    FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('dbo.Payment_Slips');

    PRINT '✅ Created ' + CAST(@IndexCount AS varchar(10)) + ' indexes';
    PRINT '✅ Created ' + CAST(@FKCount AS varchar(10)) + ' foreign keys';
END
ELSE
BEGIN
    PRINT '❌ Payment_Slips table MISSING';
    PRINT '   Check errors above for details.';
END

PRINT '';
PRINT '========================================';

IF @TableExists = 1
BEGIN
    PRINT '✅ Migration 05b completed successfully!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Payment_Slips table is ready to use.';
END
ELSE
BEGIN
    PRINT '❌ Migration 05b FAILED!';
    PRINT '========================================';
    PRINT '';
    PRINT 'Payment_Slips table was not created.';
    PRINT 'Please check the errors above.';
END

PRINT '';
PRINT 'Note: This version does NOT modify Account_Receipt table.';
PRINT 'If you need Account_Receipt columns, run:';
PRINT '  PHASE1_Migration_05a_Fix_Account_Receipt.sql';
PRINT '';

-- Re-enable execution if it was disabled
SET NOEXEC OFF;
