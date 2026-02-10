-- =============================================================================
-- Migration 006: Create CRM Service Schema
-- =============================================================================
-- Target Database: TakeTime_CRM_{TenantId} (per-tenant)
-- Description: Creates all tables for the CRM microservice including
--              guest profiles, loyalty programs, communication history,
--              marketing campaigns, and guest preferences.
-- =============================================================================

-- =============================================
-- GuestProfiles - Master guest records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestProfiles')
BEGIN
    CREATE TABLE GuestProfiles (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProfileNumber       NVARCHAR(20)     NOT NULL,
        Salutation          NVARCHAR(20)     NULL,
        FirstName           NVARCHAR(200)    NOT NULL,
        LastName            NVARCHAR(200)    NOT NULL,
        FirstNameThai       NVARCHAR(200)    NULL,
        LastNameThai        NVARCHAR(200)    NULL,
        Email               NVARCHAR(500)    NULL,
        Phone               NVARCHAR(50)     NULL,
        MobilePhone         NVARCHAR(50)     NULL,
        LineId              NVARCHAR(200)    NULL,
        DateOfBirth         DATE             NULL,
        Gender              NVARCHAR(20)     NULL,
        Nationality         NVARCHAR(100)    NULL,
        Language            NVARCHAR(20)     NOT NULL DEFAULT 'th',
        IdType              NVARCHAR(50)     NULL,
        IdNumber            NVARCHAR(100)    NULL,
        PassportNumber      NVARCHAR(50)     NULL,
        PassportExpiry      DATE             NULL,
        CompanyName         NVARCHAR(500)    NULL,
        CompanyTaxId        NVARCHAR(50)     NULL,
        Address             NVARCHAR(MAX)    NULL,
        City                NVARCHAR(200)    NULL,
        StateProvince       NVARCHAR(200)    NULL,
        PostalCode          NVARCHAR(20)     NULL,
        Country             NVARCHAR(100)    NULL,
        VipLevel            NVARCHAR(50)     NOT NULL DEFAULT 'Standard',
        Source              NVARCHAR(100)    NULL,
        TotalStays          INT              NOT NULL DEFAULT 0,
        TotalNights         INT              NOT NULL DEFAULT 0,
        TotalSpend          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        LastStayDate        DATE             NULL,
        AverageRating       DECIMAL(3,1)     NULL,
        IsBlacklisted       BIT              NOT NULL DEFAULT 0,
        BlacklistReason     NVARCHAR(500)    NULL,
        Notes               NVARCHAR(MAX)    NULL,
        ConsentMarketing    BIT              NOT NULL DEFAULT 0,
        ConsentDataProcessing BIT            NOT NULL DEFAULT 1,
        PhotoUrl            NVARCHAR(1000)   NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_GuestProfiles PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_GuestProfiles_Number UNIQUE (ProfileNumber)
    );

    CREATE NONCLUSTERED INDEX IX_GuestProfiles_Email ON GuestProfiles (Email) WHERE Email IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_GuestProfiles_Phone ON GuestProfiles (Phone) WHERE Phone IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_GuestProfiles_Name ON GuestProfiles (LastName, FirstName);
    CREATE NONCLUSTERED INDEX IX_GuestProfiles_VipLevel ON GuestProfiles (VipLevel) INCLUDE (TotalStays, TotalSpend);
    CREATE NONCLUSTERED INDEX IX_GuestProfiles_LineId ON GuestProfiles (LineId) WHERE LineId IS NOT NULL;
END
GO

-- =============================================
-- GuestPreferences - Guest likes, dislikes, and special needs
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestPreferences')
BEGIN
    CREATE TABLE GuestPreferences (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        GuestId         UNIQUEIDENTIFIER NOT NULL,
        Category        NVARCHAR(100)    NOT NULL,
        PreferenceKey   NVARCHAR(200)    NOT NULL,
        PreferenceValue NVARCHAR(500)    NOT NULL,
        Notes           NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_GuestPreferences PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_GuestPreferences_Guests FOREIGN KEY (GuestId) REFERENCES GuestProfiles(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_GuestPreferences_Unique UNIQUE (GuestId, Category, PreferenceKey)
    );
END
GO

-- =============================================
-- LoyaltyPrograms - Loyalty program definitions
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyPrograms')
BEGIN
    CREATE TABLE LoyaltyPrograms (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        Description     NVARCHAR(MAX)    NULL,
        PointsPerBaht   DECIMAL(10,4)    NOT NULL DEFAULT 1.0000,
        PointValue      DECIMAL(10,4)    NOT NULL DEFAULT 0.5000,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_LoyaltyPrograms PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_LoyaltyPrograms_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- LoyaltyTiers - Tier levels within loyalty program
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyTiers')
BEGIN
    CREATE TABLE LoyaltyTiers (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProgramId       UNIQUEIDENTIFIER NOT NULL,
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        MinPoints       INT              NOT NULL DEFAULT 0,
        MinNights       INT              NOT NULL DEFAULT 0,
        DiscountPercent DECIMAL(5,2)     NOT NULL DEFAULT 0,
        BonusPointsPercent DECIMAL(5,2)  NOT NULL DEFAULT 0,
        EarlyCheckIn    BIT              NOT NULL DEFAULT 0,
        LateCheckOut    BIT              NOT NULL DEFAULT 0,
        RoomUpgrade     BIT              NOT NULL DEFAULT 0,
        WelcomeAmenity  BIT              NOT NULL DEFAULT 0,
        SortOrder       INT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_LoyaltyTiers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LoyaltyTiers_Programs FOREIGN KEY (ProgramId) REFERENCES LoyaltyPrograms(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_LoyaltyTiers_Code UNIQUE (ProgramId, Code)
    );
END
GO

-- =============================================
-- GuestLoyalty - Guest enrollment in loyalty program
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestLoyalty')
BEGIN
    CREATE TABLE GuestLoyalty (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        GuestId         UNIQUEIDENTIFIER NOT NULL,
        ProgramId       UNIQUEIDENTIFIER NOT NULL,
        TierId          UNIQUEIDENTIFIER NULL,
        MemberNumber    NVARCHAR(30)     NOT NULL,
        PointsBalance   INT              NOT NULL DEFAULT 0,
        LifetimePoints  INT              NOT NULL DEFAULT 0,
        TotalNights     INT              NOT NULL DEFAULT 0,
        EnrolledAt      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        TierExpiresAt   DATETIME2        NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_GuestLoyalty PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_GuestLoyalty_Guests FOREIGN KEY (GuestId) REFERENCES GuestProfiles(Id),
        CONSTRAINT FK_GuestLoyalty_Programs FOREIGN KEY (ProgramId) REFERENCES LoyaltyPrograms(Id),
        CONSTRAINT FK_GuestLoyalty_Tiers FOREIGN KEY (TierId) REFERENCES LoyaltyTiers(Id),
        CONSTRAINT UQ_GuestLoyalty_MemberNumber UNIQUE (MemberNumber),
        CONSTRAINT UQ_GuestLoyalty_GuestProgram UNIQUE (GuestId, ProgramId)
    );
END
GO

-- =============================================
-- LoyaltyTransactions - Points earn/redeem/expire
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LoyaltyTransactions')
BEGIN
    CREATE TABLE LoyaltyTransactions (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        GuestLoyaltyId  UNIQUEIDENTIFIER NOT NULL,
        TransactionType NVARCHAR(50)     NOT NULL,
        Points          INT              NOT NULL,
        BalanceAfter    INT              NOT NULL,
        Description     NVARCHAR(500)    NOT NULL,
        ReferenceType   NVARCHAR(100)    NULL,
        ReferenceId     NVARCHAR(200)    NULL,
        ExpiresAt       DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_LoyaltyTransactions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_LoyaltyTransactions_GuestLoyalty FOREIGN KEY (GuestLoyaltyId) REFERENCES GuestLoyalty(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_LoyaltyTransactions_GuestLoyalty ON LoyaltyTransactions (GuestLoyaltyId, CreatedAt DESC);
END
GO

-- =============================================
-- GuestStayHistory - Aggregated stay history
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestStayHistory')
BEGIN
    CREATE TABLE GuestStayHistory (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        GuestId         UNIQUEIDENTIFIER NOT NULL,
        ReservationId   UNIQUEIDENTIFIER NOT NULL,
        CheckInDate     DATE             NOT NULL,
        CheckOutDate    DATE             NOT NULL,
        Nights          INT              NOT NULL,
        RoomNumber      NVARCHAR(20)     NULL,
        RoomTypeName    NVARCHAR(200)    NULL,
        RatePlanName    NVARCHAR(200)    NULL,
        TotalSpend      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Rating          INT              NULL,
        ReviewText      NVARCHAR(MAX)    NULL,
        ReviewedAt      DATETIME2        NULL,
        Source          NVARCHAR(100)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_GuestStayHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_GuestStayHistory_Guests FOREIGN KEY (GuestId) REFERENCES GuestProfiles(Id)
    );

    CREATE NONCLUSTERED INDEX IX_GuestStayHistory_GuestId ON GuestStayHistory (GuestId, CheckInDate DESC);
    CREATE NONCLUSTERED INDEX IX_GuestStayHistory_ReservationId ON GuestStayHistory (ReservationId);
END
GO

-- =============================================
-- CommunicationLog - All guest communications
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'CommunicationLog')
BEGIN
    CREATE TABLE CommunicationLog (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        GuestId         UNIQUEIDENTIFIER NULL,
        Channel         NVARCHAR(50)     NOT NULL,
        Direction       NVARCHAR(20)     NOT NULL DEFAULT 'Outbound',
        Subject         NVARCHAR(500)    NULL,
        Body            NVARCHAR(MAX)    NULL,
        Recipient       NVARCHAR(500)    NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Sent',
        TemplateCode    NVARCHAR(100)    NULL,
        CampaignId      UNIQUEIDENTIFIER NULL,
        ExternalMessageId NVARCHAR(200)  NULL,
        SentAt          DATETIME2        NULL,
        DeliveredAt     DATETIME2        NULL,
        OpenedAt        DATETIME2        NULL,
        ClickedAt       DATETIME2        NULL,
        BouncedAt       DATETIME2        NULL,
        ErrorMessage    NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_CommunicationLog PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_CommunicationLog_Guests FOREIGN KEY (GuestId) REFERENCES GuestProfiles(Id)
    );

    CREATE NONCLUSTERED INDEX IX_CommunicationLog_GuestId ON CommunicationLog (GuestId, CreatedAt DESC);
    CREATE NONCLUSTERED INDEX IX_CommunicationLog_Channel ON CommunicationLog (Channel, Status, CreatedAt DESC);
END
GO

-- =============================================
-- MarketingCampaigns - Email/SMS/LINE campaigns
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MarketingCampaigns')
BEGIN
    CREATE TABLE MarketingCampaigns (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Name            NVARCHAR(200)    NOT NULL,
        Channel         NVARCHAR(50)     NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        Subject         NVARCHAR(500)    NULL,
        BodyTemplate    NVARCHAR(MAX)    NULL,
        TargetSegment   NVARCHAR(200)    NULL,
        TargetCount     INT              NOT NULL DEFAULT 0,
        SentCount       INT              NOT NULL DEFAULT 0,
        DeliveredCount  INT              NOT NULL DEFAULT 0,
        OpenedCount     INT              NOT NULL DEFAULT 0,
        ClickedCount    INT              NOT NULL DEFAULT 0,
        ScheduledAt     DATETIME2        NULL,
        SentAt          DATETIME2        NULL,
        CompletedAt     DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_MarketingCampaigns PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_MarketingCampaigns_Status ON MarketingCampaigns (Status, ScheduledAt);
END
GO

-- =============================================
-- GuestSegments - Dynamic guest segments for targeting
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestSegments')
BEGIN
    CREATE TABLE GuestSegments (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        Description     NVARCHAR(500)    NULL,
        FilterCriteria  NVARCHAR(MAX)    NULL,
        GuestCount      INT              NOT NULL DEFAULT 0,
        IsAutomatic     BIT              NOT NULL DEFAULT 0,
        LastRefreshedAt DATETIME2        NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_GuestSegments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_GuestSegments_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- GuestSegmentMembers - Guests in each segment
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestSegmentMembers')
BEGIN
    CREATE TABLE GuestSegmentMembers (
        Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        SegmentId   UNIQUEIDENTIFIER NOT NULL,
        GuestId     UNIQUEIDENTIFIER NOT NULL,
        AddedAt     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_GuestSegmentMembers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_GuestSegmentMembers_Segments FOREIGN KEY (SegmentId) REFERENCES GuestSegments(Id) ON DELETE CASCADE,
        CONSTRAINT FK_GuestSegmentMembers_Guests FOREIGN KEY (GuestId) REFERENCES GuestProfiles(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_GuestSegmentMembers_Unique UNIQUE (SegmentId, GuestId)
    );
END
GO
