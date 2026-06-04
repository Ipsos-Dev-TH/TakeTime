-- ============================================================
-- PHASE13 Migration 02: Membership / Loyalty System Restructure
-- ============================================================
-- Introduces a DUAL-POOL points model:
--   * YearlyPoints   = tier-qualifying points earned within the
--                      current calendar year. Determines Level/Tier.
--                      Resets automatically each year.
--   * AvailablePoints = redeemable balance (spend on rewards).
--                      Redeeming does NOT lower the tier.
-- The SAME earned points feed both pools.
-- Also adds reward redemption tracking + once-per-year review rule.
-- ============================================================

USE TakeTime;
GO

-- ------------------------------------------------------------
-- 1) Extend Customer_Loyalty with yearly tier-points tracking
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customer_Loyalty') AND name = 'YearlyPoints')
BEGIN
    ALTER TABLE [dbo].[Customer_Loyalty] ADD [YearlyPoints] INT NOT NULL DEFAULT 0;
    PRINT 'Added Customer_Loyalty.YearlyPoints';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customer_Loyalty') AND name = 'PointsYear')
BEGIN
    ALTER TABLE [dbo].[Customer_Loyalty] ADD [PointsYear] INT NULL;
    PRINT 'Added Customer_Loyalty.PointsYear';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Customer_Loyalty') AND name = 'LastReviewDate')
BEGIN
    ALTER TABLE [dbo].[Customer_Loyalty] ADD [LastReviewDate] DATETIME NULL;
    PRINT 'Added Customer_Loyalty.LastReviewDate';
END
GO

-- Backfill YearlyPoints from this calendar year's EARN transactions
UPDATE CL
SET CL.[YearlyPoints] = ISNULL(T.YearPts, 0),
    CL.[PointsYear]    = YEAR(GETDATE())
FROM [dbo].[Customer_Loyalty] CL
LEFT JOIN (
    SELECT [Customer_MobilePhone], SUM([Points]) AS YearPts
    FROM [dbo].[Loyalty_Transactions]
    WHERE [TransactionType] IN ('EARN', 'BONUS', 'ADJUST')
      AND [Points] > 0
      AND YEAR([TransactionDate]) = YEAR(GETDATE())
    GROUP BY [Customer_MobilePhone]
) T ON T.[Customer_MobilePhone] = CL.[Customer_MobilePhone];
GO

-- ------------------------------------------------------------
-- 2) Reward redemption tracking table
-- ------------------------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Loyalty_Redemptions]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Loyalty_Redemptions] (
        [ID] BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Customer_MobilePhone] NVARCHAR(30) NOT NULL,
        [Reward_ID] INT NOT NULL,
        [RewardName] NVARCHAR(200),
        [PointsCost] INT NOT NULL,
        [RedemptionCode] VARCHAR(20) NOT NULL,
        [Status] VARCHAR(20) DEFAULT 'PENDING', -- PENDING, FULFILLED, CANCELLED, EXPIRED
        [RedeemedDate] DATETIME DEFAULT GETDATE(),
        [ExpiryDate] DATE,
        [FulfilledDate] DATETIME,
        [FulfilledBy_AdminID] SMALLINT,
        [Notes] NVARCHAR(500),

        CONSTRAINT [FK_LoyaltyRedemptions_Customer] FOREIGN KEY ([Customer_MobilePhone])
            REFERENCES [dbo].[Customer]([MobilePhone]) ON UPDATE CASCADE,
        CONSTRAINT [FK_LoyaltyRedemptions_Reward] FOREIGN KEY ([Reward_ID])
            REFERENCES [dbo].[Loyalty_Rewards]([ID]),
        CONSTRAINT [FK_LoyaltyRedemptions_Admin] FOREIGN KEY ([FulfilledBy_AdminID])
            REFERENCES [dbo].[Admin]([ID])
    );

    CREATE INDEX [IDX_LoyaltyRedemptions_Customer] ON [dbo].[Loyalty_Redemptions]([Customer_MobilePhone]);
    CREATE INDEX [IDX_LoyaltyRedemptions_Status] ON [dbo].[Loyalty_Redemptions]([Status]);
    CREATE UNIQUE INDEX [UX_LoyaltyRedemptions_Code] ON [dbo].[Loyalty_Redemptions]([RedemptionCode]);

    PRINT 'Created Loyalty_Redemptions table';
END
GO

-- ------------------------------------------------------------
-- 3) Rewrite sp_EarnLoyaltyPoints with yearly pool handling
-- ------------------------------------------------------------
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
            INSERT INTO [dbo].[Customer_Loyalty] ([Customer_MobilePhone], [MemberSince], [PointsYear])
            VALUES (@CustomerPhone, CAST(GETDATE() AS DATE), YEAR(GETDATE()));
        END

        -- Reset yearly pool if we have rolled into a new year
        UPDATE [dbo].[Customer_Loyalty]
        SET [YearlyPoints] = 0,
            [PointsYear] = YEAR(GETDATE())
        WHERE [Customer_MobilePhone] = @CustomerPhone
          AND (ISNULL([PointsYear], 0) <> YEAR(GETDATE()));

        -- Get current tier multiplier
        DECLARE @CurrentTierID TINYINT;
        DECLARE @Multiplier DECIMAL(3,2);

        SELECT @CurrentTierID = [CurrentTier_ID]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        SELECT @Multiplier = [PointsMultiplier]
        FROM [dbo].[Loyalty_Tiers]
        WHERE [ID] = @CurrentTierID;

        DECLARE @ActualPoints INT = CEILING(@Points * ISNULL(@Multiplier, 1.0));
        DECLARE @ExpiryDate DATE = DATEADD(MONTH, @ExpiryMonths, GETDATE());

        -- Update both pools: yearly (tier) AND available (redeemable)
        UPDATE [dbo].[Customer_Loyalty]
        SET [TotalPoints]     = [TotalPoints] + @ActualPoints,
            [AvailablePoints] = [AvailablePoints] + @ActualPoints,
            [LifetimePoints]  = [LifetimePoints] + @ActualPoints,
            [YearlyPoints]    = [YearlyPoints] + @ActualPoints,
            [LastEarnDate]    = GETDATE(),
            [LastUpdated]     = GETDATE()
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

        -- Re-evaluate tier (based on yearly pool)
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

-- ------------------------------------------------------------
-- 4) Rewrite sp_UpdateLoyaltyTier to use YearlyPoints
-- ------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_UpdateLoyaltyTier]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_UpdateLoyaltyTier];
GO

CREATE PROCEDURE [dbo].[sp_UpdateLoyaltyTier]
    @CustomerPhone VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @YearlyPoints INT;
    DECLARE @CurrentTierID TINYINT;
    DECLARE @NewTierID TINYINT;

    -- Roll yearly pool if needed (defensive)
    UPDATE [dbo].[Customer_Loyalty]
    SET [YearlyPoints] = 0, [PointsYear] = YEAR(GETDATE())
    WHERE [Customer_MobilePhone] = @CustomerPhone
      AND (ISNULL([PointsYear], 0) <> YEAR(GETDATE()));

    SELECT @YearlyPoints = [YearlyPoints], @CurrentTierID = [CurrentTier_ID]
    FROM [dbo].[Customer_Loyalty]
    WHERE [Customer_MobilePhone] = @CustomerPhone;

    -- Determine tier from YEARLY points (tier qualifying)
    SELECT TOP 1 @NewTierID = [ID]
    FROM [dbo].[Loyalty_Tiers]
    WHERE [MinPoints] <= ISNULL(@YearlyPoints, 0)
      AND [IsActive] = 1
    ORDER BY [MinPoints] DESC;

    IF @NewTierID IS NULL
        SELECT @NewTierID = MIN([ID]) FROM [dbo].[Loyalty_Tiers] WHERE [IsActive] = 1;

    IF @NewTierID <> @CurrentTierID OR @CurrentTierID IS NULL
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

-- ------------------------------------------------------------
-- 5) Manual points adjustment (admin) - affects yearly + available
-- ------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_AdjustLoyaltyPoints]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_AdjustLoyaltyPoints];
GO

CREATE PROCEDURE [dbo].[sp_AdjustLoyaltyPoints]
    @CustomerPhone VARCHAR(20),
    @Points INT,                 -- positive = add, negative = deduct
    @Description NVARCHAR(500) = NULL,
    @AdminID SMALLINT = NULL,
    @AffectTier BIT = 1          -- whether to change yearly/tier pool too
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        IF NOT EXISTS (SELECT 1 FROM [dbo].[Customer_Loyalty] WHERE [Customer_MobilePhone] = @CustomerPhone)
        BEGIN
            INSERT INTO [dbo].[Customer_Loyalty] ([Customer_MobilePhone], [MemberSince], [PointsYear])
            VALUES (@CustomerPhone, CAST(GETDATE() AS DATE), YEAR(GETDATE()));
        END

        -- Guard: cannot drop available below zero
        DECLARE @Available INT;
        SELECT @Available = [AvailablePoints] FROM [dbo].[Customer_Loyalty] WHERE [Customer_MobilePhone] = @CustomerPhone;

        IF (@Available + @Points) < 0
        BEGIN
            ROLLBACK TRANSACTION;
            SELECT 'INSUFFICIENT_POINTS' AS Result, @Available AS AvailablePoints;
            RETURN;
        END

        UPDATE [dbo].[Customer_Loyalty]
        SET [AvailablePoints] = [AvailablePoints] + @Points,
            [TotalPoints]     = CASE WHEN @Points > 0 THEN [TotalPoints] + @Points ELSE [TotalPoints] END,
            [LifetimePoints]  = CASE WHEN @Points > 0 THEN [LifetimePoints] + @Points ELSE [LifetimePoints] END,
            [YearlyPoints]    = CASE WHEN @AffectTier = 1
                                     THEN CASE WHEN ([YearlyPoints] + @Points) < 0 THEN 0 ELSE [YearlyPoints] + @Points END
                                     ELSE [YearlyPoints] END,
            [LastUpdated]     = GETDATE()
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        DECLARE @NewBalance INT;
        SELECT @NewBalance = [AvailablePoints] FROM [dbo].[Customer_Loyalty] WHERE [Customer_MobilePhone] = @CustomerPhone;

        INSERT INTO [dbo].[Loyalty_Transactions] (
            [Customer_MobilePhone], [TransactionType], [Points], [BalanceAfter],
            [Description], [ProcessedBy_AdminID]
        )
        VALUES (
            @CustomerPhone, 'ADJUST', @Points, @NewBalance,
            ISNULL(@Description, 'Manual adjustment'), @AdminID
        );

        EXEC [dbo].[sp_UpdateLoyaltyTier] @CustomerPhone;

        COMMIT TRANSACTION;
        SELECT 'SUCCESS' AS Result, @Points AS PointsAdjusted, @NewBalance AS NewBalance;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- ------------------------------------------------------------
-- 6) Redeem a reward (decrement available + stock, log redemption)
-- ------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_RedeemReward]') AND type in (N'P', N'PC'))
    DROP PROCEDURE [dbo].[sp_RedeemReward];
GO

CREATE PROCEDURE [dbo].[sp_RedeemReward]
    @CustomerPhone VARCHAR(20),
    @RewardID INT,
    @AdminID SMALLINT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;

    BEGIN TRY
        DECLARE @PointsCost INT, @MinTier TINYINT, @Stock INT, @RewardName NVARCHAR(200),
                @ValidityDays INT, @IsActive BIT;

        SELECT @PointsCost = [PointsCost], @MinTier = [MinTierRequired], @Stock = [StockQuantity],
               @RewardName = [RewardName], @ValidityDays = ISNULL([ValidityDays], 365), @IsActive = [IsActive]
        FROM [dbo].[Loyalty_Rewards]
        WHERE [ID] = @RewardID;

        IF @PointsCost IS NULL
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'REWARD_NOT_FOUND' AS Result; RETURN;
        END
        IF @IsActive = 0
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'REWARD_INACTIVE' AS Result; RETURN;
        END
        IF @Stock IS NOT NULL AND @Stock <= 0
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'OUT_OF_STOCK' AS Result; RETURN;
        END

        DECLARE @Available INT, @CurrentTier TINYINT;
        SELECT @Available = [AvailablePoints], @CurrentTier = [CurrentTier_ID]
        FROM [dbo].[Customer_Loyalty]
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        IF @Available IS NULL
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'NOT_A_MEMBER' AS Result; RETURN;
        END
        IF @CurrentTier < @MinTier
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'TIER_TOO_LOW' AS Result; RETURN;
        END
        IF @Available < @PointsCost
        BEGIN
            ROLLBACK TRANSACTION; SELECT 'INSUFFICIENT_POINTS' AS Result, @Available AS AvailablePoints; RETURN;
        END

        -- Deduct available points (tier/yearly unaffected)
        UPDATE [dbo].[Customer_Loyalty]
        SET [AvailablePoints] = [AvailablePoints] - @PointsCost,
            [TotalPoints]     = [TotalPoints] - @PointsCost,
            [LastRedemptionDate] = GETDATE(),
            [LastUpdated] = GETDATE()
        WHERE [Customer_MobilePhone] = @CustomerPhone;

        -- Decrement stock if limited
        IF @Stock IS NOT NULL
            UPDATE [dbo].[Loyalty_Rewards] SET [StockQuantity] = [StockQuantity] - 1 WHERE [ID] = @RewardID;

        DECLARE @NewBalance INT;
        SELECT @NewBalance = [AvailablePoints] FROM [dbo].[Customer_Loyalty] WHERE [Customer_MobilePhone] = @CustomerPhone;

        -- Transaction log
        INSERT INTO [dbo].[Loyalty_Transactions] (
            [Customer_MobilePhone], [TransactionType], [Points], [BalanceAfter],
            [Reward_ID], [Description], [ProcessedBy_AdminID]
        )
        VALUES (
            @CustomerPhone, 'REDEEM', -@PointsCost, @NewBalance,
            @RewardID, N'แลกรางวัล: ' + @RewardName, @AdminID
        );

        -- Redemption voucher
        DECLARE @Code VARCHAR(20) = 'RDM' + RIGHT('00000000' + CAST(ABS(CHECKSUM(NEWID())) % 100000000 AS VARCHAR(8)), 8);
        DECLARE @Expiry DATE = DATEADD(DAY, @ValidityDays, GETDATE());

        INSERT INTO [dbo].[Loyalty_Redemptions] (
            [Customer_MobilePhone], [Reward_ID], [RewardName], [PointsCost],
            [RedemptionCode], [Status], [ExpiryDate]
        )
        VALUES (
            @CustomerPhone, @RewardID, @RewardName, @PointsCost,
            @Code, 'PENDING', @Expiry
        );

        COMMIT TRANSACTION;
        SELECT 'SUCCESS' AS Result, @PointsCost AS PointsRedeemed, @NewBalance AS NewBalance,
               @Code AS RedemptionCode, @RewardName AS RewardName;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

-- ------------------------------------------------------------
-- 7) Member summary view (for admin management page)
-- ------------------------------------------------------------
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_Loyalty_Member_Summary')
    DROP VIEW [dbo].[vw_Loyalty_Member_Summary];
GO

CREATE VIEW [dbo].[vw_Loyalty_Member_Summary]
AS
SELECT
    CL.[Customer_MobilePhone],
    C.[Name] AS CustomerName,
    C.[Email],
    CL.[CurrentTier_ID],
    LT.[TierName],
    LT.[TierNameEN],
    LT.[TierColor],
    LT.[MinPoints] AS TierMinPoints,
    CL.[YearlyPoints],
    CL.[PointsYear],
    CL.[AvailablePoints],
    CL.[TotalPoints],
    CL.[LifetimePoints],
    CL.[MemberSince],
    CL.[LastEarnDate],
    CL.[LastRedemptionDate],
    CL.[LastReviewDate],
    -- Next tier info
    (SELECT TOP 1 NT.[MinPoints] FROM [dbo].[Loyalty_Tiers] NT
       WHERE NT.[MinPoints] > CL.[YearlyPoints] AND NT.[IsActive] = 1
       ORDER BY NT.[MinPoints] ASC) AS NextTierMinPoints,
    (SELECT TOP 1 NT.[TierName] FROM [dbo].[Loyalty_Tiers] NT
       WHERE NT.[MinPoints] > CL.[YearlyPoints] AND NT.[IsActive] = 1
       ORDER BY NT.[MinPoints] ASC) AS NextTierName,
    -- Pending redemptions
    (SELECT COUNT(*) FROM [dbo].[Loyalty_Redemptions] LR
       WHERE LR.[Customer_MobilePhone] = CL.[Customer_MobilePhone] AND LR.[Status] = 'PENDING') AS PendingRedemptions
FROM [dbo].[Customer_Loyalty] CL
INNER JOIN [dbo].[Customer] C ON C.[MobilePhone] = CL.[Customer_MobilePhone]
LEFT JOIN [dbo].[Loyalty_Tiers] LT ON LT.[ID] = CL.[CurrentTier_ID];
GO

PRINT 'Loyalty restructure migration completed.';
GO
