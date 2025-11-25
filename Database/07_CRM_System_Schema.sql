-- ============================================================================
-- CRM System Database Schema
-- Version: 1.0
-- Date: 2025-01-24
-- Description: Complete CRM system with Guest Preferences, Loyalty Program,
--              Reviews, Communication History, and Guest Segmentation
-- ============================================================================

USE TakeTime;
GO

-- ============================================================================
-- 1. GUEST PREFERENCES & NOTES
-- ============================================================================

-- Guest Preferences (ความชอบของแขก)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Guest_Preferences]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Guest_Preferences] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [PreferenceType] VARCHAR(50) NOT NULL, -- ROOM, PILLOW, BED, FOOD, ALLERGY, ACTIVITY, SPECIAL_REQUEST
        [PreferenceKey] NVARCHAR(100) NOT NULL, -- e.g., 'favorite_room_type', 'pillow_firmness', 'food_allergy'
        [PreferenceValue] NVARCHAR(MAX), -- e.g., 'Deluxe Bungalow', 'Soft', 'Peanuts'
        [Notes] NVARCHAR(500),
        [Priority] TINYINT DEFAULT 1, -- 1=Low, 2=Medium, 3=High, 4=Critical
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,
        [LastUpdated] DATETIME DEFAULT GETDATE(),
        [UpdatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_GuestPreferences_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_GuestPreferences_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_GuestPreferences_UpdatedBy] FOREIGN KEY ([UpdatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_GuestPreferences_Customer] ON [dbo].[Guest_Preferences]([Customer_MobilePhone]);
    CREATE INDEX [IDX_GuestPreferences_Type] ON [dbo].[Guest_Preferences]([PreferenceType]);
    CREATE INDEX [IDX_GuestPreferences_Active] ON [dbo].[Guest_Preferences]([IsActive]);
END
GO

-- Guest Notes (บันทึกเกี่ยวกับแขก)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Guest_Notes]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Guest_Notes] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [NoteType] VARCHAR(30) NOT NULL, -- GENERAL, VIP, COMPLAINT, COMPLIMENT, BEHAVIOR, HEALTH, WARNING
        [Subject] NVARCHAR(200),
        [NoteText] NVARCHAR(MAX) NOT NULL,
        [IsImportant] BIT DEFAULT 0,
        [IsPrivate] BIT DEFAULT 0, -- Only visible to management
        [Reservation_ID] BIGINT, -- Link to specific reservation if relevant
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT NOT NULL,
        [LastUpdated] DATETIME DEFAULT GETDATE(),
        [UpdatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_GuestNotes_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_GuestNotes_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]),
        CONSTRAINT [FK_GuestNotes_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_GuestNotes_UpdatedBy] FOREIGN KEY ([UpdatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_GuestNotes_Customer] ON [dbo].[Guest_Notes]([Customer_MobilePhone]);
    CREATE INDEX [IDX_GuestNotes_Type] ON [dbo].[Guest_Notes]([NoteType]);
    CREATE INDEX [IDX_GuestNotes_Important] ON [dbo].[Guest_Notes]([IsImportant]);
    CREATE INDEX [IDX_GuestNotes_Reservation] ON [dbo].[Guest_Notes]([Reservation_ID]);
END
GO

-- ============================================================================
-- 2. LOYALTY PROGRAM
-- ============================================================================

-- Loyalty Tiers (ระดับสมาชิก)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Tiers]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Tiers] (
        [ID] TINYINT IDENTITY(1,1) PRIMARY KEY,
        [TierName] NVARCHAR(50) NOT NULL UNIQUE,
        [TierNameEN] VARCHAR(50) NOT NULL,
        [MinPoints] INT NOT NULL DEFAULT 0,
        [PointsMultiplier] DECIMAL(3,2) NOT NULL DEFAULT 1.00, -- Earn rate: 1.0x, 1.5x, 2.0x
        [DiscountPercent] DECIMAL(5,2) DEFAULT 0, -- Automatic discount %
        [Benefits] NVARCHAR(MAX), -- JSON or text description
        [TierColor] VARCHAR(7), -- Hex color for UI
        [DisplayOrder] TINYINT NOT NULL,
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE()
    );

    -- Default tiers
    INSERT INTO [dbo].[Loyalty_Tiers] ([TierName], [TierNameEN], [MinPoints], [PointsMultiplier], [DiscountPercent], [TierColor], [DisplayOrder]) VALUES
    (N'สมาชิกทั่วไป', 'Member', 0, 1.00, 0, '#95A5A6', 1),
    (N'สมาชิกเงิน', 'Silver', 1000, 1.25, 5, '#BDC3C7', 2),
    (N'สมาชิกทอง', 'Gold', 5000, 1.50, 10, '#F39C12', 3),
    (N'สมาชิกแพลทินัม', 'Platinum', 15000, 2.00, 15, '#9B59B6', 4),
    (N'สมาชิก VIP', 'VIP', 50000, 3.00, 20, '#E74C3C', 5);
END
GO

-- Customer Loyalty (คะแนนสะสมของลูกค้า)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customer_Loyalty]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Customer_Loyalty] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL UNIQUE,
        [CurrentTier_ID] TINYINT NOT NULL DEFAULT 1,
        [TotalPoints] INT NOT NULL DEFAULT 0,
        [AvailablePoints] INT NOT NULL DEFAULT 0, -- Points not yet used
        [LifetimePoints] INT NOT NULL DEFAULT 0, -- Total points ever earned
        [PointsExpiring] INT DEFAULT 0, -- Points expiring soon
        [ExpiryDate] DATE, -- Next points expiry date
        [TotalSpend] DECIMAL(12,2) DEFAULT 0, -- Lifetime spend
        [TotalReservations] INT DEFAULT 0,
        [LastEarnDate] DATETIME,
        [LastRedemptionDate] DATETIME,
        [MemberSince] DATE DEFAULT CAST(GETDATE() AS DATE),
        [LastUpdated] DATETIME DEFAULT GETDATE(),

        CONSTRAINT [FK_CustomerLoyalty_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_CustomerLoyalty_Tier] FOREIGN KEY ([CurrentTier_ID])
            REFERENCES [dbo].[Loyalty_Tiers]([ID]),
        CONSTRAINT [CHK_CustomerLoyalty_Points] CHECK ([AvailablePoints] >= 0 AND [TotalPoints] >= 0)
    );

    CREATE INDEX [IDX_CustomerLoyalty_Tier] ON [dbo].[Customer_Loyalty]([CurrentTier_ID]);
    CREATE INDEX [IDX_CustomerLoyalty_Points] ON [dbo].[Customer_Loyalty]([AvailablePoints]);
END
GO

-- Loyalty Points Transactions (ประวัติการได้-ใช้คะแนน)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Transactions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Transactions] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [TransactionType] VARCHAR(20) NOT NULL, -- EARN, REDEEM, EXPIRE, ADJUSTMENT, BONUS
        [Points] INT NOT NULL,
        [BalanceAfter] INT NOT NULL,
        [Reservation_ID] BIGINT,
        [Receipt_ID] NVARCHAR(15),
        [Reward_ID] INT, -- Link to redeemed reward
        [Description] NVARCHAR(500),
        [ExpiryDate] DATE, -- For earned points
        [Status] VARCHAR(20) DEFAULT 'ACTIVE', -- ACTIVE, EXPIRED, CANCELLED
        [TransactionDate] DATETIME DEFAULT GETDATE(),
        [ProcessedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_LoyaltyTransactions_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_LoyaltyTransactions_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]),
        CONSTRAINT [FK_LoyaltyTransactions_Receipt] FOREIGN KEY ([Receipt_ID])
            REFERENCES [dbo].[Account_Receipt]([ID]),
        CONSTRAINT [FK_LoyaltyTransactions_Admin] FOREIGN KEY ([ProcessedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_LoyaltyTransactions_Customer] ON [dbo].[Loyalty_Transactions]([Customer_MobilePhone]);
    CREATE INDEX [IDX_LoyaltyTransactions_Type] ON [dbo].[Loyalty_Transactions]([TransactionType]);
    CREATE INDEX [IDX_LoyaltyTransactions_Date] ON [dbo].[Loyalty_Transactions]([TransactionDate]);
    CREATE INDEX [IDX_LoyaltyTransactions_Status] ON [dbo].[Loyalty_Transactions]([Status]);
END
GO

-- Loyalty Rewards Catalog (ของรางวัลที่แลกได้)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Rewards]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Rewards] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [RewardName] NVARCHAR(200) NOT NULL,
        [RewardNameEN] VARCHAR(200),
        [Description] NVARCHAR(MAX),
        [Category] VARCHAR(50), -- DISCOUNT, FREE_NIGHT, UPGRADE, GIFT, VOUCHER, SERVICE
        [PointsCost] INT NOT NULL,
        [MonetaryValue] DECIMAL(10,2), -- Value in Baht
        [StockQuantity] INT, -- NULL = unlimited
        [MinTierRequired] TINYINT DEFAULT 1,
        [ImageUrl] VARCHAR(500),
        [Terms] NVARCHAR(MAX),
        [ValidityDays] INT DEFAULT 365, -- Days valid after redemption
        [IsActive] BIT DEFAULT 1,
        [DisplayOrder] INT,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_LoyaltyRewards_MinTier] FOREIGN KEY ([MinTierRequired])
            REFERENCES [dbo].[Loyalty_Tiers]([ID]),
        CONSTRAINT [FK_LoyaltyRewards_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CHK_LoyaltyRewards_PointsCost] CHECK ([PointsCost] > 0)
    );

    CREATE INDEX [IDX_LoyaltyRewards_Category] ON [dbo].[Loyalty_Rewards]([Category]);
    CREATE INDEX [IDX_LoyaltyRewards_Active] ON [dbo].[Loyalty_Rewards]([IsActive]);
    CREATE INDEX [IDX_LoyaltyRewards_PointsCost] ON [dbo].[Loyalty_Rewards]([PointsCost]);

    -- Sample rewards
    INSERT INTO [dbo].[Loyalty_Rewards] ([RewardName], [RewardNameEN], [Description], [Category], [PointsCost], [MonetaryValue], [MinTierRequired], [DisplayOrder]) VALUES
    (N'ส่วนลด 100 บาท', 'THB 100 Discount', N'ส่วนลด 100 บาทสำหรับการจองครั้งถัดไป', 'DISCOUNT', 500, 100, 1, 1),
    (N'ส่วนลด 500 บาท', 'THB 500 Discount', N'ส่วนลด 500 บาทสำหรับการจองครั้งถัดไป', 'DISCOUNT', 2000, 500, 2, 2),
    (N'อัพเกรดห้องฟรี', 'Free Room Upgrade', N'อัพเกรดห้องพักฟรี 1 ครั้ง', 'UPGRADE', 3000, 1000, 3, 3),
    (N'พักฟรี 1 คืน', 'Free Night Stay', N'พักฟรี 1 คืน (ห้องมาตรฐาน)', 'FREE_NIGHT', 8000, 2500, 4, 4),
    (N'บริการสปาฟรี', 'Free Spa Service', N'บริการนวดไทย 1 ชั่วโมงฟรี', 'SERVICE', 1500, 500, 2, 5);
END
GO

-- ============================================================================
-- 3. GUEST REVIEWS & RATINGS
-- ============================================================================

-- Guest Reviews (รีวิวจากแขก)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Guest_Reviews]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Guest_Reviews] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Reservation_ID] BIGINT NOT NULL,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,

        -- Overall ratings
        [OverallRating] TINYINT NOT NULL CHECK ([OverallRating] BETWEEN 1 AND 5),
        [CleanlinessRating] TINYINT CHECK ([CleanlinessRating] BETWEEN 1 AND 5),
        [ServiceRating] TINYINT CHECK ([ServiceRating] BETWEEN 1 AND 5),
        [FacilitiesRating] TINYINT CHECK ([FacilitiesRating] BETWEEN 1 AND 5),
        [LocationRating] TINYINT CHECK ([LocationRating] BETWEEN 1 AND 5),
        [ValueForMoneyRating] TINYINT CHECK ([ValueForMoneyRating] BETWEEN 1 AND 5),

        -- Review content
        [ReviewTitle] NVARCHAR(200),
        [ReviewText] NVARCHAR(MAX),
        [Pros] NVARCHAR(1000), -- What they liked
        [Cons] NVARCHAR(1000), -- What needs improvement

        -- Recommendations
        [WouldRecommend] BIT,
        [WouldReturn] BIT,
        [TravelType] VARCHAR(30), -- SOLO, COUPLE, FAMILY, FRIENDS, BUSINESS

        -- Review management
        [Status] VARCHAR(20) DEFAULT 'PENDING', -- PENDING, APPROVED, REJECTED, ARCHIVED
        [IsPublic] BIT DEFAULT 1,
        [IsFeatured] BIT DEFAULT 0,
        [IsVerified] BIT DEFAULT 1, -- Verified stay
        [ResponseText] NVARCHAR(MAX), -- Management response
        [ResponseDate] DATETIME,
        [ResponseBy_AdminID] SMALLINT,

        -- Tracking
        [SubmittedDate] DATETIME DEFAULT GETDATE(),
        [ApprovedDate] DATETIME,
        [ApprovedBy_AdminID] SMALLINT,
        [Source] VARCHAR(30) DEFAULT 'DIRECT', -- DIRECT, EMAIL, OTA, GOOGLE, FACEBOOK

        CONSTRAINT [FK_GuestReviews_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]),
        CONSTRAINT [FK_GuestReviews_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_GuestReviews_ResponseBy] FOREIGN KEY ([ResponseBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_GuestReviews_ApprovedBy] FOREIGN KEY ([ApprovedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_GuestReviews_Reservation] ON [dbo].[Guest_Reviews]([Reservation_ID]);
    CREATE INDEX [IDX_GuestReviews_Customer] ON [dbo].[Guest_Reviews]([Customer_MobilePhone]);
    CREATE INDEX [IDX_GuestReviews_Rating] ON [dbo].[Guest_Reviews]([OverallRating]);
    CREATE INDEX [IDX_GuestReviews_Status] ON [dbo].[Guest_Reviews]([Status]);
    CREATE INDEX [IDX_GuestReviews_Public] ON [dbo].[Guest_Reviews]([IsPublic]);
END
GO

-- ============================================================================
-- 4. COMMUNICATION HISTORY
-- ============================================================================

-- Communication Log (ประวัติการติดต่อกับลูกค้า)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Communication_Log]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Communication_Log] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [CommunicationType] VARCHAR(30) NOT NULL, -- EMAIL, SMS, LINE, PHONE, WHATSAPP, IN_PERSON
        [Direction] VARCHAR(10) NOT NULL, -- INBOUND, OUTBOUND
        [Subject] NVARCHAR(200),
        [MessageContent] NVARCHAR(MAX),
        [Purpose] VARCHAR(50), -- BOOKING, INQUIRY, COMPLAINT, FEEDBACK, MARKETING, REMINDER, FOLLOW_UP
        [Reservation_ID] BIGINT,
        [Status] VARCHAR(20) DEFAULT 'SENT', -- SENT, DELIVERED, READ, FAILED, BOUNCED
        [SentDate] DATETIME DEFAULT GETDATE(),
        [DeliveredDate] DATETIME,
        [ReadDate] DATETIME,
        [SentBy_AdminID] SMALLINT,
        [Campaign_ID] INT, -- Link to marketing campaign if applicable
        [Attachments] NVARCHAR(MAX), -- JSON array of attachment paths
        [Notes] NVARCHAR(500),

        CONSTRAINT [FK_CommunicationLog_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_CommunicationLog_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]),
        CONSTRAINT [FK_CommunicationLog_Admin] FOREIGN KEY ([SentBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_CommunicationLog_Customer] ON [dbo].[Communication_Log]([Customer_MobilePhone]);
    CREATE INDEX [IDX_CommunicationLog_Type] ON [dbo].[Communication_Log]([CommunicationType]);
    CREATE INDEX [IDX_CommunicationLog_Date] ON [dbo].[Communication_Log]([SentDate]);
    CREATE INDEX [IDX_CommunicationLog_Purpose] ON [dbo].[Communication_Log]([Purpose]);
END
GO

-- ============================================================================
-- 5. GUEST SEGMENTATION
-- ============================================================================

-- Guest Segments (กลุ่มลูกค้า)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Guest_Segments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Guest_Segments] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [SegmentName] NVARCHAR(100) NOT NULL UNIQUE,
        [SegmentNameEN] VARCHAR(100),
        [Description] NVARCHAR(500),
        [Criteria] NVARCHAR(MAX), -- JSON with segmentation rules
        [Color] VARCHAR(7),
        [Icon] VARCHAR(50),
        [IsAutomatic] BIT DEFAULT 1, -- Auto-assign based on criteria
        [IsActive] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_GuestSegments_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    -- Default segments
    INSERT INTO [dbo].[Guest_Segments] ([SegmentName], [SegmentNameEN], [Description], [Color], [Icon], [IsAutomatic]) VALUES
    (N'ลูกค้า VIP', 'VIP Guests', N'ลูกค้าใช้บริการมากกว่า 10 ครั้งหรือมูลค่ามากกว่า 50,000 บาท', '#E74C3C', 'crown', 1),
    (N'ลูกค้าประจำ', 'Repeat Guests', N'ลูกค้าที่เคยใช้บริการมากกว่า 2 ครั้ง', '#3498DB', 'star', 1),
    (N'ลูกค้าใหม่', 'New Guests', N'ลูกค้าที่ใช้บริการครั้งแรก', '#2ECC71', 'user-plus', 1),
    (N'ลูกค้าไม่กลับมา', 'Inactive Guests', N'ลูกค้าที่ไม่กลับมาใช้บริการมากกว่า 1 ปี', '#95A5A6', 'user-times', 1),
    (N'ครอบครัว', 'Families', N'ลูกค้าที่มากับครอบครัว', '#F39C12', 'users', 1),
    (N'คู่รัก', 'Couples', N'ลูกค้าที่มาเป็นคู่', '#E91E63', 'heart', 1);
END
GO

-- Customer Segment Assignments (การกำหนดกลุ่มลูกค้า)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customer_Segment_Assignments]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Customer_Segment_Assignments] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Segment_ID] INT NOT NULL,
        [AssignedDate] DATETIME DEFAULT GETDATE(),
        [AssignedBy_AdminID] SMALLINT, -- NULL if automatic
        [IsActive] BIT DEFAULT 1,

        CONSTRAINT [FK_CustomerSegments_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_CustomerSegments_Segment] FOREIGN KEY ([Segment_ID])
            REFERENCES [dbo].[Guest_Segments]([ID]),
        CONSTRAINT [FK_CustomerSegments_AssignedBy] FOREIGN KEY ([AssignedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [UQ_CustomerSegment] UNIQUE ([Customer_MobilePhone], [Segment_ID])
    );

    CREATE INDEX [IDX_CustomerSegments_Customer] ON [dbo].[Customer_Segment_Assignments]([Customer_MobilePhone]);
    CREATE INDEX [IDX_CustomerSegments_Segment] ON [dbo].[Customer_Segment_Assignments]([Segment_ID]);
END
GO

-- ============================================================================
-- 6. BIRTHDAY & SPECIAL OCCASIONS
-- ============================================================================

-- Customer Special Dates (วันเกิดและวันสำคัญ)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Customer_Special_Dates]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Customer_Special_Dates] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [OccasionType] VARCHAR(30) NOT NULL, -- BIRTHDAY, ANNIVERSARY, WEDDING, OTHER
        [OccasionName] NVARCHAR(100),
        [OccasionDate] DATE NOT NULL, -- Store as month-day (year optional)
        [YearOptional] BIT DEFAULT 1, -- If true, celebrate every year regardless
        [SendReminder] BIT DEFAULT 1,
        [ReminderDaysBefore] INT DEFAULT 7,
        [LastReminderSent] DATE,
        [SpecialNotes] NVARCHAR(500),
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_CustomerSpecialDates_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_CustomerSpecialDates_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_CustomerSpecialDates_Customer] ON [dbo].[Customer_Special_Dates]([Customer_MobilePhone]);
    CREATE INDEX [IDX_CustomerSpecialDates_Date] ON [dbo].[Customer_Special_Dates]([OccasionDate]);
    CREATE INDEX [IDX_CustomerSpecialDates_Type] ON [dbo].[Customer_Special_Dates]([OccasionType]);
END
GO

PRINT 'CRM System Schema created successfully!';
GO
