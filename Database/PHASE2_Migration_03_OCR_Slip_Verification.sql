/*==============================================================
  Migration: PHASE2_03_OCR_Slip_Verification

  Purpose:
  - Add OCR capabilities to Payment_Slips table
  - Track OCR processing results and confidence
  - Enable automatic slip amount verification

  Features:
  - OCR_Amount: Amount detected from slip image
  - OCR_Status: SUCCESS/FAILED/PENDING/SKIPPED
  - OCR_Confidence: Confidence level (0-100%)
  - OCR_RawText: Full text extracted (for debugging)
  - OCR_ProcessedDate: When OCR was performed

  Business Rules:
  - OCR runs automatically on slip upload
  - If OCR fails or amount mismatch: Warning only, booking continues
  - Admin can manually verify slips in verification page
  - OCR_Confidence < 70%: Auto-flag for manual review

  Author: Claude
  Date: 2025-11-05
==============================================================*/

USE [Taketime]
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MigrationName NVARCHAR(255) = 'PHASE2_03_OCR_Slip_Verification'
DECLARE @StartTime DATETIME = GETDATE()

PRINT ''
PRINT '============================================================='
PRINT 'Migration: ' + @MigrationName
PRINT 'Started: ' + CONVERT(VARCHAR, @StartTime, 120)
PRINT '============================================================='
PRINT ''

-- ===================================================================
-- Check if migration already applied
-- ===================================================================

IF EXISTS (SELECT 1 FROM Database_Migrations WHERE MigrationName = @MigrationName AND Success = 1)
BEGIN
    PRINT '⚠ Migration already applied: ' + @MigrationName
    PRINT '⚠ Skipping execution'
    GOTO MigrationEnd
END

-- ===================================================================
-- Begin Transaction
-- ===================================================================

BEGIN TRANSACTION

BEGIN TRY

    PRINT 'Step 1: Adding OCR columns to Payment_Slips table...'
    PRINT ''

    -- OCR_Amount
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_Amount')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_Amount] [decimal](18, 2) NULL

        PRINT '  ✓ Added OCR_Amount column'
    END
    ELSE
        PRINT '  ⚠ OCR_Amount already exists'

    -- OCR_Status
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_Status')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_Status] [nvarchar](20) NULL DEFAULT 'PENDING'

        PRINT '  ✓ Added OCR_Status column'
    END
    ELSE
        PRINT '  ⚠ OCR_Status already exists'

    -- OCR_Confidence
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_Confidence')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_Confidence] [decimal](5, 2) NULL

        PRINT '  ✓ Added OCR_Confidence column'
    END
    ELSE
        PRINT '  ⚠ OCR_Confidence already exists'

    -- OCR_RawText
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_RawText')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_RawText] [nvarchar](MAX) NULL

        PRINT '  ✓ Added OCR_RawText column'
    END
    ELSE
        PRINT '  ⚠ OCR_RawText already exists'

    -- OCR_ProcessedDate
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_ProcessedDate')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_ProcessedDate] [datetime] NULL

        PRINT '  ✓ Added OCR_ProcessedDate column'
    END
    ELSE
        PRINT '  ⚠ OCR_ProcessedDate already exists'

    -- OCR_ErrorMessage
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND name = 'OCR_ErrorMessage')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD [OCR_ErrorMessage] [nvarchar](1000) NULL

        PRINT '  ✓ Added OCR_ErrorMessage column'
    END
    ELSE
        PRINT '  ⚠ OCR_ErrorMessage already exists'

    PRINT ''
    PRINT 'Step 2: Adding check constraints...'
    PRINT ''

    -- OCR_Status constraint
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_OCR_Status')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_OCR_Status]
        CHECK ([OCR_Status] IN ('PENDING', 'SUCCESS', 'FAILED', 'SKIPPED', 'MANUAL_REVIEW'))

        PRINT '  ✓ Added CK_Payment_Slips_OCR_Status'
    END

    -- OCR_Confidence constraint (0-100%)
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_OCR_Confidence')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_OCR_Confidence]
        CHECK ([OCR_Confidence] IS NULL OR ([OCR_Confidence] >= 0 AND [OCR_Confidence] <= 100))

        PRINT '  ✓ Added CK_Payment_Slips_OCR_Confidence'
    END

    -- OCR_Amount must be positive
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_OCR_Amount')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_OCR_Amount]
        CHECK ([OCR_Amount] IS NULL OR [OCR_Amount] >= 0)

        PRINT '  ✓ Added CK_Payment_Slips_OCR_Amount'
    END

    PRINT ''
    PRINT 'Step 3: Creating indexes for OCR queries...'
    PRINT ''

    -- Index for pending OCR processing
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_OCR_Pending')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_OCR_Pending]
        ON [dbo].[Payment_Slips] ([OCR_Status], [UploadedDate] DESC)
        WHERE [IsActive] = 1 AND [Status] = 1 AND [OCR_Status] = 'PENDING'

        PRINT '  ✓ Created IX_Payment_Slips_OCR_Pending'
    END

    -- Index for manual review (low confidence or failed)
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_Manual_Review')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_Manual_Review]
        ON [dbo].[Payment_Slips] ([VerificationStatus], [OCR_Status], [UploadedDate] DESC)
        WHERE [IsActive] = 1 AND [Status] = 1 AND [VerificationStatus] = 'PENDING'

        PRINT '  ✓ Created IX_Payment_Slips_Manual_Review'
    END

    PRINT ''
    PRINT 'Step 4: Updating existing records...'
    PRINT ''

    -- Set OCR_Status to SKIPPED for existing slips
    UPDATE Payment_Slips
    SET OCR_Status = 'SKIPPED'
    WHERE OCR_Status IS NULL

    DECLARE @UpdatedCount INT = @@ROWCOUNT
    PRINT '  ✓ Set ' + CAST(@UpdatedCount AS VARCHAR) + ' existing slips to OCR_Status = SKIPPED'

    COMMIT TRANSACTION

    PRINT ''
    PRINT '✓ Migration completed successfully!'
    PRINT ''

    -- Record successful migration
    DECLARE @ExecutionTime INT = DATEDIFF(MILLISECOND, @StartTime, GETDATE())

    EXEC sp_RecordMigration
        @MigrationName = @MigrationName,
        @AppliedBy = SYSTEM_USER,
        @Success = 1,
        @ExecutionTimeMs = @ExecutionTime

    PRINT '============================================================='
    PRINT 'Execution time: ' + CAST(@ExecutionTime AS VARCHAR) + ' ms'
    PRINT '============================================================='

END TRY

BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
    DECLARE @ErrorLine INT = ERROR_LINE()

    PRINT ''
    PRINT '============================================================='
    PRINT '✗ Migration failed!'
    PRINT '============================================================='
    PRINT 'Error: ' + @ErrorMessage
    PRINT 'Line: ' + CAST(@ErrorLine AS VARCHAR)
    PRINT '============================================================='

    -- Record failed migration
    EXEC sp_RecordMigration
        @MigrationName = @MigrationName,
        @AppliedBy = SYSTEM_USER,
        @Success = 0,
        @ErrorMessage = @ErrorMessage

    RAISERROR(@ErrorMessage, 16, 1)
END CATCH

MigrationEnd:

PRINT ''
PRINT 'Migration script ended: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT ''

GO
