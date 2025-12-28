-- =============================================
-- Add Product Charge Type to Account_ProductType
-- For room charges (charge-to-room) functionality
-- Created: 2025-12-28
-- =============================================

-- Add ProductType_ID = 3 for Product Charges (ค่าสินค้า/บริการชาร์จเข้าห้อง)
IF NOT EXISTS (SELECT 1 FROM Account_ProductType WHERE ID = 3)
BEGIN
    -- First, check if IDENTITY_INSERT is needed
    IF EXISTS (SELECT * FROM sys.columns c
               JOIN sys.tables t ON c.object_id = t.object_id
               WHERE t.name = 'Account_ProductType'
               AND c.name = 'ID'
               AND c.is_identity = 1)
    BEGIN
        SET IDENTITY_INSERT Account_ProductType ON;

        INSERT INTO Account_ProductType (ID, ProductType)
        VALUES (3, N'สินค้า/บริการ');

        SET IDENTITY_INSERT Account_ProductType OFF;

        PRINT 'Added ProductType ID 3: สินค้า/บริการ (with IDENTITY_INSERT)';
    END
    ELSE
    BEGIN
        INSERT INTO Account_ProductType (ID, ProductType)
        VALUES (3, N'สินค้า/บริการ');

        PRINT 'Added ProductType ID 3: สินค้า/บริการ';
    END
END
ELSE
BEGIN
    PRINT 'ProductType ID 3 already exists';
END
GO

-- Verify the product types exist
SELECT 'Account_ProductType Contents:' AS Info;
SELECT * FROM Account_ProductType ORDER BY ID;
GO
