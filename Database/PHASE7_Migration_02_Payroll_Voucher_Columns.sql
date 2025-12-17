-- ============================================
-- PHASE 7 Migration 02: Payroll Voucher Columns
-- Date: 2024
-- Description: Adds proper voucher tracking to Payroll_Records
-- ============================================

USE [TakeTime]
GO

PRINT 'Starting Payroll Voucher Columns Migration...'
GO

-- ============================================
-- 1. Add Voucher Columns to Payroll_Records
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payroll_Records') AND name = 'VoucherNumber')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherNumber] NVARCHAR(50) NULL
    PRINT 'Added VoucherNumber column to Payroll_Records'
END
ELSE
BEGIN
    PRINT 'VoucherNumber column already exists'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payroll_Records') AND name = 'VoucherGeneratedDate')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherGeneratedDate] DATETIME NULL
    PRINT 'Added VoucherGeneratedDate column to Payroll_Records'
END
ELSE
BEGIN
    PRINT 'VoucherGeneratedDate column already exists'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Payroll_Records') AND name = 'VoucherGeneratedBy')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records] ADD [VoucherGeneratedBy] SMALLINT NULL
    PRINT 'Added VoucherGeneratedBy column to Payroll_Records'
END
ELSE
BEGIN
    PRINT 'VoucherGeneratedBy column already exists'
END
GO

-- ============================================
-- 2. Add Foreign Key Constraint
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Payroll_Records_VoucherGeneratedBy')
BEGIN
    ALTER TABLE [dbo].[Payroll_Records]
    ADD CONSTRAINT [FK_Payroll_Records_VoucherGeneratedBy]
    FOREIGN KEY ([VoucherGeneratedBy]) REFERENCES [dbo].[Admin]([ID])
    PRINT 'Added FK_Payroll_Records_VoucherGeneratedBy constraint'
END
GO

-- ============================================
-- 3. Create Index for Voucher Number Lookup
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Payroll_Records_VoucherNumber' AND object_id = OBJECT_ID('Payroll_Records'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Payroll_Records_VoucherNumber]
    ON [dbo].[Payroll_Records] ([VoucherNumber])
    WHERE VoucherNumber IS NOT NULL
    PRINT 'Created IX_Payroll_Records_VoucherNumber index'
END
GO

-- ============================================
-- 4. Add Notes column to Account_Payment if not exists
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Account_Payment') AND name = 'Notes')
BEGIN
    ALTER TABLE [dbo].[Account_Payment] ADD [Notes] NVARCHAR(500) NULL
    PRINT 'Added Notes column to Account_Payment'
END
GO

PRINT 'Payroll Voucher Columns Migration completed successfully!'
GO
