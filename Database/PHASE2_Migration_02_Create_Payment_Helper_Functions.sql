-- =============================================
-- Phase 2 Migration 02: Create Payment Helper Functions
-- Purpose: Create SQL functions to replace Deposit column usage
-- Author: Claude AI
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Phase 2 Migration 02: Create Payment Helper Functions';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Create/Update fn_GetTotalPaid Function
-- =============================================
IF OBJECT_ID('dbo.fn_GetTotalPaid', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetTotalPaid];
GO

CREATE FUNCTION [dbo].[fn_GetTotalPaid]
(
    @ReservationID bigint
)
RETURNS decimal(18,2)
AS
BEGIN
    DECLARE @TotalPaid decimal(18,2);

    -- Sum all completed payments from Payment_History
    SELECT @TotalPaid = ISNULL(SUM(PaymentAmount), 0)
    FROM [dbo].[Payment_History]
    WHERE Reservation_ID = @ReservationID
      AND Status = 'COMPLETED';

    -- If no payments in Payment_History, fallback to Deposit column (for backward compatibility)
    IF @TotalPaid = 0
    BEGIN
        SELECT @TotalPaid = ISNULL(Deposit, 0)
        FROM [dbo].[Reservation]
        WHERE ID = @ReservationID;
    END

    RETURN @TotalPaid;
END
GO

PRINT '✅ Created fn_GetTotalPaid function';
GO

-- =============================================
-- 2. Create/Update fn_GetRemainingBalance Function (if not exists)
-- =============================================
IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
BEGIN
    PRINT '⚠️ fn_GetRemainingBalance already exists, updating...';
    DROP FUNCTION [dbo].[fn_GetRemainingBalance];
END
GO

CREATE FUNCTION [dbo].[fn_GetRemainingBalance]
(
    @ReservationID bigint
)
RETURNS decimal(18,2)
AS
BEGIN
    DECLARE @TotalPrice decimal(18,2);
    DECLARE @TotalPaid decimal(18,2);
    DECLARE @RemainingBalance decimal(18,2);

    -- Get total price
    SELECT @TotalPrice = ISNULL(TotalPrice, 0)
    FROM [dbo].[Reservation]
    WHERE ID = @ReservationID;

    -- Get total paid using fn_GetTotalPaid
    SET @TotalPaid = dbo.fn_GetTotalPaid(@ReservationID);

    -- Calculate remaining balance
    SET @RemainingBalance = @TotalPrice - @TotalPaid;

    RETURN @RemainingBalance;
END
GO

PRINT '✅ Created/Updated fn_GetRemainingBalance function';
GO

-- =============================================
-- 3. Create View for Easy Payment Lookup
-- =============================================
IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ReservationPaymentSummary];
GO

CREATE VIEW [dbo].[vw_ReservationPaymentSummary]
AS
SELECT
    r.ID AS ReservationID,
    r.Customer_MobilePhone,
    c.Name AS CustomerName,
    r.CheckinDate,
    r.CheckoutDate,
    r.Status AS ReservationStatus,
    r.TotalPrice,
    r.Deposit AS LegacyDeposit,
    dbo.fn_GetTotalPaid(r.ID) AS TotalPaid,
    dbo.fn_GetRemainingBalance(r.ID) AS RemainingBalance,
    CASE
        WHEN dbo.fn_GetRemainingBalance(r.ID) <= 0 THEN N'ชำระครบแล้ว'
        WHEN dbo.fn_GetTotalPaid(r.ID) > 0 THEN N'ชำระบางส่วน'
        ELSE N'ยังไม่ชำระ'
    END AS PaymentStatus,
    (SELECT COUNT(*) FROM Payment_History ph
     WHERE ph.Reservation_ID = r.ID AND ph.Status = 'COMPLETED') AS PaymentCount,
    (SELECT MAX(PaymentDate) FROM Payment_History ph
     WHERE ph.Reservation_ID = r.ID AND ph.Status = 'COMPLETED') AS LastPaymentDate
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Customer] c ON r.Customer_MobilePhone = c.MobilePhone
WHERE r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน');
GO

PRINT '✅ Created vw_ReservationPaymentSummary view';
GO

-- =============================================
-- 4. Create Stored Procedure for Quick Payment Lookup
-- =============================================
IF OBJECT_ID('dbo.sp_GetReservationPaymentInfo', 'P') IS NOT NULL
    DROP PROCEDURE [dbo].[sp_GetReservationPaymentInfo];
GO

CREATE PROCEDURE [dbo].[sp_GetReservationPaymentInfo]
    @ReservationID bigint
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        r.ID AS ReservationID,
        r.Customer_MobilePhone,
        c.Name AS CustomerName,
        c.NickName,
        r.CheckinDate,
        r.CheckoutDate,
        r.TotalPrice,
        dbo.fn_GetTotalPaid(r.ID) AS TotalPaid,
        dbo.fn_GetRemainingBalance(r.ID) AS RemainingBalance,
        CASE
            WHEN dbo.fn_GetRemainingBalance(r.ID) <= 0 THEN N'ชำระครบแล้ว'
            WHEN dbo.fn_GetTotalPaid(r.ID) > 0 THEN N'ชำระบางส่วน'
            ELSE N'ยังไม่ชำระ'
        END AS PaymentStatus,
        r.Status AS ReservationStatus,
        r.Deposit AS LegacyDeposit
    FROM [dbo].[Reservation] r
    LEFT JOIN [dbo].[Customer] c ON r.Customer_MobilePhone = c.MobilePhone
    WHERE r.ID = @ReservationID;

    -- Also return payment history
    SELECT
        ID,
        PaymentDate,
        PaymentAmount,
        PaymentType,
        PaymentMethod,
        Status,
        Notes,
        CreatedDate
    FROM [dbo].[Payment_History]
    WHERE Reservation_ID = @ReservationID
    ORDER BY PaymentDate DESC, CreatedDate DESC;
END
GO

PRINT '✅ Created sp_GetReservationPaymentInfo procedure';
GO

-- =============================================
-- 5. Grant Permissions
-- =============================================
GRANT EXECUTE ON [dbo].[fn_GetTotalPaid] TO PUBLIC;
GRANT EXECUTE ON [dbo].[fn_GetRemainingBalance] TO PUBLIC;
GRANT SELECT ON [dbo].[vw_ReservationPaymentSummary] TO PUBLIC;
GRANT EXECUTE ON [dbo].[sp_GetReservationPaymentInfo] TO PUBLIC;

PRINT '✅ Permissions granted';
GO

-- =============================================
-- VERIFICATION
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Verification';
PRINT '========================================';

IF OBJECT_ID('dbo.fn_GetTotalPaid', 'FN') IS NOT NULL
    PRINT '✅ fn_GetTotalPaid exists';
ELSE
    PRINT '❌ fn_GetTotalPaid missing';

IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    PRINT '✅ fn_GetRemainingBalance exists';
ELSE
    PRINT '❌ fn_GetRemainingBalance missing';

IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationPaymentSummary exists';
ELSE
    PRINT '❌ vw_ReservationPaymentSummary missing';

IF OBJECT_ID('dbo.sp_GetReservationPaymentInfo', 'P') IS NOT NULL
    PRINT '✅ sp_GetReservationPaymentInfo exists';
ELSE
    PRINT '❌ sp_GetReservationPaymentInfo missing';

-- Test functions
PRINT '';
PRINT 'Testing functions with sample data:';
PRINT '------------------------------------';

SELECT TOP 5
    ReservationID,
    TotalPrice,
    LegacyDeposit,
    TotalPaid,
    RemainingBalance,
    PaymentStatus
FROM vw_ReservationPaymentSummary
WHERE TotalPrice > 0
ORDER BY ReservationID DESC;

PRINT '';
PRINT '========================================';
PRINT '✅ Migration 02 completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'USAGE EXAMPLES:';
PRINT '---------------';
PRINT '';
PRINT '1. Get total paid for a reservation:';
PRINT '   SELECT dbo.fn_GetTotalPaid(123)';
PRINT '';
PRINT '2. Get remaining balance:';
PRINT '   SELECT dbo.fn_GetRemainingBalance(123)';
PRINT '';
PRINT '3. Get payment summary:';
PRINT '   SELECT * FROM vw_ReservationPaymentSummary WHERE ReservationID = 123';
PRINT '';
PRINT '4. Get complete payment info:';
PRINT '   EXEC sp_GetReservationPaymentInfo @ReservationID = 123';
PRINT '';
PRINT 'Next step: Update C# code to use these functions instead of Deposit column';
GO
