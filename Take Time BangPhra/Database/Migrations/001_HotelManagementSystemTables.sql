-- =============================================
-- TakeTime BangPhra Hotel Management System
-- Database Migration Script v1.0
-- Date: 2025-12-27
-- Description: Create all necessary tables for:
--   - Channel Manager (OTA Integration)
--   - Dynamic Pricing & Revenue Management
--   - Housekeeping Management
--   - Maintenance Request System
--   - Notification System
--   - Advanced Analytics Support
-- =============================================

-- =============================================
-- SECTION 1: CHANNEL MANAGER TABLES
-- =============================================

-- Channel Configuration
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChannelConfigurations' AND xtype='U')
BEGIN
    CREATE TABLE ChannelConfigurations (
        ChannelId INT IDENTITY(1,1) PRIMARY KEY,
        ChannelCode NVARCHAR(50) NOT NULL UNIQUE,
        ChannelName NVARCHAR(100) NOT NULL,
        ApiEndpoint NVARCHAR(500),
        ApiKey NVARCHAR(255),
        ApiSecret NVARCHAR(255),
        HotelCode NVARCHAR(100),
        IsActive BIT DEFAULT 1,
        SyncEnabled BIT DEFAULT 1,
        LastSyncDate DATETIME,
        SyncFrequencyMinutes INT DEFAULT 15,
        CommissionRate DECIMAL(5,2) DEFAULT 0,
        Settings NVARCHAR(MAX), -- JSON for additional settings
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );

    -- Insert default channels
    INSERT INTO ChannelConfigurations (ChannelCode, ChannelName, CommissionRate)
    VALUES
        ('AGODA', 'Agoda', 15.00),
        ('BOOKING', 'Booking.com', 15.00),
        ('EXPEDIA', 'Expedia', 18.00),
        ('DIRECT', 'Direct Booking', 0.00);
END
GO

-- Channel Room Mapping
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChannelRoomMappings' AND xtype='U')
BEGIN
    CREATE TABLE ChannelRoomMappings (
        MappingId INT IDENTITY(1,1) PRIMARY KEY,
        ChannelId INT NOT NULL,
        LocalAccommodationId INT NOT NULL,
        ChannelRoomId NVARCHAR(100),
        ChannelRoomName NVARCHAR(200),
        ChannelRateId NVARCHAR(100),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (ChannelId) REFERENCES ChannelConfigurations(ChannelId)
    );
END
GO

-- Channel Sync Log
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ChannelSyncLogs' AND xtype='U')
BEGIN
    CREATE TABLE ChannelSyncLogs (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        ChannelId INT NOT NULL,
        SyncType NVARCHAR(50) NOT NULL, -- AVAILABILITY, RATES, RESERVATIONS
        SyncDirection NVARCHAR(20) NOT NULL, -- PUSH, PULL
        Status NVARCHAR(20) NOT NULL, -- SUCCESS, FAILED, PARTIAL
        RecordsProcessed INT DEFAULT 0,
        RecordsFailed INT DEFAULT 0,
        StartTime DATETIME NOT NULL,
        EndTime DATETIME,
        ErrorMessage NVARCHAR(MAX),
        RequestPayload NVARCHAR(MAX),
        ResponsePayload NVARCHAR(MAX),
        FOREIGN KEY (ChannelId) REFERENCES ChannelConfigurations(ChannelId)
    );
END
GO

-- OTA Reservations (for tracking imported bookings)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='OTAReservations' AND xtype='U')
BEGIN
    CREATE TABLE OTAReservations (
        OTAReservationId INT IDENTITY(1,1) PRIMARY KEY,
        ChannelId INT NOT NULL,
        OTABookingCode NVARCHAR(100) NOT NULL,
        LocalReservationId INT,
        GuestName NVARCHAR(200),
        GuestEmail NVARCHAR(200),
        GuestPhone NVARCHAR(50),
        CheckInDate DATE NOT NULL,
        CheckOutDate DATE NOT NULL,
        RoomNights INT,
        TotalPrice DECIMAL(18,2),
        CommissionAmount DECIMAL(18,2),
        NetRevenue DECIMAL(18,2),
        Currency NVARCHAR(10) DEFAULT 'THB',
        Status NVARCHAR(50),
        BookingDate DATETIME,
        SpecialRequests NVARCHAR(MAX),
        RawData NVARCHAR(MAX), -- JSON of original OTA data
        ImportedAt DATETIME DEFAULT GETDATE(),
        ProcessedAt DATETIME,
        FOREIGN KEY (ChannelId) REFERENCES ChannelConfigurations(ChannelId)
    );

    CREATE INDEX IX_OTAReservations_ChannelBooking ON OTAReservations(ChannelId, OTABookingCode);
END
GO

-- =============================================
-- SECTION 2: DYNAMIC PRICING TABLES
-- =============================================

-- Pricing Rules
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PricingRules' AND xtype='U')
BEGIN
    CREATE TABLE PricingRules (
        RuleId INT IDENTITY(1,1) PRIMARY KEY,
        RuleName NVARCHAR(100) NOT NULL,
        RuleType NVARCHAR(50) NOT NULL, -- OCCUPANCY, SEASONAL, DEMAND, DAY_OF_WEEK, LEAD_TIME
        AccommodationTypeId INT, -- NULL = applies to all
        MinThreshold DECIMAL(10,2),
        MaxThreshold DECIMAL(10,2),
        Multiplier DECIMAL(5,2) DEFAULT 1.00,
        Priority INT DEFAULT 100,
        IsActive BIT DEFAULT 1,
        StartDate DATE,
        EndDate DATE,
        DaysOfWeek NVARCHAR(20), -- e.g., '1,2,3,4,5,6,7' for all days
        CreatedAt DATETIME DEFAULT GETDATE(),
        CreatedBy INT,
        UpdatedAt DATETIME
    );

    -- Insert default rules
    INSERT INTO PricingRules (RuleName, RuleType, MinThreshold, MaxThreshold, Multiplier, Priority)
    VALUES
        ('Low Occupancy Discount', 'OCCUPANCY', 0, 30, 0.85, 100),
        ('Medium Occupancy', 'OCCUPANCY', 30, 60, 1.00, 100),
        ('High Occupancy Premium', 'OCCUPANCY', 60, 80, 1.15, 100),
        ('Very High Occupancy', 'OCCUPANCY', 80, 100, 1.30, 100),
        ('Weekend Premium', 'DAY_OF_WEEK', NULL, NULL, 1.20, 90),
        ('Last Minute Discount', 'LEAD_TIME', 0, 3, 0.90, 80),
        ('Early Bird Discount', 'LEAD_TIME', 30, 90, 0.95, 80);
END
GO

-- Pricing Seasons
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PricingSeasons' AND xtype='U')
BEGIN
    CREATE TABLE PricingSeasons (
        SeasonId INT IDENTITY(1,1) PRIMARY KEY,
        SeasonName NVARCHAR(100) NOT NULL,
        SeasonType NVARCHAR(50) NOT NULL, -- PEAK, HIGH, REGULAR, LOW
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        Multiplier DECIMAL(5,2) DEFAULT 1.00,
        Description NVARCHAR(500),
        IsRecurring BIT DEFAULT 0,
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE()
    );

    -- Insert Thai seasons/holidays
    INSERT INTO PricingSeasons (SeasonName, SeasonType, StartDate, EndDate, Multiplier, Description)
    VALUES
        ('Songkran Festival 2025', 'PEAK', '2025-04-11', '2025-04-16', 1.50, 'Thai New Year'),
        ('New Year 2025', 'PEAK', '2024-12-28', '2025-01-02', 1.60, 'New Year Holiday'),
        ('High Season', 'HIGH', '2024-11-01', '2025-02-28', 1.25, 'Tourist High Season'),
        ('Low Season', 'LOW', '2025-05-01', '2025-10-31', 0.85, 'Rainy Season');
END
GO

-- Price History Log
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PriceHistoryLog' AND xtype='U')
BEGIN
    CREATE TABLE PriceHistoryLog (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        AccommodationId INT NOT NULL,
        StayDate DATE NOT NULL,
        BasePrice DECIMAL(18,2),
        FinalPrice DECIMAL(18,2),
        AppliedMultiplier DECIMAL(5,2),
        AppliedRules NVARCHAR(500), -- JSON array of applied rule IDs
        CalculatedAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_PriceHistory_AccommodationDate ON PriceHistoryLog(AccommodationId, StayDate);
END
GO

-- =============================================
-- SECTION 3: HOUSEKEEPING TABLES
-- =============================================

-- Room Status
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RoomStatusHistory' AND xtype='U')
BEGIN
    CREATE TABLE RoomStatusHistory (
        StatusId INT IDENTITY(1,1) PRIMARY KEY,
        AccommodationId INT NOT NULL,
        Status NVARCHAR(50) NOT NULL, -- VACANT_CLEAN, VACANT_DIRTY, OCCUPIED, CLEANING, INSPECTING, OUT_OF_ORDER, MAINTENANCE
        PreviousStatus NVARCHAR(50),
        ChangedBy INT,
        ChangedAt DATETIME DEFAULT GETDATE(),
        Notes NVARCHAR(500)
    );

    CREATE INDEX IX_RoomStatus_Accommodation ON RoomStatusHistory(AccommodationId, ChangedAt DESC);
END
GO

-- Add Status column to Accommodations if not exists
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accommodations') AND name = 'HousekeepingStatus')
BEGIN
    ALTER TABLE Accommodations ADD HousekeepingStatus NVARCHAR(50) DEFAULT 'VACANT_CLEAN';
END
GO

-- Housekeeping Tasks
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HousekeepingTasks' AND xtype='U')
BEGIN
    CREATE TABLE HousekeepingTasks (
        TaskId INT IDENTITY(1,1) PRIMARY KEY,
        TaskNumber NVARCHAR(50) NOT NULL UNIQUE,
        AccommodationId INT NOT NULL,
        TaskType NVARCHAR(50) NOT NULL, -- CHECKOUT_CLEAN, STAY_OVER, DEEP_CLEAN, TURNDOWN, INSPECTION
        Priority NVARCHAR(20) DEFAULT 'NORMAL', -- URGENT, HIGH, NORMAL, LOW
        Status NVARCHAR(30) DEFAULT 'PENDING', -- PENDING, ASSIGNED, IN_PROGRESS, COMPLETED, VERIFIED, FAILED
        AssignedTo INT,
        AssignedAt DATETIME,
        StartedAt DATETIME,
        CompletedAt DATETIME,
        VerifiedBy INT,
        VerifiedAt DATETIME,
        EstimatedMinutes INT DEFAULT 30,
        ActualMinutes INT,
        Notes NVARCHAR(MAX),
        ChecklistCompleted BIT DEFAULT 0,
        CreatedAt DATETIME DEFAULT GETDATE(),
        DueDate DATETIME
    );

    CREATE INDEX IX_HousekeepingTasks_Status ON HousekeepingTasks(Status, AssignedTo);
    CREATE INDEX IX_HousekeepingTasks_Accommodation ON HousekeepingTasks(AccommodationId, CreatedAt DESC);
END
GO

-- Housekeeping Checklist Items
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HousekeepingChecklistItems' AND xtype='U')
BEGIN
    CREATE TABLE HousekeepingChecklistItems (
        ItemId INT IDENTITY(1,1) PRIMARY KEY,
        TaskId INT NOT NULL,
        ChecklistItemName NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100), -- BEDROOM, BATHROOM, AMENITIES, GENERAL
        IsCompleted BIT DEFAULT 0,
        CompletedAt DATETIME,
        Notes NVARCHAR(500),
        FOREIGN KEY (TaskId) REFERENCES HousekeepingTasks(TaskId)
    );
END
GO

-- Housekeeping Staff Schedule
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HousekeepingSchedule' AND xtype='U')
BEGIN
    CREATE TABLE HousekeepingSchedule (
        ScheduleId INT IDENTITY(1,1) PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ShiftDate DATE NOT NULL,
        ShiftStart TIME NOT NULL,
        ShiftEnd TIME NOT NULL,
        AssignedFloor NVARCHAR(50),
        AssignedZone NVARCHAR(100),
        Status NVARCHAR(30) DEFAULT 'SCHEDULED', -- SCHEDULED, CHECKED_IN, CHECKED_OUT, ABSENT
        CheckInTime DATETIME,
        CheckOutTime DATETIME,
        Notes NVARCHAR(500)
    );

    CREATE INDEX IX_HousekeepingSchedule_Date ON HousekeepingSchedule(ShiftDate, EmployeeId);
END
GO

-- Housekeeping Supplies Inventory
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='HousekeepingSupplies' AND xtype='U')
BEGIN
    CREATE TABLE HousekeepingSupplies (
        SupplyId INT IDENTITY(1,1) PRIMARY KEY,
        SupplyCode NVARCHAR(50) NOT NULL UNIQUE,
        SupplyName NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100), -- LINENS, TOILETRIES, CLEANING, AMENITIES
        Unit NVARCHAR(20),
        CurrentStock INT DEFAULT 0,
        MinimumStock INT DEFAULT 10,
        ReorderLevel INT DEFAULT 20,
        UnitCost DECIMAL(18,2),
        LastRestockDate DATETIME,
        IsActive BIT DEFAULT 1
    );

    -- Insert common supplies
    INSERT INTO HousekeepingSupplies (SupplyCode, SupplyName, Category, Unit, MinimumStock, ReorderLevel)
    VALUES
        ('LIN-TOWEL-BATH', 'Bath Towel', 'LINENS', 'piece', 50, 100),
        ('LIN-TOWEL-HAND', 'Hand Towel', 'LINENS', 'piece', 50, 100),
        ('LIN-SHEET-KING', 'Bed Sheet King Size', 'LINENS', 'set', 20, 40),
        ('LIN-SHEET-TWIN', 'Bed Sheet Twin Size', 'LINENS', 'set', 20, 40),
        ('TOI-SHAMPOO', 'Shampoo Bottle', 'TOILETRIES', 'bottle', 100, 200),
        ('TOI-SOAP', 'Soap Bar', 'TOILETRIES', 'piece', 100, 200),
        ('TOI-TISSUE', 'Tissue Box', 'TOILETRIES', 'box', 50, 100),
        ('CLN-DISINFECT', 'Disinfectant Spray', 'CLEANING', 'bottle', 20, 40),
        ('AMN-WATER', 'Drinking Water', 'AMENITIES', 'bottle', 100, 200),
        ('AMN-COFFEE', 'Coffee Sachet', 'AMENITIES', 'pack', 50, 100);
END
GO

-- =============================================
-- SECTION 4: MAINTENANCE REQUEST TABLES
-- =============================================

-- Maintenance Requests
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MaintenanceRequests' AND xtype='U')
BEGIN
    CREATE TABLE MaintenanceRequests (
        RequestId INT IDENTITY(1,1) PRIMARY KEY,
        RequestNumber NVARCHAR(50) NOT NULL UNIQUE,
        LocationType NVARCHAR(50) NOT NULL, -- ROOM, COMMON_AREA, FACILITY, EQUIPMENT
        LocationId INT NOT NULL,
        LocationName NVARCHAR(200),
        Category NVARCHAR(50) NOT NULL, -- PLUMBING, ELECTRICAL, HVAC, FURNITURE, APPLIANCE, STRUCTURAL, OTHER
        Priority NVARCHAR(20) DEFAULT 'MEDIUM', -- EMERGENCY, HIGH, MEDIUM, LOW
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(MAX),
        ReportedBy INT NOT NULL,
        ReportedByType NVARCHAR(20) NOT NULL, -- GUEST, STAFF, SYSTEM
        RequestDate DATETIME DEFAULT GETDATE(),
        Status NVARCHAR(30) DEFAULT 'PENDING', -- PENDING, ASSIGNED, IN_PROGRESS, ON_HOLD, COMPLETED, CANCELLED
        AssignedTo INT,
        AssignedDate DATETIME,
        StartedDate DATETIME,
        CompletedDate DATETIME,
        EstimatedCost DECIMAL(18,2) DEFAULT 0,
        ActualCost DECIMAL(18,2) DEFAULT 0,
        Notes NVARCHAR(MAX),
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );

    CREATE INDEX IX_MaintenanceRequests_Status ON MaintenanceRequests(Status, Priority);
    CREATE INDEX IX_MaintenanceRequests_Location ON MaintenanceRequests(LocationType, LocationId);
END
GO

-- Maintenance Status Log
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MaintenanceStatusLog' AND xtype='U')
BEGIN
    CREATE TABLE MaintenanceStatusLog (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        RequestId INT NOT NULL,
        FromStatus NVARCHAR(30),
        ToStatus NVARCHAR(30) NOT NULL,
        ChangedBy INT NOT NULL,
        ChangedAt DATETIME DEFAULT GETDATE(),
        Notes NVARCHAR(500),
        FOREIGN KEY (RequestId) REFERENCES MaintenanceRequests(RequestId)
    );
END
GO

-- Preventive Maintenance Schedules
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PreventiveMaintenanceSchedules' AND xtype='U')
BEGIN
    CREATE TABLE PreventiveMaintenanceSchedules (
        ScheduleId INT IDENTITY(1,1) PRIMARY KEY,
        ScheduleName NVARCHAR(200) NOT NULL,
        LocationType NVARCHAR(50) NOT NULL,
        LocationId INT NOT NULL,
        LocationName NVARCHAR(200),
        Category NVARCHAR(50) NOT NULL,
        Description NVARCHAR(MAX),
        FrequencyType NVARCHAR(20) NOT NULL, -- DAILY, WEEKLY, MONTHLY, QUARTERLY, YEARLY
        FrequencyValue INT DEFAULT 1,
        LastPerformedDate DATETIME,
        NextDueDate DATE NOT NULL,
        EstimatedDuration INT DEFAULT 60, -- minutes
        EstimatedCost DECIMAL(18,2) DEFAULT 0,
        AssignedTeam NVARCHAR(100),
        IsActive BIT DEFAULT 1,
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );

    CREATE INDEX IX_PMSchedule_NextDue ON PreventiveMaintenanceSchedules(NextDueDate, IsActive);
END
GO

-- Preventive Maintenance Request Links
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='PreventiveMaintenanceRequestLinks' AND xtype='U')
BEGIN
    CREATE TABLE PreventiveMaintenanceRequestLinks (
        LinkId INT IDENTITY(1,1) PRIMARY KEY,
        ScheduleId INT NOT NULL,
        RequestId INT NOT NULL,
        CreatedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (ScheduleId) REFERENCES PreventiveMaintenanceSchedules(ScheduleId),
        FOREIGN KEY (RequestId) REFERENCES MaintenanceRequests(RequestId)
    );
END
GO

-- Maintenance Parts Inventory
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MaintenancePartsInventory' AND xtype='U')
BEGIN
    CREATE TABLE MaintenancePartsInventory (
        PartId INT IDENTITY(1,1) PRIMARY KEY,
        PartNumber NVARCHAR(50) NOT NULL UNIQUE,
        PartName NVARCHAR(200) NOT NULL,
        Category NVARCHAR(100),
        Description NVARCHAR(500),
        Unit NVARCHAR(20),
        CurrentStock INT DEFAULT 0,
        ReorderLevel INT DEFAULT 5,
        UnitCost DECIMAL(18,2),
        Supplier NVARCHAR(200),
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );
END
GO

-- Maintenance Parts Used
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='MaintenancePartsUsed' AND xtype='U')
BEGIN
    CREATE TABLE MaintenancePartsUsed (
        UsageId INT IDENTITY(1,1) PRIMARY KEY,
        RequestId INT NOT NULL,
        PartId INT NOT NULL,
        PartName NVARCHAR(200),
        Quantity INT NOT NULL,
        UnitCost DECIMAL(18,2),
        TotalCost DECIMAL(18,2),
        UsedAt DATETIME DEFAULT GETDATE(),
        FOREIGN KEY (RequestId) REFERENCES MaintenanceRequests(RequestId),
        FOREIGN KEY (PartId) REFERENCES MaintenancePartsInventory(PartId)
    );
END
GO

-- =============================================
-- SECTION 5: NOTIFICATION TABLES
-- =============================================

-- Notifications
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Notifications' AND xtype='U')
BEGIN
    CREATE TABLE Notifications (
        NotificationId INT IDENTITY(1,1) PRIMARY KEY,
        UserId INT NOT NULL,
        UserType NVARCHAR(20) NOT NULL, -- EMPLOYEE, CUSTOMER, GUEST
        Title NVARCHAR(200) NOT NULL,
        Message NVARCHAR(MAX) NOT NULL,
        NotificationType NVARCHAR(50) NOT NULL, -- RESERVATION, PAYMENT, HOUSEKEEPING, MAINTENANCE, SYSTEM, PROMOTION
        Priority NVARCHAR(20) DEFAULT 'NORMAL', -- LOW, NORMAL, HIGH, URGENT
        RelatedEntityType NVARCHAR(50),
        RelatedEntityId INT,
        ActionUrl NVARCHAR(500),
        IsRead BIT DEFAULT 0,
        ReadAt DATETIME,
        CreatedAt DATETIME DEFAULT GETDATE(),
        ExpiresAt DATETIME
    );

    CREATE INDEX IX_Notifications_User ON Notifications(UserId, UserType, IsRead);
    CREATE INDEX IX_Notifications_Created ON Notifications(CreatedAt DESC);
END
GO

-- Email Templates
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EmailTemplates' AND xtype='U')
BEGIN
    CREATE TABLE EmailTemplates (
        TemplateId INT IDENTITY(1,1) PRIMARY KEY,
        TemplateName NVARCHAR(100) NOT NULL UNIQUE,
        Subject NVARCHAR(500),
        TemplateBody NVARCHAR(MAX) NOT NULL,
        Language NVARCHAR(10) DEFAULT 'th',
        IsActive BIT DEFAULT 1,
        CreatedAt DATETIME DEFAULT GETDATE(),
        UpdatedAt DATETIME
    );
END
GO

-- Email Logs
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='EmailLogs' AND xtype='U')
BEGIN
    CREATE TABLE EmailLogs (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        ToEmail NVARCHAR(200) NOT NULL,
        Subject NVARCHAR(500),
        Success BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX),
        SentAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_EmailLogs_Date ON EmailLogs(SentAt DESC);
END
GO

-- SMS Logs
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SmsLogs' AND xtype='U')
BEGIN
    CREATE TABLE SmsLogs (
        LogId INT IDENTITY(1,1) PRIMARY KEY,
        PhoneNumber NVARCHAR(50) NOT NULL,
        Message NVARCHAR(500),
        Success BIT NOT NULL,
        ErrorMessage NVARCHAR(MAX),
        SentAt DATETIME DEFAULT GETDATE()
    );

    CREATE INDEX IX_SmsLogs_Date ON SmsLogs(SentAt DESC);
END
GO

-- System Settings (for SMTP, SMS, etc.)
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='SystemSettings' AND xtype='U')
BEGIN
    CREATE TABLE SystemSettings (
        SettingId INT IDENTITY(1,1) PRIMARY KEY,
        SettingGroup NVARCHAR(50) NOT NULL,
        SettingKey NVARCHAR(100) NOT NULL,
        SettingValue NVARCHAR(MAX),
        Description NVARCHAR(500),
        IsEncrypted BIT DEFAULT 0,
        UpdatedAt DATETIME DEFAULT GETDATE(),
        UpdatedBy INT,
        UNIQUE(SettingGroup, SettingKey)
    );

    -- Insert default settings
    INSERT INTO SystemSettings (SettingGroup, SettingKey, SettingValue, Description)
    VALUES
        ('SMTP', 'SmtpHost', 'smtp.gmail.com', 'SMTP Server Host'),
        ('SMTP', 'SmtpPort', '587', 'SMTP Server Port'),
        ('SMTP', 'SmtpEnableSsl', 'true', 'Enable SSL for SMTP'),
        ('SMTP', 'SmtpFromEmail', 'noreply@taketime.co.th', 'From Email Address'),
        ('SMTP', 'SmtpFromName', 'TakeTime BangPhra', 'From Display Name'),
        ('HOTEL', 'HotelName', 'TakeTime BangPhra', 'Hotel Name'),
        ('HOTEL', 'HotelAddress', 'บางพระ ศรีราชา ชลบุรี', 'Hotel Address'),
        ('HOTEL', 'HotelPhone', '038-xxx-xxx', 'Hotel Phone'),
        ('HOTEL', 'CheckInTime', '14:00', 'Standard Check-in Time'),
        ('HOTEL', 'CheckOutTime', '12:00', 'Standard Check-out Time');
END
GO

-- =============================================
-- SECTION 6: MODIFY EXISTING TABLES
-- =============================================

-- Add columns to Reservations table if not exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'BookingSource')
BEGIN
    ALTER TABLE Reservations ADD BookingSource NVARCHAR(50);
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'OTABookingCode')
BEGIN
    ALTER TABLE Reservations ADD OTABookingCode NVARCHAR(100);
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'ReminderSent')
BEGIN
    ALTER TABLE Reservations ADD ReminderSent BIT DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'FeedbackRequestSent')
BEGIN
    ALTER TABLE Reservations ADD FeedbackRequestSent BIT DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'DynamicPriceApplied')
BEGIN
    ALTER TABLE Reservations ADD DynamicPriceApplied BIT DEFAULT 0;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Reservations') AND name = 'PriceMultiplier')
BEGIN
    ALTER TABLE Reservations ADD PriceMultiplier DECIMAL(5,2) DEFAULT 1.00;
END
GO

-- Add columns to Accommodations table
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accommodations') AND name = 'MinOccupancyRate')
BEGIN
    ALTER TABLE Accommodations ADD MinOccupancyRate DECIMAL(5,2) DEFAULT 0.85;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accommodations') AND name = 'MaxOccupancyRate')
BEGIN
    ALTER TABLE Accommodations ADD MaxOccupancyRate DECIMAL(5,2) DEFAULT 1.50;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accommodations') AND name = 'LastCleanedAt')
BEGIN
    ALTER TABLE Accommodations ADD LastCleanedAt DATETIME;
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('Accommodations') AND name = 'LastInspectedAt')
BEGIN
    ALTER TABLE Accommodations ADD LastInspectedAt DATETIME;
END
GO

-- =============================================
-- SECTION 7: CREATE VIEWS FOR ANALYTICS
-- =============================================

-- Daily Revenue View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_DailyRevenue')
    DROP VIEW vw_DailyRevenue;
GO

CREATE VIEW vw_DailyRevenue AS
SELECT
    CAST(CheckInDate AS DATE) as RevenueDate,
    COUNT(*) as Bookings,
    SUM(TotalPrice) as Revenue,
    AVG(TotalPrice) as AvgBookingValue,
    SUM(DATEDIFF(day, CheckInDate, CheckOutDate)) as RoomNights
FROM Reservations
WHERE Status NOT IN ('Cancelled')
GROUP BY CAST(CheckInDate AS DATE);
GO

-- Room Occupancy View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_RoomOccupancy')
    DROP VIEW vw_RoomOccupancy;
GO

CREATE VIEW vw_RoomOccupancy AS
SELECT
    a.Id as AccommodationId,
    a.AccommodationName,
    a.AccommodationType,
    a.HousekeepingStatus,
    (SELECT COUNT(*) FROM Reservations r
     WHERE r.AccommodationId = a.Id
     AND GETDATE() BETWEEN r.CheckInDate AND r.CheckOutDate
     AND r.Status NOT IN ('Cancelled')) as IsCurrentlyOccupied,
    (SELECT MAX(CheckOutDate) FROM Reservations r
     WHERE r.AccommodationId = a.Id
     AND r.Status NOT IN ('Cancelled')) as LastCheckout
FROM Accommodations a
WHERE a.IsActive = 1;
GO

-- Maintenance Summary View
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_MaintenanceSummary')
    DROP VIEW vw_MaintenanceSummary;
GO

CREATE VIEW vw_MaintenanceSummary AS
SELECT
    Status,
    Priority,
    Category,
    COUNT(*) as RequestCount,
    SUM(EstimatedCost) as TotalEstimatedCost,
    SUM(ActualCost) as TotalActualCost,
    AVG(DATEDIFF(hour, RequestDate, CompletedDate)) as AvgResolutionHours
FROM MaintenanceRequests
GROUP BY Status, Priority, Category;
GO

PRINT 'Database migration completed successfully!';
GO
