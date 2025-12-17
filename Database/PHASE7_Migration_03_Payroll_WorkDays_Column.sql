-- ============================================================================
-- PHASE 7 Migration 03: Add WorkDays Column to Payroll_Records if missing
-- ============================================================================
-- This migration ensures WorkDays column exists in Payroll_Records table
-- ============================================================================

USE [Taketime]
GO

PRINT '============================================================================'
PRINT 'PHASE 7 Migration 03: Ensuring WorkDays Column exists in Payroll_Records'
PRINT '============================================================================'
GO

-- Add WorkDays column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'WorkDays')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [WorkDays] DECIMAL(5,2) NULL;
    PRINT '+ Added WorkDays column to Payroll_Records';
END
ELSE
    PRINT '= WorkDays column already exists in Payroll_Records';
GO

-- Add LeaveDays column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'LeaveDays')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [LeaveDays] DECIMAL(5,2) NULL;
    PRINT '+ Added LeaveDays column to Payroll_Records';
END
ELSE
    PRINT '= LeaveDays column already exists in Payroll_Records';
GO

-- Add OTHours column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'OTHours')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [OTHours] DECIMAL(5,2) NULL;
    PRINT '+ Added OTHours column to Payroll_Records';
END
ELSE
    PRINT '= OTHours column already exists in Payroll_Records';
GO

-- Add VoucherNumber column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'VoucherNumber')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherNumber] NVARCHAR(50) NULL;
    PRINT '+ Added VoucherNumber column to Payroll_Records';
END
ELSE
    PRINT '= VoucherNumber column already exists in Payroll_Records';
GO

-- Add VoucherGeneratedDate column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'VoucherGeneratedDate')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherGeneratedDate] DATETIME NULL;
    PRINT '+ Added VoucherGeneratedDate column to Payroll_Records';
END
ELSE
    PRINT '= VoucherGeneratedDate column already exists in Payroll_Records';
GO

-- Add VoucherGeneratedBy column if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Payroll_Records]') AND name = 'VoucherGeneratedBy')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherGeneratedBy] SMALLINT NULL;
    PRINT '+ Added VoucherGeneratedBy column to Payroll_Records';
END
ELSE
    PRINT '= VoucherGeneratedBy column already exists in Payroll_Records';
GO

-- Add Payroll Vendor for payroll payments (if not exists)
IF NOT EXISTS (SELECT * FROM [dbo].[Vendor] WHERE [Name] = N'เงินเดือนพนักงาน')
BEGIN
    -- Get the next available ID
    DECLARE @NextVendorID INT;
    SELECT @NextVendorID = ISNULL(MAX(ID), 0) + 1 FROM [dbo].[Vendor];

    INSERT INTO [dbo].[Vendor] ([ID], [Name], [Address], [Tax_ID], [Bank_Code], [Bank_Number])
    VALUES (@NextVendorID, N'เงินเดือนพนักงาน', N'Internal - Payroll', N'0000000000000', N'', N'');

    PRINT '+ Added Payroll Vendor (เงินเดือนพนักงาน) with ID: ' + CAST(@NextVendorID AS VARCHAR);
END
ELSE
    PRINT '= Payroll Vendor (เงินเดือนพนักงาน) already exists';
GO

PRINT ''
PRINT '============================================================================'
PRINT 'PHASE 7 Migration 03 Completed Successfully!'
PRINT '============================================================================'
GO
