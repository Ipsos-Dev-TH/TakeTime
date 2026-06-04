-- ================================================================
-- PHASE 15: Omni-Channel Messaging System
-- Migration 01: Channel Configuration, Unified Messages, Contacts
-- ================================================================

-- 1. Channel Configurations
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Channels')
BEGIN
    CREATE TABLE OmniChannel_Channels (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        ChannelCode NVARCHAR(30) NOT NULL,
        ChannelName NVARCHAR(100) NOT NULL,
        ChannelType NVARCHAR(20) NOT NULL,
        IconClass NVARCHAR(50),
        BrandColor NVARCHAR(10),
        IsEnabled BIT DEFAULT 0,
        Config NVARCHAR(MAX),
        WebhookSecret NVARCHAR(200),
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_OmniChannel_Code UNIQUE (ChannelCode)
    );

    INSERT INTO OmniChannel_Channels (ChannelCode, ChannelName, ChannelType, IconClass, BrandColor, IsEnabled) VALUES
    ('GUEST_PORTAL', N'Guest Portal',           'INTERNAL', 'fas fa-hotel',       '#667eea', 1),
    ('LINE',         N'LINE Official Account',  'SOCIAL',   'fab fa-line',        '#06C755', 0),
    ('FACEBOOK',     N'Facebook Messenger',     'SOCIAL',   'fab fa-facebook-messenger', '#0084FF', 0),
    ('WHATSAPP',     N'WhatsApp Business',      'SOCIAL',   'fab fa-whatsapp',    '#25D366', 0),
    ('WECHAT',       N'WeChat',                 'SOCIAL',   'fab fa-weixin',      '#07C160', 0),
    ('INSTAGRAM',    N'Instagram DM',           'SOCIAL',   'fab fa-instagram',   '#E4405F', 0),
    ('AGODA',        N'Agoda',                  'OTA',      'fas fa-bed',         '#5542F6', 0),
    ('BOOKING',      N'Booking.com',            'OTA',      'fas fa-globe',       '#003580', 0),
    ('TRIP',         N'Trip.com',               'OTA',      'fas fa-plane',       '#287DFA', 0),
    ('EXPEDIA',      N'Expedia',                'OTA',      'fas fa-suitcase',    '#FBAF17', 0),
    ('EMAIL',        N'Email',                  'EMAIL',    'fas fa-envelope',    '#EA4335', 0),
    ('TELEGRAM',     N'Telegram',               'SOCIAL',   'fab fa-telegram',    '#0088CC', 0),
    ('SMS',          N'SMS',                    'SMS',      'fas fa-sms',         '#4CAF50', 0);

    PRINT 'Created table: OmniChannel_Channels';
END
GO

-- 2. Contacts (unified customer identity across channels)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Contacts')
BEGIN
    CREATE TABLE OmniChannel_Contacts (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        DisplayName NVARCHAR(200),
        AvatarUrl NVARCHAR(500),
        MobilePhone NVARCHAR(50),
        Email NVARCHAR(200),
        Customer_MobilePhone NVARCHAR(50),
        Reservation_ID BIGINT NULL,
        Notes NVARCHAR(MAX),
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_OmniContact_Phone (MobilePhone),
        INDEX IX_OmniContact_Customer (Customer_MobilePhone)
    );

    PRINT 'Created table: OmniChannel_Contacts';
END
GO

-- 3. Contact-Channel Identifiers (link a contact to their ID on each platform)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Contact_Identifiers')
BEGIN
    CREATE TABLE OmniChannel_Contact_Identifiers (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ContactID BIGINT NOT NULL REFERENCES OmniChannel_Contacts(ID),
        ChannelCode NVARCHAR(30) NOT NULL,
        PlatformUserId NVARCHAR(300) NOT NULL,
        PlatformUserName NVARCHAR(200),
        Created_Date DATETIME DEFAULT GETDATE(),
        CONSTRAINT UQ_OmniContact_Channel UNIQUE (ChannelCode, PlatformUserId),
        INDEX IX_OmniContactId_Contact (ContactID)
    );

    PRINT 'Created table: OmniChannel_Contact_Identifiers';
END
GO

-- 4. Conversations (a thread per contact per channel)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Conversations')
BEGIN
    CREATE TABLE OmniChannel_Conversations (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ContactID BIGINT NOT NULL REFERENCES OmniChannel_Contacts(ID),
        ChannelCode NVARCHAR(30) NOT NULL,
        Subject NVARCHAR(300),
        Status NVARCHAR(20) DEFAULT 'OPEN',
        Priority NVARCHAR(10) DEFAULT 'NORMAL',
        AssignedTo NVARCHAR(100),
        Tags NVARCHAR(500),
        LastMessageDate DATETIME,
        LastMessagePreview NVARCHAR(200),
        UnreadCount INT DEFAULT 0,
        Created_Date DATETIME DEFAULT GETDATE(),
        Updated_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_OmniConv_Contact (ContactID),
        INDEX IX_OmniConv_Channel (ChannelCode),
        INDEX IX_OmniConv_Status (Status),
        INDEX IX_OmniConv_LastMsg (LastMessageDate DESC)
    );

    PRINT 'Created table: OmniChannel_Conversations';
END
GO

-- 5. Unified Messages
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_Messages')
BEGIN
    CREATE TABLE OmniChannel_Messages (
        ID BIGINT IDENTITY(1,1) PRIMARY KEY,
        ConversationID BIGINT NOT NULL REFERENCES OmniChannel_Conversations(ID),
        Direction NVARCHAR(10) NOT NULL,
        SenderName NVARCHAR(200),
        MessageType NVARCHAR(20) DEFAULT 'TEXT',
        Content NVARCHAR(MAX),
        MediaUrl NVARCHAR(500),
        MediaType NVARCHAR(50),
        PlatformMessageId NVARCHAR(200),
        IsRead BIT DEFAULT 0,
        ReadDate DATETIME,
        DeliveryStatus NVARCHAR(20) DEFAULT 'SENT',
        Metadata NVARCHAR(MAX),
        Created_Date DATETIME DEFAULT GETDATE(),
        INDEX IX_OmniMsg_Conv (ConversationID),
        INDEX IX_OmniMsg_Date (Created_Date DESC),
        INDEX IX_OmniMsg_Unread (ConversationID, IsRead) WHERE IsRead = 0
    );

    PRINT 'Created table: OmniChannel_Messages';
END
GO

-- 6. Canned Responses (quick reply templates for staff)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'OmniChannel_CannedResponses')
BEGIN
    CREATE TABLE OmniChannel_CannedResponses (
        ID INT IDENTITY(1,1) PRIMARY KEY,
        Shortcut NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Content NVARCHAR(MAX) NOT NULL,
        Category NVARCHAR(50),
        Language NVARCHAR(10) DEFAULT 'TH',
        IsActive BIT DEFAULT 1,
        UsageCount INT DEFAULT 0,
        Created_Date DATETIME DEFAULT GETDATE()
    );

    INSERT INTO OmniChannel_CannedResponses (Shortcut, Title, Content, Category, Language) VALUES
    ('/hi',       N'ทักทาย',           N'สวัสดีค่ะ ยินดีต้อนรับสู่ TakeTime BangPhra ขอบคุณที่ติดต่อเข้ามา มีอะไรให้ช่วยไหมคะ?', 'GREETING', 'TH'),
    ('/hi-en',    N'Greeting EN',      N'Hello! Welcome to TakeTime BangPhra. Thank you for reaching out. How can I assist you?', 'GREETING', 'EN'),
    ('/checkin',  N'เวลาเช็คอิน',     N'เวลาเช็คอิน 14:00 น. และเช็คเอาท์ 12:00 น. ค่ะ หากต้องการ Early check-in หรือ Late check-out กรุณาแจ้งล่วงหน้านะคะ', 'INFO', 'TH'),
    ('/location', N'ที่ตั้ง',          N'TakeTime BangPhra ตั้งอยู่ที่ตำบลบางพระ อำเภอศรีราชา จังหวัดชลบุรี ห่างจากกรุงเทพฯ ประมาณ 1.5 ชั่วโมง สามารถดูแผนที่ได้ที่ Google Maps', 'INFO', 'TH'),
    ('/thanks',   N'ขอบคุณ',           N'ขอบคุณค่ะ หากมีข้อสงสัยเพิ่มเติมสามารถทักมาได้ตลอดเลยนะคะ ยินดีให้บริการค่ะ 😊', 'CLOSING', 'TH'),
    ('/wifi',     N'WiFi',             N'รหัส WiFi คือ TakeTime2024 ค่ะ เชื่อมต่อ WiFi ชื่อ "TakeTime-Guest" ได้เลยนะคะ', 'INFO', 'TH'),
    ('/promo',    N'โปรโมชัน',         N'ขอบคุณที่สนใจค่ะ สามารถดูโปรโมชันล่าสุดได้ที่เว็บไซต์ของเรา หรือแจ้งวันที่ต้องการเข้าพักมาได้เลยค่ะ จะเช็คราคาพิเศษให้ค่ะ', 'SALES', 'TH');

    PRINT 'Created table: OmniChannel_CannedResponses';
END
GO

-- 7. Stored procedure: Upsert conversation and add message
IF EXISTS (SELECT * FROM sys.objects WHERE name = 'sp_OmniChannel_AddMessage' AND type = 'P')
    DROP PROCEDURE sp_OmniChannel_AddMessage;
GO

CREATE PROCEDURE sp_OmniChannel_AddMessage
    @ChannelCode NVARCHAR(30),
    @PlatformUserId NVARCHAR(300),
    @PlatformUserName NVARCHAR(200) = NULL,
    @Direction NVARCHAR(10),
    @SenderName NVARCHAR(200) = NULL,
    @MessageType NVARCHAR(20) = 'TEXT',
    @Content NVARCHAR(MAX),
    @MediaUrl NVARCHAR(500) = NULL,
    @MediaType NVARCHAR(50) = NULL,
    @PlatformMessageId NVARCHAR(200) = NULL,
    @Metadata NVARCHAR(MAX) = NULL,
    @DisplayName NVARCHAR(200) = NULL,
    @AvatarUrl NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Find or create contact
        DECLARE @ContactID BIGINT;
        SELECT @ContactID = ci.ContactID
        FROM OmniChannel_Contact_Identifiers ci
        WHERE ci.ChannelCode = @ChannelCode AND ci.PlatformUserId = @PlatformUserId;

        IF @ContactID IS NULL
        BEGIN
            INSERT INTO OmniChannel_Contacts (DisplayName, AvatarUrl, Created_Date, Updated_Date)
            VALUES (ISNULL(@DisplayName, @PlatformUserName), @AvatarUrl, GETDATE(), GETDATE());
            SET @ContactID = SCOPE_IDENTITY();

            INSERT INTO OmniChannel_Contact_Identifiers (ContactID, ChannelCode, PlatformUserId, PlatformUserName)
            VALUES (@ContactID, @ChannelCode, @PlatformUserId, @PlatformUserName);
        END
        ELSE
        BEGIN
            IF @DisplayName IS NOT NULL
                UPDATE OmniChannel_Contacts SET DisplayName = @DisplayName, Updated_Date = GETDATE() WHERE ID = @ContactID;
        END

        -- 2. Find or create conversation
        DECLARE @ConvID BIGINT;
        SELECT TOP 1 @ConvID = ID FROM OmniChannel_Conversations
        WHERE ContactID = @ContactID AND ChannelCode = @ChannelCode AND Status IN ('OPEN', 'PENDING')
        ORDER BY Created_Date DESC;

        IF @ConvID IS NULL
        BEGIN
            INSERT INTO OmniChannel_Conversations (ContactID, ChannelCode, Status, LastMessageDate, LastMessagePreview, UnreadCount, Created_Date, Updated_Date)
            VALUES (@ContactID, @ChannelCode,
                    CASE WHEN @Direction = 'IN' THEN 'OPEN' ELSE 'PENDING' END,
                    GETDATE(), LEFT(@Content, 200),
                    CASE WHEN @Direction = 'IN' THEN 1 ELSE 0 END,
                    GETDATE(), GETDATE());
            SET @ConvID = SCOPE_IDENTITY();
        END
        ELSE
        BEGIN
            UPDATE OmniChannel_Conversations
            SET LastMessageDate = GETDATE(),
                LastMessagePreview = LEFT(@Content, 200),
                UnreadCount = CASE WHEN @Direction = 'IN' THEN UnreadCount + 1 ELSE UnreadCount END,
                Status = CASE WHEN @Direction = 'IN' AND Status = 'RESOLVED' THEN 'OPEN' ELSE Status END,
                Updated_Date = GETDATE()
            WHERE ID = @ConvID;
        END

        -- 3. Insert message
        INSERT INTO OmniChannel_Messages (ConversationID, Direction, SenderName, MessageType, Content, MediaUrl, MediaType, PlatformMessageId, IsRead, DeliveryStatus, Metadata, Created_Date)
        VALUES (@ConvID, @Direction, @SenderName, @MessageType, @Content, @MediaUrl, @MediaType, @PlatformMessageId,
                CASE WHEN @Direction = 'OUT' THEN 1 ELSE 0 END,
                CASE WHEN @Direction = 'OUT' THEN 'SENT' ELSE 'DELIVERED' END,
                @Metadata, GETDATE());

        DECLARE @MsgID BIGINT = SCOPE_IDENTITY();

        COMMIT TRANSACTION;

        SELECT @MsgID AS MessageID, @ConvID AS ConversationID, @ContactID AS ContactID;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

PRINT 'Created procedure: sp_OmniChannel_AddMessage';
GO
