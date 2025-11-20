-- =============================================
-- View: vw_ActiveGuestReservations
-- Purpose: Display active guest reservations for room charge dropdown
-- Created: 2025-11-06
-- Important: Run 01_fn_GetReservationRoomNames.sql first!
-- =============================================

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
        SUM(TotalPrice) AS PendingTotal
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
-- SELECT * FROM vw_ActiveGuestReservations;
