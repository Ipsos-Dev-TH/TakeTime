-- =============================================
-- PHASE 5 - Migration 04: Product Stock System
-- Purpose: Create proper stock calculation (ProductIn - ProductOut)
-- Issue: Product table does NOT have Amount column
-- Solution: Calculate stock from Product_In and Product_Out tables
-- Date: 2025-11-06
-- =============================================

USE [Taketime];
GO

PRINT 'Starting PHASE 5 Migration 04: Product Stock System...';
PRINT '';
GO

-- =============================================
-- 1. Create Function to Calculate Product Stock
-- =============================================
IF OBJECT_ID('dbo.fn_GetProductStock', 'FN') IS NOT NULL
    DROP FUNCTION dbo.fn_GetProductStock;
GO

PRINT '📊 Creating function: fn_GetProductStock...';
GO

CREATE FUNCTION dbo.fn_GetProductStock(@ProductID INT)
RETURNS DECIMAL(18,2)
AS
BEGIN
    DECLARE @StockIn DECIMAL(18,2);
    DECLARE @StockOut DECIMAL(18,2);
    DECLARE @CurrentStock DECIMAL(18,2);

    -- Get total stock in
    SELECT @StockIn = ISNULL(SUM(Amount), 0)
    FROM Product_In
    WHERE Product_ID = @ProductID;

    -- Get total stock out
    SELECT @StockOut = ISNULL(SUM(Amount), 0)
    FROM Product_Out
    WHERE Product_ID = @ProductID;

    -- Calculate current stock
    SET @CurrentStock = @StockIn - @StockOut;

    RETURN @CurrentStock;
END
GO

PRINT '✅ Function fn_GetProductStock created successfully!';
GO

-- =============================================
-- 2. Create View for Product Stock
-- =============================================
IF OBJECT_ID('vw_ProductStock', 'V') IS NOT NULL
    DROP VIEW vw_ProductStock;
GO

PRINT '📊 Creating view: vw_ProductStock...';
GO

CREATE VIEW vw_ProductStock
AS
SELECT
    P.ID,
    P.Product_Name,
    P.Barcode,
    P.Category_ID,
    P.Sell_Price,
    P.Status,
    P.CanPreBook,
    ISNULL(PIN.TotalIn, 0) AS TotalStockIn,
    ISNULL(POUT.TotalOut, 0) AS TotalStockOut,
    ISNULL(PIN.TotalIn, 0) - ISNULL(POUT.TotalOut, 0) AS CurrentStock
FROM Product P
LEFT JOIN (
    SELECT Product_ID, SUM(Amount) AS TotalIn
    FROM Product_In
    GROUP BY Product_ID
) PIN ON P.ID = PIN.Product_ID
LEFT JOIN (
    SELECT Product_ID, SUM(Amount) AS TotalOut
    FROM Product_Out
    GROUP BY Product_ID
) POUT ON P.ID = POUT.Product_ID
WHERE P.Status = 'True';
GO

PRINT '✅ View vw_ProductStock created successfully!';
GO

-- Grant permissions
-- Note: Functions require EXECUTE permission, not SELECT
GRANT EXECUTE ON dbo.fn_GetProductStock TO PUBLIC;
GRANT SELECT ON vw_ProductStock TO PUBLIC;
GO

-- =============================================
-- 3. Test the Function and View
-- =============================================
PRINT '';
PRINT '🧪 Testing stock calculation...';
GO

-- Test function
DECLARE @TestProductID INT = (SELECT TOP 1 ID FROM Product);
IF @TestProductID IS NOT NULL
BEGIN
    SELECT
        @TestProductID AS ProductID,
        dbo.fn_GetProductStock(@TestProductID) AS CurrentStock;
END

-- Test view
SELECT TOP 10
    ID,
    Product_Name,
    TotalStockIn,
    TotalStockOut,
    CurrentStock
FROM vw_ProductStock
ORDER BY Product_Name;
GO

PRINT '';
PRINT '✅ PHASE 5 Migration 04 completed successfully!';
PRINT '';
PRINT '📋 Summary of changes:';
PRINT '   - Created fn_GetProductStock() - Calculate stock for specific product';
PRINT '   - Created vw_ProductStock - View all products with stock levels';
PRINT '';
PRINT '💡 Usage:';
PRINT '   - Get stock: SELECT dbo.fn_GetProductStock(ProductID)';
PRINT '   - View all: SELECT * FROM vw_ProductStock';
PRINT '   - Check low stock: SELECT * FROM vw_ProductStock WHERE CurrentStock < 10';
PRINT '';
GO
