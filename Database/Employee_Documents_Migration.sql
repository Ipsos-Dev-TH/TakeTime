-- =============================================
-- Employee Documents Table Migration
-- Created: 2025-12-23
-- Description: Store employee related documents
-- =============================================

USE [TakeTime]
GO

-- Create Employee_Documents table if not exists
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Documents]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[Employee_Documents](
        [ID] [bigint] IDENTITY(1,1) NOT NULL,
        [Admin_ID] [smallint] NOT NULL,
        [DocumentType] [nvarchar](50) NOT NULL,
        [DocumentName] [nvarchar](255) NOT NULL,
        [FileName] [nvarchar](255) NOT NULL,
        [FilePath] [nvarchar](500) NOT NULL,
        [FileSize] [bigint] NULL,
        [ContentType] [nvarchar](100) NULL,
        [Description] [nvarchar](500) NULL,
        [UploadedBy] [smallint] NULL,
        [UploadedDate] [datetime] NOT NULL DEFAULT (GETDATE()),
        [ExpiryDate] [date] NULL,
        [IsActive] [bit] NOT NULL DEFAULT (1),
        CONSTRAINT [PK_Employee_Documents] PRIMARY KEY CLUSTERED ([ID] ASC)
    );

    PRINT 'Table Employee_Documents created successfully';
END
ELSE
BEGIN
    PRINT 'Table Employee_Documents already exists';
END
GO

-- Add foreign key to Admin table if not exists
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Employee_Documents_Admin')
BEGIN
    ALTER TABLE [dbo].[Employee_Documents]
    ADD CONSTRAINT [FK_Employee_Documents_Admin]
    FOREIGN KEY ([Admin_ID]) REFERENCES [dbo].[Admin]([ID]);

    PRINT 'Foreign key FK_Employee_Documents_Admin added';
END
GO

-- Create index for faster queries
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_Employee_Documents_AdminID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Employee_Documents_AdminID]
    ON [dbo].[Employee_Documents] ([Admin_ID] ASC);

    PRINT 'Index IX_Employee_Documents_AdminID created';
END
GO

-- Insert default document types (for reference)
-- DocumentType values:
-- 'ID_CARD' - สำเนาบัตรประชาชน
-- 'HOUSE_REG' - สำเนาทะเบียนบ้าน
-- 'BANK_BOOK' - สำเนาหน้าสมุดบัญชี
-- 'CONTRACT' - สัญญาจ้างงาน
-- 'RESUME' - ประวัติย่อ/Resume
-- 'CERTIFICATE' - ใบรับรอง/Certificate
-- 'MEDICAL' - ใบรับรองแพทย์
-- 'OTHER' - เอกสารอื่นๆ

PRINT 'Employee Documents Migration completed successfully';
GO
