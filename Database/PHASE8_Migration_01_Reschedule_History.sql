-- ============================================================
-- PHASE 8: Reservation Reschedule History System
-- ============================================================
-- สร้างตาราง Reservation_Reschedule_History
-- เพื่อเก็บประวัติการเลื่อนวันเข้าพักอย่างครบถ้วน
-- ============================================================

-- 1. Create Reschedule History Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Reservation_Reschedule_History')
BEGIN
    CREATE TABLE [dbo].[Reservation_Reschedule_History] (
        [ID]                    BIGINT IDENTITY(1,1) PRIMARY KEY,
        [Reservation_ID]        INT NOT NULL,
        [RescheduleType]        NVARCHAR(50) NOT NULL,         -- 'POSTPONE' = เลื่อนวัน, 'DATE_CHANGE' = เปลี่ยนวัน, 'CANCEL_POSTPONE' = ยกเลิกการเลื่อน
        [OldCheckinDate]        DATETIME NULL,
        [OldCheckoutDate]       DATETIME NULL,
        [OldStayDays]           INT NULL,
        [NewCheckinDate]        DATETIME NULL,
        [NewCheckoutDate]       DATETIME NULL,
        [NewStayDays]           INT NULL,
        [OldStatus]             NVARCHAR(100) NULL,
        [NewStatus]             NVARCHAR(100) NULL,
        [Reason]                NVARCHAR(500) NULL,             -- เหตุผลในการเลื่อน
        [RescheduledBy_AdminID] SMALLINT NULL,
        [RescheduledBy_Name]    NVARCHAR(200) NULL,
        [RescheduledDate]       DATETIME NOT NULL DEFAULT GETDATE(),
        [Remark]                NVARCHAR(500) NULL,

        CONSTRAINT [FK_RescheduleHistory_Reservation]
            FOREIGN KEY ([Reservation_ID]) REFERENCES [dbo].[Reservation]([ID])
    );

    PRINT 'Created table: Reservation_Reschedule_History';
END
GO

-- 2. Create indexes for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RescheduleHistory_ReservationID')
BEGIN
    CREATE INDEX [IX_RescheduleHistory_ReservationID]
        ON [dbo].[Reservation_Reschedule_History] ([Reservation_ID]);
    PRINT 'Created index: IX_RescheduleHistory_ReservationID';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RescheduleHistory_RescheduledDate')
BEGIN
    CREATE INDEX [IX_RescheduleHistory_RescheduledDate]
        ON [dbo].[Reservation_Reschedule_History] ([RescheduledDate] DESC);
    PRINT 'Created index: IX_RescheduleHistory_RescheduledDate';
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_RescheduleHistory_Type')
BEGIN
    CREATE INDEX [IX_RescheduleHistory_Type]
        ON [dbo].[Reservation_Reschedule_History] ([RescheduleType]);
    PRINT 'Created index: IX_RescheduleHistory_Type';
END
GO

-- 3. Add IsPostponed column to Reservation table (proper flag instead of placeholder dates)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'IsPostponed')
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [IsPostponed] BIT NOT NULL DEFAULT 0;
    PRINT 'Added column: Reservation.IsPostponed';
END
GO

-- 4. Add PostponedDate column (when it was postponed)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'PostponedDate')
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [PostponedDate] DATETIME NULL;
    PRINT 'Added column: Reservation.PostponedDate';
END
GO

-- 5. Add RescheduleCount column (track how many times rescheduled)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'RescheduleCount')
BEGIN
    ALTER TABLE [dbo].[Reservation] ADD [RescheduleCount] INT NOT NULL DEFAULT 0;
    PRINT 'Added column: Reservation.RescheduleCount';
END
GO

-- 6. Migrate existing postponed reservations (placeholder dates -> proper flag)
UPDATE [dbo].[Reservation]
SET [IsPostponed] = 1,
    [PostponedDate] = [Created_Date]
WHERE (CheckinDate = '1990-01-01' OR CheckinDate = '0001-01-01')
  AND Status = N'มัดจำแล้ว'
  AND IsPostponed = 0;

PRINT 'Migrated existing postponed reservations';
GO

-- 7. View: Postponed reservations (replaces raw query in PostponeList.aspx.cs)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_PostponedReservations')
    DROP VIEW [dbo].[vw_PostponedReservations];
GO

CREATE VIEW [dbo].[vw_PostponedReservations]
AS
SELECT
    R.ID,
    R.Customer_MobilePhone,
    R.CheckinDate,
    R.CheckoutDate,
    R.StayDays,
    R.Status,
    R.TotalPrice,
    R.Deposit,
    R.Remark,
    R.Created_Date,
    R.IsPostponed,
    R.PostponedDate,
    R.RescheduleCount,
    C.Name,
    C.NickName,
    C.FullName,
    C.Email,
    (SELECT COUNT(*) FROM Reservation_Reschedule_History RH WHERE RH.Reservation_ID = R.ID) AS TotalReschedules,
    (SELECT TOP 1 RH.RescheduledDate FROM Reservation_Reschedule_History RH
     WHERE RH.Reservation_ID = R.ID ORDER BY RH.RescheduledDate DESC) AS LastRescheduleDate,
    (SELECT TOP 1 RH.Reason FROM Reservation_Reschedule_History RH
     WHERE RH.Reservation_ID = R.ID ORDER BY RH.RescheduledDate DESC) AS LastRescheduleReason
FROM [dbo].[Reservation] R
INNER JOIN [dbo].[Customer] C ON C.MobilePhone = R.Customer_MobilePhone
WHERE R.IsPostponed = 1
  AND R.Status = N'มัดจำแล้ว';
GO

PRINT 'Created view: vw_PostponedReservations';
GO

PRINT '============================================================';
PRINT 'PHASE 8 Migration Complete: Reschedule History System';
PRINT '============================================================';
