-- =============================================================================
-- Migration 002: Create Reservation Service Schema
-- =============================================================================
-- Target Database: TakeTime_Reservation_{TenantId} (per-tenant)
-- Description: Creates all tables for the reservation/booking microservice
--              including rooms, rate plans, reservations, and availability.
-- =============================================================================

-- =============================================
-- RoomTypes - Categories of rooms (Standard, Deluxe, Suite, etc.)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomTypes')
BEGIN
    CREATE TABLE RoomTypes (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        Description     NVARCHAR(MAX)    NULL,
        DescriptionThai NVARCHAR(MAX)    NULL,
        MaxOccupancy    INT              NOT NULL DEFAULT 2,
        MaxAdults       INT              NOT NULL DEFAULT 2,
        MaxChildren     INT              NOT NULL DEFAULT 1,
        MaxInfants      INT              NOT NULL DEFAULT 1,
        BasePrice       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        SortOrder       INT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        AmenityJson     NVARCHAR(MAX)    NULL,
        ImageUrls       NVARCHAR(MAX)    NULL,
        SizeSqm         DECIMAL(10,2)    NULL,
        BedType         NVARCHAR(100)    NULL,
        ViewType        NVARCHAR(100)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(200)    NULL,
        CONSTRAINT PK_RoomTypes PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RoomTypes_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- Rooms - Individual room inventory
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Rooms')
BEGIN
    CREATE TABLE Rooms (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        RoomTypeId      UNIQUEIDENTIFIER NOT NULL,
        RoomNumber      NVARCHAR(20)     NOT NULL,
        Floor           NVARCHAR(10)     NULL,
        Building        NVARCHAR(100)    NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Available',
        HousekeepingStatus NVARCHAR(50)  NOT NULL DEFAULT 'Clean',
        IsActive        BIT              NOT NULL DEFAULT 1,
        Notes           NVARCHAR(MAX)    NULL,
        LastCleanedAt   DATETIME2        NULL,
        LastInspectedAt DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(200)    NULL,
        CONSTRAINT PK_Rooms PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Rooms_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(Id),
        CONSTRAINT UQ_Rooms_Number UNIQUE (RoomNumber)
    );

    CREATE NONCLUSTERED INDEX IX_Rooms_RoomTypeId ON Rooms (RoomTypeId) INCLUDE (Status, IsActive);
    CREATE NONCLUSTERED INDEX IX_Rooms_Status ON Rooms (Status) WHERE IsActive = 1;
END
GO

-- =============================================
-- RatePlans - Pricing strategies (Rack Rate, BAR, Promo, etc.)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RatePlans')
BEGIN
    CREATE TABLE RatePlans (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Code            NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        NameThai        NVARCHAR(200)    NULL,
        Description     NVARCHAR(500)    NULL,
        RateType        NVARCHAR(50)     NOT NULL DEFAULT 'PerNight',
        MealPlan        NVARCHAR(50)     NOT NULL DEFAULT 'RoomOnly',
        IsRefundable    BIT              NOT NULL DEFAULT 1,
        CancellationHours INT            NOT NULL DEFAULT 24,
        MinNights       INT              NOT NULL DEFAULT 1,
        MaxNights       INT              NOT NULL DEFAULT 365,
        ValidFrom       DATETIME2        NULL,
        ValidTo         DATETIME2        NULL,
        IsActive        BIT              NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy       NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt       DATETIME2        NULL,
        UpdatedBy       NVARCHAR(200)    NULL,
        CONSTRAINT PK_RatePlans PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_RatePlans_Code UNIQUE (Code)
    );
END
GO

-- =============================================
-- RatePlanPrices - Price per room type per rate plan per date
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RatePlanPrices')
BEGIN
    CREATE TABLE RatePlanPrices (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        RatePlanId      UNIQUEIDENTIFIER NOT NULL,
        RoomTypeId      UNIQUEIDENTIFIER NOT NULL,
        [Date]          DATE             NOT NULL,
        Price           DECIMAL(18,2)    NOT NULL,
        ExtraAdultPrice DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ExtraChildPrice DECIMAL(18,2)    NOT NULL DEFAULT 0,
        SingleUsePrice  DECIMAL(18,2)    NULL,
        IsOverride      BIT              NOT NULL DEFAULT 0,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RatePlanPrices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RatePlanPrices_RatePlans FOREIGN KEY (RatePlanId) REFERENCES RatePlans(Id),
        CONSTRAINT FK_RatePlanPrices_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(Id),
        CONSTRAINT UQ_RatePlanPrices_Unique UNIQUE (RatePlanId, RoomTypeId, [Date])
    );

    CREATE NONCLUSTERED INDEX IX_RatePlanPrices_Date ON RatePlanPrices ([Date]) INCLUDE (RatePlanId, RoomTypeId, Price);
END
GO

-- =============================================
-- Reservations - Core booking records
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservations')
BEGIN
    CREATE TABLE Reservations (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ConfirmationNumber  NVARCHAR(20)     NOT NULL,
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Confirmed',
        Source              NVARCHAR(50)     NOT NULL DEFAULT 'Direct',
        ChannelRefNumber    NVARCHAR(100)    NULL,
        GuestId             UNIQUEIDENTIFIER NULL,
        GuestFirstName      NVARCHAR(200)    NOT NULL,
        GuestLastName       NVARCHAR(200)    NOT NULL,
        GuestEmail          NVARCHAR(500)    NULL,
        GuestPhone          NVARCHAR(50)     NULL,
        GuestNationality    NVARCHAR(100)    NULL,
        GuestIdType         NVARCHAR(50)     NULL,
        GuestIdNumber       NVARCHAR(100)    NULL,
        RoomTypeId          UNIQUEIDENTIFIER NOT NULL,
        RoomId              UNIQUEIDENTIFIER NULL,
        RatePlanId          UNIQUEIDENTIFIER NOT NULL,
        CheckInDate         DATE             NOT NULL,
        CheckOutDate        DATE             NOT NULL,
        CheckInTime         TIME             NULL,
        CheckOutTime        TIME             NULL,
        ActualCheckIn       DATETIME2        NULL,
        ActualCheckOut      DATETIME2        NULL,
        Adults              INT              NOT NULL DEFAULT 1,
        Children            INT              NOT NULL DEFAULT 0,
        Infants             INT              NOT NULL DEFAULT 0,
        Nights              AS DATEDIFF(DAY, CheckInDate, CheckOutDate) PERSISTED,
        RoomRate            DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalAmount         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DiscountAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TaxAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ServiceCharge       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        GrandTotal          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode        NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        DepositAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        DepositPaid         BIT              NOT NULL DEFAULT 0,
        BalanceDue          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        SpecialRequests     NVARCHAR(MAX)    NULL,
        InternalNotes       NVARCHAR(MAX)    NULL,
        CancellationReason  NVARCHAR(500)    NULL,
        CancelledAt         DATETIME2        NULL,
        CancelledBy         NVARCHAR(200)    NULL,
        AffiliateId         UNIQUEIDENTIFIER NULL,
        AffiliateCommission DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NOT NULL DEFAULT 'System',
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_Reservations PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Reservations_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(Id),
        CONSTRAINT FK_Reservations_Rooms FOREIGN KEY (RoomId) REFERENCES Rooms(Id),
        CONSTRAINT FK_Reservations_RatePlans FOREIGN KEY (RatePlanId) REFERENCES RatePlans(Id),
        CONSTRAINT UQ_Reservations_ConfirmationNumber UNIQUE (ConfirmationNumber)
    );

    CREATE NONCLUSTERED INDEX IX_Reservations_Dates ON Reservations (CheckInDate, CheckOutDate) INCLUDE (Status, RoomTypeId, RoomId);
    CREATE NONCLUSTERED INDEX IX_Reservations_Status ON Reservations (Status) INCLUDE (CheckInDate, CheckOutDate);
    CREATE NONCLUSTERED INDEX IX_Reservations_GuestEmail ON Reservations (GuestEmail) WHERE GuestEmail IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Reservations_GuestId ON Reservations (GuestId) WHERE GuestId IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Reservations_Source ON Reservations (Source, CreatedAt DESC);
    CREATE NONCLUSTERED INDEX IX_Reservations_ChannelRef ON Reservations (ChannelRefNumber) WHERE ChannelRefNumber IS NOT NULL;
END
GO

-- =============================================
-- ReservationNightlyRates - Breakdown of rate per night
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReservationNightlyRates')
BEGIN
    CREATE TABLE ReservationNightlyRates (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ReservationId   UNIQUEIDENTIFIER NOT NULL,
        [Date]          DATE             NOT NULL,
        BaseRate        DECIMAL(18,2)    NOT NULL,
        DiscountAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        NetRate         DECIMAL(18,2)    NOT NULL,
        CONSTRAINT PK_ReservationNightlyRates PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ReservationNightlyRates_Reservations FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE CASCADE,
        CONSTRAINT UQ_ReservationNightlyRates_Unique UNIQUE (ReservationId, [Date])
    );
END
GO

-- =============================================
-- ReservationAddOns - Extra services (breakfast, airport transfer, etc.)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ReservationAddOns')
BEGIN
    CREATE TABLE ReservationAddOns (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ReservationId   UNIQUEIDENTIFIER NOT NULL,
        AddOnCode       NVARCHAR(50)     NOT NULL,
        Name            NVARCHAR(200)    NOT NULL,
        Quantity        INT              NOT NULL DEFAULT 1,
        UnitPrice       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalPrice      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Notes           NVARCHAR(500)    NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_ReservationAddOns PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ReservationAddOns_Reservations FOREIGN KEY (ReservationId) REFERENCES Reservations(Id) ON DELETE CASCADE
    );
END
GO

-- =============================================
-- RoomAvailability - Daily availability cache
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoomAvailability')
BEGIN
    CREATE TABLE RoomAvailability (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        RoomTypeId      UNIQUEIDENTIFIER NOT NULL,
        [Date]          DATE             NOT NULL,
        TotalRooms      INT              NOT NULL DEFAULT 0,
        SoldRooms       INT              NOT NULL DEFAULT 0,
        BlockedRooms    INT              NOT NULL DEFAULT 0,
        AvailableRooms  AS (TotalRooms - SoldRooms - BlockedRooms) PERSISTED,
        IsClosed        BIT              NOT NULL DEFAULT 0,
        MinStay         INT              NOT NULL DEFAULT 1,
        MaxStay         INT              NOT NULL DEFAULT 365,
        ClosedToArrival BIT              NOT NULL DEFAULT 0,
        ClosedToDeparture BIT            NOT NULL DEFAULT 0,
        UpdatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_RoomAvailability PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RoomAvailability_RoomTypes FOREIGN KEY (RoomTypeId) REFERENCES RoomTypes(Id),
        CONSTRAINT UQ_RoomAvailability_Unique UNIQUE (RoomTypeId, [Date])
    );

    CREATE NONCLUSTERED INDEX IX_RoomAvailability_Date ON RoomAvailability ([Date]) INCLUDE (RoomTypeId, AvailableRooms, IsClosed);
END
GO

-- =============================================
-- GuestFolios - Guest charges during stay
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'GuestFolios')
BEGIN
    CREATE TABLE GuestFolios (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        ReservationId   UNIQUEIDENTIFIER NOT NULL,
        FolioNumber     NVARCHAR(20)     NOT NULL,
        Status          NVARCHAR(50)     NOT NULL DEFAULT 'Open',
        GuestName       NVARCHAR(400)    NOT NULL,
        TotalCharges    DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalPayments   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Balance         AS (TotalCharges - TotalPayments) PERSISTED,
        ClosedAt        DATETIME2        NULL,
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt       DATETIME2        NULL,
        CONSTRAINT PK_GuestFolios PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_GuestFolios_Reservations FOREIGN KEY (ReservationId) REFERENCES Reservations(Id),
        CONSTRAINT UQ_GuestFolios_Number UNIQUE (FolioNumber)
    );
END
GO

-- =============================================
-- FolioCharges - Individual charge line items
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FolioCharges')
BEGIN
    CREATE TABLE FolioCharges (
        Id              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        FolioId         UNIQUEIDENTIFIER NOT NULL,
        ChargeDate      DATE             NOT NULL,
        Category        NVARCHAR(100)    NOT NULL,
        Description     NVARCHAR(500)    NOT NULL,
        Quantity        DECIMAL(10,2)    NOT NULL DEFAULT 1,
        UnitPrice       DECIMAL(18,2)    NOT NULL,
        Amount          DECIMAL(18,2)    NOT NULL,
        TaxAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        ServiceCharge   DECIMAL(18,2)    NOT NULL DEFAULT 0,
        IsVoided        BIT              NOT NULL DEFAULT 0,
        VoidedBy        NVARCHAR(200)    NULL,
        VoidedReason    NVARCHAR(500)    NULL,
        PostedBy        NVARCHAR(200)    NOT NULL DEFAULT 'System',
        CreatedAt       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT PK_FolioCharges PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_FolioCharges_GuestFolios FOREIGN KEY (FolioId) REFERENCES GuestFolios(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_FolioCharges_FolioId ON FolioCharges (FolioId) INCLUDE (ChargeDate, Amount, IsVoided);
END
GO
