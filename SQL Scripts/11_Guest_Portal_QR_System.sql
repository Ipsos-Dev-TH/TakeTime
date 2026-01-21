-- ============================================
-- Guest Portal QR Code System
-- Stores QR codes for room access and guest verification
-- ============================================

USE [TakeTime]
GO

-- ============================================
-- Table: Room_QR_Codes
-- Purpose: Store QR codes for each accommodation
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Room_QR_Codes')
BEGIN
    CREATE TABLE [dbo].[Room_QR_Codes] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Accommodation_ID] TINYINT NOT NULL,
        [Accommodation_Name] NVARCHAR(100) NOT NULL,
        [QR_Token] NVARCHAR(255) NOT NULL UNIQUE, -- Unique token for QR code
        [QR_Data] NVARCHAR(500) NOT NULL, -- Full QR code data (URL with token)
        [Generated_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Last_Scanned_Date] DATETIME NULL,
        [Scan_Count] INT NOT NULL DEFAULT 0,
        [Is_Active] BIT NOT NULL DEFAULT 1,
        [Created_By] SMALLINT NULL,
        [Updated_Date] DATETIME NULL,

        CONSTRAINT FK_RoomQR_Accommodation FOREIGN KEY (Accommodation_ID)
            REFERENCES Accommodation(ID)
    );

    CREATE INDEX IX_RoomQR_Token ON Room_QR_Codes(QR_Token);
    CREATE INDEX IX_RoomQR_AccomID ON Room_QR_Codes(Accommodation_ID);

    PRINT 'Table Room_QR_Codes created successfully';
END
ELSE
BEGIN
    PRINT 'Table Room_QR_Codes already exists';
END
GO

-- ============================================
-- Table: Guest_Portal_Sessions
-- Purpose: Track guest portal access sessions
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Portal_Sessions')
BEGIN
    CREATE TABLE [dbo].[Guest_Portal_Sessions] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Session_Token] NVARCHAR(255) NOT NULL UNIQUE,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Accommodation_ID] TINYINT NOT NULL,
        [QR_Token] NVARCHAR(255) NOT NULL,
        [Check_In_Date] DATE NOT NULL,
        [Check_Out_Date] DATE NOT NULL,
        [Login_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Last_Activity_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [IP_Address] NVARCHAR(50) NULL,
        [User_Agent] NVARCHAR(500) NULL,
        [Is_Active] BIT NOT NULL DEFAULT 1,
        [Logout_Date] DATETIME NULL,

        CONSTRAINT FK_PortalSession_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID),
        CONSTRAINT FK_PortalSession_Customer FOREIGN KEY (Customer_MobilePhone)
            REFERENCES Customer(MobilePhone)
    );

    CREATE INDEX IX_PortalSession_Token ON Guest_Portal_Sessions(Session_Token);
    CREATE INDEX IX_PortalSession_Customer ON Guest_Portal_Sessions(Customer_MobilePhone);
    CREATE INDEX IX_PortalSession_Reservation ON Guest_Portal_Sessions(Reservation_ID);

    PRINT 'Table Guest_Portal_Sessions created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Portal_Sessions already exists';
END
GO

-- ============================================
-- Table: Guest_Room_Service_Orders
-- Purpose: Store room service orders from guests
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Room_Service_Orders')
BEGIN
    CREATE TABLE [dbo].[Guest_Room_Service_Orders] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Order_Number] NVARCHAR(50) NOT NULL UNIQUE,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Accommodation_ID] TINYINT NOT NULL,
        [Order_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Delivery_Instructions] NVARCHAR(500) NULL,
        [Total_Amount] DECIMAL(10,2) NOT NULL DEFAULT 0,
        [Payment_Method] NVARCHAR(50) NOT NULL, -- 'CHARGE_TO_ROOM', 'TRANSFER', 'CASH'
        [Payment_Status] NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'PAID', 'CHARGED'
        [Payment_Slip_Path] NVARCHAR(500) NULL,
        [Order_Status] NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'CONFIRMED', 'PREPARING', 'DELIVERED', 'CANCELLED'
        [Confirmed_By] SMALLINT NULL,
        [Confirmed_Date] DATETIME NULL,
        [Delivered_By] SMALLINT NULL,
        [Delivered_Date] DATETIME NULL,
        [Cancelled_Reason] NVARCHAR(500) NULL,
        [Notes] NVARCHAR(1000) NULL,

        CONSTRAINT FK_RoomService_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID),
        CONSTRAINT FK_RoomService_Customer FOREIGN KEY (Customer_MobilePhone)
            REFERENCES Customer(MobilePhone)
    );

    CREATE INDEX IX_RoomService_OrderNumber ON Guest_Room_Service_Orders(Order_Number);
    CREATE INDEX IX_RoomService_Reservation ON Guest_Room_Service_Orders(Reservation_ID);
    CREATE INDEX IX_RoomService_Status ON Guest_Room_Service_Orders(Order_Status);

    PRINT 'Table Guest_Room_Service_Orders created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Room_Service_Orders already exists';
END
GO

-- ============================================
-- Table: Guest_Room_Service_Items
-- Purpose: Store individual items in room service orders
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Room_Service_Items')
BEGIN
    CREATE TABLE [dbo].[Guest_Room_Service_Items] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Order_ID] BIGINT NOT NULL,
        [Product_ID] INT NOT NULL,
        [Product_Name] NVARCHAR(200) NOT NULL,
        [Quantity] INT NOT NULL,
        [Unit_Price] DECIMAL(10,2) NOT NULL,
        [Subtotal] DECIMAL(10,2) NOT NULL,
        [Notes] NVARCHAR(500) NULL,

        CONSTRAINT FK_RoomServiceItem_Order FOREIGN KEY (Order_ID)
            REFERENCES Guest_Room_Service_Orders(ID) ON DELETE CASCADE,
        CONSTRAINT FK_RoomServiceItem_Product FOREIGN KEY (Product_ID)
            REFERENCES Product(ID)
    );

    CREATE INDEX IX_RoomServiceItem_Order ON Guest_Room_Service_Items(Order_ID);

    PRINT 'Table Guest_Room_Service_Items created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Room_Service_Items already exists';
END
GO

-- ============================================
-- Table: Guest_Housekeeping_Requests
-- Purpose: Store housekeeping requests from guests
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Housekeeping_Requests')
BEGIN
    CREATE TABLE [dbo].[Guest_Housekeeping_Requests] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Request_Number] NVARCHAR(50) NOT NULL UNIQUE,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Accommodation_ID] TINYINT NOT NULL,
        [Request_Type] NVARCHAR(100) NOT NULL, -- 'CLEANING', 'TOWELS', 'TOILETRIES', 'MAINTENANCE', 'OTHER'
        [Request_Description] NVARCHAR(1000) NOT NULL,
        [Priority] NVARCHAR(20) NOT NULL DEFAULT 'NORMAL', -- 'LOW', 'NORMAL', 'HIGH', 'URGENT'
        [Request_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Preferred_Time] NVARCHAR(100) NULL,
        [Request_Status] NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'ASSIGNED', 'IN_PROGRESS', 'COMPLETED', 'CANCELLED'
        [Assigned_To] SMALLINT NULL,
        [Assigned_Date] DATETIME NULL,
        [Completed_By] SMALLINT NULL,
        [Completed_Date] DATETIME NULL,
        [Staff_Notes] NVARCHAR(1000) NULL,
        [Guest_Rating] TINYINT NULL, -- 1-5 stars
        [Guest_Feedback] NVARCHAR(500) NULL,

        CONSTRAINT FK_Housekeeping_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID),
        CONSTRAINT FK_Housekeeping_Customer FOREIGN KEY (Customer_MobilePhone)
            REFERENCES Customer(MobilePhone)
    );

    CREATE INDEX IX_Housekeeping_RequestNumber ON Guest_Housekeeping_Requests(Request_Number);
    CREATE INDEX IX_Housekeeping_Reservation ON Guest_Housekeeping_Requests(Reservation_ID);
    CREATE INDEX IX_Housekeeping_Status ON Guest_Housekeeping_Requests(Request_Status);

    PRINT 'Table Guest_Housekeeping_Requests created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Housekeeping_Requests already exists';
END
GO

-- ============================================
-- Table: Guest_Concierge_Requests
-- Purpose: Store concierge service requests (tours, spa, etc.)
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Concierge_Requests')
BEGIN
    CREATE TABLE [dbo].[Guest_Concierge_Requests] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Request_Number] NVARCHAR(50) NOT NULL UNIQUE,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Accommodation_ID] TINYINT NOT NULL,
        [Service_Type] NVARCHAR(100) NOT NULL, -- 'TOUR', 'SPA', 'RESTAURANT', 'TRANSPORTATION', 'ACTIVITY', 'OTHER'
        [Service_Name] NVARCHAR(200) NOT NULL,
        [Request_Description] NVARCHAR(1000) NOT NULL,
        [Preferred_Date] DATE NULL,
        [Preferred_Time] NVARCHAR(100) NULL,
        [Number_Of_Guests] INT NULL,
        [Request_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Request_Status] NVARCHAR(50) NOT NULL DEFAULT 'PENDING', -- 'PENDING', 'CONFIRMED', 'CANCELLED', 'COMPLETED'
        [Confirmed_By] SMALLINT NULL,
        [Confirmed_Date] DATETIME NULL,
        [Estimated_Cost] DECIMAL(10,2) NULL,
        [Final_Cost] DECIMAL(10,2) NULL,
        [Payment_Status] NVARCHAR(50) NULL, -- 'PENDING', 'PAID', 'CHARGED_TO_ROOM'
        [Staff_Notes] NVARCHAR(1000) NULL,
        [Guest_Rating] TINYINT NULL,
        [Guest_Feedback] NVARCHAR(500) NULL,

        CONSTRAINT FK_Concierge_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID),
        CONSTRAINT FK_Concierge_Customer FOREIGN KEY (Customer_MobilePhone)
            REFERENCES Customer(MobilePhone)
    );

    CREATE INDEX IX_Concierge_RequestNumber ON Guest_Concierge_Requests(Request_Number);
    CREATE INDEX IX_Concierge_Reservation ON Guest_Concierge_Requests(Reservation_ID);
    CREATE INDEX IX_Concierge_Status ON Guest_Concierge_Requests(Request_Status);

    PRINT 'Table Guest_Concierge_Requests created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Concierge_Requests already exists';
END
GO

-- ============================================
-- Table: Guest_Chat_Messages
-- Purpose: Store chat messages between guests and front desk
-- ============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Guest_Chat_Messages')
BEGIN
    CREATE TABLE [dbo].[Guest_Chat_Messages] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Message_Text] NVARCHAR(2000) NOT NULL,
        [Sender_Type] NVARCHAR(20) NOT NULL, -- 'GUEST', 'STAFF'
        [Sender_Name] NVARCHAR(200) NOT NULL,
        [Sender_ID] SMALLINT NULL, -- Staff ID if sender is staff
        [Message_Date] DATETIME NOT NULL DEFAULT GETDATE(),
        [Is_Read] BIT NOT NULL DEFAULT 0,
        [Read_Date] DATETIME NULL,
        [Attachment_Path] NVARCHAR(500) NULL,
        [Is_System_Message] BIT NOT NULL DEFAULT 0,

        CONSTRAINT FK_ChatMessage_Reservation FOREIGN KEY (Reservation_ID)
            REFERENCES Reservation(ID),
        CONSTRAINT FK_ChatMessage_Customer FOREIGN KEY (Customer_MobilePhone)
            REFERENCES Customer(MobilePhone)
    );

    CREATE INDEX IX_ChatMessage_Reservation ON Guest_Chat_Messages(Reservation_ID);
    CREATE INDEX IX_ChatMessage_Date ON Guest_Chat_Messages(Message_Date);

    PRINT 'Table Guest_Chat_Messages created successfully';
END
ELSE
BEGIN
    PRINT 'Table Guest_Chat_Messages already exists';
END
GO

-- ============================================
-- Stored Procedure: Generate QR Code for Room
-- ============================================
IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_Generate_Room_QR_Code')
    DROP PROCEDURE sp_Generate_Room_QR_Code;
GO

CREATE PROCEDURE sp_Generate_Room_QR_Code
    @Accommodation_ID TINYINT,
    @Admin_ID SMALLINT,
    @Base_URL NVARCHAR(500) = 'https://taketimebangphra.com/Guest/Portal?qr='
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @AccomName NVARCHAR(100);
    DECLARE @QRToken NVARCHAR(255);
    DECLARE @QRData NVARCHAR(500);

    -- Get accommodation name
    SELECT @AccomName = AccomName
    FROM Accommodation
    WHERE ID = @Accommodation_ID;

    IF @AccomName IS NULL
    BEGIN
        RAISERROR('Accommodation not found', 16, 1);
        RETURN;
    END

    -- Generate unique token (GUID + timestamp)
    SET @QRToken = REPLACE(NEWID(), '-', '') + '_' + CONVERT(NVARCHAR(20), DATEDIFF(SECOND, '1970-01-01', GETDATE()));

    -- Create QR data (URL with token)
    SET @QRData = @Base_URL + @QRToken;

    -- Check if QR already exists for this room
    IF EXISTS (SELECT 1 FROM Room_QR_Codes WHERE Accommodation_ID = @Accommodation_ID)
    BEGIN
        -- Update existing QR code
        UPDATE Room_QR_Codes
        SET QR_Token = @QRToken,
            QR_Data = @QRData,
            Updated_Date = GETDATE(),
            Is_Active = 1
        WHERE Accommodation_ID = @Accommodation_ID;
    END
    ELSE
    BEGIN
        -- Insert new QR code
        INSERT INTO Room_QR_Codes (Accommodation_ID, Accommodation_Name, QR_Token, QR_Data, Created_By)
        VALUES (@Accommodation_ID, @AccomName, @QRToken, @QRData, @Admin_ID);
    END

    -- Return the generated QR data
    SELECT TOP 1 * FROM Room_QR_Codes
    WHERE Accommodation_ID = @Accommodation_ID;
END
GO

-- ============================================
-- Stored Procedure: Verify Guest Access
-- ============================================
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
    -- Use CAST AS DATE to compare date only (allow access on checkout day until status changes)
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
        SELECT 0 AS IsValid, 'No active reservation found for this room' AS ErrorMessage;
    END
END
GO

PRINT 'Guest Portal QR System created successfully!';
GO
