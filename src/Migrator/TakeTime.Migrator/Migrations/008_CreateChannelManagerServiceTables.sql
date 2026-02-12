-- =============================================================================
-- Migration 008: Create Channel Manager Service Schema
-- =============================================================================
-- Target Database: TakeTime_ChannelManager_{TenantId} (per-tenant)
-- Description: Creates all tables for the channel manager microservice
--              including OTA connections, room mappings, imported reservations,
--              and rate/availability update logs.
-- =============================================================================

-- =============================================
-- ChannelConnections - OTA channel integrations (Agoda, Booking.com, Expedia, etc.)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChannelConnections')
BEGIN
    CREATE TABLE ChannelConnections (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        ChannelType         NVARCHAR(100)    NOT NULL,
        PropertyId          NVARCHAR(200)    NOT NULL,
        ApiKey              NVARCHAR(500)    NULL,
        ApiSecret           NVARCHAR(500)    NULL,
        HotelCode           NVARCHAR(100)    NULL,
        ChannelHotelId      NVARCHAR(200)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        LastSyncAt          DATETIME2        NULL,
        SyncStatus          NVARCHAR(50)     NOT NULL DEFAULT 'Idle',
        LastSyncError       NVARCHAR(MAX)    NULL,
        [Version]           INT              NOT NULL DEFAULT 0,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_ChannelConnections PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ChannelConnections_ChannelType ON ChannelConnections (ChannelType) INCLUDE (IsActive, SyncStatus);
    CREATE NONCLUSTERED INDEX IX_ChannelConnections_IsActive ON ChannelConnections (IsActive) WHERE IsDeleted = 0;
    CREATE NONCLUSTERED INDEX IX_ChannelConnections_TenantId ON ChannelConnections (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- ChannelRoomMappings - Maps local room types to OTA room types
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChannelRoomMappings')
BEGIN
    CREATE TABLE ChannelRoomMappings (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                NVARCHAR(100)    NULL,
        ChannelConnectionId     UNIQUEIDENTIFIER NOT NULL,
        LocalAccommodationId    UNIQUEIDENTIFIER NOT NULL,
        ChannelRoomTypeId       NVARCHAR(200)    NOT NULL,
        ChannelRoomTypeName     NVARCHAR(500)    NOT NULL,
        ChannelRatePlanId       NVARCHAR(200)    NULL,
        IsActive                BIT              NOT NULL DEFAULT 1,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(200)    NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(200)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(200)    NULL,
        CONSTRAINT PK_ChannelRoomMappings PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ChannelRoomMappings_ChannelConnections FOREIGN KEY (ChannelConnectionId) REFERENCES ChannelConnections(Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ChannelRoomMappings_ConnectionId ON ChannelRoomMappings (ChannelConnectionId) INCLUDE (IsActive);
    CREATE NONCLUSTERED INDEX IX_ChannelRoomMappings_LocalAccommodation ON ChannelRoomMappings (LocalAccommodationId);
END
GO

-- =============================================
-- ChannelReservations - Reservations imported from OTA channels
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChannelReservations')
BEGIN
    CREATE TABLE ChannelReservations (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                NVARCHAR(100)    NULL,
        ChannelConnectionId     UNIQUEIDENTIFIER NOT NULL,
        ChannelBookingId        NVARCHAR(200)    NOT NULL,
        LocalReservationId      UNIQUEIDENTIFIER NULL,
        GuestName               NVARCHAR(400)    NOT NULL,
        GuestEmail              NVARCHAR(500)    NULL,
        CheckIn                 DATETIME2        NOT NULL,
        CheckOut                DATETIME2        NOT NULL,
        RoomType                NVARCHAR(200)    NULL,
        TotalAmount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'New',
        RawData                 NVARCHAR(MAX)    NULL,
        ImportedAt              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        SyncedAt                DATETIME2        NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(200)    NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(200)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(200)    NULL,
        CONSTRAINT PK_ChannelReservations PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ChannelReservations_ChannelConnections FOREIGN KEY (ChannelConnectionId) REFERENCES ChannelConnections(Id)
    );

    CREATE NONCLUSTERED INDEX IX_ChannelReservations_ConnectionId ON ChannelReservations (ChannelConnectionId);
    CREATE NONCLUSTERED INDEX IX_ChannelReservations_ChannelBookingId ON ChannelReservations (ChannelBookingId);
    CREATE NONCLUSTERED INDEX IX_ChannelReservations_Status ON ChannelReservations (Status) INCLUDE (ChannelBookingId, ImportedAt);
    CREATE NONCLUSTERED INDEX IX_ChannelReservations_CheckIn ON ChannelReservations (CheckIn, CheckOut) INCLUDE (Status);
    CREATE NONCLUSTERED INDEX IX_ChannelReservations_LocalReservation ON ChannelReservations (LocalReservationId) WHERE LocalReservationId IS NOT NULL;
END
GO

-- =============================================
-- RateUpdates - Rate and availability updates sent to OTA channels
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RateUpdates')
BEGIN
    CREATE TABLE RateUpdates (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                NVARCHAR(100)    NULL,
        ChannelConnectionId     UNIQUEIDENTIFIER NOT NULL,
        AccommodationId         UNIQUEIDENTIFIER NOT NULL,
        StartDate               DATETIME2        NOT NULL,
        EndDate                 DATETIME2        NOT NULL,
        Rate                    DECIMAL(18,2)    NOT NULL,
        Currency                NVARCHAR(3)      NOT NULL DEFAULT 'THB',
        Availability            INT              NOT NULL DEFAULT 0,
        MinStay                 INT              NULL,
        MaxStay                 INT              NULL,
        Status                  NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        SentAt                  DATETIME2        NULL,
        ConfirmedAt             DATETIME2        NULL,
        ErrorMessage            NVARCHAR(MAX)    NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(200)    NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(200)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(200)    NULL,
        CONSTRAINT PK_RateUpdates PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_RateUpdates_ChannelConnections FOREIGN KEY (ChannelConnectionId) REFERENCES ChannelConnections(Id)
    );

    CREATE NONCLUSTERED INDEX IX_RateUpdates_ConnectionId ON RateUpdates (ChannelConnectionId);
    CREATE NONCLUSTERED INDEX IX_RateUpdates_Status ON RateUpdates (Status) INCLUDE (ChannelConnectionId, SentAt);
    CREATE NONCLUSTERED INDEX IX_RateUpdates_Dates ON RateUpdates (StartDate, EndDate) INCLUDE (Rate, Availability);
END
GO
