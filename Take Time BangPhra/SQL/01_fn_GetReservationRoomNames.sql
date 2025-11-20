-- =============================================
-- Function: fn_GetReservationRoomNames
-- Purpose: Get comma-separated room names for a reservation
-- Created: 2025-11-06
-- =============================================

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

-- Test the function (uncomment to test with a real reservation ID)
-- SELECT dbo.fn_GetReservationRoomNames(1);
