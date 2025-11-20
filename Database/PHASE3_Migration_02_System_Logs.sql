-- =============================================
-- PHASE 3 - Accounting System Improvements
-- Migration 02: System Logging Infrastructure
-- Date: 2025-11-06
-- Description:
--   1. Create System_Logs table for comprehensive error and audit logging
--   2. Add indexes for performance
--   3. Create log cleanup stored procedure
-- =============================================

BEGIN TRANSACTION;

PRINT 'Starting PHASE3_Migration_02: System Logging Infrastructure...'

-- =============================================
-- 1. CREATE SYSTEM_LOGS TABLE
-- =============================================

PRINT '1. Creating System_Logs table...'

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[System_Logs] (
        [ID] BIGINT IDENTITY(1,1) NOT NULL,
        [CreatedDate] DATETIME NOT NULL DEFAULT GETDATE(),
        [LogLevel] INT NOT NULL,                        -- 1=Debug, 2=Info, 3=Warning, 4=Error, 5=Critical
        [Category] INT NOT NULL DEFAULT 0,              -- 0=General, 1=Accounting, 2=Payment, 3=Receipt, 4=Revenue, 5=Reconciliation, 6=DataIntegrity, 7=Performance
        [Message] NVARCHAR(500) NOT NULL,
        [Details] NVARCHAR(MAX) NULL,
        [StackTrace] NVARCHAR(MAX) NULL,
        [AffectedTable] NVARCHAR(100) NULL,
        [UserID] INT NULL,
        [ReservationID] BIGINT NULL,
        [ReceiptID] NVARCHAR(50) NULL,

        CONSTRAINT [PK_System_Logs] PRIMARY KEY CLUSTERED ([ID] ASC),
        CONSTRAINT [FK_System_Logs_User] FOREIGN KEY ([UserID])
            REFERENCES [dbo].[Users]([ID]),
        CONSTRAINT [FK_System_Logs_Reservation] FOREIGN KEY ([ReservationID])
            REFERENCES [dbo].[Reservation]([ID])
    );

    PRINT '   ✓ System_Logs table created successfully'
END
ELSE
BEGIN
    PRINT '   → System_Logs table already exists'
END
GO

-- =============================================
-- 2. CREATE INDEXES FOR PERFORMANCE
-- =============================================

PRINT '2. Creating indexes on System_Logs...'

-- Index for date-based queries
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]')
               AND name = N'IX_System_Logs_CreatedDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_System_Logs_CreatedDate]
        ON [dbo].[System_Logs] ([CreatedDate] DESC)
        INCLUDE ([LogLevel], [Category], [Message]);

    PRINT '   ✓ Index IX_System_Logs_CreatedDate created'
END
ELSE
BEGIN
    PRINT '   → Index IX_System_Logs_CreatedDate already exists'
END
GO

-- Index for category-based queries
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]')
               AND name = N'IX_System_Logs_Category')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_System_Logs_Category]
        ON [dbo].[System_Logs] ([Category], [LogLevel])
        INCLUDE ([CreatedDate], [Message]);

    PRINT '   ✓ Index IX_System_Logs_Category created'
END
ELSE
BEGIN
    PRINT '   → Index IX_System_Logs_Category already exists'
END
GO

-- Index for error tracking
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]')
               AND name = N'IX_System_Logs_Errors')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_System_Logs_Errors]
        ON [dbo].[System_Logs] ([LogLevel], [CreatedDate] DESC)
        WHERE [LogLevel] >= 4;  -- Only Error and Critical

    PRINT '   ✓ Index IX_System_Logs_Errors created (filtered for errors)'
END
ELSE
BEGIN
    PRINT '   → Index IX_System_Logs_Errors already exists'
END
GO

-- Index for user activity tracking
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]')
               AND name = N'IX_System_Logs_User')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_System_Logs_User]
        ON [dbo].[System_Logs] ([UserID], [CreatedDate] DESC)
        WHERE [UserID] IS NOT NULL;

    PRINT '   ✓ Index IX_System_Logs_User created'
END
ELSE
BEGIN
    PRINT '   → Index IX_System_Logs_User already exists'
END
GO

-- Index for reservation tracking
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[dbo].[System_Logs]')
               AND name = N'IX_System_Logs_Reservation')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_System_Logs_Reservation]
        ON [dbo].[System_Logs] ([ReservationID], [CreatedDate] DESC)
        WHERE [ReservationID] IS NOT NULL;

    PRINT '   ✓ Index IX_System_Logs_Reservation created'
END
ELSE
BEGIN
    PRINT '   → Index IX_System_Logs_Reservation already exists'
END
GO

-- =============================================
-- 3. CREATE LOG CLEANUP STORED PROCEDURE
-- =============================================

PRINT '3. Creating log cleanup stored procedure...'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_CleanupOldLogs]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_CleanupOldLogs];
    PRINT '   → Dropped existing procedure'
END
GO

CREATE PROCEDURE [dbo].[sp_CleanupOldLogs]
    @RetentionDays INT = 90,        -- Keep logs for 90 days by default
    @KeepErrorsDays INT = 180,      -- Keep errors for 180 days
    @KeepCriticalDays INT = 365,    -- Keep critical logs for 1 year
    @BatchSize INT = 1000           -- Delete in batches to avoid blocking
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @RowsAffected INT = 0;
    DECLARE @TotalDeleted INT = 0;
    DECLARE @CutoffDate DATETIME;
    DECLARE @ErrorCutoffDate DATETIME;
    DECLARE @CriticalCutoffDate DATETIME;

    BEGIN TRY
        BEGIN TRANSACTION;

        -- Calculate cutoff dates
        SET @CutoffDate = DATEADD(DAY, -@RetentionDays, GETDATE());
        SET @ErrorCutoffDate = DATEADD(DAY, -@KeepErrorsDays, GETDATE());
        SET @CriticalCutoffDate = DATEADD(DAY, -@KeepCriticalDays, GETDATE());

        PRINT 'Starting log cleanup...'
        PRINT '  General logs older than: ' + CONVERT(VARCHAR, @CutoffDate, 120);
        PRINT '  Error logs older than: ' + CONVERT(VARCHAR, @ErrorCutoffDate, 120);
        PRINT '  Critical logs older than: ' + CONVERT(VARCHAR, @CriticalCutoffDate, 120);

        -- Delete old general logs (Debug, Info, Warning) in batches
        WHILE 1 = 1
        BEGIN
            DELETE TOP (@BatchSize)
            FROM System_Logs
            WHERE CreatedDate < @CutoffDate
              AND LogLevel < 4;  -- Less than Error level

            SET @RowsAffected = @@ROWCOUNT;
            SET @TotalDeleted = @TotalDeleted + @RowsAffected;

            IF @RowsAffected < @BatchSize
                BREAK;

            -- Small delay to avoid blocking
            WAITFOR DELAY '00:00:00.100';
        END

        PRINT '  ✓ Deleted ' + CAST(@TotalDeleted AS VARCHAR) + ' general log records';

        -- Delete old error logs in batches
        SET @RowsAffected = 0;
        SET @TotalDeleted = 0;

        WHILE 1 = 1
        BEGIN
            DELETE TOP (@BatchSize)
            FROM System_Logs
            WHERE CreatedDate < @ErrorCutoffDate
              AND LogLevel = 4;  -- Error level

            SET @RowsAffected = @@ROWCOUNT;
            SET @TotalDeleted = @TotalDeleted + @RowsAffected;

            IF @RowsAffected < @BatchSize
                BREAK;

            WAITFOR DELAY '00:00:00.100';
        END

        PRINT '  ✓ Deleted ' + CAST(@TotalDeleted AS VARCHAR) + ' error log records';

        -- Delete old critical logs in batches
        SET @RowsAffected = 0;
        SET @TotalDeleted = 0;

        WHILE 1 = 1
        BEGIN
            DELETE TOP (@BatchSize)
            FROM System_Logs
            WHERE CreatedDate < @CriticalCutoffDate
              AND LogLevel = 5;  -- Critical level

            SET @RowsAffected = @@ROWCOUNT;
            SET @TotalDeleted = @TotalDeleted + @RowsAffected;

            IF @RowsAffected < @BatchSize
                BREAK;

            WAITFOR DELAY '00:00:00.100';
        END

        PRINT '  ✓ Deleted ' + CAST(@TotalDeleted AS VARCHAR) + ' critical log records';

        COMMIT TRANSACTION;

        PRINT 'Log cleanup completed successfully';

    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();

        RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
    END CATCH
END
GO

PRINT '   ✓ Log cleanup stored procedure created'
GO

-- =============================================
-- 4. CREATE LOG STATISTICS VIEW
-- =============================================

PRINT '4. Creating log statistics view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Log_Statistics]'))
BEGIN
    DROP VIEW [dbo].[vw_Log_Statistics];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Log_Statistics]
AS
SELECT
    CONVERT(DATE, CreatedDate) AS LogDate,
    Category,
    CASE Category
        WHEN 0 THEN 'General'
        WHEN 1 THEN 'Accounting'
        WHEN 2 THEN 'Payment'
        WHEN 3 THEN 'Receipt'
        WHEN 4 THEN 'Revenue'
        WHEN 5 THEN 'Reconciliation'
        WHEN 6 THEN 'DataIntegrity'
        WHEN 7 THEN 'Performance'
        ELSE 'Unknown'
    END AS CategoryName,
    LogLevel,
    CASE LogLevel
        WHEN 1 THEN 'Debug'
        WHEN 2 THEN 'Info'
        WHEN 3 THEN 'Warning'
        WHEN 4 THEN 'Error'
        WHEN 5 THEN 'Critical'
        ELSE 'Unknown'
    END AS LogLevelName,
    COUNT(*) AS LogCount,
    COUNT(DISTINCT UserID) AS UniqueUsers,
    COUNT(DISTINCT ReservationID) AS UniqueReservations
FROM System_Logs
WHERE CreatedDate >= DATEADD(DAY, -30, GETDATE())  -- Last 30 days
GROUP BY
    CONVERT(DATE, CreatedDate),
    Category,
    LogLevel;
GO

PRINT '   ✓ Log statistics view created'
GO

-- =============================================
-- 5. CREATE SQL AGENT JOB FOR LOG CLEANUP (IF AVAILABLE)
-- =============================================

PRINT '5. Checking if SQL Agent is available for scheduled cleanup...'

IF EXISTS (SELECT 1 FROM sys.all_objects WHERE object_id = OBJECT_ID('msdb.dbo.sp_add_job'))
BEGIN
    -- Check if job already exists
    IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'Cleanup_System_Logs')
    BEGIN
        PRINT '   Creating SQL Agent job for automated log cleanup...'

        DECLARE @jobId BINARY(16);

        -- Create the job
        EXEC msdb.dbo.sp_add_job
            @job_name = N'Cleanup_System_Logs',
            @enabled = 1,
            @description = N'Cleans up old System_Logs records to maintain database performance',
            @category_name = N'Database Maintenance',
            @job_id = @jobId OUTPUT;

        -- Add job step
        EXEC msdb.dbo.sp_add_jobstep
            @job_id = @jobId,
            @step_name = N'Execute Cleanup',
            @subsystem = N'TSQL',
            @command = N'EXEC dbo.sp_CleanupOldLogs @RetentionDays = 90, @KeepErrorsDays = 180, @KeepCriticalDays = 365',
            @retry_attempts = 3,
            @retry_interval = 5;

        -- Create schedule (run weekly on Sunday at 2 AM)
        EXEC msdb.dbo.sp_add_schedule
            @schedule_name = N'Weekly_Sunday_2AM',
            @freq_type = 8,  -- Weekly
            @freq_interval = 1,  -- Sunday
            @freq_recurrence_factor = 1,
            @active_start_time = 20000;  -- 2:00 AM

        -- Attach schedule to job
        EXEC msdb.dbo.sp_attach_schedule
            @job_id = @jobId,
            @schedule_name = N'Weekly_Sunday_2AM';

        -- Add job server
        EXEC msdb.dbo.sp_add_jobserver
            @job_id = @jobId,
            @server_name = N'(local)';

        PRINT '   ✓ SQL Agent job created successfully (runs weekly on Sunday at 2 AM)'
    END
    ELSE
    BEGIN
        PRINT '   → SQL Agent job already exists'
    END
END
ELSE
BEGIN
    PRINT '   → SQL Agent not available - you can run sp_CleanupOldLogs manually'
END
GO

-- =============================================
-- COMMIT TRANSACTION
-- =============================================

COMMIT TRANSACTION;

PRINT ''
PRINT '========================================='
PRINT 'PHASE3_Migration_02 completed successfully!'
PRINT '========================================='
PRINT ''
PRINT 'Summary of changes:'
PRINT '  ✓ System_Logs table created for comprehensive logging'
PRINT '  ✓ Performance indexes created for fast log queries'
PRINT '  ✓ Log cleanup stored procedure created (sp_CleanupOldLogs)'
PRINT '  ✓ Log statistics view created (vw_Log_Statistics)'
PRINT '  ✓ SQL Agent job configured for automated cleanup (if available)'
PRINT ''
PRINT 'Log Categories:'
PRINT '  0 = General, 1 = Accounting, 2 = Payment, 3 = Receipt'
PRINT '  4 = Revenue, 5 = Reconciliation, 6 = DataIntegrity, 7 = Performance'
PRINT ''
PRINT 'Log Levels:'
PRINT '  1 = Debug, 2 = Info, 3 = Warning, 4 = Error, 5 = Critical'
PRINT ''
PRINT 'Retention Policy:'
PRINT '  - General logs: 90 days'
PRINT '  - Error logs: 180 days'
PRINT '  - Critical logs: 365 days'
PRINT ''
PRINT 'Manual Cleanup:'
PRINT '  EXEC sp_CleanupOldLogs @RetentionDays = 90, @KeepErrorsDays = 180, @KeepCriticalDays = 365'
PRINT ''
GO
