-- ============================================
-- Query to find Accommodation and Reservation ID data types
-- ============================================

USE [TakeTime]
GO

PRINT '============================================';
PRINT 'Accommodation Table Structure:';
PRINT '============================================';

-- Check Accommodation table columns
SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name = 'Accommodation'
ORDER BY c.column_id;

PRINT '';
PRINT '============================================';
PRINT 'Reservation Table Structure:';
PRINT '============================================';

-- Check Reservation table columns
SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length AS MaxLength,
    c.precision AS Precision,
    c.scale AS Scale,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name = 'Reservation'
ORDER BY c.column_id;

GO
