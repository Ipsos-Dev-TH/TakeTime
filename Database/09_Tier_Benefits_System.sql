-- ============================================================================
-- Tier Benefits System (Accor-style Loyalty Benefits)
-- Version: 1.0
-- Date: 2025-01-24
-- Description: Complete tier benefits system with accommodation discounts,
--              product category discounts, and various perks
-- ============================================================================

USE TakeTime;
GO

-- ============================================================================
-- 1. TIER BENEFITS CONFIGURATION
-- ============================================================================

-- Tier Benefits (สิทธิพิเศษแต่ละระดับ)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Tier_Benefits]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Tier_Benefits] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Tier_ID] TINYINT NOT NULL,
        [BenefitType] VARCHAR(50) NOT NULL, -- ACCOMMODATION_DISCOUNT, PRODUCT_DISCOUNT, EARLY_CHECKIN, LATE_CHECKOUT, ROOM_UPGRADE, FREE_BREAKFAST, etc.
        [BenefitName] NVARCHAR(200) NOT NULL,
        [BenefitNameEN] VARCHAR(200),
        [Description] NVARCHAR(500),

        -- Discount configuration
        [DiscountType] VARCHAR(20), -- PERCENTAGE, FIXED_AMOUNT, FREE
        [DiscountValue] DECIMAL(10,2), -- Percentage (5.00 = 5%) or Fixed Amount
        [MaxDiscountAmount] DECIMAL(10,2), -- Cap for percentage discounts

        -- Time-based benefits
        [TimeValue] INT, -- Hours for early/late checkout

        -- Quantity-based benefits
        [QuantityValue] INT, -- Number of free items, upgrades, etc.

        -- Restrictions
        [MinSpendRequired] DECIMAL(10,2), -- Minimum spend to use benefit
        [MaxUsagePerStay] INT, -- Max times can use per reservation
        [MaxUsagePerMonth] INT, -- Max times per month
        [ApplicableDays] VARCHAR(100), -- JSON array: ["MON","TUE","WED"] or "ALL"
        [BlackoutDates] NVARCHAR(MAX), -- JSON array of date ranges

        -- Status
        [IsActive] BIT DEFAULT 1,
        [DisplayOrder] INT,
        [Icon] VARCHAR(50),
        [ColorCode] VARCHAR(7),

        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,
        [LastUpdated] DATETIME DEFAULT GETDATE(),
        [UpdatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_TierBenefits_Tier] FOREIGN KEY ([Tier_ID])
            REFERENCES [dbo].[Loyalty_Tiers]([ID]),
        CONSTRAINT [FK_TierBenefits_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [FK_TierBenefits_UpdatedBy] FOREIGN KEY ([UpdatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CHK_TierBenefits_DiscountValue] CHECK ([DiscountValue] >= 0)
    );

    CREATE INDEX [IDX_TierBenefits_Tier] ON [dbo].[Loyalty_Tier_Benefits]([Tier_ID]);
    CREATE INDEX [IDX_TierBenefits_Type] ON [dbo].[Loyalty_Tier_Benefits]([BenefitType]);
    CREATE INDEX [IDX_TierBenefits_Active] ON [dbo].[Loyalty_Tier_Benefits]([IsActive]);
END
GO

-- Product Category Discounts (ส่วนลดตามประเภทสินค้า)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Product_Category_Discounts]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Product_Category_Discounts] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [Tier_ID] TINYINT NOT NULL,
        [ProductCategory_ID] BIGINT NOT NULL,
        [DiscountPercent] DECIMAL(5,2) NOT NULL, -- e.g., 10.00 = 10%
        [MaxDiscountAmount] DECIMAL(10,2), -- Cap per transaction
        [MinPurchaseAmount] DECIMAL(10,2), -- Minimum purchase to get discount
        [IsActive] BIT DEFAULT 1,
        [ValidFrom] DATE,
        [ValidTo] DATE,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy_AdminID] SMALLINT,

        CONSTRAINT [FK_ProductCategoryDiscount_Tier] FOREIGN KEY ([Tier_ID])
            REFERENCES [dbo].[Loyalty_Tiers]([ID]),
        CONSTRAINT [FK_ProductCategoryDiscount_Category] FOREIGN KEY ([ProductCategory_ID])
            REFERENCES [dbo].[Product_Category]([ID]),
        CONSTRAINT [FK_ProductCategoryDiscount_CreatedBy] FOREIGN KEY ([CreatedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID]),
        CONSTRAINT [CHK_ProductCategoryDiscount_Percent] CHECK ([DiscountPercent] >= 0 AND [DiscountPercent] <= 100),
        CONSTRAINT [UQ_TierCategory] UNIQUE ([Tier_ID], [ProductCategory_ID])
    );

    CREATE INDEX [IDX_ProductCategoryDiscount_Tier] ON [dbo].[Loyalty_Product_Category_Discounts]([Tier_ID]);
    CREATE INDEX [IDX_ProductCategoryDiscount_Category] ON [dbo].[Loyalty_Product_Category_Discounts]([ProductCategory_ID]);
END
GO

-- ============================================================================
-- 2. BENEFIT USAGE TRACKING
-- ============================================================================

-- Benefit Usage History (ประวัติการใช้สิทธิ)
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Benefit_Usage]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Benefit_Usage] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] VARCHAR(10) NOT NULL,
        [Benefit_ID] INT NOT NULL,
        [Reservation_ID] BIGINT,
        [Receipt_ID] BIGINT,

        -- Usage details
        [BenefitType] VARCHAR(50) NOT NULL,
        [DiscountAmount] DECIMAL(10,2), -- Actual discount given
        [OriginalAmount] DECIMAL(10,2), -- Before discount
        [FinalAmount] DECIMAL(10,2), -- After discount

        [UsageDate] DATETIME DEFAULT GETDATE(),
        [UsageMonth] AS (CAST(YEAR([UsageDate]) AS VARCHAR(4)) + '-' + RIGHT('0' + CAST(MONTH([UsageDate]) AS VARCHAR(2)), 2)) PERSISTED,

        [ProcessedBy_AdminID] SMALLINT,
        [Notes] NVARCHAR(500),

        CONSTRAINT [FK_BenefitUsage_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_BenefitUsage_Benefit] FOREIGN KEY ([Benefit_ID])
            REFERENCES [dbo].[Loyalty_Tier_Benefits]([ID]),
        CONSTRAINT [FK_BenefitUsage_Reservation] FOREIGN KEY ([Reservation_ID])
            REFERENCES [dbo].[Reservation]([ID]),
        CONSTRAINT [FK_BenefitUsage_Receipt] FOREIGN KEY ([Receipt_ID])
            REFERENCES [dbo].[Account_Receipt]([ID]),
        CONSTRAINT [FK_BenefitUsage_Admin] FOREIGN KEY ([ProcessedBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_BenefitUsage_Customer] ON [dbo].[Loyalty_Benefit_Usage]([Customer_MobilePhone]);
    CREATE INDEX [IDX_BenefitUsage_Benefit] ON [dbo].[Loyalty_Benefit_Usage]([Benefit_ID]);
    CREATE INDEX [IDX_BenefitUsage_Date] ON [dbo].[Loyalty_Benefit_Usage]([UsageDate]);
    CREATE INDEX [IDX_BenefitUsage_Month] ON [dbo].[Loyalty_Benefit_Usage]([UsageMonth]);
END
GO

-- ============================================================================
-- 3. POPULATE DEFAULT TIER BENEFITS (Accor-style)
-- ============================================================================

-- Clear existing if needed
-- DELETE FROM [dbo].[Loyalty_Tier_Benefits];

-- Member Tier (Tier 1) - Basic benefits
INSERT INTO [dbo].[Loyalty_Tier_Benefits]
([Tier_ID], [BenefitType], [BenefitName], [BenefitNameEN], [Description], [DiscountType], [DiscountValue], [DisplayOrder], [Icon], [ColorCode])
VALUES
(1, 'ACCOMMODATION_DISCOUNT', N'ส่วนลดที่พัก', 'Accommodation Discount', N'ไม่มีส่วนลด', 'PERCENTAGE', 0, 1, 'home', '#95A5A6'),
(1, 'POINTS_MULTIPLIER', N'คะแนนสะสมปกติ', 'Standard Points', N'รับคะแนน 1x', 'PERCENTAGE', 0, 2, 'star', '#95A5A6');

-- Silver Tier (Tier 2) - 5% accommodation discount
INSERT INTO [dbo].[Loyalty_Tier_Benefits]
([Tier_ID], [BenefitType], [BenefitName], [BenefitNameEN], [Description], [DiscountType], [DiscountValue], [MaxDiscountAmount], [DisplayOrder], [Icon], [ColorCode])
VALUES
(2, 'ACCOMMODATION_DISCOUNT', N'ส่วนลดที่พัก 5%', '5% Accommodation Discount', N'ส่วนลดค่าที่พักสูงสุด 500 บาท', 'PERCENTAGE', 5.00, 500, 1, 'home', '#BDC3C7'),
(2, 'LATE_CHECKOUT', N'เช็คเอาท์สายฟรี', 'Late Checkout', N'เช็คเอาท์สาย 1 ชั่วโมงฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, 2, 'clock', '#BDC3C7'),
(2, 'POINTS_MULTIPLIER', N'คะแนนสะสม 1.25x', '1.25x Points', N'รับคะแนนมากขึ้น 25%', 'PERCENTAGE', 25, NULL, 3, 'star', '#BDC3C7');

-- Gold Tier (Tier 3) - 10% accommodation discount + more perks
INSERT INTO [dbo].[Loyalty_Tier_Benefits]
([Tier_ID], [BenefitType], [BenefitName], [BenefitNameEN], [Description], [DiscountType], [DiscountValue], [MaxDiscountAmount], [TimeValue], [DisplayOrder], [Icon], [ColorCode])
VALUES
(3, 'ACCOMMODATION_DISCOUNT', N'ส่วนลดที่พัก 10%', '10% Accommodation Discount', N'ส่วนลดค่าที่พักสูงสุด 1,000 บาท', 'PERCENTAGE', 10.00, 1000, NULL, 1, 'home', '#F39C12'),
(3, 'LATE_CHECKOUT', N'เช็คเอาท์สายฟรี 2 ชม.', '2-Hour Late Checkout', N'เช็คเอาท์สาย 2 ชั่วโมงฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, 2, 2, 'clock', '#F39C12'),
(3, 'EARLY_CHECKIN', N'เช็คอินเร็วฟรี', 'Early Check-in', N'เช็คอินเร็ว 2 ชั่วโมงฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, 2, 3, 'calendar-check', '#F39C12'),
(3, 'WELCOME_DRINK', N'เครื่องดื่มต้อนรับ', 'Welcome Drink', N'เครื่องดื่มต้อนรับฟรี 1 แก้ว', 'FREE', 0, NULL, NULL, 4, 'coffee', '#F39C12'),
(3, 'POINTS_MULTIPLIER', N'คะแนนสะสม 1.5x', '1.5x Points', N'รับคะแนนมากขึ้น 50%', 'PERCENTAGE', 50, NULL, NULL, 5, 'star', '#F39C12');

-- Platinum Tier (Tier 4) - 15% accommodation discount + premium perks
INSERT INTO [dbo].[Loyalty_Tier_Benefits]
([Tier_ID], [BenefitType], [BenefitName], [BenefitNameEN], [Description], [DiscountType], [DiscountValue], [MaxDiscountAmount], [TimeValue], [QuantityValue], [DisplayOrder], [Icon], [ColorCode])
VALUES
(4, 'ACCOMMODATION_DISCOUNT', N'ส่วนลดที่พัก 15%', '15% Accommodation Discount', N'ส่วนลดค่าที่พักสูงสุด 2,000 บาท', 'PERCENTAGE', 15.00, 2000, NULL, NULL, 1, 'home', '#9B59B6'),
(4, 'ROOM_UPGRADE', N'อัพเกรดห้องฟรี', 'Complimentary Room Upgrade', N'อัพเกรดห้องพักฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, NULL, 1, 2, 'arrow-up', '#9B59B6'),
(4, 'LATE_CHECKOUT', N'เช็คเอาท์สายฟรี 3 ชม.', '3-Hour Late Checkout', N'เช็คเอาท์สาย 3 ชั่วโมงฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, 3, NULL, 3, 'clock', '#9B59B6'),
(4, 'EARLY_CHECKIN', N'เช็คอินเร็วฟรี 3 ชม.', '3-Hour Early Check-in', N'เช็คอินเร็ว 3 ชั่วโมงฟรี (ขึ้นอยู่กับห้องว่าง)', 'FREE', 0, NULL, 3, NULL, 4, 'calendar-check', '#9B59B6'),
(4, 'FREE_BREAKFAST', N'อาหารเช้าฟรี', 'Complimentary Breakfast', N'อาหารเช้าฟรี 2 ท่าน', 'FREE', 0, NULL, NULL, 2, 5, 'utensils', '#9B59B6'),
(4, 'WELCOME_AMENITY', N'ของขวัญต้อนรับ', 'Welcome Amenity', N'ของขวัญพิเศษต้อนรับ', 'FREE', 0, NULL, NULL, NULL, 6, 'gift', '#9B59B6'),
(4, 'POINTS_MULTIPLIER', N'คะแนนสะสม 2x', '2x Points', N'รับคะแนนเพิ่มเป็น 2 เท่า', 'PERCENTAGE', 100, NULL, NULL, NULL, 7, 'star', '#9B59B6'),
(4, 'PRODUCT_DISCOUNT_SPA', N'ส่วนลดสปา 20%', '20% Spa Discount', N'ส่วนลดบริการสปา 20%', 'PERCENTAGE', 20.00, 1000, NULL, NULL, 8, 'spa', '#9B59B6'),
(4, 'PRODUCT_DISCOUNT_F&B', N'ส่วนลดอาหาร 10%', '10% F&B Discount', N'ส่วนลดอาหารและเครื่องดื่ม 10%', 'PERCENTAGE', 10.00, 500, NULL, NULL, 9, 'utensils', '#9B59B6');

-- VIP Tier (Tier 5) - 20% accommodation discount + exclusive perks
INSERT INTO [dbo].[Loyalty_Tier_Benefits]
([Tier_ID], [BenefitType], [BenefitName], [BenefitNameEN], [Description], [DiscountType], [DiscountValue], [MaxDiscountAmount], [TimeValue], [QuantityValue], [DisplayOrder], [Icon], [ColorCode])
VALUES
(5, 'ACCOMMODATION_DISCOUNT', N'ส่วนลดที่พัก 20%', '20% Accommodation Discount', N'ส่วนลดค่าที่พักสูงสุด 5,000 บาท', 'PERCENTAGE', 20.00, 5000, NULL, NULL, 1, 'home', '#E74C3C'),
(5, 'ROOM_UPGRADE', N'อัพเกรดห้องสูงสุด', 'Premium Room Upgrade', N'อัพเกรดไปห้องพักระดับสูงสุด (รับประกันห้องว่าง)', 'FREE', 0, NULL, NULL, 1, 2, 'crown', '#E74C3C'),
(5, 'LATE_CHECKOUT', N'เช็คเอาท์สายฟรี 4 ชม.', '4-Hour Late Checkout', N'เช็คเอาท์สาย 4 ชั่วโมงฟรี (รับประกัน)', 'FREE', 0, NULL, 4, NULL, 3, 'clock', '#E74C3C'),
(5, 'EARLY_CHECKIN', N'เช็คอินเร็วฟรี 4 ชม.', '4-Hour Early Check-in', N'เช็คอินเร็ว 4 ชั่วโมงฟรี (รับประกัน)', 'FREE', 0, NULL, 4, NULL, 4, 'calendar-check', '#E74C3C'),
(5, 'FREE_BREAKFAST', N'อาหารเช้าฟรี VIP', 'VIP Breakfast', N'อาหารเช้าฟรี 2 ท่าน + เมนูพิเศษ', 'FREE', 0, NULL, NULL, 2, 5, 'utensils', '#E74C3C'),
(5, 'WELCOME_AMENITY', N'ของขวัญต้อนรับพิเศษ', 'Premium Welcome Amenity', N'ของขวัญพิเศษระดับ VIP', 'FREE', 0, NULL, NULL, NULL, 6, 'gift', '#E74C3C'),
(5, 'CONCIERGE_SERVICE', N'บริการคอนเซียร์จ', 'Dedicated Concierge', N'บริการคอนเซียร์จส่วนตัว 24/7', 'FREE', 0, NULL, NULL, NULL, 7, 'concierge-bell', '#E74C3C'),
(5, 'PRIORITY_BOOKING', N'จองห้องล่วงหน้าพิเศษ', 'Priority Booking', N'จองห้องล่วงหน้าได้ก่อนใคร + รับประกันห้องว่าง', 'FREE', 0, NULL, NULL, NULL, 8, 'calendar-plus', '#E74C3C'),
(5, 'POINTS_MULTIPLIER', N'คะแนนสะสม 3x', '3x Points', N'รับคะแนนเพิ่มเป็น 3 เท่า', 'PERCENTAGE', 200, NULL, NULL, NULL, 9, 'star', '#E74C3C'),
(5, 'PRODUCT_DISCOUNT_SPA', N'ส่วนลดสปา 30%', '30% Spa Discount', N'ส่วนลดบริการสปา 30%', 'PERCENTAGE', 30.00, 2000, NULL, NULL, 10, 'spa', '#E74C3C'),
(5, 'PRODUCT_DISCOUNT_F&B', N'ส่วนลดอาหาร 20%', '20% F&B Discount', N'ส่วนลดอาหารและเครื่องดื่ม 20%', 'PERCENTAGE', 20.00, 1000, NULL, NULL, 11, 'utensils', '#E74C3C'),
(5, 'FREE_PARKING', N'ที่จอดรถฟรี', 'Complimentary Parking', N'ที่จอดรถฟรีตลอดการเข้าพัก', 'FREE', 0, NULL, NULL, NULL, 12, 'parking', '#E74C3C');

PRINT 'Default tier benefits populated successfully!';
GO

-- ============================================================================
-- 4. UPDATE LOYALTY_TIERS TABLE (Add columns if not exist)
-- ============================================================================

-- Add accommodation discount column to Loyalty_Tiers for backward compatibility
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Tiers]') AND name = 'AccommodationDiscountPercent')
BEGIN
    ALTER TABLE [dbo].[Loyalty_Tiers]
    ADD [AccommodationDiscountPercent] DECIMAL(5,2) DEFAULT 0;

    -- Update with default values
    UPDATE [dbo].[Loyalty_Tiers] SET [AccommodationDiscountPercent] = 0 WHERE [ID] = 1; -- Member
    UPDATE [dbo].[Loyalty_Tiers] SET [AccommodationDiscountPercent] = 5 WHERE [ID] = 2; -- Silver
    UPDATE [dbo].[Loyalty_Tiers] SET [AccommodationDiscountPercent] = 10 WHERE [ID] = 3; -- Gold
    UPDATE [dbo].[Loyalty_Tiers] SET [AccommodationDiscountPercent] = 15 WHERE [ID] = 4; -- Platinum
    UPDATE [dbo].[Loyalty_Tiers] SET [AccommodationDiscountPercent] = 20 WHERE [ID] = 5; -- VIP

    PRINT 'AccommodationDiscountPercent column added to Loyalty_Tiers';
END
GO

PRINT 'Tier Benefits System schema created successfully!';
GO
