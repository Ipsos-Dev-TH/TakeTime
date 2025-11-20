-- =============================================
-- Migration: Fix Product Page - Hide Completed Reservations
-- Purpose: Update vw_ActiveGuestReservations to exclude "เสร็จสิ้น" status
-- Created: 2025-11-09
-- =============================================

USE [Taketime]
GO

-- Update the view to exclude completed reservations
IF OBJECT_ID('vw_ActiveGuestReservations', 'V') IS NOT NULL
    DROP VIEW vw_ActiveGuestReservations;
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

    -- Formatted display text for dropdown (using + instead of CONCAT, CONVERT instead of FORMAT)
    C.Name + N' (' + dbo.fn_GetReservationRoomNames(R.ID) + N') - เข้า: ' +
    CONVERT(VARCHAR, R.CheckinDate, 103) + N' ออก: ' +
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
    -- 🔒 Exclude cancelled, checked-out, and completed reservations
    AND R.Status NOT IN (N'ยกเลิก', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน');
GO

-- Grant permissions
GRANT SELECT ON vw_ActiveGuestReservations TO PUBLIC;
GO

PRINT '✅ View vw_ActiveGuestReservations updated successfully - Completed reservations are now excluded';
GO
