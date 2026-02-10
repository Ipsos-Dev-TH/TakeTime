-- =============================================================================
-- Migration 004: Create Inventory Service Schema
-- =============================================================================
-- Target Database: TakeTime_Inventory_{TenantId} (per-tenant)
-- Description: Creates all tables for the inventory/stock microservice
--              including products, categories, stock movements, purchase
--              orders, and suppliers for resort F&B and amenities.
-- =============================================================================

-- =============================================
-- ProductCategories - Hierarchical category tree
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ProductCategories')
BEGIN
    CREATE TABLE ProductCategories (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ParentId        UNIQUEIDENTIFIER NULL,
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        Description     NVARCHAR(500)    NULL,
        SortOrder       INT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_ProductCategories PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ProductCategories_Parent FOREIGN KEY (ParentId) REFERENCES ProductCategories(Id),
        CONSTRAINT UQ_ProductCategories_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- Products - Inventory items
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Products')
BEGIN
    CREATE TABLE Products (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        CategoryId      UNIQUEIDENTIFIER NOT NULL,
        SKU             NVARCHAR(50)     NOT NULL,
        Barcode         NVARCHAR(50)     NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        Description     NVARCHAR(500)    NULL,
        Unit            NVARCHAR(50)     NOT NULL DEFAULT 'piece',
        UnitPrice       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CostPrice       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TaxRate         DECIMAL(5,2)     NOT NULL DEFAULT 7.00,
        IsTaxable       BIT              NOT NULL DEFAULT 1,
        MinStockLevel   DECIMAL(18,3)    NOT NULL DEFAULT 0,
        MaxStockLevel   DECIMAL(18,3)    NOT NULL DEFAULT 0,
        ReorderPoint    DECIMAL(18,3)    NOT NULL DEFAULT 0,
        ReorderQuantity DECIMAL(18,3)    NOT NULL DEFAULT 0,
        IsPerishable    BIT              NOT NULL DEFAULT 0,
        ShelfLifeDays   INT              NULL,
        StorageLocation NVARCHAR(200)    NULL,
        ImageUrl        NVARCHAR(1000)   NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(200)    NULL,
        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Products_Categories FOREIGN KEY (CategoryId) REFERENCES ProductCategories(Id),
        CONSTRAINT UQ_Products_SKU UNIQUE (SKU)
    );

    CREATE NONCLUSTERED INDEX IX_Products_CategoryId ON Products (CategoryId) INCLUDE (Name, IsActive);
    CREATE NONCLUSTERED INDEX IX_Products_Barcode ON Products (Barcode) WHERE Barcode IS NOT NULL;
END
GO

-- =============================================
-- Warehouses - Storage locations
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Warehouses')
BEGIN
    CREATE TABLE Warehouses (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        Location        NVARCHAR(500)    NULL,
        WarehouseType   NVARCHAR(50)     NOT NULL DEFAULT 'General',
        IsDefault       BIT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_Warehouses PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Warehouses_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- StockLevels - Current stock quantity per product per warehouse
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockLevels')
BEGIN
    CREATE TABLE StockLevels (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProductId       UNIQUEIDENTIFIER NOT NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        QuantityOnHand  DECIMAL(18,3)    NOT NULL DEFAULT 0,
        QuantityReserved DECIMAL(18,3)   NOT NULL DEFAULT 0,
        QuantityAvailable AS (QuantityOnHand - QuantityReserved) PERSISTED,
        LastCountedAt   DATETIME2        NULL,
        LastReceivedAt  DATETIME2        NULL,
        UpdatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_StockLevels PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StockLevels_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT FK_StockLevels_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
        CONSTRAINT UQ_StockLevels_ProductWarehouse UNIQUE (ProductId, WarehouseId)
    );

    CREATE NONCLUSTERED INDEX IX_StockLevels_LowStock ON StockLevels (QuantityOnHand) INCLUDE (ProductId, WarehouseId);
END
GO

-- =============================================
-- StockMovements - Inbound/outbound stock transactions
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockMovements')
BEGIN
    CREATE TABLE StockMovements (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ProductId       UNIQUEIDENTIFIER NOT NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        MovementType    NVARCHAR(50)     NOT NULL,
        Quantity        DECIMAL(18,3)    NOT NULL,
        UnitCost        DECIMAL(18,2)    NULL,
        TotalCost       DECIMAL(18,2)    NULL,
        ReferenceType   NVARCHAR(100)    NULL,
        ReferenceId     NVARCHAR(200)    NULL,
        Reason          NVARCHAR(500)    NULL,
        BatchNumber     NVARCHAR(50)     NULL,
        ExpiryDate      DATE             NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_StockMovements PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StockMovements_Products FOREIGN KEY (ProductId) REFERENCES Products(Id),
        CONSTRAINT FK_StockMovements_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id)
    );

    CREATE NONCLUSTERED INDEX IX_StockMovements_ProductId ON StockMovements (ProductId, CreatedAt DESC);
    CREATE NONCLUSTERED INDEX IX_StockMovements_Type ON StockMovements (MovementType, CreatedAt DESC);
    CREATE NONCLUSTERED INDEX IX_StockMovements_Reference ON StockMovements (ReferenceType, ReferenceId);
END
GO

-- =============================================
-- Suppliers - Vendor registry
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Suppliers')
BEGIN
    CREATE TABLE Suppliers (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(500)    NOT NULL,
        ContactPerson   NVARCHAR(200)    NULL,
        Email           NVARCHAR(500)    NULL,
        Phone           NVARCHAR(50)     NULL,
        Address         NVARCHAR(MAX)    NULL,
        TaxId           NVARCHAR(50)     NULL,
        PaymentTerms    NVARCHAR(100)    NULL,
        CreditLimitDays INT              NOT NULL DEFAULT 30,
        BankAccountName NVARCHAR(200)    NULL,
        BankAccountNumber NVARCHAR(50)   NULL,
        BankCode        NVARCHAR(20)     NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        Rating          INT              NULL,
        Notes           NVARCHAR(MAX)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_Suppliers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Suppliers_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- PurchaseOrders - Orders to suppliers
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrders')
BEGIN
    CREATE TABLE PurchaseOrders (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PONumber        NVARCHAR(30)     NOT NULL,
        SupplierId      UNIQUEIDENTIFIER NOT NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        OrderDate       DATE             NOT NULL,
        ExpectedDate    DATE             NULL,
        ReceivedDate    DATE             NULL,
        SubTotal        DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TaxAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        GrandTotal      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode    NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Notes           NVARCHAR(MAX)    NULL,
        ApprovedBy      NVARCHAR(200)    NULL,
        ApprovedAt      DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_PurchaseOrders PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PurchaseOrders_Suppliers FOREIGN KEY (SupplierId) REFERENCES Suppliers(Id),
        CONSTRAINT FK_PurchaseOrders_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
        CONSTRAINT UQ_PurchaseOrders_Number UNIQUE (PONumber)
    );

    CREATE NONCLUSTERED INDEX IX_PurchaseOrders_SupplierId ON PurchaseOrders (SupplierId);
    CREATE NONCLUSTERED INDEX IX_PurchaseOrders_Status ON PurchaseOrders (Status) INCLUDE (OrderDate, GrandTotal);
END
GO

-- =============================================
-- PurchaseOrderItems - Line items on purchase orders
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'PurchaseOrderItems')
BEGIN
    CREATE TABLE PurchaseOrderItems (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        PurchaseOrderId UNIQUEIDENTIFIER NOT NULL,
        ProductId       UNIQUEIDENTIFIER NOT NULL,
        Quantity        DECIMAL(18,3)    NOT NULL,
        ReceivedQuantity DECIMAL(18,3)   NOT NULL DEFAULT 0,
        UnitPrice       DECIMAL(18,2)    NOT NULL,
        DiscountPercent DECIMAL(5,2)     NOT NULL DEFAULT 0,
        TaxRate         DECIMAL(5,2)     NOT NULL DEFAULT 7.00,
        TotalPrice      DECIMAL(18,2)    NOT NULL,
        Notes           NVARCHAR(500)    NULL,
        CONSTRAINT PK_PurchaseOrderItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_PurchaseOrderItems_PO FOREIGN KEY (PurchaseOrderId) REFERENCES PurchaseOrders(Id) ON DELETE CASCADE,
        CONSTRAINT FK_PurchaseOrderItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
    );
END
GO

-- =============================================
-- StockCounts - Physical inventory counts
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockCounts')
BEGIN
    CREATE TABLE StockCounts (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        CountNumber     NVARCHAR(30)     NOT NULL,
        WarehouseId     UNIQUEIDENTIFIER NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'InProgress',
        CountDate       DATE             NOT NULL,
        CompletedAt     DATETIME2        NULL,
        CompletedBy     NVARCHAR(200)    NULL,
        ApprovedBy      NVARCHAR(200)    NULL,
        ApprovedAt      DATETIME2        NULL,
        TotalVariance   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Notes           NVARCHAR(MAX)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CONSTRAINT PK_StockCounts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StockCounts_Warehouses FOREIGN KEY (WarehouseId) REFERENCES Warehouses(Id),
        CONSTRAINT UQ_StockCounts_Number UNIQUE (CountNumber)
    );
END
GO

-- =============================================
-- StockCountItems - Individual count entries
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'StockCountItems')
BEGIN
    CREATE TABLE StockCountItems (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        StockCountId    UNIQUEIDENTIFIER NOT NULL,
        ProductId       UNIQUEIDENTIFIER NOT NULL,
        SystemQuantity  DECIMAL(18,3)    NOT NULL DEFAULT 0,
        CountedQuantity DECIMAL(18,3)    NULL,
        Variance        AS (CountedQuantity - SystemQuantity) PERSISTED,
        VarianceCost    DECIMAL(18,2)    NULL,
        Notes           NVARCHAR(500)    NULL,
        CONSTRAINT PK_StockCountItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_StockCountItems_StockCounts FOREIGN KEY (StockCountId) REFERENCES StockCounts(Id) ON DELETE CASCADE,
        CONSTRAINT FK_StockCountItems_Products FOREIGN KEY (ProductId) REFERENCES Products(Id)
    );
END
GO
