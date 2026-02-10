-- =============================================================================
-- Migration 003: Create Payment Service Schema
-- =============================================================================
-- Target Database: TakeTime_Payment_{TenantId} (per-tenant)
-- Description: Creates all tables for the payment microservice including
--              payments, refunds, payment slips, reconciliation, and
--              Thai-specific payment methods (PromptPay, bank transfers).
-- =============================================================================

-- =============================================
-- Payments - Core payment records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payments')
BEGIN
    CREATE TABLE Payments (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PaymentNumber       NVARCHAR(30)     NOT NULL,
        ReservationId       UNIQUEIDENTIFIER NULL,
        FolioId             UNIQUEIDENTIFIER NULL,
        GuestName           NVARCHAR(400)    NOT NULL,
        PaymentType         NVARCHAR(50)     NOT NULL,
        PaymentMethod       NVARCHAR(50)     NOT NULL,
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        Amount              DECIMAL(18,2)    NOT NULL,
        CurrencyCode        NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        ExchangeRate        DECIMAL(18,6)    NOT NULL DEFAULT 1.000000,
        AmountInBaseCurrency DECIMAL(18,2)   NOT NULL,
        TaxAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ServiceChargeAmount DECIMAL(18,2)    NOT NULL DEFAULT 0,
        NetAmount           DECIMAL(18,2)    NOT NULL,
        Description         NVARCHAR(500)    NULL,
        ReferenceNumber     NVARCHAR(200)    NULL,
        ExternalTransactionId NVARCHAR(200)  NULL,
        GatewayResponse     NVARCHAR(MAX)    NULL,
        PaidAt              DATETIME2        NULL,
        VoidedAt            DATETIME2        NULL,
        VoidedBy            NVARCHAR(200)    NULL,
        VoidReason          NVARCHAR(500)    NULL,
        ProcessedBy         NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Payments_PaymentNumber UNIQUE (PaymentNumber)
    );

    CREATE NONCLUSTERED INDEX IX_Payments_ReservationId ON Payments (ReservationId) WHERE ReservationId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Payments_Status ON Payments (Status) INCLUDE (PaymentMethod, Amount, PaidAt);
    CREATE NONCLUSTERED INDEX IX_Payments_PaidAt ON Payments (PaidAt) WHERE PaidAt IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Payments_PaymentMethod ON Payments (PaymentMethod, Status);
END
GO

-- =============================================
-- PaymentSlips - Bank transfer slip verification
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PaymentSlips')
BEGIN
    CREATE TABLE PaymentSlips (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PaymentId       UNIQUEIDENTIFIER NOT NULL,
        SlipImageUrl    NVARCHAR(1000)   NOT NULL,
        SlipImageThumbUrl NVARCHAR(1000) NULL,
        BankCode        NVARCHAR(20)     NULL,
        TransferDate    DATETIME2        NULL,
        TransferAmount  DECIMAL(18,2)    NULL,
        SenderName      NVARCHAR(200)    NULL,
        SenderAccountLast4 NVARCHAR(4)   NULL,
        VerificationStatus NVARCHAR(50)  NOT NULL DEFAULT 'Pending',
        VerifiedAt      DATETIME2        NULL,
        VerifiedBy      NVARCHAR(200)    NULL,
        RejectionReason NVARCHAR(500)    NULL,
        OcrData         NVARCHAR(MAX)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_PaymentSlips PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PaymentSlips_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_PaymentSlips_PaymentId ON PaymentSlips (PaymentId);
    CREATE NONCLUSTERED INDEX IX_PaymentSlips_VerificationStatus ON PaymentSlips (VerificationStatus) INCLUDE (PaymentId, CreatedAt);
END
GO

-- =============================================
-- Refunds - Refund records linked to payments
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Refunds')
BEGIN
    CREATE TABLE Refunds (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        RefundNumber    NVARCHAR(30)     NOT NULL,
        PaymentId       UNIQUEIDENTIFIER NOT NULL,
        ReservationId   UNIQUEIDENTIFIER NULL,
        Amount          DECIMAL(18,2)    NOT NULL,
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Reason          NVARCHAR(500)    NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        RefundMethod    NVARCHAR(50)     NOT NULL,
        BankAccountName NVARCHAR(200)    NULL,
        BankAccountNumber NVARCHAR(50)   NULL,
        BankCode        NVARCHAR(20)     NULL,
        ExternalRefundId NVARCHAR(200)   NULL,
        ApprovedBy      NVARCHAR(200)    NULL,
        ApprovedAt      DATETIME2        NULL,
        ProcessedAt     DATETIME2        NULL,
        ProcessedBy     NVARCHAR(200)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_Refunds PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Refunds_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id),
        CONSTRAINT UQ_Refunds_Number UNIQUE (RefundNumber)
    );

    CREATE NONCLUSTERED INDEX IX_Refunds_PaymentId ON Refunds (PaymentId);
    CREATE NONCLUSTERED INDEX IX_Refunds_Status ON Refunds (Status) INCLUDE (Amount, CreatedAt);
END
GO

-- =============================================
-- Invoices - Tax invoices
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Invoices')
BEGIN
    CREATE TABLE Invoices (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        InvoiceNumber   NVARCHAR(30)     NOT NULL,
        ReservationId   UNIQUEIDENTIFIER NULL,
        FolioId         UNIQUEIDENTIFIER NULL,
        InvoiceType     NVARCHAR(50)     NOT NULL DEFAULT 'TaxInvoice',
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        CustomerName    NVARCHAR(500)    NOT NULL,
        CustomerTaxId   NVARCHAR(50)     NULL,
        CustomerAddress NVARCHAR(MAX)    NULL,
        SubTotal        DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TaxableAmount   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        VatRate         DECIMAL(5,2)     NOT NULL DEFAULT 7.00,
        VatAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ServiceChargeRate DECIMAL(5,2)   NOT NULL DEFAULT 0,
        ServiceChargeAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        WithholdingTaxRate DECIMAL(5,2)  NOT NULL DEFAULT 0,
        WithholdingTaxAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
        GrandTotal      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        IssuedDate      DATE             NOT NULL,
        DueDate         DATE             NULL,
        PaidDate        DATE             NULL,
        Notes           NVARCHAR(MAX)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Invoices_Number UNIQUE (InvoiceNumber)
    );

    CREATE NONCLUSTERED INDEX IX_Invoices_ReservationId ON Invoices (ReservationId) WHERE ReservationId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Invoices_Status ON Invoices (Status) INCLUDE (InvoiceNumber, GrandTotal);
    CREATE NONCLUSTERED INDEX IX_Invoices_IssuedDate ON Invoices (IssuedDate DESC);
END
GO

-- =============================================
-- InvoiceLineItems - Invoice detail lines
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'InvoiceLineItems')
BEGIN
    CREATE TABLE InvoiceLineItems (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        InvoiceId       UNIQUEIDENTIFIER NOT NULL,
        LineNumber      INT              NOT NULL,
        Description     NVARCHAR(500)    NOT NULL,
        Quantity        DECIMAL(10,2)    NOT NULL DEFAULT 1,
        UnitPrice       DECIMAL(18,2)    NOT NULL,
        DiscountPercent DECIMAL(5,2)     NOT NULL DEFAULT 0,
        DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Amount          DECIMAL(18,2)    NOT NULL,
        TaxRate         DECIMAL(5,2)     NOT NULL DEFAULT 7.00,
        TaxAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_InvoiceLineItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_InvoiceLineItems_Invoices FOREIGN KEY (InvoiceId) REFERENCES Invoices(Id) ON DELETE CASCADE
    );
END
GO

-- =============================================
-- Receipts - Payment receipts
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Receipts')
BEGIN
    CREATE TABLE Receipts (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ReceiptNumber   NVARCHAR(30)     NOT NULL,
        PaymentId       UNIQUEIDENTIFIER NOT NULL,
        InvoiceId       UNIQUEIDENTIFIER NULL,
        Amount          DECIMAL(18,2)    NOT NULL,
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        CustomerName    NVARCHAR(500)    NOT NULL,
        CustomerTaxId   NVARCHAR(50)     NULL,
        IssuedDate      DATE             NOT NULL,
        Notes           NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_Receipts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Receipts_Payments FOREIGN KEY (PaymentId) REFERENCES Payments(Id),
        CONSTRAINT UQ_Receipts_Number UNIQUE (ReceiptNumber)
    );
END
GO

-- =============================================
-- DailyReconciliation - End-of-day settlement
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DailyReconciliation')
BEGIN
    CREATE TABLE DailyReconciliation (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ReconciliationDate  DATE             NOT NULL,
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Open',
        TotalCashReceived   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalCardReceived   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalTransferReceived DECIMAL(18,2)  NOT NULL DEFAULT 0,
        TotalPromptPayReceived DECIMAL(18,2) NOT NULL DEFAULT 0,
        TotalOtherReceived  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalRefunds        DECIMAL(18,2)    NOT NULL DEFAULT 0,
        GrandTotal          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Variance            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ClosedBy            NVARCHAR(200)    NULL,
        ClosedAt            DATETIME2        NULL,
        Notes               NVARCHAR(MAX)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_DailyReconciliation PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_DailyReconciliation_Date UNIQUE (ReconciliationDate)
    );
END
GO
