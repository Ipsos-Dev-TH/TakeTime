-- =============================================
-- Asset Management System Migration Script v2
-- สร้างระบบจัดการสินทรัพย์ถาวร (แก้ไข)
-- =============================================

-- ลบ Objects เดิมก่อน (ถ้ามี)
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_AssetSummary')
    DROP VIEW vw_AssetSummary;

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'sp_CalculateMonthlyDepreciation')
    DROP PROCEDURE sp_CalculateMonthlyDepreciation;

IF EXISTS (SELECT * FROM sys.objects WHERE name = 'fn_GenerateAssetCode' AND type = 'FN')
    DROP FUNCTION fn_GenerateAssetCode;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Asset_History')
    DROP TABLE Asset_History;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Asset_Depreciation')
    DROP TABLE Asset_Depreciation;

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Assets')
    DROP TABLE Assets;

-- ไม่ลบ Asset_Category เพื่อเก็บข้อมูลเดิม

PRINT 'Cleaned up old objects';

-- 1. ตารางหมวดหมู่สินทรัพย์
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Asset_Category')
BEGIN
    CREATE TABLE [dbo].[Asset_Category] (
        [ID] INT IDENTITY(1,1) PRIMARY KEY,
        [CategoryCode] NVARCHAR(20) NOT NULL,
        [CategoryName] NVARCHAR(100) NOT NULL,
        [Description] NVARCHAR(500) NULL,
        [DepreciationMethod] NVARCHAR(50) DEFAULT 'STRAIGHT_LINE',
        [DefaultUsefulLifeYears] INT DEFAULT 5,
        [DefaultResidualValuePercent] DECIMAL(5,2) DEFAULT 0,
        [AccountCode] NVARCHAR(20) NULL,
        [Status] BIT DEFAULT 1,
        [CreatedDate] DATETIME DEFAULT GETDATE(),
        [CreatedBy] SMALLINT NULL,
        [ModifiedDate] DATETIME NULL,
        [ModifiedBy] SMALLINT NULL
    );
    PRINT 'Created Asset_Category table';
END
ELSE
BEGIN
    PRINT 'Asset_Category table already exists';
END
GO

-- 2. ตารางสินทรัพย์หลัก
CREATE TABLE [dbo].[Assets] (
    [ID] INT IDENTITY(1,1) PRIMARY KEY,
    [AssetCode] NVARCHAR(30) NOT NULL UNIQUE,
    [AssetName] NVARCHAR(200) NOT NULL,
    [Description] NVARCHAR(1000) NULL,
    [CategoryID] INT NOT NULL,
    [SerialNumber] NVARCHAR(100) NULL,
    [Brand] NVARCHAR(100) NULL,
    [Model] NVARCHAR(100) NULL,
    [PurchaseDate] DATE NOT NULL,
    [PurchasePrice] DECIMAL(18,2) NOT NULL,
    [VendorID] INT NULL,
    [PaymentVoucherID] NVARCHAR(20) NULL,
    [InvoiceNumber] NVARCHAR(50) NULL,
    [WarrantyExpireDate] DATE NULL,
    [DepreciationMethod] NVARCHAR(50) DEFAULT 'STRAIGHT_LINE',
    [UsefulLifeYears] INT NOT NULL DEFAULT 5,
    [UsefulLifeMonths] INT NOT NULL DEFAULT 60,
    [ResidualValue] DECIMAL(18,2) DEFAULT 0,
    [DepreciationStartDate] DATE NULL,
    [MonthlyDepreciation] DECIMAL(18,2) NULL,
    [AccumulatedDepreciation] DECIMAL(18,2) DEFAULT 0,
    [BookValue] DECIMAL(18,2) NULL,
    [Location] NVARCHAR(200) NULL,
    [Department] NVARCHAR(100) NULL,
    [ResponsiblePersonID] SMALLINT NULL,
    [Status] NVARCHAR(20) DEFAULT 'ACTIVE',
    [DisposalDate] DATE NULL,
    [DisposalPrice] DECIMAL(18,2) NULL,
    [DisposalReason] NVARCHAR(500) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [ImagePath] NVARCHAR(500) NULL,
    [CreatedDate] DATETIME DEFAULT GETDATE(),
    [CreatedBy] SMALLINT NULL,
    [ModifiedDate] DATETIME NULL,
    [ModifiedBy] SMALLINT NULL,
    CONSTRAINT FK_Assets_Category FOREIGN KEY (CategoryID) REFERENCES Asset_Category(ID)
);
PRINT 'Created Assets table';

-- Create indexes
CREATE INDEX IX_Assets_CategoryID ON Assets(CategoryID);
CREATE INDEX IX_Assets_Status ON Assets(Status);
CREATE INDEX IX_Assets_PurchaseDate ON Assets(PurchaseDate);
CREATE INDEX IX_Assets_PaymentVoucherID ON Assets(PaymentVoucherID);
GO

-- 3. ตารางประวัติค่าเสื่อมราคารายเดือน
CREATE TABLE [dbo].[Asset_Depreciation] (
    [ID] INT IDENTITY(1,1) PRIMARY KEY,
    [AssetID] INT NOT NULL,
    [Year] INT NOT NULL,
    [Month] INT NOT NULL,
    [DepreciationAmount] DECIMAL(18,2) NOT NULL,
    [AccumulatedDepreciation] DECIMAL(18,2) NOT NULL,
    [BookValue] DECIMAL(18,2) NOT NULL,
    [IsCalculated] BIT DEFAULT 0,
    [CalculatedDate] DATETIME NULL,
    [Notes] NVARCHAR(500) NULL,
    [CreatedDate] DATETIME DEFAULT GETDATE(),
    CONSTRAINT FK_AssetDepreciation_Asset FOREIGN KEY (AssetID) REFERENCES Assets(ID),
    CONSTRAINT UQ_AssetDepreciation_YearMonth UNIQUE (AssetID, [Year], [Month])
);
PRINT 'Created Asset_Depreciation table';

CREATE INDEX IX_AssetDepreciation_AssetID ON Asset_Depreciation(AssetID);
CREATE INDEX IX_AssetDepreciation_YearMonth ON Asset_Depreciation([Year], [Month]);
GO

-- 4. ตารางประวัติการโอนย้าย/บำรุงรักษา
CREATE TABLE [dbo].[Asset_History] (
    [ID] INT IDENTITY(1,1) PRIMARY KEY,
    [AssetID] INT NOT NULL,
    [ActionType] NVARCHAR(50) NOT NULL,
    [ActionDate] DATE NOT NULL,
    [Description] NVARCHAR(1000) NULL,
    [FromLocation] NVARCHAR(200) NULL,
    [ToLocation] NVARCHAR(200) NULL,
    [FromDepartment] NVARCHAR(100) NULL,
    [ToDepartment] NVARCHAR(100) NULL,
    [Cost] DECIMAL(18,2) NULL,
    [CreatedDate] DATETIME DEFAULT GETDATE(),
    [CreatedBy] SMALLINT NULL,
    CONSTRAINT FK_AssetHistory_Asset FOREIGN KEY (AssetID) REFERENCES Assets(ID)
);
PRINT 'Created Asset_History table';
GO

-- 5. Insert default categories (ถ้ายังไม่มี)
IF NOT EXISTS (SELECT * FROM Asset_Category WHERE CategoryCode = 'COMP')
BEGIN
    INSERT INTO Asset_Category (CategoryCode, CategoryName, Description, DefaultUsefulLifeYears, AccountCode) VALUES
    ('COMP', N'คอมพิวเตอร์และอุปกรณ์', N'คอมพิวเตอร์ โน๊ตบุ๊ค เครื่องพิมพ์ อุปกรณ์ IT', 3, '1520'),
    ('FURN', N'เฟอร์นิเจอร์และอุปกรณ์สำนักงาน', N'โต๊ะ เก้าอี้ ตู้เอกสาร', 5, '1530'),
    ('ELEC', N'เครื่องใช้ไฟฟ้า', N'แอร์ ตู้เย็น เครื่องซักผ้า โทรทัศน์', 5, '1540'),
    ('VEHI', N'ยานพาหนะ', N'รถยนต์ รถจักรยานยนต์', 5, '1550'),
    ('MACH', N'เครื่องจักรและอุปกรณ์', N'เครื่องจักร เครื่องมือช่าง', 10, '1560'),
    ('BUIL', N'อาคารและสิ่งปลูกสร้าง', N'อาคาร โรงเรือน สิ่งก่อสร้าง', 20, '1510'),
    ('LAND', N'ที่ดิน', N'ที่ดิน (ไม่คิดค่าเสื่อม)', 0, '1500'),
    ('TOOL', N'เครื่องมือและอุปกรณ์', N'เครื่องมือช่าง อุปกรณ์การเกษตร', 5, '1570'),
    ('OTHR', N'สินทรัพย์อื่นๆ', N'สินทรัพย์อื่นที่ไม่จัดอยู่ในหมวดหมู่ข้างต้น', 5, '1590');
    PRINT 'Inserted default asset categories';
END
GO

-- 6. Create view for asset summary
CREATE VIEW [dbo].[vw_AssetSummary] AS
SELECT
    a.ID,
    a.AssetCode,
    a.AssetName,
    a.Description,
    a.CategoryID,
    c.CategoryCode,
    c.CategoryName,
    a.SerialNumber,
    a.Brand,
    a.Model,
    a.PurchaseDate,
    a.PurchasePrice,
    a.UsefulLifeYears,
    a.ResidualValue,
    a.MonthlyDepreciation,
    a.AccumulatedDepreciation,
    a.BookValue,
    a.Location,
    a.Department,
    a.Status,
    a.DisposalDate,
    a.DisposalPrice,
    a.VendorID,
    v.Name AS VendorName,
    a.PaymentVoucherID,
    a.ResponsiblePersonID,
    adm.UserName AS ResponsiblePerson,
    a.DepreciationStartDate,
    a.UsefulLifeMonths,
    DATEDIFF(MONTH, a.DepreciationStartDate, GETDATE()) AS MonthsDepreciated,
    a.UsefulLifeMonths - ISNULL(DATEDIFF(MONTH, a.DepreciationStartDate, GETDATE()), 0) AS RemainingMonths,
    a.CreatedDate,
    a.ModifiedDate
FROM Assets a
LEFT JOIN Asset_Category c ON a.CategoryID = c.ID
LEFT JOIN Vendor v ON a.VendorID = v.ID
LEFT JOIN Admin adm ON a.ResponsiblePersonID = adm.ID;
GO

PRINT 'Created vw_AssetSummary view';
GO

-- 7. Create stored procedure for calculating depreciation
CREATE PROCEDURE [dbo].[sp_CalculateMonthlyDepreciation]
    @Year INT,
    @Month INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @CalculationDate DATE = DATEFROMPARTS(@Year, @Month, 1);
    DECLARE @EndOfMonth DATE = EOMONTH(@CalculationDate);

    -- คำนวณค่าเสื่อมสำหรับสินทรัพย์ที่ยัง Active
    INSERT INTO Asset_Depreciation (AssetID, [Year], [Month], DepreciationAmount, AccumulatedDepreciation, BookValue, IsCalculated, CalculatedDate)
    SELECT
        a.ID,
        @Year,
        @Month,
        a.MonthlyDepreciation,
        a.AccumulatedDepreciation + a.MonthlyDepreciation,
        CASE
            WHEN a.BookValue - a.MonthlyDepreciation < a.ResidualValue
            THEN a.ResidualValue
            ELSE a.BookValue - a.MonthlyDepreciation
        END,
        1,
        GETDATE()
    FROM Assets a
    WHERE a.Status = 'ACTIVE'
        AND a.DepreciationStartDate <= @EndOfMonth
        AND a.BookValue > a.ResidualValue
        AND NOT EXISTS (
            SELECT 1 FROM Asset_Depreciation ad
            WHERE ad.AssetID = a.ID AND ad.[Year] = @Year AND ad.[Month] = @Month
        );

    -- อัพเดทค่าเสื่อมสะสมและมูลค่าตามบัญชีในตาราง Assets
    UPDATE a
    SET
        a.AccumulatedDepreciation = d.AccumulatedDepreciation,
        a.BookValue = d.BookValue,
        a.ModifiedDate = GETDATE()
    FROM Assets a
    INNER JOIN Asset_Depreciation d ON a.ID = d.AssetID
    WHERE d.[Year] = @Year AND d.[Month] = @Month AND d.IsCalculated = 1;

    SELECT @@ROWCOUNT AS AffectedAssets;
END
GO

PRINT 'Created sp_CalculateMonthlyDepreciation procedure';
GO

-- 8. Create function to generate asset code
CREATE FUNCTION [dbo].[fn_GenerateAssetCode]
(
    @CategoryCode NVARCHAR(20)
)
RETURNS NVARCHAR(30)
AS
BEGIN
    DECLARE @Year NVARCHAR(4) = RIGHT(YEAR(GETDATE()), 2);
    DECLARE @NextNum INT;

    SELECT @NextNum = ISNULL(MAX(CAST(RIGHT(AssetCode, 4) AS INT)), 0) + 1
    FROM Assets
    WHERE AssetCode LIKE @CategoryCode + '-' + @Year + '-%';

    RETURN @CategoryCode + '-' + @Year + '-' + RIGHT('0000' + CAST(@NextNum AS NVARCHAR(4)), 4);
END
GO

PRINT 'Created fn_GenerateAssetCode function';
PRINT '';
PRINT '========================================';
PRINT 'Asset Management Migration v2 Completed!';
PRINT '========================================';
