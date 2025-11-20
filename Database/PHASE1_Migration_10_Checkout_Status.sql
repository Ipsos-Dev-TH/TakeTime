-- =============================================
-- Migration 10: Checkout Status Enhancement
-- Purpose: Add explicit checkout tracking and status management
-- Author: Claude AI
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Migration 10: Checkout Status Enhancement';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Add Checkout Columns to Reservation Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'CheckoutDate')
BEGIN
    ALTER TABLE [dbo].[Reservation]
    ADD [CheckoutDate] datetime NULL;
    PRINT '✅ Added CheckoutDate column';
END
ELSE
    PRINT '⚠️ CheckoutDate column already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'CheckoutBy_AdminID')
BEGIN
    ALTER TABLE [dbo].[Reservation]
    ADD [CheckoutBy_AdminID] smallint NULL;
    PRINT '✅ Added CheckoutBy_AdminID column';
END
ELSE
    PRINT '⚠️ CheckoutBy_AdminID column already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'CheckoutNotes')
BEGIN
    ALTER TABLE [dbo].[Reservation]
    ADD [CheckoutNotes] nvarchar(500) NULL;
    PRINT '✅ Added CheckoutNotes column';
END
ELSE
    PRINT '⚠️ CheckoutNotes column already exists';
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'FinalSettlementAmount')
BEGIN
    ALTER TABLE [dbo].[Reservation]
    ADD [FinalSettlementAmount] decimal(10,2) NULL;
    PRINT '✅ Added FinalSettlementAmount column';
END
ELSE
    PRINT '⚠️ FinalSettlementAmount column already exists';
GO

-- Add foreign key constraint
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Reservation_CheckoutAdmin')
BEGIN
    ALTER TABLE [dbo].[Reservation]
    ADD CONSTRAINT [FK_Reservation_CheckoutAdmin]
    FOREIGN KEY ([CheckoutBy_AdminID]) REFERENCES [dbo].[Admin]([ID]);
    PRINT '✅ Added FK_Reservation_CheckoutAdmin constraint';
END
ELSE
    PRINT '⚠️ FK_Reservation_CheckoutAdmin already exists';
GO

-- =============================================
-- 2. Create Checkout History Table
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Checkout_History')
BEGIN
    CREATE TABLE [dbo].[Checkout_History] (
        [ID] bigint IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [Reservation_ID] bigint NOT NULL,
        [CheckoutDate] datetime NOT NULL DEFAULT GETDATE(),
        [CheckedOutBy_AdminID] smallint NOT NULL,
        [FinalAmount] decimal(10,2) NOT NULL,
        [PaymentStatus] nvarchar(20) NOT NULL, -- PAID/PARTIAL/UNPAID
        [RoomDamage] bit NOT NULL DEFAULT 0,
        [DamageDescription] nvarchar(500) NULL,
        [DamageCharge] decimal(10,2) NULL DEFAULT 0,
        [MissingItems] bit NOT NULL DEFAULT 0,
        [MissingItemsDescription] nvarchar(500) NULL,
        [MissingItemsCharge] decimal(10,2) NULL DEFAULT 0,
        [KeyReturned] bit NOT NULL DEFAULT 0,
        [CleaningStatus] nvarchar(20) NULL, -- GOOD/DIRTY/VERY_DIRTY
        [GuestSatisfaction] tinyint NULL, -- 1-5 rating
        [Notes] nvarchar(1000) NULL,
        [CreatedDate] datetime NOT NULL DEFAULT GETDATE(),
        CONSTRAINT [FK_Checkout_History_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]) ON DELETE CASCADE,
        CONSTRAINT [FK_Checkout_History_Admin] FOREIGN KEY ([CheckedOutBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CK_Checkout_History_Satisfaction] CHECK ([GuestSatisfaction] BETWEEN 1 AND 5 OR [GuestSatisfaction] IS NULL)
    );

    PRINT '✅ Checkout_History table created';
END
ELSE
    PRINT '⚠️ Checkout_History table already exists';
GO

-- Create index
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Checkout_History_Reservation')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Checkout_History_Reservation]
    ON [dbo].[Checkout_History] ([Reservation_ID], [CheckoutDate] DESC);
    PRINT '✅ Index IX_Checkout_History_Reservation created';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Checkout_History_Date')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Checkout_History_Date]
    ON [dbo].[Checkout_History] ([CheckoutDate] DESC);
    PRINT '✅ Index IX_Checkout_History_Date created';
END
GO

-- =============================================
-- 3. Create View for Checkout Summary
-- =============================================
IF OBJECT_ID('dbo.vw_CheckoutSummary', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_CheckoutSummary];
GO

CREATE VIEW [dbo].[vw_CheckoutSummary]
AS
SELECT
    r.ID AS ReservationID,
    r.Customer_MobilePhone,
    c.Name AS CustomerName,
    r.CheckinDate,
    r.CheckoutDate AS ScheduledCheckout,
    ch.CheckoutDate AS ActualCheckout,
    r.TotalPrice,
    r.Deposit,
    r.FinalSettlementAmount,
    ch.PaymentStatus,
    ch.RoomDamage,
    ch.DamageCharge,
    ch.MissingItems,
    ch.MissingItemsCharge,
    ch.KeyReturned,
    ch.CleaningStatus,
    ch.GuestSatisfaction,
    a.Username AS CheckedOutBy,
    r.Status AS ReservationStatus,
    DATEDIFF(DAY, r.CheckinDate, ch.CheckoutDate) AS ActualStayDays,
    CASE
        WHEN ch.CheckoutDate IS NULL THEN NULL
        WHEN ch.CheckoutDate > r.CheckoutDate THEN 'LATE'
        WHEN ch.CheckoutDate < r.CheckoutDate THEN 'EARLY'
        ELSE 'ON_TIME'
    END AS CheckoutTiming
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Checkout_History] ch ON r.ID = ch.Reservation_ID
LEFT JOIN [dbo].[Customer] c ON r.Customer_MobilePhone = c.MobilePhone
LEFT JOIN [dbo].[Admin] a ON ch.CheckedOutBy_AdminID = a.ID
WHERE r.Status IN (N'เช็คอินแล้ว', N'เข้าพักแล้ว', N'เสร็จสิ้น');
GO

PRINT '✅ View vw_CheckoutSummary created';
GO

-- =============================================
-- 4. Create Stored Procedure for Checkout Process
-- =============================================
IF OBJECT_ID('dbo.sp_ProcessCheckout', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_ProcessCheckout];
GO

CREATE PROCEDURE [dbo].[sp_ProcessCheckout]
    @ReservationID bigint,
    @AdminID smallint,
    @RoomDamage bit = 0,
    @DamageDescription nvarchar(500) = NULL,
    @DamageCharge decimal(10,2) = 0,
    @MissingItems bit = 0,
    @MissingItemsDescription nvarchar(500) = NULL,
    @MissingItemsCharge decimal(10,2) = 0,
    @KeyReturned bit = 1,
    @CleaningStatus nvarchar(20) = 'GOOD',
    @GuestSatisfaction tinyint = NULL,
    @Notes nvarchar(1000) = NULL,
    @CheckoutID bigint OUTPUT,
    @ErrorMessage nvarchar(500) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TotalPrice decimal(10,2);
    DECLARE @TotalPaid decimal(10,2);
    DECLARE @RemainingBalance decimal(10,2);
    DECLARE @Status nvarchar(50);
    DECLARE @FinalAmount decimal(10,2);
    DECLARE @PaymentStatus nvarchar(20);

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Validate reservation exists and is checked in
        SELECT @TotalPrice = TotalPrice, @Status = Status
        FROM [dbo].[Reservation]
        WHERE ID = @ReservationID;

        IF @TotalPrice IS NULL
        BEGIN
            SET @ErrorMessage = 'Reservation not found';
            ROLLBACK TRANSACTION;
            RETURN -1;
        END

        IF @Status NOT IN (N'เช็คอินแล้ว', N'เข้าพักแล้ว')
        BEGIN
            SET @ErrorMessage = 'Reservation must be checked in to checkout. Current status: ' + @Status;
            ROLLBACK TRANSACTION;
            RETURN -2;
        END

        -- 2. Calculate payment status
        IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
        BEGIN
            SET @RemainingBalance = dbo.fn_GetRemainingBalance(@ReservationID);
        END
        ELSE
        BEGIN
            -- Fallback calculation
            SELECT @TotalPaid = ISNULL(SUM(PaymentAmount), 0)
            FROM [dbo].[Payment_History]
            WHERE Reservation_ID = @ReservationID AND Status = 'COMPLETED';

            SET @RemainingBalance = @TotalPrice - ISNULL(@TotalPaid, 0);
        END

        -- Add damage and missing items charges
        SET @FinalAmount = @TotalPrice + ISNULL(@DamageCharge, 0) + ISNULL(@MissingItemsCharge, 0);
        SET @RemainingBalance = @RemainingBalance + ISNULL(@DamageCharge, 0) + ISNULL(@MissingItemsCharge, 0);

        -- Determine payment status
        IF @RemainingBalance <= 0.01 -- Allow for rounding
            SET @PaymentStatus = 'PAID';
        ELSE IF @RemainingBalance < @FinalAmount
            SET @PaymentStatus = 'PARTIAL';
        ELSE
            SET @PaymentStatus = 'UNPAID';

        -- 3. Prevent checkout if payment not complete (optional - can be disabled)
        -- IF @PaymentStatus != 'PAID'
        -- BEGIN
        --     SET @ErrorMessage = 'Cannot checkout. Payment not complete. Remaining: ' + CAST(@RemainingBalance AS nvarchar(20));
        --     ROLLBACK TRANSACTION;
        --     RETURN -3;
        -- END

        -- 4. Update reservation
        UPDATE [dbo].[Reservation]
        SET CheckoutDate = GETDATE(),
            CheckoutBy_AdminID = @AdminID,
            CheckoutNotes = @Notes,
            FinalSettlementAmount = @FinalAmount,
            Status = N'เสร็จสิ้น'
        WHERE ID = @ReservationID;

        -- 5. Insert checkout history
        INSERT INTO [dbo].[Checkout_History] (
            Reservation_ID,
            CheckoutDate,
            CheckedOutBy_AdminID,
            FinalAmount,
            PaymentStatus,
            RoomDamage,
            DamageDescription,
            DamageCharge,
            MissingItems,
            MissingItemsDescription,
            MissingItemsCharge,
            KeyReturned,
            CleaningStatus,
            GuestSatisfaction,
            Notes
        )
        VALUES (
            @ReservationID,
            GETDATE(),
            @AdminID,
            @FinalAmount,
            @PaymentStatus,
            @RoomDamage,
            @DamageDescription,
            @DamageCharge,
            @MissingItems,
            @MissingItemsDescription,
            @MissingItemsCharge,
            @KeyReturned,
            @CleaningStatus,
            @GuestSatisfaction,
            @Notes
        );

        SET @CheckoutID = SCOPE_IDENTITY();

        -- 6. Update room status to DIRTY (if Room_Status table exists)
        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Room_Status')
        BEGIN
            UPDATE rs
            SET RoomStatus = 'DIRTY',
                StatusDate = GETDATE(),
                CheckOutTime = GETDATE()
            FROM [dbo].[Room_Status] rs
            INNER JOIN [dbo].[Reservation_Accommodation] ra ON rs.Accommodation_ID = ra.Accommodation_ID
            WHERE ra.Reservation_ID = @ReservationID
            AND rs.CurrentReservation_ID = @ReservationID;
        END

        SET @ErrorMessage = NULL;
        COMMIT TRANSACTION;

        RETURN 0; -- Success
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        SET @ErrorMessage = ERROR_MESSAGE();
        RETURN -99;
    END CATCH
END
GO

PRINT '✅ Stored procedure sp_ProcessCheckout created';
GO

-- =============================================
-- 5. Create Function to Check if Can Checkout
-- =============================================
IF OBJECT_ID('dbo.fn_CanCheckout', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_CanCheckout];
GO

CREATE FUNCTION [dbo].[fn_CanCheckout]
(
    @ReservationID bigint
)
RETURNS bit
AS
BEGIN
    DECLARE @CanCheckout bit = 0;
    DECLARE @Status nvarchar(50);
    DECLARE @RemainingBalance decimal(10,2);

    SELECT @Status = Status
    FROM [dbo].[Reservation]
    WHERE ID = @ReservationID;

    -- Must be checked in
    IF @Status NOT IN (N'เช็คอินแล้ว', N'เข้าพักแล้ว')
        RETURN 0;

    -- Check payment (optional - can allow checkout with outstanding balance)
    IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    BEGIN
        SET @RemainingBalance = dbo.fn_GetRemainingBalance(@ReservationID);

        -- If you want to enforce full payment before checkout, uncomment this:
        -- IF @RemainingBalance > 0.01
        --     RETURN 0;
    END

    SET @CanCheckout = 1;
    RETURN @CanCheckout;
END
GO

PRINT '✅ Function fn_CanCheckout created';
GO

-- =============================================
-- 6. Grant Permissions
-- =============================================
GRANT SELECT ON [dbo].[vw_CheckoutSummary] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_ProcessCheckout] TO PUBLIC;

PRINT '✅ Permissions granted';
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Migration 10 Verification';
PRINT '========================================';

-- Check columns added
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'CheckoutDate')
    PRINT '✅ CheckoutDate column exists';
ELSE
    PRINT '❌ CheckoutDate column missing';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'CheckoutBy_AdminID')
    PRINT '✅ CheckoutBy_AdminID column exists';

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'FinalSettlementAmount')
    PRINT '✅ FinalSettlementAmount column exists';

-- Check Checkout_History table
IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Checkout_History')
    PRINT '✅ Checkout_History table exists';
ELSE
    PRINT '❌ Checkout_History table missing';

-- Check view
IF OBJECT_ID('dbo.vw_CheckoutSummary', 'V') IS NOT NULL
    PRINT '✅ vw_CheckoutSummary exists';

-- Check stored procedure
IF OBJECT_ID('dbo.sp_ProcessCheckout', 'P') IS NOT NULL
    PRINT '✅ sp_ProcessCheckout exists';

-- Check function
IF OBJECT_ID('dbo.fn_CanCheckout', 'FN') IS NOT NULL
    PRINT '✅ fn_CanCheckout exists';

PRINT '';
PRINT '========================================';
PRINT '✅ Migration 10 completed successfully!';
PRINT '========================================';
GO
