-- =============================================
-- Fix Checkout Unicode Issue
-- Purpose: Add N prefix to Thai strings for proper Unicode comparison
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT 'Fixing Unicode string comparison in checkout procedures...';
PRINT '';

-- =============================================
-- 1. Drop and Recreate sp_ProcessCheckout with N prefix
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

        -- FIX: Added N prefix for Unicode string comparison
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

        -- 3. Update reservation (FIX: Added N prefix)
        UPDATE [dbo].[Reservation]
        SET CheckoutDate = GETDATE(),
            CheckoutBy_AdminID = @AdminID,
            CheckoutNotes = @Notes,
            FinalSettlementAmount = @FinalAmount,
            Status = N'เสร็จสิ้น'
        WHERE ID = @ReservationID;

        -- 4. Insert checkout history
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

        -- 5. Update room status to DIRTY (if Room_Status table exists)
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

PRINT '✅ Stored procedure sp_ProcessCheckout updated with Unicode fixes';
GO

-- =============================================
-- 2. Drop and Recreate fn_CanCheckout with N prefix
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

    -- Must be checked in (FIX: Added N prefix)
    IF @Status NOT IN (N'เช็คอินแล้ว', N'เข้าพักแล้ว')
        RETURN 0;

    -- Check payment (optional - can allow checkout with outstanding balance)
    IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    BEGIN
        SET @RemainingBalance = dbo.fn_GetRemainingBalance(@ReservationID);
    END

    SET @CanCheckout = 1;
    RETURN @CanCheckout;
END
GO

PRINT '✅ Function fn_CanCheckout updated with Unicode fixes';
GO

-- =============================================
-- 3. Recreate vw_CheckoutSummary with N prefix
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

PRINT '✅ View vw_CheckoutSummary updated with Unicode fixes';
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Verification';
PRINT '========================================';

IF OBJECT_ID('dbo.sp_ProcessCheckout', 'P') IS NOT NULL
    PRINT '✅ sp_ProcessCheckout exists';
ELSE
    PRINT '❌ sp_ProcessCheckout missing';

IF OBJECT_ID('dbo.fn_CanCheckout', 'FN') IS NOT NULL
    PRINT '✅ fn_CanCheckout exists';
ELSE
    PRINT '❌ fn_CanCheckout missing';

IF OBJECT_ID('dbo.vw_CheckoutSummary', 'V') IS NOT NULL
    PRINT '✅ vw_CheckoutSummary exists';
ELSE
    PRINT '❌ vw_CheckoutSummary missing';

PRINT '';
PRINT '========================================';
PRINT '✅ Unicode fixes completed!';
PRINT '========================================';
PRINT '';
PRINT 'Thai status values now supported:';
PRINT '  - เช็คอินแล้ว (Checked In)';
PRINT '  - เข้าพักแล้ว (In Stay)';
PRINT '  - เสร็จสิ้น (Completed)';
GO
