-- =============================================
-- PHASE9 Migration 02: Fix Document ID Column Sizes
-- Purpose: Widen varchar/nvarchar columns that store Receipt IDs,
--          Voucher Numbers, and related document identifiers.
--
-- Root Cause: CreateDocumentNumber() now generates IDs in format
--   DOCYYMMDDNNN (12 chars, e.g., REC260401001)
--   which includes the Day component.
--   Old format was DOCYYMMNNN (11 chars, e.g., REC2410001).
--   Columns sized for the old format cause:
--   "String or binary data would be truncated"
--
-- Date: 2026-04-01
-- =============================================

USE [Taketime]
GO

SET NOCOUNT ON;

PRINT '========================================';
PRINT 'PHASE9 Migration 02: Fix Document Column Sizes';
PRINT '========================================';
PRINT '';

-- ===================================================================
-- 1. Account_Receipt.ID - Primary receipt identifier
-- ===================================================================
PRINT 'Step 1: Checking Account_Receipt.ID column size...';

DECLARE @ar_id_maxlen INT;
SELECT @ar_id_maxlen = c.max_length
FROM sys.columns c
INNER JOIN sys.tables t ON c.object_id = t.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE t.name = 'Account_Receipt' AND c.name = 'ID';

IF @ar_id_maxlen IS NOT NULL
BEGIN
    -- For nvarchar, max_length is in bytes (2 bytes per char)
    -- For varchar, max_length is in bytes (1 byte per char)
    DECLARE @ar_id_type NVARCHAR(50);
    SELECT @ar_id_type = ty.name
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Account_Receipt' AND c.name = 'ID';

    DECLARE @ar_id_charlen INT;
    IF @ar_id_type = 'nvarchar'
        SET @ar_id_charlen = @ar_id_maxlen / 2;
    ELSE
        SET @ar_id_charlen = @ar_id_maxlen;

    PRINT '  Current type: ' + @ar_id_type + '(' + CAST(@ar_id_charlen AS VARCHAR) + ')';

    IF @ar_id_charlen < 20
    BEGIN
        -- Need to drop and recreate constraints that reference this column
        PRINT '  Widening Account_Receipt.ID to 20 characters...';

        -- Drop foreign key constraints referencing Account_Receipt.ID
        DECLARE @sql NVARCHAR(MAX) = '';

        SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT [' + fk.name + '];' + CHAR(13)
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.referenced_object_id = OBJECT_ID('Account_Receipt')
          AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) = 'ID';

        IF LEN(@sql) > 0
        BEGIN
            PRINT '  Dropping FK constraints...';
            EXEC sp_executesql @sql;
        END

        -- Drop primary key if exists
        DECLARE @pk_name NVARCHAR(200);
        SELECT @pk_name = i.name
        FROM sys.indexes i
        WHERE i.object_id = OBJECT_ID('Account_Receipt') AND i.is_primary_key = 1;

        IF @pk_name IS NOT NULL
        BEGIN
            PRINT '  Dropping PK constraint: ' + @pk_name;
            EXEC('ALTER TABLE [dbo].[Account_Receipt] DROP CONSTRAINT [' + @pk_name + ']');
        END

        -- Alter the column
        IF @ar_id_type = 'nvarchar'
            ALTER TABLE [dbo].[Account_Receipt] ALTER COLUMN [ID] NVARCHAR(20) NOT NULL;
        ELSE
            ALTER TABLE [dbo].[Account_Receipt] ALTER COLUMN [ID] VARCHAR(20) NOT NULL;

        -- Recreate primary key
        IF @pk_name IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[Account_Receipt] ADD CONSTRAINT [PK_Account_Receipt] PRIMARY KEY ([ID]);
            PRINT '  Recreated PK constraint';
        END

        PRINT '  OK - Account_Receipt.ID widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@ar_id_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Account_Receipt.ID column not found';
END

PRINT '';

-- ===================================================================
-- 2. Account_Receipt_Detail.Receipt_ID
-- ===================================================================
PRINT 'Step 2: Checking Account_Receipt_Detail.Receipt_ID...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Account_Receipt_Detail' AND c.name = 'Receipt_ID')
BEGIN
    DECLARE @ard_type NVARCHAR(50), @ard_maxlen INT, @ard_charlen INT;

    SELECT @ard_type = ty.name, @ard_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Account_Receipt_Detail' AND c.name = 'Receipt_ID';

    SET @ard_charlen = CASE WHEN @ard_type = 'nvarchar' THEN @ard_maxlen / 2 ELSE @ard_maxlen END;
    PRINT '  Current type: ' + @ard_type + '(' + CAST(@ard_charlen AS VARCHAR) + ')';

    IF @ard_charlen < 20
    BEGIN
        -- Drop FK constraints on this column
        SET @sql = '';
        SELECT @sql = @sql + 'ALTER TABLE [Account_Receipt_Detail] DROP CONSTRAINT [' + fk.name + '];' + CHAR(13)
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.parent_object_id = OBJECT_ID('Account_Receipt_Detail')
          AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'Receipt_ID';

        IF LEN(@sql) > 0
        BEGIN
            PRINT '  Dropping FK constraints...';
            EXEC sp_executesql @sql;
        END

        IF @ard_type = 'nvarchar'
            ALTER TABLE [dbo].[Account_Receipt_Detail] ALTER COLUMN [Receipt_ID] NVARCHAR(20);
        ELSE
            ALTER TABLE [dbo].[Account_Receipt_Detail] ALTER COLUMN [Receipt_ID] VARCHAR(20);

        PRINT '  OK - Widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@ard_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 3. Voucher.Voucher_Number
-- ===================================================================
PRINT 'Step 3: Checking Voucher.Voucher_Number...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Voucher' AND c.name = 'Voucher_Number')
BEGIN
    DECLARE @vn_type NVARCHAR(50), @vn_maxlen INT, @vn_charlen INT;

    SELECT @vn_type = ty.name, @vn_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Voucher' AND c.name = 'Voucher_Number';

    SET @vn_charlen = CASE WHEN @vn_type = 'nvarchar' THEN @vn_maxlen / 2 ELSE @vn_maxlen END;
    PRINT '  Current type: ' + @vn_type + '(' + CAST(@vn_charlen AS VARCHAR) + ')';

    IF @vn_charlen < 20
    BEGIN
        -- Drop FK constraints
        SET @sql = '';
        SELECT @sql = @sql + 'ALTER TABLE [' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT [' + fk.name + '];' + CHAR(13)
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.referenced_object_id = OBJECT_ID('Voucher')
          AND COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) = 'Voucher_Number';

        IF LEN(@sql) > 0
        BEGIN
            PRINT '  Dropping FK constraints...';
            EXEC sp_executesql @sql;
        END

        -- Drop PK if on this column
        DECLARE @vpk_name NVARCHAR(200);
        SELECT @vpk_name = i.name
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
        WHERE i.object_id = OBJECT_ID('Voucher') AND i.is_primary_key = 1
          AND COL_NAME(i.object_id, ic.column_id) = 'Voucher_Number';

        IF @vpk_name IS NOT NULL
        BEGIN
            PRINT '  Dropping PK constraint: ' + @vpk_name;
            EXEC('ALTER TABLE [dbo].[Voucher] DROP CONSTRAINT [' + @vpk_name + ']');
        END

        IF @vn_type = 'nvarchar'
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Voucher_Number] NVARCHAR(20) NOT NULL;
        ELSE
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Voucher_Number] VARCHAR(20) NOT NULL;

        -- Recreate PK if it was dropped
        IF @vpk_name IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[Voucher] ADD CONSTRAINT [PK_Voucher] PRIMARY KEY ([Voucher_Number]);
            PRINT '  Recreated PK constraint';
        END

        PRINT '  OK - Widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@vn_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 4. Voucher.Receipt_ID
-- ===================================================================
PRINT 'Step 4: Checking Voucher.Receipt_ID...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Voucher' AND c.name = 'Receipt_ID')
BEGIN
    DECLARE @vr_type NVARCHAR(50), @vr_maxlen INT, @vr_charlen INT;

    SELECT @vr_type = ty.name, @vr_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Voucher' AND c.name = 'Receipt_ID';

    SET @vr_charlen = CASE WHEN @vr_type = 'nvarchar' THEN @vr_maxlen / 2 ELSE @vr_maxlen END;
    PRINT '  Current type: ' + @vr_type + '(' + CAST(@vr_charlen AS VARCHAR) + ')';

    IF @vr_charlen < 20
    BEGIN
        -- Drop FK constraints
        SET @sql = '';
        SELECT @sql = @sql + 'ALTER TABLE [Voucher] DROP CONSTRAINT [' + fk.name + '];' + CHAR(13)
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.parent_object_id = OBJECT_ID('Voucher')
          AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'Receipt_ID';

        IF LEN(@sql) > 0
        BEGIN
            PRINT '  Dropping FK constraints...';
            EXEC sp_executesql @sql;
        END

        IF @vr_type = 'nvarchar'
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Receipt_ID] NVARCHAR(20);
        ELSE
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Receipt_ID] VARCHAR(20);

        PRINT '  OK - Widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@vr_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 5. Voucher_RatePlan_Group.Voucher_Number
-- ===================================================================
PRINT 'Step 5: Checking Voucher_RatePlan_Group.Voucher_Number...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Voucher_RatePlan_Group' AND c.name = 'Voucher_Number')
BEGIN
    DECLARE @vrg_type NVARCHAR(50), @vrg_maxlen INT, @vrg_charlen INT;

    SELECT @vrg_type = ty.name, @vrg_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Voucher_RatePlan_Group' AND c.name = 'Voucher_Number';

    SET @vrg_charlen = CASE WHEN @vrg_type = 'nvarchar' THEN @vrg_maxlen / 2 ELSE @vrg_maxlen END;
    PRINT '  Current type: ' + @vrg_type + '(' + CAST(@vrg_charlen AS VARCHAR) + ')';

    IF @vrg_charlen < 20
    BEGIN
        -- Drop FK constraints
        SET @sql = '';
        SELECT @sql = @sql + 'ALTER TABLE [Voucher_RatePlan_Group] DROP CONSTRAINT [' + fk.name + '];' + CHAR(13)
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
        WHERE fkc.parent_object_id = OBJECT_ID('Voucher_RatePlan_Group')
          AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = 'Voucher_Number';

        IF LEN(@sql) > 0
        BEGIN
            PRINT '  Dropping FK constraints...';
            EXEC sp_executesql @sql;
        END

        IF @vrg_type = 'nvarchar'
            ALTER TABLE [dbo].[Voucher_RatePlan_Group] ALTER COLUMN [Voucher_Number] NVARCHAR(20);
        ELSE
            ALTER TABLE [dbo].[Voucher_RatePlan_Group] ALTER COLUMN [Voucher_Number] VARCHAR(20);

        PRINT '  OK - Widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@vrg_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 6. Voucher.Remark (may also be too small for user input)
-- ===================================================================
PRINT 'Step 6: Checking Voucher.Remark...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Voucher' AND c.name = 'Remark')
BEGIN
    DECLARE @vrem_type NVARCHAR(50), @vrem_maxlen INT, @vrem_charlen INT;

    SELECT @vrem_type = ty.name, @vrem_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Voucher' AND c.name = 'Remark';

    SET @vrem_charlen = CASE WHEN @vrem_type = 'nvarchar' THEN @vrem_maxlen / 2 ELSE @vrem_maxlen END;
    PRINT '  Current type: ' + @vrem_type + '(' + CAST(@vrem_charlen AS VARCHAR) + ')';

    IF @vrem_charlen < 500
    BEGIN
        IF @vrem_type = 'nvarchar'
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Remark] NVARCHAR(500);
        ELSE
            ALTER TABLE [dbo].[Voucher] ALTER COLUMN [Remark] VARCHAR(500);

        PRINT '  OK - Widened to 500';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@vrem_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 7. Payment_History.Receipt_ID (also stores REC format IDs)
-- ===================================================================
PRINT 'Step 7: Checking Payment_History.Receipt_ID...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Payment_History' AND c.name = 'Receipt_ID')
BEGIN
    DECLARE @ph_type NVARCHAR(50), @ph_maxlen INT, @ph_charlen INT;

    SELECT @ph_type = ty.name, @ph_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Payment_History' AND c.name = 'Receipt_ID';

    SET @ph_charlen = CASE WHEN @ph_type = 'nvarchar' THEN @ph_maxlen / 2 ELSE @ph_maxlen END;
    PRINT '  Current type: ' + @ph_type + '(' + CAST(@ph_charlen AS VARCHAR) + ')';

    IF @ph_charlen < 20
    BEGIN
        IF @ph_type = 'nvarchar'
            ALTER TABLE [dbo].[Payment_History] ALTER COLUMN [Receipt_ID] NVARCHAR(20);
        ELSE
            ALTER TABLE [dbo].[Payment_History] ALTER COLUMN [Receipt_ID] VARCHAR(20);

        PRINT '  OK - Widened to 20';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@ph_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- 8. Account_Receipt.Paid_Type (stores payment method text, Thai text can be long)
-- ===================================================================
PRINT 'Step 8: Checking Account_Receipt.Paid_Type...';

IF EXISTS (SELECT 1 FROM sys.columns c
           INNER JOIN sys.tables t ON c.object_id = t.object_id
           WHERE t.name = 'Account_Receipt' AND c.name = 'Paid_Type')
BEGIN
    DECLARE @pt_type NVARCHAR(50), @pt_maxlen INT, @pt_charlen INT;

    SELECT @pt_type = ty.name, @pt_maxlen = c.max_length
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
    WHERE t.name = 'Account_Receipt' AND c.name = 'Paid_Type';

    SET @pt_charlen = CASE WHEN @pt_type = 'nvarchar' THEN @pt_maxlen / 2 ELSE @pt_maxlen END;
    PRINT '  Current type: ' + @pt_type + '(' + CAST(@pt_charlen AS VARCHAR) + ')';

    IF @pt_charlen < 100
    BEGIN
        IF @pt_type = 'nvarchar'
            ALTER TABLE [dbo].[Account_Receipt] ALTER COLUMN [Paid_Type] NVARCHAR(100);
        ELSE
            ALTER TABLE [dbo].[Account_Receipt] ALTER COLUMN [Paid_Type] VARCHAR(100);

        PRINT '  OK - Widened to 100';
    END
    ELSE
    BEGIN
        PRINT '  OK - Already wide enough (' + CAST(@pt_charlen AS VARCHAR) + ' chars)';
    END
END
ELSE
BEGIN
    PRINT '  SKIPPED - Column not found';
END

PRINT '';

-- ===================================================================
-- Verification: Print all column sizes for reference
-- ===================================================================
PRINT '========================================';
PRINT 'Verification - Current Column Sizes:';
PRINT '========================================';

SELECT
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    CASE WHEN ty.name = 'nvarchar' THEN c.max_length / 2 ELSE c.max_length END AS CharLength,
    c.is_nullable AS IsNullable
FROM sys.tables t
INNER JOIN sys.columns c ON t.object_id = c.object_id
INNER JOIN sys.types ty ON c.user_type_id = ty.user_type_id
WHERE (t.name = 'Voucher' AND c.name IN ('Voucher_Number', 'Receipt_ID', 'Remark', 'Sell_Price', 'Created_Date', 'Customer_ID', 'Expired_Date'))
   OR (t.name = 'Voucher_RatePlan_Group' AND c.name = 'Voucher_Number')
   OR (t.name = 'Account_Receipt' AND c.name IN ('ID', 'Paid_Type'))
   OR (t.name = 'Account_Receipt_Detail' AND c.name = 'Receipt_ID')
   OR (t.name = 'Payment_History' AND c.name = 'Receipt_ID')
ORDER BY t.name, c.column_id;

PRINT '';
PRINT '========================================';
PRINT 'Migration completed successfully!';
PRINT '========================================';

GO
