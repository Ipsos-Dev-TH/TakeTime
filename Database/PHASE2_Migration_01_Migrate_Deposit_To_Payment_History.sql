-- =============================================
-- Phase 2 Migration 01: Migrate Deposit to Payment_History
-- Purpose: Migrate existing Deposit data to Payment_History table
-- Author: Claude AI
-- Date: 2025-11-05
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Phase 2 Migration 01: Migrate Deposit to Payment_History';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Check Prerequisites
-- =============================================
PRINT 'Checking prerequisites...';

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Payment_History')
BEGIN
    PRINT '❌ ERROR: Payment_History table does not exist!';
    PRINT '   Please run PHASE1_Migration_09_Payment_Tracking.sql first';
    RAISERROR('Payment_History table not found', 16, 1);
    RETURN;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Reservation') AND name = 'Deposit')
BEGIN
    PRINT '❌ ERROR: Reservation.Deposit column does not exist!';
    RAISERROR('Deposit column not found', 16, 1);
    RETURN;
END

PRINT '✅ All prerequisites met';
PRINT '';

-- =============================================
-- 2. Backup Existing Payment_History (optional)
-- =============================================
PRINT 'Creating backup of existing Payment_History...';

IF OBJECT_ID('tempdb..#Payment_History_Backup') IS NOT NULL
    DROP TABLE #Payment_History_Backup;

SELECT * INTO #Payment_History_Backup FROM [dbo].[Payment_History];

DECLARE @ExistingCount int;
SELECT @ExistingCount = COUNT(*) FROM #Payment_History_Backup;
PRINT '✅ Backed up ' + CAST(@ExistingCount AS nvarchar(10)) + ' existing payment records';
PRINT '';

-- =============================================
-- 3. Analyze Deposit Data
-- =============================================
PRINT 'Analyzing Deposit data...';

DECLARE @TotalReservations int;
DECLARE @ReservationsWithDeposit int;
DECLARE @TotalDepositAmount decimal(18,2);

SELECT @TotalReservations = COUNT(*) FROM [dbo].[Reservation];
SELECT @ReservationsWithDeposit = COUNT(*)
FROM [dbo].[Reservation]
WHERE Deposit > 0 AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน');

SELECT @TotalDepositAmount = SUM(Deposit)
FROM [dbo].[Reservation]
WHERE Deposit > 0 AND Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน');

PRINT 'Total reservations: ' + CAST(@TotalReservations AS nvarchar(10));
PRINT 'Reservations with deposit: ' + CAST(@ReservationsWithDeposit AS nvarchar(10));
PRINT 'Total deposit amount: ' + CAST(ISNULL(@TotalDepositAmount, 0) AS nvarchar(20)) + ' THB';
PRINT '';

-- =============================================
-- 4. Check for Existing Payment_History Records
-- =============================================
PRINT 'Checking for existing payment records...';

-- Find reservations that already have payment history
IF OBJECT_ID('tempdb..#ReservationsWithPaymentHistory') IS NOT NULL
    DROP TABLE #ReservationsWithPaymentHistory;

SELECT DISTINCT Reservation_ID
INTO #ReservationsWithPaymentHistory
FROM [dbo].[Payment_History]
WHERE Status = 'COMPLETED';

DECLARE @ReservationsAlreadyMigrated int;
SELECT @ReservationsAlreadyMigrated = COUNT(*) FROM #ReservationsWithPaymentHistory;

PRINT 'Reservations already in Payment_History: ' + CAST(@ReservationsAlreadyMigrated AS nvarchar(10));
PRINT '';

-- =============================================
-- 5. Identify Reservations to Migrate
-- =============================================
PRINT 'Identifying reservations to migrate...';

IF OBJECT_ID('tempdb..#ReservationsToMigrate') IS NOT NULL
    DROP TABLE #ReservationsToMigrate;

SELECT
    r.ID AS Reservation_ID,
    r.Deposit,
    r.TotalPrice,
    r.CheckinDate,
    r.Created_Date,
    r.Status,
    -- Determine payment date (use CheckinDate if exists, otherwise Created_Date)
    CASE
        WHEN r.CheckinDate IS NOT NULL AND r.CheckinDate != '1990-01-01' THEN r.CheckinDate
        ELSE r.Created_Date
    END AS PaymentDate,
    -- Determine payment method (assume CASH for old records)
    'CASH' AS PaymentMethod,
    -- Determine payment type based on deposit amount
    CASE
        WHEN r.Deposit >= r.TotalPrice THEN 'FULL'
        WHEN r.Deposit > 0 AND r.Deposit < r.TotalPrice THEN 'DEPOSIT'
        ELSE 'NONE'
    END AS PaymentType
INTO #ReservationsToMigrate
FROM [dbo].[Reservation] r
WHERE r.Deposit > 0
  AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
  AND NOT EXISTS (
      SELECT 1 FROM #ReservationsWithPaymentHistory ph
      WHERE ph.Reservation_ID = r.ID
  );

DECLARE @RecordsToMigrate int;
SELECT @RecordsToMigrate = COUNT(*) FROM #ReservationsToMigrate;

PRINT 'Reservations to migrate: ' + CAST(@RecordsToMigrate AS nvarchar(10));
PRINT '';

IF @RecordsToMigrate = 0
BEGIN
    PRINT '⚠️ No reservations to migrate. All deposits already in Payment_History.';
    PRINT '';
    PRINT '========================================';
    PRINT '✅ Migration completed (no changes needed)!';
    PRINT '========================================';
    RETURN;
END

-- =============================================
-- 6. Migrate Deposit to Payment_History
-- =============================================
PRINT 'Migrating deposits to Payment_History...';
PRINT '';

BEGIN TRY
    BEGIN TRANSACTION;

    -- Insert payment records for deposits
    INSERT INTO [dbo].[Payment_History] (
        Reservation_ID,
        PaymentDate,
        PaymentAmount,
        PaymentType,
        PaymentMethod,
        Status,
        Notes,
        CreatedDate,
        UpdatedDate
    )
    SELECT
        Reservation_ID,
        PaymentDate,
        Deposit AS PaymentAmount,
        PaymentType,
        PaymentMethod,
        'COMPLETED' AS Status,
        N'Migrated from Reservation.Deposit column' AS Notes,
        PaymentDate AS CreatedDate,
        GETDATE() AS UpdatedDate
    FROM #ReservationsToMigrate
    WHERE PaymentType != 'NONE';

    DECLARE @MigratedCount int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    PRINT '✅ Successfully migrated ' + CAST(@MigratedCount AS nvarchar(10)) + ' deposit records';
    PRINT '';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage nvarchar(4000) = ERROR_MESSAGE();
    PRINT '❌ ERROR during migration: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, 16, 1);
    RETURN;
END CATCH

-- =============================================
-- 7. Verification
-- =============================================
PRINT '';
PRINT '========================================';
PRINT 'Migration Verification';
PRINT '========================================';

-- Count migrated records
DECLARE @NewPaymentCount int;
SELECT @NewPaymentCount = COUNT(*)
FROM [dbo].[Payment_History]
WHERE Notes LIKE '%Migrated from Reservation.Deposit%';

PRINT 'New payment records created: ' + CAST(@NewPaymentCount AS nvarchar(10));

-- Verify amounts match
DECLARE @TotalMigratedAmount decimal(18,2);
SELECT @TotalMigratedAmount = SUM(PaymentAmount)
FROM [dbo].[Payment_History]
WHERE Notes LIKE '%Migrated from Reservation.Deposit%';

PRINT 'Total migrated amount: ' + CAST(@TotalMigratedAmount AS nvarchar(20)) + ' THB';

-- Compare with original
IF ABS(@TotalMigratedAmount - @TotalDepositAmount) < 0.01
    PRINT '✅ Amount verification passed';
ELSE
BEGIN
    PRINT '⚠️ WARNING: Migrated amount differs from original deposits!';
    PRINT '   Original: ' + CAST(@TotalDepositAmount AS nvarchar(20)) + ' THB';
    PRINT '   Migrated: ' + CAST(@TotalMigratedAmount AS nvarchar(20)) + ' THB';
    PRINT '   Difference: ' + CAST(ABS(@TotalMigratedAmount - @TotalDepositAmount) AS nvarchar(20)) + ' THB';
END

-- Sample verification query
PRINT '';
PRINT 'Sample comparison (first 10 records):';
PRINT '--------------------------------------';

SELECT TOP 10
    r.ID AS ReservationID,
    r.Deposit AS Original_Deposit,
    ISNULL(SUM(ph.PaymentAmount), 0) AS Migrated_Amount,
    r.Deposit - ISNULL(SUM(ph.PaymentAmount), 0) AS Difference
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Payment_History] ph ON r.ID = ph.Reservation_ID
    AND ph.Notes LIKE '%Migrated from Reservation.Deposit%'
WHERE r.Deposit > 0
  AND r.Status NOT IN (N'ยกเลิกคืนเงิน', N'ยกเลิกไม่คืนเงิน')
GROUP BY r.ID, r.Deposit
ORDER BY r.ID;

PRINT '';
PRINT '========================================';
PRINT '✅ Migration completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'IMPORTANT NOTES:';
PRINT '1. The Deposit column in Reservation table has NOT been removed';
PRINT '2. All new code should use Payment_History table via fn_GetRemainingBalance()';
PRINT '3. Consider deprecating direct Deposit column usage in future updates';
PRINT '4. Run PHASE2_Migration_02_Update_Payment_Functions.sql next';
GO
