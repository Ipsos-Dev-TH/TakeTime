-- =============================================================================
-- Migration 001: Create Tenant Management Schema
-- =============================================================================
-- Target Database: TakeTime_TenantManagement (shared catalog)
-- Description: Creates the core multi-tenancy management tables including
--              tenants, users, subscriptions, feature flags, and audit log.
-- =============================================================================

-- =============================================
-- Tenants - Core tenant registry
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE Tenants (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Identifier      NVARCHAR(100)    NOT NULL,
        Name            NVARCHAR(500)    NOT NULL,
        Subdomain       NVARCHAR(100)    NULL,
        CustomDomain    NVARCHAR(500)    NULL,
        LogoUrl         NVARCHAR(1000)   NULL,
        Timezone        NVARCHAR(100)    NOT NULL DEFAULT 'Asia/Bangkok',
        Locale          NVARCHAR(20)     NOT NULL DEFAULT 'th-TH',
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        CurrencySymbol  NVARCHAR(10)     NOT NULL DEFAULT N'฿',
        IsActive        BIT              NOT NULL DEFAULT 1,
        IsSuspended     BIT              NOT NULL DEFAULT 0,
        SuspendedReason NVARCHAR(1000)   NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(200)    NULL,
        CONSTRAINT PK_Tenants PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Tenants_Identifier UNIQUE (Identifier),
        CONSTRAINT UQ_Tenants_Subdomain UNIQUE (Subdomain)
    );

    CREATE NONCLUSTERED INDEX IX_Tenants_IsActive ON Tenants (IsActive) INCLUDE (Identifier, Name);
    CREATE NONCLUSTERED INDEX IX_Tenants_Subdomain ON Tenants (Subdomain) WHERE Subdomain IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Tenants_CustomDomain ON Tenants (CustomDomain) WHERE CustomDomain IS NOT NULL;
END
GO

-- =============================================
-- TenantSettings - Key-value configuration per tenant
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantSettings')
BEGIN
    CREATE TABLE TenantSettings (
        Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        Category    NVARCHAR(100)    NOT NULL,
        [Key]       NVARCHAR(200)    NOT NULL,
        Value       NVARCHAR(MAX)    NOT NULL,
        ValueType   NVARCHAR(50)     NOT NULL DEFAULT 'String',
        Description NVARCHAR(500)    NULL,
        IsEncrypted BIT              NOT NULL DEFAULT 0,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2        NULL,
        CONSTRAINT PK_TenantSettings PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantSettings_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_TenantSettings_TenantCategoryKey UNIQUE (TenantId, Category, [Key])
    );

    CREATE NONCLUSTERED INDEX IX_TenantSettings_TenantCategory ON TenantSettings (TenantId, Category);
END
GO

-- =============================================
-- TenantFeatures - Feature flags per tenant
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantFeatures')
BEGIN
    CREATE TABLE TenantFeatures (
        Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        FeatureCode NVARCHAR(100)    NOT NULL,
        IsEnabled   BIT              NOT NULL DEFAULT 1,
        ExpiresAt   DATETIME2        NULL,
        Metadata    NVARCHAR(MAX)    NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2        NULL,
        CONSTRAINT PK_TenantFeatures PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantFeatures_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_TenantFeatures_TenantFeature UNIQUE (TenantId, FeatureCode)
    );
END
GO

-- =============================================
-- TenantSubscriptions - Subscription/billing per tenant
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantSubscriptions')
BEGIN
    CREATE TABLE TenantSubscriptions (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        PlanCode        NVARCHAR(50)     NOT NULL DEFAULT 'Professional',
        PlanName        NVARCHAR(200)    NOT NULL DEFAULT N'Professional Plan',
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Active',
        BillingCycle    NVARCHAR(20)     NOT NULL DEFAULT 'Monthly',
        PricePerMonth   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        StartDate       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        EndDate         DATETIME2        NULL,
        TrialEndsAt     DATETIME2        NULL,
        CancelledAt     DATETIME2        NULL,
        MaxRooms        INT              NOT NULL DEFAULT 100,
        MaxUsers        INT              NOT NULL DEFAULT 50,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_TenantSubscriptions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantSubscriptions_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_TenantSubscriptions_TenantId ON TenantSubscriptions (TenantId);
    CREATE NONCLUSTERED INDEX IX_TenantSubscriptions_Status ON TenantSubscriptions (Status) INCLUDE (TenantId);
END
GO

-- =============================================
-- Users - Platform-level users (can belong to multiple tenants)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE Users (
        Id               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Email            NVARCHAR(500)    NOT NULL,
        PasswordHash     NVARCHAR(500)    NOT NULL,
        PasswordSalt     NVARCHAR(500)    NOT NULL,
        FirstName        NVARCHAR(200)    NOT NULL,
        LastName         NVARCHAR(200)    NOT NULL,
        DisplayName      NVARCHAR(400)    NULL,
        PhoneNumber      NVARCHAR(50)     NULL,
        AvatarUrl        NVARCHAR(1000)   NULL,
        Role             NVARCHAR(50)     NOT NULL DEFAULT 'User',
        IsActive         BIT              NOT NULL DEFAULT 1,
        EmailConfirmed   BIT              NOT NULL DEFAULT 0,
        PhoneConfirmed   BIT              NOT NULL DEFAULT 0,
        TwoFactorEnabled BIT              NOT NULL DEFAULT 0,
        LockoutEnd       DATETIME2        NULL,
        AccessFailedCount INT             NOT NULL DEFAULT 0,
        LastLoginAt      DATETIME2        NULL,
        LastLoginIp      NVARCHAR(50)     NULL,
        RefreshToken     NVARCHAR(500)    NULL,
        RefreshTokenExpiry DATETIME2      NULL,
        CreatedAt        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy        NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt        DATETIME2        NULL,
        UpdatedBy        NVARCHAR(200)    NULL,
        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Users_Email UNIQUE (Email)
    );

    CREATE NONCLUSTERED INDEX IX_Users_Email ON Users (Email) INCLUDE (PasswordHash, PasswordSalt, IsActive);
    CREATE NONCLUSTERED INDEX IX_Users_Role ON Users (Role) WHERE IsActive = 1;
END
GO

-- =============================================
-- TenantUsers - Maps users to tenants with roles
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantUsers')
BEGIN
    CREATE TABLE TenantUsers (
        Id          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId    UNIQUEIDENTIFIER NOT NULL,
        UserId      UNIQUEIDENTIFIER NOT NULL,
        Role        NVARCHAR(50)     NOT NULL DEFAULT 'Staff',
        Department  NVARCHAR(200)    NULL,
        Title       NVARCHAR(200)    NULL,
        IsActive    BIT              NOT NULL DEFAULT 1,
        JoinedAt    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        LeftAt      DATETIME2        NULL,
        CreatedAt   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2        NULL,
        CONSTRAINT PK_TenantUsers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantUsers_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
        CONSTRAINT FK_TenantUsers_Users FOREIGN KEY (UserId) REFERENCES Users(Id),
        CONSTRAINT UQ_TenantUsers_TenantUser UNIQUE (TenantId, UserId)
    );

    CREATE NONCLUSTERED INDEX IX_TenantUsers_UserId ON TenantUsers (UserId) INCLUDE (TenantId, Role, IsActive);
    CREATE NONCLUSTERED INDEX IX_TenantUsers_TenantId ON TenantUsers (TenantId) INCLUDE (UserId, Role, IsActive);
END
GO

-- =============================================
-- TenantBankAccounts - Bank accounts for payment receipt per tenant
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantBankAccounts')
BEGIN
    CREATE TABLE TenantBankAccounts (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        BankCode        NVARCHAR(20)     NOT NULL,
        BankName        NVARCHAR(200)    NOT NULL,
        AccountName     NVARCHAR(500)    NOT NULL,
        AccountNumber   NVARCHAR(50)     NOT NULL,
        BranchName      NVARCHAR(200)    NULL,
        AccountType     NVARCHAR(50)     NOT NULL DEFAULT 'Savings',
        IsActive        BIT              NOT NULL DEFAULT 1,
        IsDefault       BIT              NOT NULL DEFAULT 0,
        PromptPayId     NVARCHAR(50)     NULL,
        PromptPayType   NVARCHAR(20)     NULL,
        QrCodeUrl       NVARCHAR(1000)   NULL,
        SortOrder       INT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_TenantBankAccounts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantBankAccounts_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_TenantBankAccounts_TenantId ON TenantBankAccounts (TenantId) INCLUDE (IsActive, IsDefault);
END
GO

-- =============================================
-- AuditLog - Platform-wide audit trail
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AuditLog')
BEGIN
    CREATE TABLE AuditLog (
        Id          BIGINT IDENTITY(1,1) NOT NULL,
        TenantId    UNIQUEIDENTIFIER     NULL,
        UserId      UNIQUEIDENTIFIER     NULL,
        Action      NVARCHAR(100)        NOT NULL,
        EntityType  NVARCHAR(200)        NOT NULL,
        EntityId    NVARCHAR(200)        NULL,
        OldValues   NVARCHAR(MAX)        NULL,
        NewValues   NVARCHAR(MAX)        NULL,
        IpAddress   NVARCHAR(50)         NULL,
        UserAgent   NVARCHAR(500)        NULL,
        Timestamp   DATETIME2            NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_AuditLog PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_AuditLog_TenantId ON AuditLog (TenantId, Timestamp DESC);
    CREATE NONCLUSTERED INDEX IX_AuditLog_EntityType ON AuditLog (EntityType, EntityId) INCLUDE (Action, Timestamp);
    CREATE NONCLUSTERED INDEX IX_AuditLog_UserId ON AuditLog (UserId, Timestamp DESC);
END
GO

-- =============================================
-- NotificationTemplates - Platform notification templates
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationTemplates')
BEGIN
    CREATE TABLE NotificationTemplates (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId        UNIQUEIDENTIFIER NULL,
        TemplateCode    NVARCHAR(100)    NOT NULL,
        Channel         NVARCHAR(50)     NOT NULL,
        Locale          NVARCHAR(20)     NOT NULL DEFAULT 'th-TH',
        Subject         NVARCHAR(500)    NULL,
        BodyTemplate    NVARCHAR(MAX)    NOT NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_NotificationTemplates PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_NotificationTemplates_Code UNIQUE (TenantId, TemplateCode, Channel, Locale)
    );
END
GO
