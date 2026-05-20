-- ============================================================================
-- PHASE 12 Migration 11: Widen Admin Password + Identity Columns
-- ============================================================================
-- ปัญหา: เพิ่มพนักงานใหม่แล้วขึ้น error "String or binary data would be truncated"
--
-- สาเหตุ: SecurityHelper.HashPassword สร้าง hash รูปแบบ
--           {Iterations}.{Base64(salt 16B)}.{Base64(hash 32B)}
--         = "10000" + "." + 24 chars + "." + 44 chars = ~75 ตัวอักษร
--         แต่คอลัมน์ Admin.Password เดิมถูกสร้างไว้สำหรับ plain-text password
--         (โดยทั่วไป VARCHAR(20-50)) → hash ยาวเกิน → ถูก truncate
--
-- แก้ไข: ขยาย Admin.Password เป็น NVARCHAR(255) (เผื่อ algorithm ในอนาคต)
--        และขยายคอลัมน์ identity อื่นที่ form กรอกได้ ให้กว้างพอ
-- ============================================================================

USE [Taketime]
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 11: Widen Admin Password + Identity Columns'
PRINT '============================================================================'
GO

-- ── Password: ต้องเก็บ PBKDF2 hash (~75 ตัว) — ขยายเป็น 255 ──
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Password')
BEGIN
    DECLARE @pwdLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2  -- nvarchar/nchar = 2 bytes/char
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Password'
    );

    IF @pwdLen <> -1 AND @pwdLen < 255
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [Password] NVARCHAR(255) NOT NULL;
        PRINT '+ Widened Admin.Password to NVARCHAR(255) (was ' + CAST(@pwdLen AS VARCHAR(10)) + ' chars)';
    END
    ELSE
        PRINT '= Admin.Password already wide enough — skipped';
END
ELSE
    PRINT '! Admin.Password column not found — please verify Admin table';
GO

-- ── Username: รองรับ username/email ยาว ──
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Username')
BEGIN
    DECLARE @userLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Username'
    );
    IF @userLen <> -1 AND @userLen < 100
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [Username] NVARCHAR(100) NOT NULL;
        PRINT '+ Widened Admin.Username to NVARCHAR(100) (was ' + CAST(@userLen AS VARCHAR(10)) + ' chars)';
    END
    ELSE
        PRINT '= Admin.Username already wide enough — skipped';
END
GO

-- ── FirstName / LastName / Title / Role: ชื่อไทยอาจยาว ──
IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'FirstName')
BEGIN
    DECLARE @fnLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'FirstName'
    );
    IF @fnLen <> -1 AND @fnLen < 100
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [FirstName] NVARCHAR(100) NULL;
        PRINT '+ Widened Admin.FirstName to NVARCHAR(100)';
    END
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'LastName')
BEGIN
    DECLARE @lnLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'LastName'
    );
    IF @lnLen <> -1 AND @lnLen < 100
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [LastName] NVARCHAR(100) NULL;
        PRINT '+ Widened Admin.LastName to NVARCHAR(100)';
    END
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Title')
BEGIN
    DECLARE @titleLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Title'
    );
    IF @titleLen <> -1 AND @titleLen < 20
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [Title] NVARCHAR(20) NULL;
        PRINT '+ Widened Admin.Title to NVARCHAR(20)';
    END
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Role')
BEGIN
    DECLARE @roleLen INT = (
        SELECT CASE WHEN max_length = -1 THEN -1
                    WHEN system_type_id IN (231, 239) THEN max_length / 2
                    ELSE max_length END
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Role'
    );
    IF @roleLen <> -1 AND @roleLen < 20
    BEGIN
        ALTER TABLE [dbo].[Admin] ALTER COLUMN [Role] NVARCHAR(20) NULL;
        PRINT '+ Widened Admin.Role to NVARCHAR(20)';
    END
END
GO

PRINT '============================================================================'
PRINT 'PHASE 12 Migration 11: COMPLETED'
PRINT '============================================================================'
GO
