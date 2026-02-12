-- =============================================================================
-- Migration 012: Create Affiliate Service Schema
-- =============================================================================
-- Target Database: TakeTime_Affiliate_{TenantId} (per-tenant)
-- Description: Creates all tables for the affiliate partner microservice
--              including affiliate partners and commission tracking.
--              Supports Thai banking details and THB currency.
-- =============================================================================

-- =============================================
-- AffiliatePartners - Affiliate/referral partners
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AffiliatePartners')
BEGIN
    CREATE TABLE AffiliatePartners (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        AffiliateCode       NVARCHAR(50)     NOT NULL,
        FirstName           NVARCHAR(200)    NOT NULL,
        LastName            NVARCHAR(200)    NOT NULL,
        Email               NVARCHAR(500)    NOT NULL,
        Phone               NVARCHAR(50)     NOT NULL,
        BankAccount         NVARCHAR(200)    NULL,
        CommissionRate      DECIMAL(5,2)     NOT NULL DEFAULT 0,
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Active',
        TotalEarnings       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalEarningsCurrency NVARCHAR(3)    NOT NULL DEFAULT 'THB',
        TotalPaid           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalPaidCurrency   NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Balance             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        BalanceCurrency     NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        [Version]           INT              NOT NULL DEFAULT 0,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_AffiliatePartners PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_AffiliatePartners_Code UNIQUE (AffiliateCode),
        CONSTRAINT UQ_AffiliatePartners_Email UNIQUE (Email)
    );

    CREATE NONCLUSTERED INDEX IX_AffiliatePartners_Status ON AffiliatePartners (Status) INCLUDE (AffiliateCode, Email);
    CREATE NONCLUSTERED INDEX IX_AffiliatePartners_AffiliateCode ON AffiliatePartners (AffiliateCode) INCLUDE (FirstName, LastName, Status);
    CREATE NONCLUSTERED INDEX IX_AffiliatePartners_TenantId ON AffiliatePartners (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- AffiliateCommissions - Commission records per reservation
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AffiliateCommissions')
BEGIN
    CREATE TABLE AffiliateCommissions (
        Id                          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                    NVARCHAR(100)    NULL,
        AffiliateId                 UNIQUEIDENTIFIER NOT NULL,
        ReservationId               UNIQUEIDENTIFIER NOT NULL,
        ReservationAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ReservationAmountCurrency   NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        CommissionRate              DECIMAL(5,2)     NOT NULL DEFAULT 0,
        CommissionAmount            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CommissionAmountCurrency    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Status                      NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        IsDeleted                   BIT              NOT NULL DEFAULT 0,
        DeletedAt                   DATETIME2        NULL,
        DeletedBy                   NVARCHAR(200)    NULL,
        CreatedAt                   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy                   NVARCHAR(200)    NULL,
        UpdatedAt                   DATETIME2        NULL,
        UpdatedBy                   NVARCHAR(200)    NULL,
        CONSTRAINT PK_AffiliateCommissions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_AffiliateCommissions_Partners FOREIGN KEY (AffiliateId) REFERENCES AffiliatePartners(Id)
    );

    CREATE NONCLUSTERED INDEX IX_AffiliateCommissions_AffiliateId ON AffiliateCommissions (AffiliateId) INCLUDE (Status, CommissionAmount);
    CREATE NONCLUSTERED INDEX IX_AffiliateCommissions_ReservationId ON AffiliateCommissions (ReservationId);
    CREATE NONCLUSTERED INDEX IX_AffiliateCommissions_Status ON AffiliateCommissions (Status) INCLUDE (AffiliateId, CommissionAmount, CreatedAt);
    CREATE NONCLUSTERED INDEX IX_AffiliateCommissions_TenantId ON AffiliateCommissions (TenantId) WHERE TenantId IS NOT NULL;
END
GO
