-- =============================================
-- PHASE 5 - Migration 03: Performance Indexes
-- Purpose: Add missing indexes for optimal query performance
-- Impact: Improve vw_ActiveGuestReservations and related queries
-- Date: 2025-11-06
-- =============================================

USE [Taketime];
GO

PRINT 'Starting PHASE 5 Migration 03: Performance Indexes...';
PRINT '';
GO

-- =============================================
-- 1. Index on Reservation.Customer_MobilePhone
-- Used in: vw_ActiveGuestReservations INNER JOIN
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservation_Customer_MobilePhone' AND object_id = OBJECT_ID('Reservation'))
BEGIN
    PRINT '📊 Creating index: IX_Reservation_Customer_MobilePhone...';
    CREATE NONCLUSTERED INDEX IX_Reservation_Customer_MobilePhone
    ON Reservation(Customer_MobilePhone)
    INCLUDE (ID, CheckinDate, CheckoutDate, Status, TotalPrice, Deposit);
    PRINT '✅ Index IX_Reservation_Customer_MobilePhone created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Reservation_Customer_MobilePhone already exists.';
END
GO

-- =============================================
-- 2. Composite Index on Reservation date range and status
-- Used in: vw_ActiveGuestReservations WHERE clause
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservation_DateRange_Status' AND object_id = OBJECT_ID('Reservation'))
BEGIN
    PRINT '📊 Creating index: IX_Reservation_DateRange_Status...';
    CREATE NONCLUSTERED INDEX IX_Reservation_DateRange_Status
    ON Reservation(CheckinDate, CheckoutDate, Status)
    INCLUDE (ID, Customer_MobilePhone, TotalPrice, Deposit);
    PRINT '✅ Index IX_Reservation_DateRange_Status created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Reservation_DateRange_Status already exists.';
END
GO

-- =============================================
-- 3. Index on Payment_History for reservation lookups
-- Used in: Payment tracking and balance calculations
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payment_History_Reservation_ID_PaymentDate' AND object_id = OBJECT_ID('Payment_History'))
BEGIN
    PRINT '📊 Creating index: IX_Payment_History_Reservation_ID_PaymentDate...';
    CREATE NONCLUSTERED INDEX IX_Payment_History_Reservation_ID_PaymentDate
    ON Payment_History(Reservation_ID, PaymentDate DESC)
    INCLUDE (PaymentAmount, PaymentMethod, Status);
    PRINT '✅ Index IX_Payment_History_Reservation_ID_PaymentDate created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Payment_History_Reservation_ID_PaymentDate already exists.';
END
GO

-- =============================================
-- 4. Index on Customer.MobilePhone for faster lookups
-- Used in: vw_ActiveGuestReservations JOIN and customer searches
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Customer_MobilePhone' AND object_id = OBJECT_ID('Customer'))
BEGIN
    PRINT '📊 Creating index: IX_Customer_MobilePhone...';
    CREATE NONCLUSTERED INDEX IX_Customer_MobilePhone
    ON Customer(MobilePhone)
    INCLUDE (Name, NickName, Email, IDNumber);
    PRINT '✅ Index IX_Customer_MobilePhone created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Customer_MobilePhone already exists.';
END
GO

-- =============================================
-- 5. Index on Reservation_Accommodation for room name lookups
-- Used in: fn_GetReservationRoomNames function
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Reservation_Accommodation_Reservation_ID' AND object_id = OBJECT_ID('Reservation_Accommodation'))
BEGIN
    PRINT '📊 Creating index: IX_Reservation_Accommodation_Reservation_ID...';
    CREATE NONCLUSTERED INDEX IX_Reservation_Accommodation_Reservation_ID
    ON Reservation_Accommodation(Reservation_ID)
    INCLUDE (Accommodation_ID);
    PRINT '✅ Index IX_Reservation_Accommodation_Reservation_ID created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Reservation_Accommodation_Reservation_ID already exists.';
END
GO

-- =============================================
-- 6. Index on Product for POS searches
-- Used in: Product page barcode and name searches
-- Note: Product table does NOT have Amount column
--       Stock is calculated from Product_In - Product_Out
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Product_Barcode' AND object_id = OBJECT_ID('Product'))
BEGIN
    PRINT '📊 Creating index: IX_Product_Barcode...';
    CREATE NONCLUSTERED INDEX IX_Product_Barcode
    ON Product(Barcode)
    INCLUDE (ID, Product_Name, Sell_Price, Category_ID, CanPreBook)
    WHERE Barcode IS NOT NULL AND Status = 'True';
    PRINT '✅ Index IX_Product_Barcode created successfully!';
END
ELSE
BEGIN
    PRINT 'ℹ️ Index IX_Product_Barcode already exists.';
END
GO

-- =============================================
-- Analyze index usage and fragmentation
-- =============================================
PRINT '';
PRINT '📊 Index Statistics:';
PRINT '====================================';
GO

SELECT
    OBJECT_NAME(i.object_id) AS TableName,
    i.name AS IndexName,
    i.type_desc AS IndexType,
    ps.avg_fragmentation_in_percent AS FragmentationPercent,
    ps.page_count AS PageCount
FROM sys.indexes i
INNER JOIN sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ps
    ON i.object_id = ps.object_id AND i.index_id = ps.index_id
WHERE i.name IN (
    'IX_Reservation_Customer_MobilePhone',
    'IX_Reservation_DateRange_Status',
    'IX_Payment_History_Reservation_ID_PaymentDate',
    'IX_Customer_MobilePhone',
    'IX_Reservation_Accommodation_Reservation_ID',
    'IX_Product_Barcode'
)
ORDER BY TableName, IndexName;
GO

-- =============================================
-- Test query performance
-- =============================================
PRINT '';
PRINT '🧪 Testing vw_ActiveGuestReservations performance...';
GO

SET STATISTICS TIME ON;
SET STATISTICS IO ON;

SELECT COUNT(*) AS TotalActiveGuests
FROM vw_ActiveGuestReservations;

SET STATISTICS IO OFF;
SET STATISTICS TIME OFF;
GO

PRINT '';
PRINT '✅ PHASE 5 Migration 03 completed successfully!';
PRINT '';
PRINT '📋 Summary of indexes created:';
PRINT '   1. IX_Reservation_Customer_MobilePhone - Join optimization';
PRINT '   2. IX_Reservation_DateRange_Status - Date range filtering';
PRINT '   3. IX_Payment_History_Reservation_ID_PaymentDate - Payment lookups';
PRINT '   4. IX_Customer_MobilePhone - Customer searches';
PRINT '   5. IX_Reservation_Accommodation_Reservation_ID - Room name lookups';
PRINT '   6. IX_Product_Barcode - POS barcode scans';
PRINT '';
PRINT '💡 Expected performance improvements:';
PRINT '   - vw_ActiveGuestReservations: 3-5x faster';
PRINT '   - Customer lookups: 2-3x faster';
PRINT '   - POS barcode scans: 5-10x faster';
PRINT '';
PRINT '⚠️ Note: Run UPDATE STATISTICS after bulk data changes';
PRINT '';
GO
