-- =============================================================================
-- Migration 009: Create Guest Experience Service Schema
-- =============================================================================
-- Target Database: TakeTime_GuestExperience_{TenantId} (per-tenant)
-- Description: Creates all tables for the guest experience microservice
--              including housekeeping tasks, room service orders, and
--              maintenance requests.
-- =============================================================================

-- =============================================
-- HousekeepingTasks - Cleaning, turndown, deep clean, inspection tasks
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'HousekeepingTasks')
BEGIN
    CREATE TABLE HousekeepingTasks (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        AccommodationId     UNIQUEIDENTIFIER NOT NULL,
        RoomNumber          NVARCHAR(20)     NOT NULL,
        TaskType            NVARCHAR(50)     NOT NULL DEFAULT 'Cleaning',
        Priority            NVARCHAR(20)     NOT NULL DEFAULT 'Normal',
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        AssignedTo          UNIQUEIDENTIFIER NULL,
        AssignedToName      NVARCHAR(200)    NULL,
        ScheduledAt         DATETIME2        NULL,
        StartedAt           DATETIME2        NULL,
        CompletedAt         DATETIME2        NULL,
        VerifiedAt          DATETIME2        NULL,
        VerifiedBy          NVARCHAR(200)    NULL,
        Notes               NVARCHAR(MAX)    NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_HousekeepingTasks PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_HousekeepingTasks_RoomNumber ON HousekeepingTasks (RoomNumber) INCLUDE (Status, TaskType);
    CREATE NONCLUSTERED INDEX IX_HousekeepingTasks_Status ON HousekeepingTasks (Status) INCLUDE (RoomNumber, Priority, AssignedTo);
    CREATE NONCLUSTERED INDEX IX_HousekeepingTasks_AssignedTo ON HousekeepingTasks (AssignedTo) WHERE AssignedTo IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_HousekeepingTasks_ScheduledAt ON HousekeepingTasks (ScheduledAt) WHERE ScheduledAt IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_HousekeepingTasks_TenantId ON HousekeepingTasks (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- RoomServiceOrders - Food and beverage orders to guest rooms
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomServiceOrders')
BEGIN
    CREATE TABLE RoomServiceOrders (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                NVARCHAR(100)    NULL,
        OrderNumber             NVARCHAR(30)     NOT NULL,
        ReservationId           UNIQUEIDENTIFIER NOT NULL,
        RoomNumber              NVARCHAR(20)     NOT NULL,
        SubTotal                DECIMAL(18,2)    NOT NULL DEFAULT 0,
        VATAmount               DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalAmount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        ChargeToRoom            BIT              NOT NULL DEFAULT 0,
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'Ordered',
        OrderedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        PreparedAt              DATETIME2        NULL,
        DeliveredAt             DATETIME2        NULL,
        DeliveredBy             NVARCHAR(200)    NULL,
        SpecialInstructions     NVARCHAR(MAX)    NULL,
        [Version]               INT              NOT NULL DEFAULT 0,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(200)    NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(200)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(200)    NULL,
        CONSTRAINT PK_RoomServiceOrders PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RoomServiceOrders_OrderNumber UNIQUE (OrderNumber)
    );

    CREATE NONCLUSTERED INDEX IX_RoomServiceOrders_RoomNumber ON RoomServiceOrders (RoomNumber) INCLUDE (Status, OrderedAt);
    CREATE NONCLUSTERED INDEX IX_RoomServiceOrders_Status ON RoomServiceOrders (Status) INCLUDE (RoomNumber, OrderedAt);
    CREATE NONCLUSTERED INDEX IX_RoomServiceOrders_ReservationId ON RoomServiceOrders (ReservationId);
    CREATE NONCLUSTERED INDEX IX_RoomServiceOrders_TenantId ON RoomServiceOrders (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- RoomServiceOrderItems - Individual items within a room service order
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomServiceOrderItems')
BEGIN
    CREATE TABLE RoomServiceOrderItems (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        OrderId             UNIQUEIDENTIFIER NOT NULL,
        ProductId           UNIQUEIDENTIFIER NULL,
        ProductName         NVARCHAR(200)    NOT NULL,
        Quantity            INT              NOT NULL DEFAULT 1,
        UnitPrice           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalPrice          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Notes               NVARCHAR(500)    NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_RoomServiceOrderItems PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoomServiceOrderItems_Orders FOREIGN KEY (OrderId) REFERENCES RoomServiceOrders(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_RoomServiceOrderItems_OrderId ON RoomServiceOrderItems (OrderId);
END
GO

-- =============================================
-- MaintenanceRequests - Room and facility maintenance tracking
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MaintenanceRequests')
BEGIN
    CREATE TABLE MaintenanceRequests (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        RequestNumber       NVARCHAR(30)     NOT NULL,
        AccommodationId     UNIQUEIDENTIFIER NOT NULL,
        RoomNumber          NVARCHAR(20)     NOT NULL,
        IssueType           NVARCHAR(100)    NOT NULL,
        Description         NVARCHAR(MAX)    NOT NULL,
        Priority            NVARCHAR(20)     NOT NULL DEFAULT 'Normal',
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Reported',
        AssignedTo          UNIQUEIDENTIFIER NULL,
        AssignedToName      NVARCHAR(200)    NULL,
        ReportedBy          NVARCHAR(200)    NULL,
        ReportedAt          DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        ResolvedAt          DATETIME2        NULL,
        ResolvedBy          NVARCHAR(200)    NULL,
        ResolutionNotes     NVARCHAR(MAX)    NULL,
        EstimatedCost       DECIMAL(18,2)    NULL,
        ActualCost          DECIMAL(18,2)    NULL,
        Photos              NVARCHAR(MAX)    NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_MaintenanceRequests PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_MaintenanceRequests_RequestNumber UNIQUE (RequestNumber)
    );

    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_RoomNumber ON MaintenanceRequests (RoomNumber) INCLUDE (Status, Priority);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_Status ON MaintenanceRequests (Status) INCLUDE (Priority, RoomNumber, AssignedTo);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_AssignedTo ON MaintenanceRequests (AssignedTo) WHERE AssignedTo IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_IssueType ON MaintenanceRequests (IssueType, Status);
    CREATE NONCLUSTERED INDEX IX_MaintenanceRequests_TenantId ON MaintenanceRequests (TenantId) WHERE TenantId IS NOT NULL;
END
GO
