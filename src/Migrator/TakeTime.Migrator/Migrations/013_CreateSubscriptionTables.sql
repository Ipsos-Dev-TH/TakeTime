-- =============================================================================
-- Migration 013: Create Subscription Management Tables
-- =============================================================================
-- Target Database: TakeTime_TenantManagement (shared catalog)
-- Description: Creates the subscription management tables for the SaaS billing
--              system including subscription plans, tenant subscriptions,
--              payments (with Thai payment methods), and tax invoices (VAT 7%).
-- =============================================================================

-- =============================================
-- SubscriptionPlans - Available subscription plans
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SubscriptionPlans' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE SubscriptionPlans (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code                NVARCHAR(50)     NOT NULL,
        Name                NVARCHAR(200)    NOT NULL,
        NameTh              NVARCHAR(200)    NULL,
        Description         NVARCHAR(2000)   NULL,
        DescriptionTh       NVARCHAR(2000)   NULL,
        Tier                NVARCHAR(50)     NOT NULL DEFAULT 'Free',
        MonthlyPrice        DECIMAL(18,2)    NOT NULL DEFAULT 0,
        YearlyPrice         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency            NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        MaxRooms            INT              NOT NULL DEFAULT 10,
        MaxUsers            INT              NOT NULL DEFAULT 5,
        MaxStorageGB        INT              NOT NULL DEFAULT 1,
        IncludedFeatures    NVARCHAR(MAX)    NULL,
        IncludedModules     NVARCHAR(MAX)    NULL,
        TrialAvailable      BIT              NOT NULL DEFAULT 0,
        TrialDays           INT              NOT NULL DEFAULT 0,
        IsActive            BIT              NOT NULL DEFAULT 1,
        SortOrder           INT              NOT NULL DEFAULT 0,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt           DATETIME2        NULL,
        CONSTRAINT PK_SubscriptionPlans PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SubscriptionPlans_Code UNIQUE (Code)
    );

    CREATE NONCLUSTERED INDEX IX_SubscriptionPlans_Tier ON SubscriptionPlans (Tier) INCLUDE (IsActive, MonthlyPrice);
    CREATE NONCLUSTERED INDEX IX_SubscriptionPlans_IsActive ON SubscriptionPlans (IsActive) INCLUDE (Code, Name, Tier, SortOrder);
END
GO

-- =============================================
-- Seed default subscription plans
-- =============================================
IF NOT EXISTS (SELECT * FROM SubscriptionPlans WHERE Code = 'free')
BEGIN
    INSERT INTO SubscriptionPlans (Id, Code, Name, NameTh, Description, DescriptionTh, Tier, MonthlyPrice, YearlyPrice, Currency, MaxRooms, MaxUsers, MaxStorageGB, IncludedFeatures, IncludedModules, TrialAvailable, TrialDays, IsActive, SortOrder)
    VALUES
    ('00000000-0000-0000-0000-000000000001', 'free', 'Free', N'แพ็กเกจฟรี',
     'Basic plan for small properties just getting started.', N'แพ็กเกจพื้นฐานสำหรับที่พักขนาดเล็ก',
     'Free', 0, 0, 'THB', 5, 2, 1,
     '["reservation_basic","calendar_view","guest_list"]',
     '["Reservation"]',
     0, 0, 1, 0);
END
GO

IF NOT EXISTS (SELECT * FROM SubscriptionPlans WHERE Code = 'starter')
BEGIN
    INSERT INTO SubscriptionPlans (Id, Code, Name, NameTh, Description, DescriptionTh, Tier, MonthlyPrice, YearlyPrice, Currency, MaxRooms, MaxUsers, MaxStorageGB, IncludedFeatures, IncludedModules, TrialAvailable, TrialDays, IsActive, SortOrder)
    VALUES
    ('00000000-0000-0000-0000-000000000002', 'starter', 'Starter', N'แพ็กเกจสตาร์ทเตอร์',
     'Ideal for small to medium properties with reservation, payment, and CRM.', N'เหมาะสำหรับที่พักขนาดเล็กถึงกลาง รวมระบบจอง ชำระเงิน และ CRM',
     'Starter', 1990, 19900, 'THB', 20, 5, 5,
     '["reservation_basic","reservation_advanced","calendar_view","guest_list","payment_basic","reports_basic","crm_basic"]',
     '["Reservation","Payment","CRM"]',
     1, 14, 1, 1);
END
GO

IF NOT EXISTS (SELECT * FROM SubscriptionPlans WHERE Code = 'professional')
BEGIN
    INSERT INTO SubscriptionPlans (Id, Code, Name, NameTh, Description, DescriptionTh, Tier, MonthlyPrice, YearlyPrice, Currency, MaxRooms, MaxUsers, MaxStorageGB, IncludedFeatures, IncludedModules, TrialAvailable, TrialDays, IsActive, SortOrder)
    VALUES
    ('00000000-0000-0000-0000-000000000003', 'professional', 'Professional', N'แพ็กเกจโปรเฟสชันนอล',
     'Full operational management including HR, inventory, and channel management.', N'ระบบจัดการครบวงจร รวม HR สต็อกสินค้า และ Channel Manager',
     'Professional', 4990, 49900, 'THB', 100, 20, 25,
     '["reservation_basic","reservation_advanced","calendar_view","guest_list","payment_basic","payment_advanced","reports_basic","reports_advanced","crm_basic","crm_advanced","inventory_basic","hr_basic","channel_manager","housekeeping","notifications"]',
     '["Reservation","Payment","CRM","Inventory","HumanResources","ChannelManager","GuestExperience","Notification"]',
     1, 30, 1, 2);
END
GO

IF NOT EXISTS (SELECT * FROM SubscriptionPlans WHERE Code = 'enterprise')
BEGIN
    INSERT INTO SubscriptionPlans (Id, Code, Name, NameTh, Description, DescriptionTh, Tier, MonthlyPrice, YearlyPrice, Currency, MaxRooms, MaxUsers, MaxStorageGB, IncludedFeatures, IncludedModules, TrialAvailable, TrialDays, IsActive, SortOrder)
    VALUES
    ('00000000-0000-0000-0000-000000000004', 'enterprise', 'Enterprise', N'แพ็กเกจเอ็นเตอร์ไพรส์',
     'Full-featured plan for large properties and hotel chains.', N'แพ็กเกจเต็มรูปแบบสำหรับที่พักขนาดใหญ่และเครือโรงแรม',
     'Enterprise', 9990, 99900, 'THB', 999, 100, 100,
     '["reservation_basic","reservation_advanced","calendar_view","guest_list","payment_basic","payment_advanced","reports_basic","reports_advanced","crm_basic","crm_advanced","inventory_basic","inventory_advanced","hr_basic","hr_advanced","channel_manager","housekeeping","maintenance","room_service","notifications","accounting_basic","accounting_advanced","affiliate_management","api_access","custom_branding","priority_support"]',
     '["Reservation","Payment","CRM","Inventory","HumanResources","ChannelManager","GuestExperience","Notification","Accounting","Affiliate"]',
     1, 30, 1, 3);
END
GO

-- =============================================
-- TenantSubscriptionsV2 - Enhanced subscription tracking per tenant
-- (Named V2 to avoid conflict with existing TenantSubscriptions from migration 001)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'TenantSubscriptionsV2')
BEGIN
    CREATE TABLE TenantSubscriptionsV2 (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        PlanId                  UNIQUEIDENTIFIER NOT NULL,
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'Trial',
        BillingCycle            NVARCHAR(20)     NOT NULL DEFAULT 'Monthly',
        StartDate               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        EndDate                 DATETIME2        NULL,
        TrialStartDate          DATETIME2        NULL,
        TrialEndDate            DATETIME2        NULL,
        IsTrialUsed             BIT              NOT NULL DEFAULT 0,
        CancelledAt             DATETIME2        NULL,
        CancellationReason      NVARCHAR(1000)   NULL,
        PriceAtSubscription     DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DiscountPercentage      DECIMAL(5,2)     NOT NULL DEFAULT 0,
        DiscountCode            NVARCHAR(50)     NULL,
        FinalPrice              DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        AutoRenew               BIT              NOT NULL DEFAULT 1,
        NextBillingDate         DATETIME2        NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt               DATETIME2        NULL,
        CONSTRAINT PK_TenantSubscriptionsV2 PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_TenantSubscriptionsV2_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
        CONSTRAINT FK_TenantSubscriptionsV2_Plans FOREIGN KEY (PlanId) REFERENCES SubscriptionPlans(Id)
    );

    CREATE NONCLUSTERED INDEX IX_TenantSubscriptionsV2_TenantId ON TenantSubscriptionsV2 (TenantId) INCLUDE (Status, PlanId);
    CREATE NONCLUSTERED INDEX IX_TenantSubscriptionsV2_Status ON TenantSubscriptionsV2 (Status) INCLUDE (TenantId, PlanId);
    CREATE NONCLUSTERED INDEX IX_TenantSubscriptionsV2_TenantStatus ON TenantSubscriptionsV2 (TenantId, Status) INCLUDE (PlanId, EndDate);
    CREATE NONCLUSTERED INDEX IX_TenantSubscriptionsV2_NextBilling ON TenantSubscriptionsV2 (NextBillingDate) WHERE AutoRenew = 1 AND Status = 'Active';
END
GO

-- =============================================
-- SubscriptionPayments - Payment records for subscriptions
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SubscriptionPayments')
BEGIN
    CREATE TABLE SubscriptionPayments (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        SubscriptionId          UNIQUEIDENTIFIER NOT NULL,
        PaymentNumber           NVARCHAR(50)     NOT NULL,
        Amount                  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        VatAmount               DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalAmount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        PaymentMethod           NVARCHAR(50)     NOT NULL DEFAULT 'BankTransfer',
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        BankName                NVARCHAR(200)    NULL,
        TransferReference       NVARCHAR(200)    NULL,
        SlipImageUrl            NVARCHAR(1000)   NULL,
        CardLast4               NVARCHAR(4)      NULL,
        CardBrand               NVARCHAR(50)     NULL,
        GatewayTransactionId    NVARCHAR(200)    NULL,
        InvoiceNumber           NVARCHAR(50)     NULL,
        BillingPeriodStart      DATETIME2        NOT NULL,
        BillingPeriodEnd        DATETIME2        NOT NULL,
        PaidAt                  DATETIME2        NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_SubscriptionPayments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SubscriptionPayments_PaymentNumber UNIQUE (PaymentNumber),
        CONSTRAINT FK_SubscriptionPayments_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
        CONSTRAINT FK_SubscriptionPayments_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES TenantSubscriptionsV2(Id)
    );

    CREATE NONCLUSTERED INDEX IX_SubscriptionPayments_TenantId ON SubscriptionPayments (TenantId) INCLUDE (Status, Amount, PaidAt);
    CREATE NONCLUSTERED INDEX IX_SubscriptionPayments_SubscriptionId ON SubscriptionPayments (SubscriptionId) INCLUDE (Status, Amount);
    CREATE NONCLUSTERED INDEX IX_SubscriptionPayments_Status ON SubscriptionPayments (Status) INCLUDE (TenantId, Amount, PaidAt);
    CREATE NONCLUSTERED INDEX IX_SubscriptionPayments_PaidAt ON SubscriptionPayments (PaidAt) WHERE PaidAt IS NOT NULL;
END
GO

-- =============================================
-- SubscriptionInvoices - Tax invoices for subscription billing
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'SubscriptionInvoices')
BEGIN
    CREATE TABLE SubscriptionInvoices (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                UNIQUEIDENTIFIER NOT NULL,
        SubscriptionId          UNIQUEIDENTIFIER NOT NULL,
        PaymentId               UNIQUEIDENTIFIER NULL,
        InvoiceNumber           NVARCHAR(50)     NOT NULL,
        InvoiceDate             DATETIME2        NOT NULL,
        DueDate                 DATETIME2        NOT NULL,
        PlanName                NVARCHAR(200)    NOT NULL,
        BillingCycleDescription NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
        BillingPeriodStart      DATETIME2        NOT NULL,
        BillingPeriodEnd        DATETIME2        NOT NULL,
        SubTotal                DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DiscountAmount          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        VatRate                 DECIMAL(5,2)     NOT NULL DEFAULT 7.00,
        VatAmount               DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalAmount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        SellerName              NVARCHAR(500)    NULL,
        SellerTaxId             NVARCHAR(50)     NULL,
        SellerAddress           NVARCHAR(1000)   NULL,
        BuyerName               NVARCHAR(500)    NULL,
        BuyerTaxId              NVARCHAR(50)     NULL,
        BuyerAddress            NVARCHAR(1000)   NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        PaidAt                  DATETIME2        NULL,
        CONSTRAINT PK_SubscriptionInvoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_SubscriptionInvoices_InvoiceNumber UNIQUE (InvoiceNumber),
        CONSTRAINT FK_SubscriptionInvoices_Tenants FOREIGN KEY (TenantId) REFERENCES Tenants(Id),
        CONSTRAINT FK_SubscriptionInvoices_Subscriptions FOREIGN KEY (SubscriptionId) REFERENCES TenantSubscriptionsV2(Id)
    );

    CREATE NONCLUSTERED INDEX IX_SubscriptionInvoices_TenantId ON SubscriptionInvoices (TenantId) INCLUDE (Status, TotalAmount);
    CREATE NONCLUSTERED INDEX IX_SubscriptionInvoices_SubscriptionId ON SubscriptionInvoices (SubscriptionId);
    CREATE NONCLUSTERED INDEX IX_SubscriptionInvoices_Status ON SubscriptionInvoices (Status) INCLUDE (TenantId, TotalAmount, DueDate);
    CREATE NONCLUSTERED INDEX IX_SubscriptionInvoices_InvoiceDate ON SubscriptionInvoices (InvoiceDate DESC) INCLUDE (TenantId, Status, TotalAmount);
END
GO
