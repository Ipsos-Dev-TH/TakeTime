-- ============================================================================
-- PHASE 7 Migration 04: Add Extended Fields to Admin Table
-- ============================================================================
-- This migration adds bank info, contact details, and other important fields
-- to the Admin table for Employee Management
-- ============================================================================

USE [Taketime]
GO

PRINT '============================================================================'
PRINT 'PHASE 7 Migration 04: Adding Extended Fields to Admin Table'
PRINT '============================================================================'
GO

-- Add Phone column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Phone')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [Phone] NVARCHAR(20) NULL;
    PRINT '+ Added Phone column to Admin';
END
ELSE
    PRINT '= Phone column already exists in Admin';
GO

-- Add Email column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Email')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [Email] NVARCHAR(100) NULL;
    PRINT '+ Added Email column to Admin';
END
ELSE
    PRINT '= Email column already exists in Admin';
GO

-- Add IDCard column (เลขบัตรประชาชน)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'IDCard')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [IDCard] NVARCHAR(20) NULL;
    PRINT '+ Added IDCard column to Admin';
END
ELSE
    PRINT '= IDCard column already exists in Admin';
GO

-- Add BankCode column (รหัสธนาคาร)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'BankCode')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [BankCode] NVARCHAR(10) NULL;
    PRINT '+ Added BankCode column to Admin';
END
ELSE
    PRINT '= BankCode column already exists in Admin';
GO

-- Add BankAccountNumber column (เลขบัญชีธนาคาร)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'BankAccountNumber')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [BankAccountNumber] NVARCHAR(20) NULL;
    PRINT '+ Added BankAccountNumber column to Admin';
END
ELSE
    PRINT '= BankAccountNumber column already exists in Admin';
GO

-- Add BankAccountName column (ชื่อบัญชีธนาคาร)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'BankAccountName')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [BankAccountName] NVARCHAR(100) NULL;
    PRINT '+ Added BankAccountName column to Admin';
END
ELSE
    PRINT '= BankAccountName column already exists in Admin';
GO

-- Add Address column
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'Address')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [Address] NVARCHAR(500) NULL;
    PRINT '+ Added Address column to Admin';
END
ELSE
    PRINT '= Address column already exists in Admin';
GO

-- Add EmergencyContact column (ผู้ติดต่อกรณีฉุกเฉิน)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'EmergencyContact')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [EmergencyContact] NVARCHAR(100) NULL;
    PRINT '+ Added EmergencyContact column to Admin';
END
ELSE
    PRINT '= EmergencyContact column already exists in Admin';
GO

-- Add EmergencyPhone column (เบอร์โทรกรณีฉุกเฉิน)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'EmergencyPhone')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [EmergencyPhone] NVARCHAR(20) NULL;
    PRINT '+ Added EmergencyPhone column to Admin';
END
ELSE
    PRINT '= EmergencyPhone column already exists in Admin';
GO

-- Add HireDate column (วันที่เริ่มงาน)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'HireDate')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [HireDate] DATE NULL;
    PRINT '+ Added HireDate column to Admin';
END
ELSE
    PRINT '= HireDate column already exists in Admin';
GO

-- Add BirthDate column (วันเกิด)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Admin]') AND name = 'BirthDate')
BEGIN
    ALTER TABLE [dbo].[Admin] ADD [BirthDate] DATE NULL;
    PRINT '+ Added BirthDate column to Admin';
END
ELSE
    PRINT '= BirthDate column already exists in Admin';
GO

PRINT ''
PRINT '============================================================================'
PRINT 'PHASE 7 Migration 04 Completed Successfully!'
PRINT '============================================================================'
GO
