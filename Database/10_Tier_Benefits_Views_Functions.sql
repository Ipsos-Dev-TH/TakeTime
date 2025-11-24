-- ============================================================================
-- Tier Benefits Views and Functions
-- Version: 1.0
-- Date: 2025-01-24
-- Description: Views and functions for tier benefits calculation
-- ============================================================================

USE TakeTime;
GO

-- ============================================================================
-- VIEWS
-- ============================================================================

-- View: All Benefits by Tier
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Tier_Benefits_Summary')
    DROP VIEW [dbo].[vw_Tier_Benefits_Summary];
GO

CREATE VIEW [dbo].[vw_Tier_Benefits_Summary]
AS
SELECT
    LT.[ID] AS TierID,
    LT.[TierName],
    LT.[TierNameEN],
    LT.[TierColor],
    LT.[MinPoints],
    LT.[PointsMultiplier],
    LT.[DiscountPercent] AS BaseDiscountPercent,
    LT.[AccommodationDiscountPercent],

    -- Count benefits by type
    COUNT(CASE WHEN LTB.[BenefitType] = 'ACCOMMODATION_DISCOUNT' THEN 1 END) AS AccomDiscountBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] = 'ROOM_UPGRADE' THEN 1 END) AS RoomUpgradeBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] = 'LATE_CHECKOUT' THEN 1 END) AS LateCheckoutBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] = 'EARLY_CHECKIN' THEN 1 END) AS EarlyCheckinBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] = 'FREE_BREAKFAST' THEN 1 END) AS FreeBreakfastBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] LIKE '%DISCOUNT%' THEN 1 END) AS TotalDiscountBenefits,
    COUNT(CASE WHEN LTB.[BenefitType] LIKE 'FREE%' THEN 1 END) AS TotalFreeBenefits,

    -- Total benefits
    COUNT(LTB.[ID]) AS TotalBenefits

FROM [dbo].[Loyalty_Tiers] LT
LEFT JOIN [dbo].[Loyalty_Tier_Benefits] LTB ON LTB.[Tier_ID] = LT.[ID] AND LTB.[IsActive] = 1

WHERE LT.[IsActive] = 1

GROUP BY
    LT.[ID], LT.[TierName], LT.[TierNameEN], LT.[TierColor],
    LT.[MinPoints], LT.[PointsMultiplier], LT.[DiscountPercent],
    LT.[AccommodationDiscountPercent];
GO

-- View: Customer Benefits with Usage Stats
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Customer_Benefits_Available')
    DROP VIEW [dbo].[vw_Customer_Benefits_Available];
GO

CREATE VIEW [dbo].[vw_Customer_Benefits_Available]
AS
SELECT
    C.[MobilePhone],
    C.[Name],
    CL.[CurrentTier_ID],
    LT.[TierName],
    LT.[TierColor],
    CL.[LifetimePoints],

    -- Benefits
    LTB.[ID] AS BenefitID,
    LTB.[BenefitType],
    LTB.[BenefitName],
    LTB.[Description],
    LTB.[DiscountType],
    LTB.[DiscountValue],
    LTB.[MaxDiscountAmount],
    LTB.[TimeValue],
    LTB.[QuantityValue],
    LTB.[MaxUsagePerStay],
    LTB.[MaxUsagePerMonth],

    -- Usage stats (current month)
    ISNULL(
        (SELECT COUNT(*)
         FROM [dbo].[Loyalty_Benefit_Usage] LBU
         WHERE LBU.[Customer_MobilePhone] = C.[MobilePhone]
         AND LBU.[Benefit_ID] = LTB.[ID]
         AND LBU.[UsageMonth] = FORMAT(GETDATE(), 'yyyy-MM')),
        0
    ) AS UsageThisMonth,

    -- Check if benefit is available
    CASE
        WHEN LTB.[MaxUsagePerMonth] IS NOT NULL
        AND (SELECT COUNT(*)
             FROM [dbo].[Loyalty_Benefit_Usage] LBU
             WHERE LBU.[Customer_MobilePhone] = C.[MobilePhone]
             AND LBU.[Benefit_ID] = LTB.[ID]
             AND LBU.[UsageMonth] = FORMAT(GETDATE(), 'yyyy-MM')) >= LTB.[MaxUsagePerMonth]
        THEN 0
        ELSE 1
    END AS IsAvailable

FROM [dbo].[Customer] C
INNER JOIN [dbo].[Customer_Loyalty] CL ON CL.[Customer_MobilePhone] = C.[MobilePhone]
INNER JOIN [dbo].[Loyalty_Tiers] LT ON LT.[ID] = CL.[CurrentTier_ID]
INNER JOIN [dbo].[Loyalty_Tier_Benefits] LTB ON LTB.[Tier_ID] = LT.[ID]

WHERE C.[Status] = 1
AND LTB.[IsActive] = 1;
GO

-- View: Benefit Usage Statistics
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Benefit_Usage_Statistics')
    DROP VIEW [dbo].[vw_Benefit_Usage_Statistics];
GO

CREATE VIEW [dbo].[vw_Benefit_Usage_Statistics]
AS
SELECT
    LTB.[Tier_ID],
    LT.[TierName],
    LTB.[BenefitType],
    LTB.[BenefitName],

    -- Usage counts
    COUNT(LBU.[ID]) AS TotalUsageCount,
    COUNT(DISTINCT LBU.[Customer_MobilePhone]) AS UniqueCustomers,
    COUNT(DISTINCT LBU.[Reservation_ID]) AS UniqueReservations,

    -- Financial impact
    ISNULL(SUM(LBU.[DiscountAmount]), 0) AS TotalDiscountGiven,
    ISNULL(AVG(LBU.[DiscountAmount]), 0) AS AvgDiscountPerUse,

    -- Time-based stats
    MIN(LBU.[UsageDate]) AS FirstUsedDate,
    MAX(LBU.[UsageDate]) AS LastUsedDate,

    -- This month
    COUNT(CASE WHEN FORMAT(LBU.[UsageDate], 'yyyy-MM') = FORMAT(GETDATE(), 'yyyy-MM') THEN 1 END) AS UsageThisMonth,
    ISNULL(SUM(CASE WHEN FORMAT(LBU.[UsageDate], 'yyyy-MM') = FORMAT(GETDATE(), 'yyyy-MM') THEN LBU.[DiscountAmount] ELSE 0 END), 0) AS DiscountThisMonth

FROM [dbo].[Loyalty_Tier_Benefits] LTB
INNER JOIN [dbo].[Loyalty_Tiers] LT ON LT.[ID] = LTB.[Tier_ID]
LEFT JOIN [dbo].[Loyalty_Benefit_Usage] LBU ON LBU.[Benefit_ID] = LTB.[ID]

GROUP BY
    LTB.[Tier_ID], LT.[TierName], LTB.[BenefitType], LTB.[BenefitName];
GO

PRINT 'Tier Benefits Views created successfully!';
GO

-- ============================================================================
-- FUNCTIONS
-- ============================================================================

-- Function: Get Accommodation Discount for Customer
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_GetAccommodationDiscount]') AND type in (N'FN', N'IF', N'TF'))
    DROP FUNCTION [dbo].[fn_GetAccommodationDiscount];
GO

CREATE FUNCTION [dbo].[fn_GetAccommodationDiscount]
(
    @CustomerPhone VARCHAR(20),
    @AccommodationPrice DECIMAL(10,2)
)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @DiscountPercent DECIMAL(5,2);
    DECLARE @MaxDiscount DECIMAL(10,2);
    DECLARE @DiscountAmount DECIMAL(10,2);

    -- Get customer's tier discount
    SELECT
        @DiscountPercent = LT.[AccommodationDiscountPercent],
        @MaxDiscount = LTB.[MaxDiscountAmount]
    FROM [dbo].[Customer_Loyalty] CL
    INNER JOIN [dbo].[Loyalty_Tiers] LT ON LT.[ID] = CL.[CurrentTier_ID]
    LEFT JOIN [dbo].[Loyalty_Tier_Benefits] LTB
        ON LTB.[Tier_ID] = LT.[ID]
        AND LTB.[BenefitType] = 'ACCOMMODATION_DISCOUNT'
        AND LTB.[IsActive] = 1
    WHERE CL.[Customer_MobilePhone] = @CustomerPhone;

    -- Calculate discount
    SET @DiscountAmount = @AccommodationPrice * (@DiscountPercent / 100.0);

    -- Apply cap if exists
    IF @MaxDiscount IS NOT NULL AND @DiscountAmount > @MaxDiscount
        SET @DiscountAmount = @MaxDiscount;

    RETURN ISNULL(@DiscountAmount, 0);
END;
GO

-- Function: Get Product Category Discount for Customer
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_GetProductCategoryDiscount]') AND type in (N'FN', N'IF', N'TF'))
    DROP FUNCTION [dbo].[fn_GetProductCategoryDiscount];
GO

CREATE FUNCTION [dbo].[fn_GetProductCategoryDiscount]
(
    @CustomerPhone VARCHAR(20),
    @ProductCategoryID BIGINT,
    @ProductPrice DECIMAL(10,2)
)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @DiscountPercent DECIMAL(5,2);
    DECLARE @MaxDiscount DECIMAL(10,2);
    DECLARE @MinPurchase DECIMAL(10,2);
    DECLARE @DiscountAmount DECIMAL(10,2);

    -- Get customer's tier product discount
    SELECT
        @DiscountPercent = LPCD.[DiscountPercent],
        @MaxDiscount = LPCD.[MaxDiscountAmount],
        @MinPurchase = LPCD.[MinPurchaseAmount]
    FROM [dbo].[Customer_Loyalty] CL
    INNER JOIN [dbo].[Loyalty_Product_Category_Discounts] LPCD
        ON LPCD.[Tier_ID] = CL.[CurrentTier_ID]
        AND LPCD.[ProductCategory_ID] = @ProductCategoryID
        AND LPCD.[IsActive] = 1
        AND (LPCD.[ValidFrom] IS NULL OR LPCD.[ValidFrom] <= CAST(GETDATE() AS DATE))
        AND (LPCD.[ValidTo] IS NULL OR LPCD.[ValidTo] >= CAST(GETDATE() AS DATE))
    WHERE CL.[Customer_MobilePhone] = @CustomerPhone;

    -- Check minimum purchase
    IF @MinPurchase IS NOT NULL AND @ProductPrice < @MinPurchase
        RETURN 0;

    -- Calculate discount
    SET @DiscountAmount = @ProductPrice * (@DiscountPercent / 100.0);

    -- Apply cap if exists
    IF @MaxDiscount IS NOT NULL AND @DiscountAmount > @MaxDiscount
        SET @DiscountAmount = @MaxDiscount;

    RETURN ISNULL(@DiscountAmount, 0);
END;
GO

-- Function: Check if Customer Can Use Benefit
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_CanUseBenefit]') AND type in (N'FN', N'IF', N'TF'))
    DROP FUNCTION [dbo].[fn_CanUseBenefit];
GO

CREATE FUNCTION [dbo].[fn_CanUseBenefit]
(
    @CustomerPhone VARCHAR(20),
    @BenefitID INT,
    @ReservationID BIGINT = NULL
)
RETURNS BIT
AS
BEGIN
    DECLARE @MaxUsagePerStay INT;
    DECLARE @MaxUsagePerMonth INT;
    DECLARE @UsageCountThisStay INT;
    DECLARE @UsageCountThisMonth INT;
    DECLARE @CanUse BIT = 1;

    -- Get benefit limits
    SELECT
        @MaxUsagePerStay = [MaxUsagePerStay],
        @MaxUsagePerMonth = [MaxUsagePerMonth]
    FROM [dbo].[Loyalty_Tier_Benefits]
    WHERE [ID] = @BenefitID;

    -- Check usage this month
    IF @MaxUsagePerMonth IS NOT NULL
    BEGIN
        SELECT @UsageCountThisMonth = COUNT(*)
        FROM [dbo].[Loyalty_Benefit_Usage]
        WHERE [Customer_MobilePhone] = @CustomerPhone
        AND [Benefit_ID] = @BenefitID
        AND [UsageMonth] = FORMAT(GETDATE(), 'yyyy-MM');

        IF @UsageCountThisMonth >= @MaxUsagePerMonth
            SET @CanUse = 0;
    END

    -- Check usage this stay
    IF @MaxUsagePerStay IS NOT NULL AND @ReservationID IS NOT NULL
    BEGIN
        SELECT @UsageCountThisStay = COUNT(*)
        FROM [dbo].[Loyalty_Benefit_Usage]
        WHERE [Customer_MobilePhone] = @CustomerPhone
        AND [Benefit_ID] = @BenefitID
        AND [Reservation_ID] = @ReservationID;

        IF @UsageCountThisStay >= @MaxUsagePerStay
            SET @CanUse = 0;
    END

    RETURN @CanUse;
END;
GO

-- Function: Get All Available Benefits for Customer
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_GetCustomerAvailableBenefits]') AND type in (N'FN', N'IF', N'TF'))
    DROP FUNCTION [dbo].[fn_GetCustomerAvailableBenefits];
GO

CREATE FUNCTION [dbo].[fn_GetCustomerAvailableBenefits]
(
    @CustomerPhone VARCHAR(20)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        LTB.[ID] AS BenefitID,
        LTB.[BenefitType],
        LTB.[BenefitName],
        LTB.[BenefitNameEN],
        LTB.[Description],
        LTB.[DiscountType],
        LTB.[DiscountValue],
        LTB.[MaxDiscountAmount],
        LTB.[TimeValue],
        LTB.[QuantityValue],
        LTB.[Icon],
        LTB.[ColorCode],
        [dbo].[fn_CanUseBenefit](@CustomerPhone, LTB.[ID], NULL) AS IsAvailable
    FROM [dbo].[Customer_Loyalty] CL
    INNER JOIN [dbo].[Loyalty_Tier_Benefits] LTB ON LTB.[Tier_ID] = CL.[CurrentTier_ID]
    WHERE CL.[Customer_MobilePhone] = @CustomerPhone
    AND LTB.[IsActive] = 1
);
GO

PRINT 'Tier Benefits Functions created successfully!';
GO

-- ============================================================================
-- STORED PROCEDURES
-- ============================================================================

-- SP: Apply Accommodation Discount
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_ApplyAccommodationDiscount]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_ApplyAccommodationDiscount];
GO

CREATE PROCEDURE [dbo].[sp_ApplyAccommodationDiscount]
    @ReservationID BIGINT,
    @CustomerPhone VARCHAR(20),
    @OriginalAmount DECIMAL(10,2),
    @AdminID SMALLINT = NULL,
    @DiscountAmount DECIMAL(10,2) OUTPUT,
    @FinalAmount DECIMAL(10,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Calculate discount
    SET @DiscountAmount = [dbo].[fn_GetAccommodationDiscount](@CustomerPhone, @OriginalAmount);
    SET @FinalAmount = @OriginalAmount - @DiscountAmount;

    -- Log benefit usage if discount given
    IF @DiscountAmount > 0
    BEGIN
        DECLARE @BenefitID INT;

        SELECT TOP 1 @BenefitID = LTB.[ID]
        FROM [dbo].[Customer_Loyalty] CL
        INNER JOIN [dbo].[Loyalty_Tier_Benefits] LTB
            ON LTB.[Tier_ID] = CL.[CurrentTier_ID]
            AND LTB.[BenefitType] = 'ACCOMMODATION_DISCOUNT'
            AND LTB.[IsActive] = 1
        WHERE CL.[Customer_MobilePhone] = @CustomerPhone;

        IF @BenefitID IS NOT NULL
        BEGIN
            INSERT INTO [dbo].[Loyalty_Benefit_Usage] (
                [Customer_MobilePhone], [Benefit_ID], [Reservation_ID],
                [BenefitType], [DiscountAmount], [OriginalAmount], [FinalAmount],
                [ProcessedBy_AdminID], [Notes]
            )
            VALUES (
                @CustomerPhone, @BenefitID, @ReservationID,
                'ACCOMMODATION_DISCOUNT', @DiscountAmount, @OriginalAmount, @FinalAmount,
                @AdminID, N'ส่วนลดที่พักจากระดับสมาชิก'
            );
        END
    END

    SELECT @DiscountAmount AS DiscountAmount, @FinalAmount AS FinalAmount;
END;
GO

-- SP: Apply Product Discount
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_ApplyProductDiscount]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_ApplyProductDiscount];
GO

CREATE PROCEDURE [dbo].[sp_ApplyProductDiscount]
    @CustomerPhone VARCHAR(20),
    @ProductCategoryID BIGINT,
    @OriginalAmount DECIMAL(10,2),
    @ReservationID BIGINT = NULL,
    @ReceiptID BIGINT = NULL,
    @AdminID SMALLINT = NULL,
    @DiscountAmount DECIMAL(10,2) OUTPUT,
    @FinalAmount DECIMAL(10,2) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Calculate discount
    SET @DiscountAmount = [dbo].[fn_GetProductCategoryDiscount](@CustomerPhone, @ProductCategoryID, @OriginalAmount);
    SET @FinalAmount = @OriginalAmount - @DiscountAmount;

    -- Log benefit usage if discount given
    IF @DiscountAmount > 0
    BEGIN
        DECLARE @TierID TINYINT;
        DECLARE @BenefitType VARCHAR(50) = 'PRODUCT_DISCOUNT';
        DECLARE @CategoryName NVARCHAR(200);

        SELECT @TierID = [CurrentTier_ID]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        SELECT @CategoryName = [CategoryName]
        FROM [dbo].[Product_Category]
        WHERE [ID] = @ProductCategoryID;

        INSERT INTO [dbo].[Loyalty_Benefit_Usage] (
            [Customer_MobilePhone], [Benefit_ID], [Reservation_ID], [Receipt_ID],
            [BenefitType], [DiscountAmount], [OriginalAmount], [FinalAmount],
            [ProcessedBy_AdminID], [Notes]
        )
        VALUES (
            @CustomerPhone, NULL, @ReservationID, @ReceiptID,
            @BenefitType, @DiscountAmount, @OriginalAmount, @FinalAmount,
            @AdminID, N'ส่วนลดสินค้าหมวด: ' + @CategoryName
        );
    END

    SELECT @DiscountAmount AS DiscountAmount, @FinalAmount AS FinalAmount;
END;
GO

PRINT 'Tier Benefits Stored Procedures created successfully!';
GO
