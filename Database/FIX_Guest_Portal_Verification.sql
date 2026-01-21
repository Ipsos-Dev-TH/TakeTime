-- ============================================
-- FIX: Guest Portal Verification Stored Procedure
-- Issue: "No active reservation found for this room" error
--
-- Problems fixed:
-- 1. Column names: CheckInDate/CheckOutDate → CheckinDate/CheckoutDate
-- 2. Status values: Use NOT IN to exclude cancelled/completed statuses
--    instead of IN with incomplete list of active statuses
-- 3. Date comparison: Use CAST AS DATE to allow access on checkout day
-- ============================================

USE [TakeTime]
GO

PRINT '🔧 Fixing sp_Verify_Guest_Portal_Access stored procedure...';
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Verify_Guest_Portal_Access')
    DROP PROCEDURE sp_Verify_Guest_Portal_Access;
GO

CREATE PROCEDURE sp_Verify_Guest_Portal_Access
    @QR_Token NVARCHAR(255),
    @Customer_MobilePhone NVARCHAR(30)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Accommodation_ID TINYINT;
    DECLARE @Accommodation_Name NVARCHAR(100);

    -- Verify QR token exists and is active
    SELECT @Accommodation_ID = Accommodation_ID,
           @Accommodation_Name = Accommodation_Name
    FROM Room_QR_Codes
    WHERE QR_Token = @QR_Token AND Is_Active = 1;

    IF @Accommodation_ID IS NULL
    BEGIN
        SELECT 0 AS IsValid, 'Invalid or inactive QR code' AS ErrorMessage;
        RETURN;
    END

    -- Update scan count
    UPDATE Room_QR_Codes
    SET Scan_Count = Scan_Count + 1,
        Last_Scanned_Date = GETDATE()
    WHERE QR_Token = @QR_Token;

    -- Check if customer has active reservation for this accommodation
    -- FIX: Use correct column names (CheckinDate/CheckoutDate instead of CheckInDate/CheckOutDate)
    -- FIX: Use NOT IN to exclude cancelled/completed statuses for better coverage
    -- FIX: Use CAST AS DATE to compare date only (allow access on checkout day until status changes)
    IF EXISTS (
        SELECT 1
        FROM Reservation R
        INNER JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID
        WHERE R.Customer_MobilePhone = @Customer_MobilePhone
          AND RA.Accommodation_ID = @Accommodation_ID
          AND CAST(R.CheckinDate AS DATE) <= CAST(GETDATE() AS DATE)
          AND CAST(R.CheckoutDate AS DATE) >= CAST(GETDATE() AS DATE)
          AND R.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')
    )
    BEGIN
        -- Valid access - return reservation details
        SELECT TOP 1
            1 AS IsValid,
            R.ID AS Reservation_ID,
            R.Customer_MobilePhone,
            @Accommodation_ID AS Accommodation_ID,
            @Accommodation_Name AS Accommodation_Name,
            R.CheckinDate AS CheckInDate,
            R.CheckoutDate AS CheckOutDate,
            C.Name AS Customer_Name,
            C.Email AS Customer_Email
        FROM Reservation R
        INNER JOIN Customer C ON C.MobilePhone = R.Customer_MobilePhone
        INNER JOIN Reservation_Accommodation RA ON RA.Reservation_ID = R.ID
        WHERE R.Customer_MobilePhone = @Customer_MobilePhone
          AND RA.Accommodation_ID = @Accommodation_ID
          AND CAST(R.CheckinDate AS DATE) <= CAST(GETDATE() AS DATE)
          AND CAST(R.CheckoutDate AS DATE) >= CAST(GETDATE() AS DATE)
          AND R.Status NOT IN (N'ยกเลิก', N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน', N'เช็คเอาท์แล้ว', N'เสร็จสิ้น')
        ORDER BY R.CheckinDate DESC;
    END
    ELSE
    BEGIN
        SELECT 0 AS IsValid, N'ไม่พบข้อมูลการจองที่ใช้งานอยู่สำหรับห้องนี้ (No active reservation found for this room)' AS ErrorMessage;
    END
END
GO

PRINT '✅ sp_Verify_Guest_Portal_Access fixed successfully!';
PRINT '';
PRINT '📋 Changes made:';
PRINT '   1. Fixed column names: CheckInDate → CheckinDate, CheckOutDate → CheckoutDate';
PRINT '   2. Changed status filter from IN (ยืนยันแล้ว, เช็คอินแล้ว) to NOT IN (cancelled/completed statuses)';
PRINT '   3. Now supports all active reservation statuses: มัดจำแล้ว, เช็คอินแล้ว, เข้าพักแล้ว, จองแล้ว, etc.';
PRINT '   4. Date comparison uses CAST AS DATE - guests can access on checkout day until status changes';
GO
