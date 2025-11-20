-- =============================================
-- PHASE 5 - Migration 02: Fix Active Guest Filter
-- Purpose: Fix vw_ActiveGuestReservations to only show guests staying TODAY
-- Issue: Previously showed ALL checked-in guests regardless of dates
-- Fix: Now shows only guests where today is within check-in/check-out range
-- Date: 2025-11-06
-- =============================================

USE [Taketime];
GO

PRINT 'Starting PHASE 5 Migration 02: Fix Active Guest Filter...';
GO

-- Drop existing view
IF OBJECT_ID('vw_ActiveGuestReservations', 'V') IS NOT NULL
BEGIN
    PRINT 'Dropping existing vw_ActiveGuestReservations...';
    DROP VIEW vw_ActiveGuestReservations;
END
GO

-- Recreate view with corrected filter logic
PRINT 'Creating corrected vw_ActiveGuestReservations...';
GO

CREATE VIEW vw_ActiveGuestReservations
AS
SELECT
    R.ID AS ReservationID,
    C.Name AS CustomerName,
    C.NickName AS CustomerNickName,
    C.MobilePhone AS CustomerPhone,
    R.CheckinDate AS CheckInDate,
    R.CheckoutDate AS CheckOutDate,
    R.Status,
    R.TotalPrice,
    R.Deposit AS TotalPaid,
    (R.TotalPrice - ISNULL(R.Deposit, 0)) AS RemainingBalance,

    -- Get room names using scalar function
    dbo.fn_GetReservationRoomNames(R.ID) AS RoomNames,

    -- Get pending product charges from pre-calculated subquery
    ISNULL(PC.PendingTotal, 0) AS PendingCharges,

    -- Formatted display text for dropdown (using simple string concatenation)
    C.Name + ' (' + dbo.fn_GetReservationRoomNames(R.ID) + ') - เข้า: ' +
    CONVERT(VARCHAR, R.CheckinDate, 103) + ' ออก: ' +
    CONVERT(VARCHAR, R.CheckoutDate, 103) AS DisplayText

FROM Reservation R
INNER JOIN Customer C ON R.Customer_MobilePhone = C.MobilePhone

-- LEFT JOIN to get pending charges (pre-aggregated)
LEFT JOIN (
    SELECT
        Reservation_ID,
        SUM(TotalAmount) AS PendingTotal
    FROM Reservation_Product_Charges
    WHERE Status = 'PENDING'
    GROUP BY Reservation_ID
) PC ON R.ID = PC.Reservation_ID

WHERE
    -- Today must be within check-in/check-out date range
    CAST(GETDATE() AS DATE) >= CAST(R.CheckinDate AS DATE)
    AND CAST(GETDATE() AS DATE) <= CAST(R.CheckoutDate AS DATE)
    -- Exclude cancelled and checked-out reservations
    AND R.Status NOT IN (N'ยกเลิก', N'เช็คเอาท์แล้ว');
GO

-- Grant permissions
GRANT SELECT ON vw_ActiveGuestReservations TO PUBLIC;
GO

-- Test the view
PRINT 'Testing vw_ActiveGuestReservations...';
GO

SELECT
    COUNT(*) AS ActiveGuestCount,
    MIN(CheckInDate) AS EarliestCheckIn,
    MAX(CheckOutDate) AS LatestCheckOut
FROM vw_ActiveGuestReservations;
GO

PRINT '';
PRINT '✅ PHASE 5 Migration 02 completed successfully!';
PRINT '';
PRINT '📋 Summary of changes:';
PRINT '   - Fixed filter logic to show only guests staying TODAY';
PRINT '   - Removed: Status = ''เช็คอินแล้ว'' without date check';
PRINT '   - Added: Date range validation (Today BETWEEN CheckIn AND CheckOut)';
PRINT '';
PRINT '📊 Impact:';
PRINT '   - Product page dropdown will now show fewer, more relevant guests';
PRINT '   - Only active guests currently on-site will appear';
PRINT '';
GO
