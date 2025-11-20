/*==============================================================
  Migration: 05_Add_Payment_Slips_System

  Purpose:
  - Add support for payment slip uploads
  - Track payment evidence/receipts for all payments
  - Required for first booking, optional for other payments
  - Support multiple file formats (images, PDFs)

  Features:
  - Payment_Slips table for storing slip information
  - Link slips to payments in Account_Receipt
  - Track upload status and verification
  - Support file metadata (size, type, upload date)
  - Migration tracking and rollback support

  Business Rules:
  - First payment on reservation: Slip REQUIRED
  - Subsequent payments: Slip OPTIONAL
  - Admin can verify/reject slips
  - Slips can be re-uploaded if rejected

  Author: Claude
  Date: 2025-11-03
  Dependencies: 00_Init_Migration_System
==============================================================*/

USE [Taketime]
GO

SET NOCOUNT ON;
SET XACT_ABORT ON; -- Rollback entire transaction on any error

DECLARE @MigrationName NVARCHAR(255) = '05_Add_Payment_Slips_System'
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

DECLARE @IsApplied BIT = 0

IF EXISTS (SELECT 1 FROM Database_Migrations WHERE MigrationName = @MigrationName AND Success = 1)
BEGIN
    SET @IsApplied = 1
    PRINT '⚠ Migration already applied: ' + @MigrationName
    PRINT '⚠ Skipping execution'
    PRINT ''

    -- Still show as success but don't re-run
    GOTO MigrationEnd
END

-- ===================================================================
-- Pre-flight checks
-- ===================================================================

PRINT 'Running pre-flight checks...'
PRINT ''

-- Check if Account_Receipt table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND type in (N'U'))
BEGIN
    RAISERROR('✗ Account_Receipt table does not exist. Cannot proceed.', 16, 1)
    GOTO MigrationEnd
END

PRINT '✓ Account_Receipt table exists'

-- Check if Reservation table exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Reservation]') AND type in (N'U'))
BEGIN
    RAISERROR('✗ Reservation table does not exist. Cannot proceed.', 16, 1)
    GOTO MigrationEnd
END

PRINT '✓ Reservation table exists'

PRINT ''

-- ===================================================================
-- Begin Transaction
-- ===================================================================

BEGIN TRANSACTION

BEGIN TRY

    PRINT 'Starting database modifications...'
    PRINT ''

    -- ===================================================================
    -- Step 1: Create Payment_Slips table
    -- ===================================================================

    PRINT 'Step 1: Creating Payment_Slips table...'

    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND type in (N'U'))
    BEGIN
        CREATE TABLE [dbo].[Payment_Slips](
            [ID] [bigint] IDENTITY(1,1) NOT NULL,
            [Account_Receipt_ID] [bigint] NOT NULL,
            [Reservation_ID] [bigint] NOT NULL,
            [SlipFileURL] [nvarchar](500) NOT NULL,
            [FileName] [nvarchar](255) NOT NULL,
            [FileType] [nvarchar](50) NULL, -- image/jpeg, image/png, application/pdf
            [FileSize] [int] NULL, -- in bytes
            [UploadedDate] [datetime] NOT NULL DEFAULT GETDATE(),
            [UploadedBy_ID] [smallint] NULL,
            [UploadedBy_CustomerPhone] [nvarchar](20) NULL, -- If customer uploads
            [IsVerified] [bit] NOT NULL DEFAULT 0,
            [VerifiedBy_ID] [smallint] NULL,
            [VerifiedDate] [datetime] NULL,
            [VerificationStatus] [nvarchar](20) NOT NULL DEFAULT 'PENDING', -- PENDING, APPROVED, REJECTED
            [RejectionReason] [nvarchar](500) NULL,
            [Notes] [nvarchar](1000) NULL,
            [IsActive] [bit] NOT NULL DEFAULT 1,
            [Status] [tinyint] NOT NULL DEFAULT 1,
         CONSTRAINT [PK_Payment_Slips] PRIMARY KEY CLUSTERED
        (
            [ID] ASC
        )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
        ) ON [PRIMARY]

        PRINT '  ✓ Created Payment_Slips table'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ Payment_Slips table already exists'
    END

    -- ===================================================================
    -- Step 2: Add foreign key constraints
    -- ===================================================================

    PRINT 'Step 2: Adding foreign key constraints...'

    -- FK to Account_Receipt
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Account_Receipt')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Account_Receipt]
        FOREIGN KEY([Account_Receipt_ID])
        REFERENCES [dbo].[Account_Receipt] ([ID])
        ON UPDATE CASCADE
        ON DELETE CASCADE

        PRINT '  ✓ Added FK_Payment_Slips_Account_Receipt'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ FK_Payment_Slips_Account_Receipt already exists'
    END

    -- FK to Reservation
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Reservation')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Reservation]
        FOREIGN KEY([Reservation_ID])
        REFERENCES [dbo].[Reservation] ([ID])
        ON UPDATE NO ACTION
        ON DELETE NO ACTION

        PRINT '  ✓ Added FK_Payment_Slips_Reservation'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ FK_Payment_Slips_Reservation already exists'
    END

    -- FK to Employees (UploadedBy)
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Employees_Upload')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Employees_Upload]
        FOREIGN KEY([UploadedBy_ID])
        REFERENCES [dbo].[Employees] ([ID])
        ON UPDATE NO ACTION
        ON DELETE SET NULL

        PRINT '  ✓ Added FK_Payment_Slips_Employees_Upload'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ FK_Payment_Slips_Employees_Upload already exists'
    END

    -- FK to Employees (VerifiedBy)
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Employees_Verify')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Employees_Verify]
        FOREIGN KEY([VerifiedBy_ID])
        REFERENCES [dbo].[Employees] ([ID])
        ON UPDATE NO ACTION
        ON DELETE SET NULL

        PRINT '  ✓ Added FK_Payment_Slips_Employees_Verify'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ FK_Payment_Slips_Employees_Verify already exists'
    END

    -- FK to Customer (UploadedBy_CustomerPhone)
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_Slips_Customer')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [FK_Payment_Slips_Customer]
        FOREIGN KEY([UploadedBy_CustomerPhone])
        REFERENCES [dbo].[Customer] ([MobilePhone])
        ON UPDATE CASCADE
        ON DELETE SET NULL

        PRINT '  ✓ Added FK_Payment_Slips_Customer'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ FK_Payment_Slips_Customer already exists'
    END

    -- ===================================================================
    -- Step 3: Add check constraints
    -- ===================================================================

    PRINT 'Step 3: Adding check constraints...'

    -- VerificationStatus must be valid
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_VerificationStatus')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_VerificationStatus]
        CHECK ([VerificationStatus] IN ('PENDING', 'APPROVED', 'REJECTED'))

        PRINT '  ✓ Added CK_Payment_Slips_VerificationStatus'
    END

    -- FileSize must be positive
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_FileSize')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_FileSize]
        CHECK ([FileSize] IS NULL OR [FileSize] > 0)

        PRINT '  ✓ Added CK_Payment_Slips_FileSize'
    END

    -- Status must be 0 or 1
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_Status')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_Status]
        CHECK ([Status] IN (0, 1))

        PRINT '  ✓ Added CK_Payment_Slips_Status'
    END

    -- If verified, must have verifier and date
    IF NOT EXISTS (SELECT * FROM sys.check_constraints WHERE name = 'CK_Payment_Slips_Verification')
    BEGIN
        ALTER TABLE [dbo].[Payment_Slips]
        ADD CONSTRAINT [CK_Payment_Slips_Verification]
        CHECK (
            ([IsVerified] = 0) OR
            ([IsVerified] = 1 AND [VerifiedBy_ID] IS NOT NULL AND [VerifiedDate] IS NOT NULL)
        )

        PRINT '  ✓ Added CK_Payment_Slips_Verification'
    END

    -- ===================================================================
    -- Step 4: Add columns to Account_Receipt table
    -- ===================================================================

    PRINT 'Step 4: Adding columns to Account_Receipt table...'

    -- HasPaymentSlip flag
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND name = 'HasPaymentSlip')
    BEGIN
        ALTER TABLE [dbo].[Account_Receipt]
        ADD [HasPaymentSlip] [bit] NOT NULL DEFAULT 0

        PRINT '  ✓ Added HasPaymentSlip column'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ HasPaymentSlip column already exists'
    END

    -- PaymentSlipRequired flag
    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND name = 'PaymentSlipRequired')
    BEGIN
        ALTER TABLE [dbo].[Account_Receipt]
        ADD [PaymentSlipRequired] [bit] NOT NULL DEFAULT 0

        PRINT '  ✓ Added PaymentSlipRequired column'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ PaymentSlipRequired column already exists'
    END

    -- ===================================================================
    -- Step 5: Add indexes for performance
    -- ===================================================================

    PRINT 'Step 5: Creating performance indexes...'

    -- Index on Account_Receipt_ID
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_ReceiptID')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_ReceiptID]
        ON [dbo].[Payment_Slips] ([Account_Receipt_ID])
        INCLUDE ([VerificationStatus], [IsActive])

        PRINT '  ✓ Created IX_Payment_Slips_ReceiptID'
    END

    -- Index on Reservation_ID
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_ReservationID')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_ReservationID]
        ON [dbo].[Payment_Slips] ([Reservation_ID], [UploadedDate] DESC)

        PRINT '  ✓ Created IX_Payment_Slips_ReservationID'
    END

    -- Index for pending verification
    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_Slips_PendingVerification')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Payment_Slips_PendingVerification]
        ON [dbo].[Payment_Slips] ([VerificationStatus], [UploadedDate] DESC)
        WHERE [IsActive] = 1 AND [Status] = 1 AND [VerificationStatus] = 'PENDING'

        PRINT '  ✓ Created IX_Payment_Slips_PendingVerification'
    END

    -- ===================================================================
    -- Step 6: Create stored procedures
    -- ===================================================================

    PRINT 'Step 6: Creating stored procedures...'

    -- Procedure: Upload payment slip
    IF OBJECT_ID('dbo.sp_UploadPaymentSlip', 'P') IS NOT NULL
        DROP PROCEDURE dbo.sp_UploadPaymentSlip

    EXEC('
    CREATE PROCEDURE [dbo].[sp_UploadPaymentSlip]
        @AccountReceiptID BIGINT,
        @ReservationID BIGINT,
        @SlipFileURL NVARCHAR(500),
        @FileName NVARCHAR(255),
        @FileType NVARCHAR(50) = NULL,
        @FileSize INT = NULL,
        @UploadedBy_ID SMALLINT = NULL,
        @UploadedBy_CustomerPhone NVARCHAR(20) = NULL,
        @Notes NVARCHAR(1000) = NULL,
        @SlipID BIGINT OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;

        BEGIN TRY
            BEGIN TRANSACTION

            -- Insert payment slip
            INSERT INTO Payment_Slips (
                Account_Receipt_ID,
                Reservation_ID,
                SlipFileURL,
                FileName,
                FileType,
                FileSize,
                UploadedBy_ID,
                UploadedBy_CustomerPhone,
                Notes
            )
            VALUES (
                @AccountReceiptID,
                @ReservationID,
                @SlipFileURL,
                @FileName,
                @FileType,
                @FileSize,
                @UploadedBy_ID,
                @UploadedBy_CustomerPhone,
                @Notes
            )

            SET @SlipID = SCOPE_IDENTITY()

            -- Update Account_Receipt flag
            UPDATE Account_Receipt
            SET HasPaymentSlip = 1
            WHERE ID = @AccountReceiptID

            COMMIT TRANSACTION

            RETURN 0 -- Success
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION

            DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN 1 -- Error
        END CATCH
    END
    ')

    PRINT '  ✓ Created sp_UploadPaymentSlip'

    -- Procedure: Verify payment slip
    IF OBJECT_ID('dbo.sp_VerifyPaymentSlip', 'P') IS NOT NULL
        DROP PROCEDURE dbo.sp_VerifyPaymentSlip

    EXEC('
    CREATE PROCEDURE [dbo].[sp_VerifyPaymentSlip]
        @SlipID BIGINT,
        @VerifiedBy_ID SMALLINT,
        @IsApproved BIT,
        @RejectionReason NVARCHAR(500) = NULL,
        @Notes NVARCHAR(1000) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        BEGIN TRY
            BEGIN TRANSACTION

            DECLARE @Status NVARCHAR(20)
            SET @Status = CASE WHEN @IsApproved = 1 THEN ''APPROVED'' ELSE ''REJECTED'' END

            UPDATE Payment_Slips
            SET
                IsVerified = 1,
                VerifiedBy_ID = @VerifiedBy_ID,
                VerifiedDate = GETDATE(),
                VerificationStatus = @Status,
                RejectionReason = @RejectionReason,
                Notes = COALESCE(@Notes, Notes)
            WHERE ID = @SlipID

            IF @@ROWCOUNT = 0
            BEGIN
                RAISERROR(''Payment slip not found'', 16, 1)
                RETURN 1
            END

            COMMIT TRANSACTION

            RETURN 0 -- Success
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0
                ROLLBACK TRANSACTION

            DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
            RAISERROR(@ErrorMessage, 16, 1)
            RETURN 1 -- Error
        END CATCH
    END
    ')

    PRINT '  ✓ Created sp_VerifyPaymentSlip'

    -- Procedure: Get payment slips for reservation
    IF OBJECT_ID('dbo.sp_GetPaymentSlips', 'P') IS NOT NULL
        DROP PROCEDURE dbo.sp_GetPaymentSlips

    EXEC('
    CREATE PROCEDURE [dbo].[sp_GetPaymentSlips]
        @ReservationID BIGINT = NULL,
        @AccountReceiptID BIGINT = NULL,
        @VerificationStatus NVARCHAR(20) = NULL
    AS
    BEGIN
        SET NOCOUNT ON;

        SELECT
            PS.ID,
            PS.Account_Receipt_ID,
            PS.Reservation_ID,
            PS.SlipFileURL,
            PS.FileName,
            PS.FileType,
            PS.FileSize,
            PS.UploadedDate,
            PS.UploadedBy_ID,
            PS.UploadedBy_CustomerPhone,
            PS.IsVerified,
            PS.VerifiedBy_ID,
            PS.VerifiedDate,
            PS.VerificationStatus,
            PS.RejectionReason,
            PS.Notes,
            PS.IsActive,
            PS.Status,
            E1.Name AS UploadedByEmployeeName,
            C.Name AS UploadedByCustomerName,
            E2.Name AS VerifiedByName,
            AR.Total_Amount AS PaymentAmount,
            AR.Type_ID AS PaymentTypeID
        FROM Payment_Slips PS
        LEFT JOIN Employees E1 ON PS.UploadedBy_ID = E1.ID
        LEFT JOIN Customer C ON PS.UploadedBy_CustomerPhone = C.MobilePhone
        LEFT JOIN Employees E2 ON PS.VerifiedBy_ID = E2.ID
        LEFT JOIN Account_Receipt AR ON PS.Account_Receipt_ID = AR.ID
        WHERE (@ReservationID IS NULL OR PS.Reservation_ID = @ReservationID)
          AND (@AccountReceiptID IS NULL OR PS.Account_Receipt_ID = @AccountReceiptID)
          AND (@VerificationStatus IS NULL OR PS.VerificationStatus = @VerificationStatus)
          AND PS.IsActive = 1
          AND PS.Status = 1
        ORDER BY PS.UploadedDate DESC
    END
    ')

    PRINT '  ✓ Created sp_GetPaymentSlips'

    -- Procedure: Check if payment slip required
    IF OBJECT_ID('dbo.sp_IsPaymentSlipRequired', 'P') IS NOT NULL
        DROP PROCEDURE dbo.sp_IsPaymentSlipRequired

    EXEC('
    CREATE PROCEDURE [dbo].[sp_IsPaymentSlipRequired]
        @ReservationID BIGINT,
        @IsRequired BIT OUTPUT
    AS
    BEGIN
        SET NOCOUNT ON;

        -- Payment slip is required if this is the first payment for the reservation
        DECLARE @PaymentCount INT

        SELECT @PaymentCount = COUNT(*)
        FROM Account_Receipt
        WHERE Reservation_ID = @ReservationID

        IF @PaymentCount = 0
            SET @IsRequired = 1 -- First payment: REQUIRED
        ELSE
            SET @IsRequired = 0 -- Subsequent payments: OPTIONAL

        RETURN 0
    END
    ')

    PRINT '  ✓ Created sp_IsPaymentSlipRequired'

    -- ===================================================================
    -- Step 7: Create views
    -- ===================================================================

    PRINT 'Step 7: Creating views...'

    IF OBJECT_ID('dbo.v_PaymentSlipsSummary', 'V') IS NOT NULL
        DROP VIEW dbo.v_PaymentSlipsSummary

    EXEC('
    CREATE VIEW [dbo].[v_PaymentSlipsSummary]
    AS
    SELECT
        PS.ID AS SlipID,
        PS.Account_Receipt_ID AS ReceiptID,
        PS.Reservation_ID AS ReservationID,
        R.ID AS ReservationNumber,
        C.Name AS CustomerName,
        C.MobilePhone AS CustomerPhone,
        AR.Total_Amount AS PaymentAmount,
        PT.PaymentTypeName,
        PS.SlipFileURL,
        PS.FileName,
        PS.FileType,
        PS.FileSize,
        PS.UploadedDate,
        COALESCE(E1.Name, C.Name) AS UploadedByName,
        PS.VerificationStatus,
        PS.IsVerified,
        E2.Name AS VerifiedByName,
        PS.VerifiedDate,
        PS.RejectionReason,
        PS.Notes,
        R.CheckinDate,
        R.CheckoutDate,
        R.StatusReserve
    FROM Payment_Slips PS
    INNER JOIN Account_Receipt AR ON PS.Account_Receipt_ID = AR.ID
    INNER JOIN Reservation R ON PS.Reservation_ID = R.ID
    INNER JOIN Customer C ON R.Customer_MobilePhone = C.MobilePhone
    LEFT JOIN PaymentType PT ON AR.Type_ID = PT.ID
    LEFT JOIN Employees E1 ON PS.UploadedBy_ID = E1.ID
    LEFT JOIN Employees E2 ON PS.VerifiedBy_ID = E2.ID
    WHERE PS.IsActive = 1 AND PS.Status = 1
    ')

    PRINT '  ✓ Created v_PaymentSlipsSummary'

    -- ===================================================================
    -- Step 8: Mark first payments as requiring slip
    -- ===================================================================

    PRINT 'Step 8: Marking first payments as requiring slip...'

    -- Check if PaymentSlipRequired column exists before updating
    IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND name = 'PaymentSlipRequired')
    BEGIN
        DECLARE @UpdatedCount INT

        -- For existing reservations, mark the first payment as requiring slip
        UPDATE AR
        SET PaymentSlipRequired = 1
        FROM Account_Receipt AR
        INNER JOIN (
            SELECT
                Reservation_ID,
                MIN(ID) AS FirstReceiptID
            FROM Account_Receipt
            WHERE Status = 1
            GROUP BY Reservation_ID
        ) FirstPayments ON AR.ID = FirstPayments.FirstReceiptID

        SET @UpdatedCount = @@ROWCOUNT

        PRINT '  ✓ Marked ' + CAST(@UpdatedCount AS VARCHAR) + ' first payments as requiring slip'
    END
    ELSE
    BEGIN
        PRINT '  ⚠ PaymentSlipRequired column not found - skipping marking first payments'
    END

    -- ===================================================================
    -- Commit Transaction
    -- ===================================================================

    COMMIT TRANSACTION

    PRINT ''
    PRINT '✓ All database modifications completed successfully'
    PRINT ''

    -- ===================================================================
    -- Record successful migration
    -- ===================================================================

    DECLARE @ExecutionTime INT = DATEDIFF(MILLISECOND, @StartTime, GETDATE())

    EXEC sp_RecordMigration
        @MigrationName = @MigrationName,
        @AppliedBy = SYSTEM_USER,
        @Success = 1,
        @ExecutionTimeMs = @ExecutionTime

    PRINT '============================================================='
    PRINT 'Migration completed successfully!'
    PRINT 'Execution time: ' + CAST(@ExecutionTime AS VARCHAR) + ' ms'
    PRINT '============================================================='
    PRINT ''

    -- ===================================================================
    -- Post-migration verification
    -- ===================================================================

    PRINT 'Post-migration verification:'
    PRINT ''

    -- Check Payment_Slips table
    DECLARE @SlipTableCount INT
    SELECT @SlipTableCount = COUNT(*)
    FROM sys.objects
    WHERE object_id = OBJECT_ID(N''[dbo].[Payment_Slips]'')
      AND type in (N''U'')

    IF @SlipTableCount = 1
        PRINT ''✓ Payment_Slips table exists''
    ELSE
        PRINT ''✗ Payment_Slips table verification failed''

    -- Check Account_Receipt columns
    DECLARE @ReceiptColCount INT
    SELECT @ReceiptColCount = COUNT(*)
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N''[dbo].[Account_Receipt]'')
      AND name IN (''HasPaymentSlip'', ''PaymentSlipRequired'')

    IF @ReceiptColCount >= 1
        PRINT ''✓ Account_Receipt columns added ('' + CAST(@ReceiptColCount AS VARCHAR) + ''/2)''
    ELSE
        PRINT ''⚠ No Account_Receipt columns were added (table might not exist)''

    -- Check stored procedures
    DECLARE @SPCount INT
    SELECT @SPCount = COUNT(*)
    FROM sys.procedures
    WHERE name IN (''sp_UploadPaymentSlip'', ''sp_VerifyPaymentSlip'', ''sp_GetPaymentSlips'', ''sp_IsPaymentSlipRequired'')

    IF @SPCount = 4
        PRINT ''✓ All stored procedures created''
    ELSE
        PRINT ''✗ Stored procedures verification failed''

    PRINT ''''
    PRINT ''Business rules:''
    PRINT ''  - First payment on reservation: Slip REQUIRED''
    PRINT ''  - Subsequent payments: Slip OPTIONAL''
    PRINT ''  - Admin must verify/approve slips''
    PRINT ''''
    PRINT ''Next steps:''
    PRINT ''  1. Implement file upload functionality in UI''
    PRINT ''  2. Create admin interface for slip verification''
    PRINT ''  3. Add slip display in reservation details''
    PRINT ''''

END TRY

BEGIN CATCH
    -- ===================================================================
    -- Error Handling
    -- ===================================================================

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE()
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY()
    DECLARE @ErrorState INT = ERROR_STATE()
    DECLARE @ErrorLine INT = ERROR_LINE()

    PRINT ''
    PRINT ''=============================================================''
    PRINT ''✗ Migration failed!''
    PRINT ''=============================================================''
    PRINT ''Error: '' + @ErrorMessage
    PRINT ''Line: '' + CAST(@ErrorLine AS VARCHAR)
    PRINT ''=============================================================''
    PRINT ''''

    -- Record failed migration
    EXEC sp_RecordMigration
        @MigrationName = @MigrationName,
        @AppliedBy = SYSTEM_USER,
        @Success = 0,
        @ErrorMessage = @ErrorMessage

    -- Re-throw error
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState)
END CATCH

MigrationEnd:

PRINT ''
PRINT 'Migration script ended: ' + CONVERT(VARCHAR, GETDATE(), 120)
PRINT ''

GO

/*==============================================================
  ROLLBACK SCRIPT (if needed)
==============================================================

-- Run this if you need to undo this migration

USE [Taketime]
GO

BEGIN TRANSACTION

-- Drop view
IF OBJECT_ID('dbo.v_PaymentSlipsSummary', 'V') IS NOT NULL
    DROP VIEW dbo.v_PaymentSlipsSummary

-- Drop stored procedures
IF OBJECT_ID('dbo.sp_IsPaymentSlipRequired', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_IsPaymentSlipRequired

IF OBJECT_ID('dbo.sp_GetPaymentSlips', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetPaymentSlips

IF OBJECT_ID('dbo.sp_VerifyPaymentSlip', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_VerifyPaymentSlip

IF OBJECT_ID('dbo.sp_UploadPaymentSlip', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_UploadPaymentSlip

-- Drop Account_Receipt columns
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND name = 'PaymentSlipRequired')
    ALTER TABLE [dbo].[Account_Receipt] DROP COLUMN [PaymentSlipRequired]

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Account_Receipt]') AND name = 'HasPaymentSlip')
    ALTER TABLE [dbo].[Account_Receipt] DROP COLUMN [HasPaymentSlip]

-- Drop Payment_Slips table (CASCADE will handle FKs)
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Payment_Slips]') AND type in (N'U'))
    DROP TABLE [dbo].[Payment_Slips]

-- Remove migration record
DELETE FROM Database_Migrations WHERE MigrationName = '05_Add_Payment_Slips_System'

COMMIT TRANSACTION

PRINT 'Rollback completed'

==============================================================*/
