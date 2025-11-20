-- =============================================
-- Migration 09: Payment Tracking Enhancement
-- Purpose: Track all payment transactions with detailed history
-- Author: Claude AI
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

-- =============================================
-- 1. Create Payment_History Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_History')
BEGIN
    CREATE TABLE [dbo].[Payment_History] (
        [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Reservation_ID] bigint NOT NULL,
        [PaymentDate] datetime NOT NULL DEFAULT GETDATE(),
        [PaymentAmount] decimal(10,2) NOT NULL,
        [PaymentType] nvarchar(50) NOT NULL, -- DEPOSIT/ADDITIONAL/FINAL/REFUND
        [PaymentMethod] nvarchar(50) NULL, -- CASH/TRANSFER/CARD/OTHER
        [Receipt_ID] nvarchar(50) NULL,
        [PaymentSlip_ID] bigint NULL,
        [RemainingBalance] decimal(10,2) NULL,
        [PaidBy_CustomerPhone] nvarchar(20) NULL,
        [ProcessedBy_AdminID] smallint NULL,
        [Notes] nvarchar(500) NULL,
        [Status] nvarchar(20) NOT NULL DEFAULT 'COMPLETED', -- COMPLETED/PENDING/CANCELLED/REFUNDED
        [CreatedDate] datetime NOT NULL DEFAULT GETDATE(),
        [UpdatedDate] datetime NULL,
        CONSTRAINT [FK_Payment_History_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_Payment_History_Receipt] FOREIGN KEY ([Receipt_ID])
            REFERENCES [dbo].[Account_Receipt]([ID]),
        CONSTRAINT [FK_Payment_History_Admin] FOREIGN KEY ([ProcessedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CK_Payment_History_Amount] CHECK ([PaymentAmount] > 0 OR [PaymentType] = 'REFUND')
    );

    PRINT '✅ Payment_History table created successfully';
END
ELSE
BEGIN
    PRINT '⚠️ Payment_History table already exists';
END
GO

-- Add FK to Payment_Slips if table exists
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_Slips')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Payment_History_PaymentSlip')
    BEGIN
        ALTER TABLE [dbo].[Payment_History]
        ADD CONSTRAINT [FK_Payment_History_PaymentSlip] FOREIGN KEY ([PaymentSlip_ID])
            REFERENCES [dbo].[Payment_Slips]([ID]);
        PRINT '✅ FK_Payment_History_PaymentSlip created';
    END
END
ELSE
BEGIN
    PRINT '⚠️ Payment_Slips table not found - skipping FK constraint';
    PRINT '   Run Migration 05 first to create Payment_Slips table';
END
GO

-- Create indexes for better performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_History_Reservation')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_History_Reservation]
    ON [dbo].[Payment_History] ([Reservation_ID], [PaymentDate] DESC);
    PRINT '✅ Index IX_Payment_History_Reservation created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_History_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_History_Date]
    ON [dbo].[Payment_History] ([PaymentDate] DESC)
    INCLUDE ([PaymentAmount], [PaymentType], [Status]);
    PRINT '✅ Index IX_Payment_History_Date created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_History_Receipt')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payment_History_Receipt]
    ON [dbo].[Payment_History] ([Receipt_ID]);
    PRINT '✅ Index IX_Payment_History_Receipt created';
END
GO

-- =============================================
-- 2. Add Payment Tracking Views
-- =============================================
IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ReservationPaymentSummary];
GO

CREATE VIEW [dbo].[vw_ReservationPaymentSummary]
AS
SELECT
    r.ID AS ReservationID,
    r.Customer_MobilePhone,
    r.CheckinDate,
    r.CheckoutDate,
    r.TotalPrice,
    r.Deposit AS DepositRecorded,
    r.Status AS ReservationStatus,
    ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' THEN ph.PaymentAmount ELSE 0 END), 0) AS TotalPaid,
    ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' AND ph.PaymentType = 'DEPOSIT' THEN ph.PaymentAmount ELSE 0 END), 0) AS DepositPaid,
    ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' AND ph.PaymentType = 'ADDITIONAL' THEN ph.PaymentAmount ELSE 0 END), 0) AS AdditionalPaid,
    ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' AND ph.PaymentType = 'FINAL' THEN ph.PaymentAmount ELSE 0 END), 0) AS FinalPaid,
    ISNULL(SUM(CASE WHEN ph.Status = 'REFUNDED' THEN ph.PaymentAmount ELSE 0 END), 0) AS TotalRefunded,
    r.TotalPrice - ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' THEN ph.PaymentAmount ELSE 0 END), 0) AS RemainingBalance,
    COUNT(ph.ID) AS PaymentCount,
    MAX(ph.PaymentDate) AS LastPaymentDate,
    CASE
        WHEN r.TotalPrice <= ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' THEN ph.PaymentAmount ELSE 0 END), 0) THEN 'PAID'
        WHEN ISNULL(SUM(CASE WHEN ph.Status = 'COMPLETED' THEN ph.PaymentAmount ELSE 0 END), 0) > 0 THEN 'PARTIAL'
        ELSE 'UNPAID'
    END AS PaymentStatus
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Payment_History] ph ON r.ID = ph.Reservation_ID
GROUP BY
    r.ID,
    r.Customer_MobilePhone,
    r.CheckinDate,
    r.CheckoutDate,
    r.TotalPrice,
    r.Deposit,
    r.Status;
GO

PRINT '✅ View vw_ReservationPaymentSummary created';
GO

-- =============================================
-- 3. Create Stored Procedure for Payment Recording
-- =============================================
IF OBJECT_ID('dbo.sp_RecordPayment', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_RecordPayment];
GO

CREATE PROCEDURE [dbo].[sp_RecordPayment]
    @ReservationID bigint,
    @PaymentAmount decimal(10,2),
    @PaymentType nvarchar(50),
    @PaymentMethod nvarchar(50) = NULL,
    @ReceiptID nvarchar(50) = NULL,
    @PaymentSlipID bigint = NULL,
    @PaidByCustomerPhone nvarchar(20) = NULL,
    @ProcessedByAdminID smallint = NULL,
    @Notes nvarchar(500) = NULL,
    @NewPaymentID bigint OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalPrice decimal(10,2);
    DECLARE @TotalPaid decimal(10,2);
    DECLARE @RemainingBalance decimal(10,2);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Get reservation total price
        SELECT @TotalPrice = TotalPrice
        FROM [dbo].[Reservation]
        WHERE ID = @ReservationID;

        IF @TotalPrice IS NULL
        BEGIN
            RAISERROR('Reservation not found', 16, 1);
            RETURN -1;
        END

        -- Calculate total paid so far
        SELECT @TotalPaid = ISNULL(SUM(PaymentAmount), 0)
        FROM [dbo].[Payment_History]
        WHERE Reservation_ID = @ReservationID
        AND Status = 'COMPLETED';

        -- Calculate remaining balance
        SET @RemainingBalance = @TotalPrice - (@TotalPaid + @PaymentAmount);

        -- Validate payment amount
        IF @RemainingBalance < -0.01 -- Allow for rounding errors
        BEGIN
            RAISERROR('Payment amount exceeds remaining balance', 16, 1);
            RETURN -2;
        END

        -- Insert payment record
        INSERT INTO [dbo].[Payment_History] (
            Reservation_ID,
            PaymentDate,
            PaymentAmount,
            PaymentType,
            PaymentMethod,
            Receipt_ID,
            PaymentSlip_ID,
            RemainingBalance,
            PaidBy_CustomerPhone,
            ProcessedBy_AdminID,
            Notes,
            Status
        )
        VALUES (
            @ReservationID,
            GETDATE(),
            @PaymentAmount,
            @PaymentType,
            @PaymentMethod,
            @ReceiptID,
            @PaymentSlipID,
            @RemainingBalance,
            @PaidByCustomerPhone,
            @ProcessedByAdminID,
            @Notes,
            'COMPLETED'
        );

        SET @NewPaymentID = SCOPE_IDENTITY();

        -- Update reservation deposit amount
        UPDATE [dbo].[Reservation]
        SET Deposit = @TotalPaid + @PaymentAmount
        WHERE ID = @ReservationID;

        COMMIT TRANSACTION;

        RETURN 0; -- Success
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
        RAISERROR(@ErrorMessage, 16, 1);
        RETURN -99;
    END CATCH
END
GO

PRINT '✅ Stored procedure sp_RecordPayment created';
GO

-- =============================================
-- 4. Create Function to Calculate Remaining Balance
-- =============================================
IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetRemainingBalance];
GO

CREATE FUNCTION [dbo].[fn_GetRemainingBalance]
(
    @ReservationID bigint
)
RETURNS decimal(10,2)
AS
BEGIN
    DECLARE @TotalPrice decimal(10,2);
    DECLARE @TotalPaid decimal(10,2);
    DECLARE @Remaining decimal(10,2);

    SELECT @TotalPrice = TotalPrice
    FROM [dbo].[Reservation]
    WHERE ID = @ReservationID;

    SELECT @TotalPaid = ISNULL(SUM(PaymentAmount), 0)
    FROM [dbo].[Payment_History]
    WHERE Reservation_ID = @ReservationID
    AND Status = 'COMPLETED';

    SET @Remaining = ISNULL(@TotalPrice, 0) - ISNULL(@TotalPaid, 0);

    RETURN @Remaining;
END
GO

PRINT '✅ Function fn_GetRemainingBalance created';
GO

-- =============================================
-- 5. Migrate Existing Data (if needed)
-- =============================================
-- Check if we should migrate existing data from Account_Receipt
DECLARE @ExistingPayments int;
SELECT @ExistingPayments = COUNT(*) FROM [dbo].[Payment_History];

IF @ExistingPayments = 0
BEGIN
    PRINT '📦 Migrating existing payment data from Account_Receipt...';

    INSERT INTO [dbo].[Payment_History] (
        Reservation_ID,
        PaymentDate,
        PaymentAmount,
        PaymentType,
        PaymentMethod,
        Receipt_ID,
        RemainingBalance,
        ProcessedBy_AdminID,
        Status,
        CreatedDate
    )
    SELECT
        ar.Reservation_ID,
        ar.Created_Date,
        ar.Total_Amount,
        CASE
            WHEN ar.IsDeposit = 1 THEN 'DEPOSIT'
            ELSE 'FINAL'
        END AS PaymentType,
        ar.Paid_Type AS PaymentMethod,
        ar.ID AS Receipt_ID,
        NULL AS RemainingBalance, -- Will be calculated
        CASE
            WHEN ar.Created_By_ID IS NOT NULL
                 AND EXISTS (SELECT 1 FROM [dbo].[Admin] WHERE ID = ar.Created_By_ID)
            THEN ar.Created_By_ID
            ELSE NULL
        END AS ProcessedBy_AdminID,
        CASE
            WHEN ar.Status = 'Normal' THEN 'COMPLETED'
            ELSE 'CANCELLED'
        END AS Status,
        ar.Created_Date
    FROM [dbo].[Account_Receipt] ar
    WHERE ar.Reservation_ID IS NOT NULL
    AND ar.Total_Amount > 0  -- Skip zero or negative amounts
    AND EXISTS (SELECT 1 FROM [dbo].[Reservation] WHERE ID = ar.Reservation_ID)  -- Only valid reservations
    ORDER BY ar.Created_Date;

    PRINT '✅ Migrated ' + CAST(@@ROWCOUNT AS nvarchar(10)) + ' payment records';

    -- Update remaining balances
    UPDATE ph
    SET RemainingBalance = dbo.fn_GetRemainingBalance(ph.Reservation_ID)
    FROM [dbo].[Payment_History] ph;

    PRINT '✅ Updated remaining balances';
END
ELSE
BEGIN
    PRINT '⚠️ Payment_History already has data. Skipping migration.';
END
GO

-- =============================================
-- 6. Grant Permissions
-- =============================================
-- Grant SELECT on view
GRANT SELECT ON [dbo].[vw_ReservationPaymentSummary] TO PUBLIC;

-- Grant EXECUTE on stored procedure
GRANT EXECUTE ON [dbo].[sp_RecordPayment] TO PUBLIC;

PRINT '✅ Permissions granted';
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Migration 09 Verification';
PRINT '========================================';

-- Check Payment_History table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_History')
    PRINT '✅ Payment_History table exists';
ELSE
    PRINT '❌ Payment_History table missing';

-- Check indexes
IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_History_Reservation')
    PRINT '✅ IX_Payment_History_Reservation exists';

IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Payment_History_Date')
    PRINT '✅ IX_Payment_History_Date exists';

-- Check view
IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationPaymentSummary exists';

-- Check stored procedure
IF OBJECT_ID('dbo.sp_RecordPayment', 'P') IS NOT NULL
    PRINT '✅ sp_RecordPayment exists';

-- Check function
IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    PRINT '✅ fn_GetRemainingBalance exists';

-- Show sample data
DECLARE @RecordCount int;
SELECT @RecordCount = COUNT(*) FROM [dbo].[Payment_History];
PRINT '';
PRINT 'Total payment records: ' + CAST(@RecordCount AS nvarchar(10));

PRINT '';
PRINT '========================================';
PRINT '✅ Migration 09 completed successfully!';
PRINT '========================================';
GO
