-- ============================================
-- Query to find actual Customer.MobilePhone data type
-- ============================================

USE [TakeTime]
GO

-- Check Customer table MobilePhone column definition
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
WHERE t.name = 'Customer'
AND c.name = 'MobilePhone';

PRINT '';
PRINT '============================================';
PRINT 'Existing Foreign Keys to Customer.MobilePhone:';
PRINT '============================================';

-- Check existing foreign keys that reference Customer.MobilePhone
SELECT
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS ReferencingTable,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ReferencingColumn,
    c.name AS ReferencingColumnName,
    ty.name AS ReferencingDataType,
    c.max_length AS ReferencingMaxLength,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
FROM sys.foreign_keys fk
INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
INNER JOIN sys.columns c ON fkc.parent_object_id = c.object_id AND fkc.parent_column_id = c.column_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE fk.referenced_object_id = OBJECT_ID('Customer')
AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) = 'MobilePhone';

GO
