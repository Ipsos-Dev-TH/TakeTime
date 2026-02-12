-- =============================================================================
-- Migration 010: Create Accounting Service Schema
-- =============================================================================
-- Target Database: TakeTime_Accounting_{TenantId} (per-tenant)
-- Description: Creates all tables for the accounting microservice
--              including financial transactions and daily revenue summaries
--              for Thai hospitality business reporting (VAT 7%, THB).
-- =============================================================================

-- =============================================
-- FinancialTransactions - Income and expense records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FinancialTransactions')
BEGIN
    CREATE TABLE FinancialTransactions (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        TransactionNumber   NVARCHAR(30)     NOT NULL,
        Type                NVARCHAR(50)     NOT NULL,
        Category            NVARCHAR(200)    NOT NULL,
        Amount              DECIMAL(18,2)    NOT NULL DEFAULT 0,
        AmountCurrency      NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        VATAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        VATAmountCurrency   NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Description         NVARCHAR(MAX)    NULL,
        ReferenceType       NVARCHAR(200)    NULL,
        ReferenceId         UNIQUEIDENTIFIER NULL,
        TransactionDate     DATE             NOT NULL,
        PostedBy            NVARCHAR(200)    NOT NULL DEFAULT 'System',
        [Version]           INT              NOT NULL DEFAULT 0,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_FinancialTransactions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_FinancialTransactions_Number UNIQUE (TransactionNumber)
    );

    CREATE NONCLUSTERED INDEX IX_FinancialTransactions_Type ON FinancialTransactions (Type) INCLUDE (Category, Amount, TransactionDate);
    CREATE NONCLUSTERED INDEX IX_FinancialTransactions_Category ON FinancialTransactions (Category, TransactionDate) INCLUDE (Type, Amount);
    CREATE NONCLUSTERED INDEX IX_FinancialTransactions_TransactionDate ON FinancialTransactions (TransactionDate DESC) INCLUDE (Type, Category, Amount);
    CREATE NONCLUSTERED INDEX IX_FinancialTransactions_ReferenceType ON FinancialTransactions (ReferenceType, ReferenceId) WHERE ReferenceType IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_FinancialTransactions_TenantId ON FinancialTransactions (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- DailyRevenueSummaries - Aggregated daily financial and occupancy metrics
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyRevenueSummaries')
BEGIN
    CREATE TABLE DailyRevenueSummaries (
        Id                              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                        NVARCHAR(100)    NULL,
        [Date]                          DATE             NOT NULL,
        TotalReservationRevenue         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalReservationRevenueCurrency NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        TotalPOSRevenue                 DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalPOSRevenueCurrency         NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        TotalOtherRevenue               DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalOtherRevenueCurrency       NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        TotalExpenses                   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalExpensesCurrency           NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        NetRevenue                      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        NetRevenueCurrency              NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        RoomsSold                       INT              NOT NULL DEFAULT 0,
        OccupancyRate                   DECIMAL(5,2)     NOT NULL DEFAULT 0,
        ADR                             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ADRCurrency                     NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        RevPAR                          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        RevPARCurrency                  NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        IsDeleted                       BIT              NOT NULL DEFAULT 0,
        DeletedAt                       DATETIME2        NULL,
        DeletedBy                       NVARCHAR(200)    NULL,
        CreatedAt                       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy                       NVARCHAR(200)    NULL,
        UpdatedAt                       DATETIME2        NULL,
        UpdatedBy                       NVARCHAR(200)    NULL,
        CONSTRAINT PK_DailyRevenueSummaries PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_DailyRevenueSummaries_Date UNIQUE ([Date])
    );

    CREATE NONCLUSTERED INDEX IX_DailyRevenueSummaries_Date ON DailyRevenueSummaries ([Date] DESC) INCLUDE (NetRevenue, OccupancyRate, ADR, RevPAR);
    CREATE NONCLUSTERED INDEX IX_DailyRevenueSummaries_TenantId ON DailyRevenueSummaries (TenantId) WHERE TenantId IS NOT NULL;
END
GO
