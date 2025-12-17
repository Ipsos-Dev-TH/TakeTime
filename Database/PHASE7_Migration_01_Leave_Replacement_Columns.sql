-- ============================================
-- PHASE 7 Migration 01: Leave Replacement Columns
-- Date: 2024
-- Description: Adds work replacement tracking to Leave_Requests
-- ============================================

USE [TakeTime]
GO

PRINT 'Starting Leave Replacement Columns Migration...'
GO

-- ============================================
-- 1. Add Work Replacement Columns to Leave_Requests
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leave_Requests') AND name = 'IsReplaced')
BEGIN
    ALTER TABLE [dbo].[Leave_Requests] ADD [IsReplaced] BIT NOT NULL DEFAULT 0
    PRINT 'Added IsReplaced column to Leave_Requests'
END
ELSE
BEGIN
    PRINT 'IsReplaced column already exists'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leave_Requests') AND name = 'ReplacementDate')
BEGIN
    ALTER TABLE [dbo].[Leave_Requests] ADD [ReplacementDate] DATE NULL
    PRINT 'Added ReplacementDate column to Leave_Requests'
END
ELSE
BEGIN
    PRINT 'ReplacementDate column already exists'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leave_Requests') AND name = 'ReplacementApprovedBy')
BEGIN
    ALTER TABLE [dbo].[Leave_Requests] ADD [ReplacementApprovedBy] SMALLINT NULL
    PRINT 'Added ReplacementApprovedBy column to Leave_Requests'
END
ELSE
BEGIN
    PRINT 'ReplacementApprovedBy column already exists'
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Leave_Requests') AND name = 'ReplacementApprovedDate')
BEGIN
    ALTER TABLE [dbo].[Leave_Requests] ADD [ReplacementApprovedDate] DATETIME NULL
    PRINT 'Added ReplacementApprovedDate column to Leave_Requests'
END
ELSE
BEGIN
    PRINT 'ReplacementApprovedDate column already exists'
END
GO

-- ============================================
-- 2. Add Foreign Key Constraint for ReplacementApprovedBy
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Leave_Requests_ReplacementApprovedBy')
BEGIN
    ALTER TABLE [dbo].[Leave_Requests]
    ADD CONSTRAINT [FK_Leave_Requests_ReplacementApprovedBy]
    FOREIGN KEY ([ReplacementApprovedBy]) REFERENCES [dbo].[Admin]([ID])
    PRINT 'Added FK_Leave_Requests_ReplacementApprovedBy constraint'
END
GO

-- ============================================
-- 3. Create Index for Work Replacement Queries
-- ============================================
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Leave_Requests_IsReplaced' AND object_id = OBJECT_ID('Leave_Requests'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Leave_Requests_IsReplaced]
    ON [dbo].[Leave_Requests] ([IsReplaced], [Status])
    INCLUDE ([Admin_ID], [TotalDays], [ReplacementDate])
    PRINT 'Created IX_Leave_Requests_IsReplaced index'
END
GO

-- ============================================
-- 4. Add Comments
-- ============================================
EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'Indicates if the leave was replaced by working on another day (ทำงานทดแทน)',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'Leave_Requests',
    @level2type = N'COLUMN', @level2name = 'IsReplaced'
GO

EXEC sp_addextendedproperty
    @name = N'MS_Description',
    @value = N'The date the employee worked to replace the leave day',
    @level0type = N'SCHEMA', @level0name = 'dbo',
    @level1type = N'TABLE', @level1name = 'Leave_Requests',
    @level2type = N'COLUMN', @level2name = 'ReplacementDate'
GO

PRINT 'Leave Replacement Columns Migration completed successfully!'
GO
