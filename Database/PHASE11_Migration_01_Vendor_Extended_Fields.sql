-- =============================================
-- PHASE 11 Migration: Vendor Extended Fields
-- Add Email, Contact_Person, Remark columns to Vendor table
-- =============================================

PRINT '=== PHASE 11: Vendor Extended Fields ==='
PRINT 'Start: ' + CONVERT(VARCHAR, GETDATE(), 120)

-- 1. Add Email column
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'Email')
BEGIN
    ALTER TABLE [dbo].[Vendor] ADD [Email] NVARCHAR(200) NULL;
    PRINT '+ Added column: Vendor.Email'
END
ELSE
    PRINT '= Column Vendor.Email already exists'

-- 2. Add Contact_Person column
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'Contact_Person')
BEGIN
    ALTER TABLE [dbo].[Vendor] ADD [Contact_Person] NVARCHAR(200) NULL;
    PRINT '+ Added column: Vendor.Contact_Person'
END
ELSE
    PRINT '= Column Vendor.Contact_Person already exists'

-- 3. Add Remark column
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'Remark')
BEGIN
    ALTER TABLE [dbo].[Vendor] ADD [Remark] NVARCHAR(MAX) NULL;
    PRINT '+ Added column: Vendor.Remark'
END
ELSE
    PRINT '= Column Vendor.Remark already exists'

-- 4. Add Created_Date column (for audit trail)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'Created_Date')
BEGIN
    ALTER TABLE [dbo].[Vendor] ADD [Created_Date] DATETIME NULL DEFAULT GETDATE();
    PRINT '+ Added column: Vendor.Created_Date'
END
ELSE
    PRINT '= Column Vendor.Created_Date already exists'

-- 5. Add Updated_Date column (for audit trail)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Vendor' AND COLUMN_NAME = 'Updated_Date')
BEGIN
    ALTER TABLE [dbo].[Vendor] ADD [Updated_Date] DATETIME NULL;
    PRINT '+ Added column: Vendor.Updated_Date'
END
ELSE
    PRINT '= Column Vendor.Updated_Date already exists'

PRINT ''
PRINT '=== PHASE 11 Migration Complete ==='
PRINT 'End: ' + CONVERT(VARCHAR, GETDATE(), 120)
