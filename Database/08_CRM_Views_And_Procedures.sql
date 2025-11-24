-- ============================================================================
-- CRM System Views and Stored Procedures
-- Version: 1.0
-- Date: 2025-01-24
-- Description: Analytics views and procedures for CRM system
-- ============================================================================

USE TakeTime;
GO

-- ============================================================================
-- VIEWS FOR CRM ANALYTICS
-- ============================================================================

-- Guest Profile 360° View (ข้อมูลลูกค้าครบวงจร)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Guest_Profile_360')
    DROP VIEW [dbo].[vw_Guest_Profile_360];
GO

CREATE VIEW [dbo].[vw_Guest_Profile_360]
AS
SELECT
    C.[MobilePhone],
    C.[Name],
    C.[Email],
    C.[TaxID],
    C.[IDNumber],
    C.[Address],
    CT.[TypeName] AS CustomerType,

    -- Loyalty Info
    CL.[CurrentTier_ID],
    LT.[TierName] AS LoyaltyTier,
    LT.[TierColor],
    CL.[TotalPoints],
    CL.[AvailablePoints],
    CL.[LifetimePoints],
    CL.[MemberSince],

    -- Booking Stats
    COUNT(DISTINCT R.[ID]) AS TotalReservations,
    COUNT(DISTINCT CASE WHEN R.[Status] = N'เช็คเอาท์แล้ว' THEN R.[ID] END) AS CompletedStays,
    MIN(R.[CheckinDate]) AS FirstStayDate,
    MAX(R.[CheckoutDate]) AS LastStayDate,
    DATEDIFF(DAY, MAX(R.[CheckoutDate]), GETDATE()) AS DaysSinceLastStay,

    -- Financial Stats
    ISNULL(SUM(R.[TotalPrice]), 0) AS TotalSpend,
    ISNULL(AVG(R.[TotalPrice]), 0) AS AvgSpendPerStay,

    -- Review Stats
    COUNT(DISTINCT GR.[ID]) AS TotalReviews,
    ISNULL(AVG(CAST(GR.[OverallRating] AS FLOAT)), 0) AS AvgRating,

    -- Communication Stats
    COUNT(DISTINCT CL_Log.[ID]) AS TotalCommunications,
    MAX(CL_Log.[SentDate]) AS LastCommunicationDate,

    -- Segmentation
    (SELECT STRING_AGG(GS.[SegmentName], ', ') WITHIN GROUP (ORDER BY GS.[SegmentName])
     FROM [dbo].[Customer_Segment_Assignments] CSA
     INNER JOIN [dbo].[Guest_Segments] GS ON GS.[ID] = CSA.[Segment_ID]
     WHERE CSA.[Customer_MobilePhone] = C.[MobilePhone] AND CSA.[IsActive] = 1) AS Segments,

    -- Flags
    CASE WHEN CL.[TotalPoints] >= 5000 THEN 1 ELSE 0 END AS IsVIP,
    CASE WHEN COUNT(DISTINCT R.[ID]) >= 3 THEN 1 ELSE 0 END AS IsRepeatGuest,
    CASE WHEN DATEDIFF(DAY, MAX(R.[CheckoutDate]), GETDATE()) > 365 THEN 1 ELSE 0 END AS IsInactive,

    C.[Status] AS IsActive,
    C.[CreatedDate]

FROM [dbo].[Customer] C
LEFT JOIN [dbo].[Customer_Type] CT ON CT.[ID] = C.[Customer_Type_ID]
LEFT JOIN [dbo].[Customer_Loyalty] CL ON CL.[Customer_MobilePhone] = C.[MobilePhone]
LEFT JOIN [dbo].[Loyalty_Tiers] LT ON LT.[ID] = CL.[CurrentTier_ID]
LEFT JOIN [dbo].[Reservation] R ON R.[Customer_MobilePhone] = C.[MobilePhone]
LEFT JOIN [dbo].[Guest_Reviews] GR ON GR.[Customer_MobilePhone] = C.[MobilePhone]
LEFT JOIN [dbo].[Communication_Log] CL_Log ON CL_Log.[Customer_MobilePhone] = C.[MobilePhone]

GROUP BY
    C.[MobilePhone], C.[Name], C.[Email], C.[TaxID], C.[IDNumber], C.[Address],
    CT.[TypeName], CL.[CurrentTier_ID], LT.[TierName], LT.[TierColor],
    CL.[TotalPoints], CL.[AvailablePoints], CL.[LifetimePoints], CL.[MemberSince],
    C.[Status], C.[CreatedDate];
GO

-- Customer Lifetime Value (CLV) View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Customer_Lifetime_Value')
    DROP VIEW [dbo].[vw_Customer_Lifetime_Value];
GO

CREATE VIEW [dbo].[vw_Customer_Lifetime_Value]
AS
SELECT
    C.[MobilePhone],
    C.[Name],

    -- Booking metrics
    COUNT(DISTINCT R.[ID]) AS TotalBookings,
    COUNT(DISTINCT CASE WHEN R.[Status] = N'เช็คเอาท์แล้ว' THEN R.[ID] END) AS CompletedStays,
    COUNT(DISTINCT CASE WHEN R.[Status] = N'ยกเลิก' THEN R.[ID] END) AS CancelledBookings,

    -- Financial metrics
    ISNULL(SUM(R.[TotalPrice]), 0) AS LifetimeValue,
    ISNULL(AVG(R.[TotalPrice]), 0) AS AverageOrderValue,
    ISNULL(SUM(PH.[PaymentAmount]), 0) AS TotalPaid,

    -- Frequency metrics
    MIN(R.[BookDate]) AS FirstBookingDate,
    MAX(R.[BookDate]) AS LastBookingDate,
    DATEDIFF(MONTH, MIN(R.[BookDate]), MAX(R.[BookDate])) AS CustomerAge_Months,

    CASE
        WHEN DATEDIFF(MONTH, MIN(R.[BookDate]), MAX(R.[BookDate])) > 0
        THEN CAST(COUNT(DISTINCT R.[ID]) AS FLOAT) / DATEDIFF(MONTH, MIN(R.[BookDate]), MAX(R.[BookDate]))
        ELSE 0
    END AS AvgBookingsPerMonth,

    DATEDIFF(DAY, MAX(R.[CheckoutDate]), GETDATE()) AS DaysSinceLastStay,

    -- Loyalty
    CASE
        WHEN COUNT(DISTINCT R.[ID]) >= 10 THEN 'High Value'
        WHEN COUNT(DISTINCT R.[ID]) >= 5 THEN 'Medium Value'
        WHEN COUNT(DISTINCT R.[ID]) >= 2 THEN 'Repeat Customer'
        ELSE 'New Customer'
    END AS CustomerSegment,

    -- Churn risk
    CASE
        WHEN DATEDIFF(DAY, MAX(R.[CheckoutDate]), GETDATE()) > 365 THEN 'High Risk'
        WHEN DATEDIFF(DAY, MAX(R.[CheckoutDate]), GETDATE()) > 180 THEN 'Medium Risk'
        ELSE 'Low Risk'
    END AS ChurnRisk

FROM [dbo].[Customer] C
LEFT JOIN [dbo].[Reservation] R ON R.[Customer_MobilePhone] = C.[MobilePhone]
LEFT JOIN [dbo].[Payment_History] PH ON PH.[Reservation_ID] = R.[ID] AND PH.[Status] = 'COMPLETED'

WHERE C.[Status] = 1

GROUP BY C.[MobilePhone], C.[Name];
GO

-- Guest Preferences Summary View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Guest_Preferences_Summary')
    DROP VIEW [dbo].[vw_Guest_Preferences_Summary];
GO

CREATE VIEW [dbo].[vw_Guest_Preferences_Summary]
AS
SELECT
    GP.[Customer_MobilePhone],
    C.[Name] AS CustomerName,

    -- Room preferences
    MAX(CASE WHEN GP.[PreferenceType] = 'ROOM' AND GP.[PreferenceKey] = 'favorite_room_type'
        THEN GP.[PreferenceValue] END) AS FavoriteRoomType,
    MAX(CASE WHEN GP.[PreferenceType] = 'ROOM' AND GP.[PreferenceKey] = 'floor_preference'
        THEN GP.[PreferenceValue] END) AS FloorPreference,
    MAX(CASE WHEN GP.[PreferenceType] = 'ROOM' AND GP.[PreferenceKey] = 'view_preference'
        THEN GP.[PreferenceValue] END) AS ViewPreference,

    -- Bed preferences
    MAX(CASE WHEN GP.[PreferenceType] = 'BED' AND GP.[PreferenceKey] = 'bed_type'
        THEN GP.[PreferenceValue] END) AS BedType,
    MAX(CASE WHEN GP.[PreferenceType] = 'PILLOW' AND GP.[PreferenceKey] = 'pillow_type'
        THEN GP.[PreferenceValue] END) AS PillowType,

    -- Food preferences and allergies
    STRING_AGG(CASE WHEN GP.[PreferenceType] = 'FOOD'
        THEN GP.[PreferenceValue] END, '; ') AS FoodPreferences,
    STRING_AGG(CASE WHEN GP.[PreferenceType] = 'ALLERGY'
        THEN GP.[PreferenceValue] END, '; ') AS Allergies,

    -- Activities
    STRING_AGG(CASE WHEN GP.[PreferenceType] = 'ACTIVITY'
        THEN GP.[PreferenceValue] END, '; ') AS FavoriteActivities,

    -- Special requests
    STRING_AGG(CASE WHEN GP.[PreferenceType] = 'SPECIAL_REQUEST'
        THEN GP.[PreferenceValue] END, '; ') AS SpecialRequests,

    -- High priority preferences
    STRING_AGG(CASE WHEN GP.[Priority] >= 3
        THEN GP.[PreferenceKey] + ': ' + GP.[PreferenceValue] END, '; ') AS HighPriorityPreferences

FROM [dbo].[Guest_Preferences] GP
INNER JOIN [dbo].[Customer] C ON C.[MobilePhone] = GP.[Customer_MobilePhone]
WHERE GP.[IsActive] = 1

GROUP BY GP.[Customer_MobilePhone], C.[Name];
GO

-- Loyalty Program Performance View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Loyalty_Program_Performance')
    DROP VIEW [dbo].[vw_Loyalty_Program_Performance];
GO

CREATE VIEW [dbo].[vw_Loyalty_Program_Performance]
AS
SELECT
    LT.[ID] AS TierID,
    LT.[TierName],
    LT.[MinPoints],
    LT.[PointsMultiplier],
    LT.[DiscountPercent],

    -- Member counts
    COUNT(DISTINCT CL.[Customer_MobilePhone]) AS TotalMembers,

    -- Points metrics
    SUM(CL.[TotalPoints]) AS TotalPointsInTier,
    AVG(CL.[TotalPoints]) AS AvgPointsPerMember,
    SUM(CL.[AvailablePoints]) AS TotalAvailablePoints,

    -- Financial metrics
    SUM(CL.[TotalSpend]) AS TotalSpendByTier,
    AVG(CL.[TotalSpend]) AS AvgSpendPerMember,

    -- Activity metrics
    SUM(CL.[TotalReservations]) AS TotalReservations,
    AVG(CL.[TotalReservations]) AS AvgReservationsPerMember,

    -- Redemption metrics
    (SELECT COUNT(*) FROM [dbo].[Loyalty_Transactions] LT_Trans
     INNER JOIN [dbo].[Customer_Loyalty] CL_Inner ON CL_Inner.[Customer_MobilePhone] = LT_Trans.[Customer_MobilePhone]
     WHERE CL_Inner.[CurrentTier_ID] = LT.[ID] AND LT_Trans.[TransactionType] = 'REDEEM') AS TotalRedemptions,

    (SELECT SUM(ABS([Points])) FROM [dbo].[Loyalty_Transactions] LT_Trans
     INNER JOIN [dbo].[Customer_Loyalty] CL_Inner ON CL_Inner.[Customer_MobilePhone] = LT_Trans.[Customer_MobilePhone]
     WHERE CL_Inner.[CurrentTier_ID] = LT.[ID] AND LT_Trans.[TransactionType] = 'REDEEM') AS TotalPointsRedeemed

FROM [dbo].[Loyalty_Tiers] LT
LEFT JOIN [dbo].[Customer_Loyalty] CL ON CL.[CurrentTier_ID] = LT.[ID]

WHERE LT.[IsActive] = 1

GROUP BY LT.[ID], LT.[TierName], LT.[MinPoints], LT.[PointsMultiplier], LT.[DiscountPercent];
GO

-- Review Analytics View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Review_Analytics')
    DROP VIEW [dbo].[vw_Review_Analytics];
GO

CREATE VIEW [dbo].[vw_Review_Analytics]
AS
SELECT
    -- Overall metrics
    COUNT(*) AS TotalReviews,
    COUNT(CASE WHEN [Status] = 'APPROVED' THEN 1 END) AS ApprovedReviews,
    COUNT(CASE WHEN [Status] = 'PENDING' THEN 1 END) AS PendingReviews,

    -- Rating averages
    AVG(CAST([OverallRating] AS FLOAT)) AS AvgOverallRating,
    AVG(CAST([CleanlinessRating] AS FLOAT)) AS AvgCleanlinessRating,
    AVG(CAST([ServiceRating] AS FLOAT)) AS AvgServiceRating,
    AVG(CAST([FacilitiesRating] AS FLOAT)) AS AvgFacilitiesRating,
    AVG(CAST([LocationRating] AS FLOAT)) AS AvgLocationRating,
    AVG(CAST([ValueForMoneyRating] AS FLOAT)) AS AvgValueForMoneyRating,

    -- Rating distribution
    SUM(CASE WHEN [OverallRating] = 5 THEN 1 ELSE 0 END) AS FiveStarCount,
    SUM(CASE WHEN [OverallRating] = 4 THEN 1 ELSE 0 END) AS FourStarCount,
    SUM(CASE WHEN [OverallRating] = 3 THEN 1 ELSE 0 END) AS ThreeStarCount,
    SUM(CASE WHEN [OverallRating] = 2 THEN 1 ELSE 0 END) AS TwoStarCount,
    SUM(CASE WHEN [OverallRating] = 1 THEN 1 ELSE 0 END) AS OneStarCount,

    -- Percentages
    CAST(SUM(CASE WHEN [OverallRating] >= 4 THEN 1 ELSE 0 END) AS FLOAT) / NULLIF(COUNT(*), 0) * 100 AS PercentPositive,

    -- Recommendations
    SUM(CASE WHEN [WouldRecommend] = 1 THEN 1 ELSE 0 END) AS WouldRecommendCount,
    SUM(CASE WHEN [WouldReturn] = 1 THEN 1 ELSE 0 END) AS WouldReturnCount,

    -- Travel types
    SUM(CASE WHEN [TravelType] = 'COUPLE' THEN 1 ELSE 0 END) AS CoupleReviews,
    SUM(CASE WHEN [TravelType] = 'FAMILY' THEN 1 ELSE 0 END) AS FamilyReviews,
    SUM(CASE WHEN [TravelType] = 'SOLO' THEN 1 ELSE 0 END) AS SoloReviews,
    SUM(CASE WHEN [TravelType] = 'FRIENDS' THEN 1 ELSE 0 END) AS FriendsReviews,
    SUM(CASE WHEN [TravelType] = 'BUSINESS' THEN 1 ELSE 0 END) AS BusinessReviews,

    -- Response metrics
    COUNT(CASE WHEN [ResponseText] IS NOT NULL THEN 1 END) AS ReviewsWithResponse,
    AVG(DATEDIFF(HOUR, [SubmittedDate], [ResponseDate])) AS AvgResponseTimeHours

FROM [dbo].[Guest_Reviews]
WHERE [IsVerified] = 1;
GO

-- Upcoming Birthdays and Anniversaries View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Upcoming_Special_Occasions')
    DROP VIEW [dbo].[vw_Upcoming_Special_Occasions];
GO

CREATE VIEW [dbo].[vw_Upcoming_Special_Occasions]
AS
SELECT
    CSD.[ID],
    CSD.[Customer_MobilePhone],
    C.[Name] AS CustomerName,
    C.[Email],
    CSD.[OccasionType],
    CSD.[OccasionName],
    CSD.[OccasionDate],

    -- Calculate next occurrence
    CASE
        WHEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate]) >= CAST(GETDATE() AS DATE)
        THEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate])
        ELSE DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()) + 1, CSD.[OccasionDate])
    END AS NextOccurrence,

    -- Days until next occurrence
    DATEDIFF(DAY, GETDATE(),
        CASE
            WHEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate]) >= CAST(GETDATE() AS DATE)
            THEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate])
            ELSE DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()) + 1, CSD.[OccasionDate])
        END
    ) AS DaysUntil,

    CSD.[SendReminder],
    CSD.[ReminderDaysBefore],
    CSD.[LastReminderSent],
    CSD.[SpecialNotes],

    -- Check if reminder should be sent
    CASE
        WHEN CSD.[SendReminder] = 1
        AND (CSD.[LastReminderSent] IS NULL OR CSD.[LastReminderSent] < DATEADD(YEAR, -1, GETDATE()))
        AND DATEDIFF(DAY, GETDATE(),
            CASE
                WHEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate]) >= CAST(GETDATE() AS DATE)
                THEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate])
                ELSE DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()) + 1, CSD.[OccasionDate])
            END
        ) <= CSD.[ReminderDaysBefore]
        AND DATEDIFF(DAY, GETDATE(),
            CASE
                WHEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate]) >= CAST(GETDATE() AS DATE)
                THEN DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()), CSD.[OccasionDate])
                ELSE DATEADD(YEAR, DATEDIFF(YEAR, CSD.[OccasionDate], GETDATE()) + 1, CSD.[OccasionDate])
            END
        ) >= 0
        THEN 1
        ELSE 0
    END AS ShouldSendReminder,

    -- Customer stats
    GP360.[TotalReservations],
    GP360.[LoyaltyTier],
    GP360.[IsVIP]

FROM [dbo].[Customer_Special_Dates] CSD
INNER JOIN [dbo].[Customer] C ON C.[MobilePhone] = CSD.[Customer_MobilePhone]
LEFT JOIN [dbo].[vw_Guest_Profile_360] GP360 ON GP360.[MobilePhone] = CSD.[Customer_MobilePhone]

WHERE C.[Status] = 1;
GO

PRINT 'CRM Views created successfully!';
GO

-- ============================================================================
-- STORED PROCEDURES FOR CRM OPERATIONS
-- ============================================================================

-- SP: Add/Update Guest Preference
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_UpsertGuestPreference]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_UpsertGuestPreference];
GO

CREATE PROCEDURE [dbo].[sp_UpsertGuestPreference]
    @CustomerPhone VARCHAR(20),
    @PreferenceType VARCHAR(50),
    @PreferenceKey NVARCHAR(100),
    @PreferenceValue NVARCHAR(MAX),
    @Notes NVARCHAR(500) = NULL,
    @Priority TINYINT = 1,
    @AdminID SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if preference exists
    IF EXISTS (SELECT 1 FROM [dbo].[Guest_Preferences]
               WHERE [Customer_MobilePhone] = @CustomerPhone
               AND [PreferenceType] = @PreferenceType
               AND [PreferenceKey] = @PreferenceKey)
    BEGIN
        -- Update existing
        UPDATE [dbo].[Guest_Preferences]
        SET [PreferenceValue] = @PreferenceValue,
            [Notes] = @Notes,
            [Priority] = @Priority,
            [LastUpdated] = GETDATE(),
            [UpdatedBy_AdminID] = @AdminID,
            [IsActive] = 1
        WHERE [Customer_MobilePhone] = @CustomerPhone
        AND [PreferenceType] = @PreferenceType
        AND [PreferenceKey] = @PreferenceKey;

        SELECT 'UPDATED' AS Result;
    END
    ELSE
    BEGIN
        -- Insert new
        INSERT INTO [dbo].[Guest_Preferences] (
            [Customer_MobilePhone], [PreferenceType], [PreferenceKey],
            [PreferenceValue], [Notes], [Priority],
            [CreatedBy_AdminID], [UpdatedBy_AdminID]
        )
        VALUES (
            @CustomerPhone, @PreferenceType, @PreferenceKey,
            @PreferenceValue, @Notes, @Priority,
            @AdminID, @AdminID
        );

        SELECT 'INSERTED' AS Result;
    END
END;
GO

-- SP: Earn Loyalty Points
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_EarnLoyaltyPoints]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_EarnLoyaltyPoints];
GO

CREATE PROCEDURE [dbo].[sp_EarnLoyaltyPoints]
    @CustomerPhone VARCHAR(20),
    @Points INT,
    @ReservationID BIGINT = NULL,
    @ReceiptID BIGINT = NULL,
    @Description NVARCHAR(500) = NULL,
    @ExpiryMonths INT = 12,
    @AdminID SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Ensure customer has loyalty record
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Customer_Loyalty] WHERE [Customer_MobilePhone] = @CustomerPhone)
        BEGIN
            INSERT INTO [dbo].[Customer_Loyalty] ([Customer_MobilePhone], [MemberSince])
            VALUES (@CustomerPhone, CAST(GETDATE() AS DATE));
        END

        -- Get current tier and multiplier
        DECLARE @CurrentTierID TINYINT;
        DECLARE @Multiplier DECIMAL(3,2);

        SELECT @CurrentTierID = [CurrentTier_ID]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        SELECT @Multiplier = [PointsMultiplier]
        FROM [dbo].[Loyalty_Tiers]
        WHERE [ID] = @CurrentTierID;

        -- Apply multiplier
        DECLARE @ActualPoints INT = CEILING(@Points * @Multiplier);

        -- Calculate expiry date
        DECLARE @ExpiryDate DATE = DATEADD(MONTH, @ExpiryMonths, GETDATE());

        -- Update loyalty balance
        UPDATE [dbo].[Customer_Loyalty]
        SET [TotalPoints] = [TotalPoints] + @ActualPoints,
            [AvailablePoints] = [AvailablePoints] + @ActualPoints,
            [LifetimePoints] = [LifetimePoints] + @ActualPoints,
            [LastEarnDate] = GETDATE(),
            [LastUpdated] = GETDATE()
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        DECLARE @NewBalance INT;
        SELECT @NewBalance = [AvailablePoints]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        -- Record transaction
        INSERT INTO [dbo].[Loyalty_Transactions] (
            [Customer_MobilePhone], [TransactionType], [Points], [BalanceAfter],
            [Reservation_ID], [Receipt_ID], [Description], [ExpiryDate],
            [ProcessedBy_AdminID]
        )
        VALUES (
            @CustomerPhone, 'EARN', @ActualPoints, @NewBalance,
            @ReservationID, @ReceiptID, @Description, @ExpiryDate,
            @AdminID
        );

        -- Check for tier upgrade
        EXEC [dbo].[sp_UpdateLoyaltyTier] @CustomerPhone;

        COMMIT TRANSACTION;

        SELECT 'SUCCESS' AS Result, @ActualPoints AS PointsEarned, @NewBalance AS NewBalance;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- SP: Redeem Loyalty Points
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_RedeemLoyaltyPoints]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_RedeemLoyaltyPoints];
GO

CREATE PROCEDURE [dbo].[sp_RedeemLoyaltyPoints]
    @CustomerPhone VARCHAR(20),
    @Points INT,
    @RewardID INT = NULL,
    @Description NVARCHAR(500) = NULL,
    @AdminID SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        -- Check available points
        DECLARE @AvailablePoints INT;
        SELECT @AvailablePoints = [AvailablePoints]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        IF @AvailablePoints < @Points
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 'INSUFFICIENT_POINTS' AS Result, @AvailablePoints AS AvailablePoints;
            RETURN;
        END

        -- Update balance
        UPDATE [dbo].[Customer_Loyalty]
        SET [TotalPoints] = [TotalPoints] - @Points,
            [AvailablePoints] = [AvailablePoints] - @Points,
            [LastRedemptionDate] = GETDATE(),
            [LastUpdated] = GETDATE()
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        DECLARE @NewBalance INT;
        SELECT @NewBalance = [AvailablePoints]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        -- Record transaction
        INSERT INTO [dbo].[Loyalty_Transactions] (
            [Customer_MobilePhone], [TransactionType], [Points], [BalanceAfter],
            [Reward_ID], [Description], [ProcessedBy_AdminID]
        )
        VALUES (
            @CustomerPhone, 'REDEEM', -@Points, @NewBalance,
            @RewardID, @Description, @AdminID
        );

        COMMIT TRANSACTION;

        SELECT 'SUCCESS' AS Result, @Points AS PointsRedeemed, @NewBalance AS NewBalance;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- SP: Update Loyalty Tier
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_UpdateLoyaltyTier]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_UpdateLoyaltyTier];
GO

CREATE PROCEDURE [dbo].[sp_UpdateLoyaltyTier]
    @CustomerPhone VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CurrentPoints INT;
    DECLARE @CurrentTierID TINYINT;
    DECLARE @NewTierID TINYINT;

    SELECT @CurrentPoints = [LifetimePoints], @CurrentTierID = [CurrentTier_ID]
    FROM [dbo].[Customer_Loyalty]
    WHERE [Customer_MobilePhone] = @CustomerPhone;

    -- Determine new tier
    SELECT TOP 1 @NewTierID = [ID]
    FROM [dbo].[Loyalty_Tiers]
    WHERE [MinPoints] <= @CurrentPoints
    AND [IsActive] = 1
    ORDER BY [MinPoints] DESC;

    -- Update if changed
    IF @NewTierID != @CurrentTierID OR @CurrentTierID IS NULL
    BEGIN
        UPDATE [dbo].[Customer_Loyalty]
        SET [CurrentTier_ID] = @NewTierID,
            [LastUpdated] = GETDATE()
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        SELECT 'TIER_UPGRADED' AS Result, @NewTierID AS NewTierID;
    END
    ELSE
    BEGIN
        SELECT 'NO_CHANGE' AS Result, @CurrentTierID AS CurrentTierID;
    END
END;
GO

-- SP: Submit Guest Review
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_SubmitGuestReview]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_SubmitGuestReview];
GO

CREATE PROCEDURE [dbo].[sp_SubmitGuestReview]
    @ReservationID BIGINT,
    @CustomerPhone VARCHAR(20),
    @OverallRating TINYINT,
    @CleanlinessRating TINYINT = NULL,
    @ServiceRating TINYINT = NULL,
    @FacilitiesRating TINYINT = NULL,
    @LocationRating TINYINT = NULL,
    @ValueForMoneyRating TINYINT = NULL,
    @ReviewTitle NVARCHAR(200) = NULL,
    @ReviewText NVARCHAR(MAX) = NULL,
    @Pros NVARCHAR(1000) = NULL,
    @Cons NVARCHAR(1000) = NULL,
    @WouldRecommend BIT = NULL,
    @WouldReturn BIT = NULL,
    @TravelType VARCHAR(30) = NULL,
    @Source VARCHAR(30) = 'DIRECT'
AS
BEGIN
    SET NOCOUNT ON;

    -- Check if review already exists
    IF EXISTS (SELECT 1 FROM [dbo].[Guest_Reviews] WHERE [Reservation_ID] = @ReservationID)
    BEGIN
        SELECT 'ALREADY_REVIEWED' AS Result;
        RETURN;
    END

    -- Insert review
    INSERT INTO [dbo].[Guest_Reviews] (
        [Reservation_ID], [Customer_MobilePhone],
        [OverallRating], [CleanlinessRating], [ServiceRating],
        [FacilitiesRating], [LocationRating], [ValueForMoneyRating],
        [ReviewTitle], [ReviewText], [Pros], [Cons],
        [WouldRecommend], [WouldReturn], [TravelType],
        [Status], [Source]
    )
    VALUES (
        @ReservationID, @CustomerPhone,
        @OverallRating, @CleanlinessRating, @ServiceRating,
        @FacilitiesRating, @LocationRating, @ValueForMoneyRating,
        @ReviewTitle, @ReviewText, @Pros, @Cons,
        @WouldRecommend, @WouldReturn, @TravelType,
        'PENDING', @Source
    );

    DECLARE @ReviewID BIGINT = SCOPE_IDENTITY();

    -- Award bonus points for review (50 points)
    EXEC [dbo].[sp_EarnLoyaltyPoints]
        @CustomerPhone = @CustomerPhone,
        @Points = 50,
        @ReservationID = @ReservationID,
        @Description = N'คะแนนสำหรับการรีวิว';

    SELECT 'SUCCESS' AS Result, @ReviewID AS ReviewID;
END;
GO

-- SP: Log Communication
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_LogCommunication]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_LogCommunication];
GO

CREATE PROCEDURE [dbo].[sp_LogCommunication]
    @CustomerPhone VARCHAR(20),
    @CommunicationType VARCHAR(30),
    @Direction VARCHAR(10),
    @Subject NVARCHAR(200) = NULL,
    @MessageContent NVARCHAR(MAX) = NULL,
    @Purpose VARCHAR(50) = NULL,
    @ReservationID BIGINT = NULL,
    @AdminID SMALLINT = NULL,
    @Status VARCHAR(20) = 'SENT'
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[Communication_Log] (
        [Customer_MobilePhone], [CommunicationType], [Direction],
        [Subject], [MessageContent], [Purpose], [Reservation_ID],
        [SentBy_AdminID], [Status]
    )
    VALUES (
        @CustomerPhone, @CommunicationType, @Direction,
        @Subject, @MessageContent, @Purpose, @ReservationID,
        @AdminID, @Status
    );

    SELECT 'SUCCESS' AS Result, SCOPE_IDENTITY() AS CommunicationID;
END;
GO

PRINT 'CRM Stored Procedures created successfully!';
GO
