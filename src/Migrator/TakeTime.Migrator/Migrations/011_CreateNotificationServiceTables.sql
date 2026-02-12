-- =============================================================================
-- Migration 011: Create Notification Service Schema
-- =============================================================================
-- Target Database: TakeTime_Notification_{TenantId} (per-tenant)
-- Description: Creates all tables for the notification microservice
--              including notification logs, templates, and chat messages.
--              Supports Email, SMS, LINE, Telegram, Push, and InApp channels.
-- =============================================================================

-- =============================================
-- NotificationLogs - Delivery audit trail for all notifications
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationLogs')
BEGIN
    CREATE TABLE NotificationLogs (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        Channel             NVARCHAR(50)     NOT NULL,
        Recipient           NVARCHAR(500)    NOT NULL,
        Subject             NVARCHAR(500)    NULL,
        Body                NVARCHAR(MAX)    NOT NULL,
        Status              NVARCHAR(50)     NOT NULL DEFAULT 'Queued',
        SentAt              DATETIME2        NULL,
        ErrorMessage        NVARCHAR(MAX)    NULL,
        RetryCount          INT              NOT NULL DEFAULT 0,
        RelatedEntityType   NVARCHAR(200)    NULL,
        RelatedEntityId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_NotificationLogs PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_NotificationLogs_Channel ON NotificationLogs (Channel) INCLUDE (Status, SentAt);
    CREATE NONCLUSTERED INDEX IX_NotificationLogs_Status ON NotificationLogs (Status) INCLUDE (Channel, RetryCount, CreatedAt);
    CREATE NONCLUSTERED INDEX IX_NotificationLogs_Recipient ON NotificationLogs (Recipient) INCLUDE (Channel, Status, SentAt);
    CREATE NONCLUSTERED INDEX IX_NotificationLogs_RelatedEntity ON NotificationLogs (RelatedEntityType, RelatedEntityId) WHERE RelatedEntityType IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_NotificationLogs_CreatedAt ON NotificationLogs (CreatedAt DESC) INCLUDE (Channel, Status);
    CREATE NONCLUSTERED INDEX IX_NotificationLogs_TenantId ON NotificationLogs (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- NotificationTemplates - Reusable message templates with placeholder support
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'NotificationTemplates')
BEGIN
    CREATE TABLE NotificationTemplates (
        Id                  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId            NVARCHAR(100)    NULL,
        TemplateName        NVARCHAR(200)    NOT NULL,
        Channel             NVARCHAR(50)     NOT NULL,
        Subject             NVARCHAR(500)    NULL,
        Body                NVARCHAR(MAX)    NOT NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        Language            NVARCHAR(10)     NOT NULL DEFAULT 'th',
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        DeletedAt           DATETIME2        NULL,
        DeletedBy           NVARCHAR(200)    NULL,
        CreatedAt           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy           NVARCHAR(200)    NULL,
        UpdatedAt           DATETIME2        NULL,
        UpdatedBy           NVARCHAR(200)    NULL,
        CONSTRAINT PK_NotificationTemplates PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_NotificationTemplates_NameChannelLang UNIQUE (TemplateName, Channel, Language)
    );

    CREATE NONCLUSTERED INDEX IX_NotificationTemplates_TemplateName ON NotificationTemplates (TemplateName) INCLUDE (Channel, Language, IsActive);
    CREATE NONCLUSTERED INDEX IX_NotificationTemplates_Channel ON NotificationTemplates (Channel) INCLUDE (TemplateName, IsActive);
    CREATE NONCLUSTERED INDEX IX_NotificationTemplates_TenantId ON NotificationTemplates (TenantId) WHERE TenantId IS NOT NULL;
END
GO

-- =============================================
-- ChatMessages - Guest-staff in-app chat conversations
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ChatMessages')
BEGIN
    CREATE TABLE ChatMessages (
        Id                      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        TenantId                NVARCHAR(100)    NULL,
        RoomNumber              NVARCHAR(20)     NOT NULL,
        SenderType              NVARCHAR(20)     NOT NULL,
        SenderName              NVARCHAR(200)    NOT NULL,
        [Message]               NVARCHAR(MAX)    NOT NULL,
        IsRead                  BIT              NOT NULL DEFAULT 0,
        ReadAt                  DATETIME2        NULL,
        IsConversationActive    BIT              NOT NULL DEFAULT 1,
        IsDeleted               BIT              NOT NULL DEFAULT 0,
        DeletedAt               DATETIME2        NULL,
        DeletedBy               NVARCHAR(200)    NULL,
        CreatedAt               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy               NVARCHAR(200)    NULL,
        UpdatedAt               DATETIME2        NULL,
        UpdatedBy               NVARCHAR(200)    NULL,
        CONSTRAINT PK_ChatMessages PRIMARY KEY CLUSTERED (Id)
    );

    CREATE NONCLUSTERED INDEX IX_ChatMessages_RoomNumber ON ChatMessages (RoomNumber, CreatedAt DESC) INCLUDE (SenderType, IsRead);
    CREATE NONCLUSTERED INDEX IX_ChatMessages_IsRead ON ChatMessages (IsRead) WHERE IsRead = 0;
    CREATE NONCLUSTERED INDEX IX_ChatMessages_ConversationActive ON ChatMessages (IsConversationActive, RoomNumber) WHERE IsConversationActive = 1;
    CREATE NONCLUSTERED INDEX IX_ChatMessages_TenantId ON ChatMessages (TenantId) WHERE TenantId IS NOT NULL;
END
GO
