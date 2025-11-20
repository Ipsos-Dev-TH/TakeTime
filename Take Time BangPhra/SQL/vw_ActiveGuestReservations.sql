-- =============================================
-- All-in-one Script: Function + View for Active Guest Reservations
-- Purpose: Display active guest reservations for room charge dropdown
-- Created: 2025-11-06
--
-- This script creates:
--   1. fn_GetReservationRoomNames - Helper function to get room names
--   2. vw_ActiveGuestReservations - View for active guests
-- =============================================

-- Step 1: Create helper function to get room names
IF OBJECT_ID('dbo.fn_GetReservationRoomNames', 'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetReservationRoomNames;
GO

CREATE FUNCTION dbo.fn_GetReservationRoomNames(@ReservationID INT)
RETURNS NVARCHAR(MAX)
AS
BEGIN
    DECLARE @RoomNames NVARCHAR(MAX);

    SELECT @RoomNames = STUFF((
        SELECT ', ' + A.AccomName
        FROM Reservation_Accommodation RA
        INNER JOIN Accommodation A ON RA.Accommodation_ID = A.ID
        WHERE RA.Reservation_ID = @ReservationID
        FOR XML PATH('')
    ), 1, 2, '');

    RETURN ISNULL(@RoomNames, '');
END
GO

-- Step 2: Create the view
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
        SUM(TotalPrice) AS PendingTotal
    FROM Reservation_Product_Charges
    WHERE Status = 'PENDING'
    GROUP BY Reservation_ID
) PC ON R.ID = PC.Reservation_ID

WHERE
    -- Today must be within check-in/check-out date range
    CAST(GETDATE() AS DATE) >= CAST(R.CheckinDate AS DATE)
    AND CAST(GETDATE() AS DATE) <= CAST(R.CheckoutDate AS DATE)
    -- Exclude cancelled, checked-out, and completed reservations
    AND R.Status NOT IN (N'ยกเลิก', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน');
GO

-- Grant permissions
GRANT SELECT ON vw_ActiveGuestReservations TO PUBLIC;
GO

-- Test the function and view (uncomment to test)
-- SELECT dbo.fn_GetReservationRoomNames(1);  -- Replace 1 with real reservation ID
-- SELECT * FROM vw_ActiveGuestReservations;
