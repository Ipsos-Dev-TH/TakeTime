-- =============================================
-- PHASE 4 - Enhanced Accounting System
-- Migration 01: Enhanced Revenue and Expense Views
-- Date: 2025-11-06
-- Description:
--   1. Create view for detailed revenue by receipt items
--   2. Create view for deposit-only receipts
--   3. Create view for expense summary from Payment Vouchers
--   4. Create view for profit/loss calculation
-- =============================================

BEGIN TRANSACTION;

PRINT 'Starting PHASE4_Migration_01: Enhanced Revenue and Expense Views...'

-- =============================================
-- 1. CREATE VIEW FOR DETAILED REVENUE BY RECEIPT ITEMS
-- =============================================

PRINT '1. Creating vw_Revenue_Detail_By_Items view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Revenue_Detail_By_Items]'))
BEGIN
    DROP VIEW [dbo].[vw_Revenue_Detail_By_Items];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Revenue_Detail_By_Items]
AS
SELECT
    ar.ID AS Receipt_ID,
    ar.Created_Date,
    ar.Reservation_ID,
    ar.Status,
    ar.IsDeposit,
    ar.UseDeposit,
    ar.Paid_Type,
    ar.Total_Amount,
    ar.Vat,

    -- Reservation info
    r.CheckinDate,
    r.CheckoutDate,
    r.Customer_MobilePhone,

    -- Receipt detail info
    ard.Number AS Item_Number,
    ard.ProductType_ID,
    pt.Product_Type,
    ard.Product_Data,
    ard.Price_Amount,
    ard.Quantity,
    ard.Discount_Amount,

    -- Calculated fields
    CASE
        WHEN ar.IsDeposit = 1 THEN 'มัดจำ'
        WHEN ar.UseDeposit = 1 THEN 'หักมัดจำ'
        ELSE 'ปกติ'
    END AS Receipt_Type,

    CASE
        WHEN ar.Reservation_ID > 0 AND r.CheckinDate IS NOT NULL THEN 'จองพัก'
        WHEN ar.Reservation_ID > 0 AND r.CheckinDate IS NULL THEN 'จองพัก (ไม่มีวันเข้าพัก)'
        WHEN ard.ProductType_ID = 3 THEN 'ขายของ'
        ELSE 'อื่นๆ'
    END AS Revenue_Category

FROM Account_Receipt ar
LEFT JOIN Reservation r ON ar.Reservation_ID = r.ID
LEFT JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
LEFT JOIN Account_ProductType pt ON ard.ProductType_ID = pt.ID
WHERE ar.Status = 'Normal';
GO

PRINT '   ✓ vw_Revenue_Detail_By_Items view created'
GO

-- =============================================
-- 2. CREATE VIEW FOR DEPOSIT TRACKING
-- =============================================

PRINT '2. Creating vw_Deposit_Receipts view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Deposit_Receipts]'))
BEGIN
    DROP VIEW [dbo].[vw_Deposit_Receipts];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Deposit_Receipts]
AS
SELECT
    ar.ID AS Receipt_ID,
    ar.Created_Date,
    ar.Reservation_ID,
    ar.Total_Amount AS Deposit_Amount,
    ar.Paid_Type,
    ar.Status,

    r.CheckinDate,
    r.CheckoutDate,
    r.Customer_MobilePhone,
    r.TotalPrice AS Reservation_Total,

    -- Classification
    CASE
        WHEN r.CheckinDate IS NULL THEN 'NO_CHECKIN_DATE'
        ELSE 'HAS_CHECKIN_DATE'
    END AS Checkin_Status,

    -- For revenue categorization
    CONVERT(DATE, ar.Created_Date) AS Receipt_Date,
    CONVERT(DATE, r.CheckinDate) AS Checkin_Date

FROM Account_Receipt ar
INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
WHERE ar.Status = 'Normal'
  AND ar.IsDeposit = 1;  -- Only deposit receipts
GO

PRINT '   ✓ vw_Deposit_Receipts view created'
GO

-- =============================================
-- 3. CREATE VIEW FOR EXPENSE SUMMARY
-- =============================================

PRINT '3. Creating vw_Expense_Summary view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Expense_Summary]'))
BEGIN
    DROP VIEW [dbo].[vw_Expense_Summary];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Expense_Summary]
AS
SELECT
    ap.ID AS Payment_ID,
    ap.Created_Date,
    ap.Vendor_ID,
    v.Name AS Vendor_Name,
    v.Vendor_Group,
    ap.Total_Amount_Exclude_Vat,
    ap.Vat,
    ap.Total_Amount,
    ap.Paid_How,
    ap.Paid_Type,
    ap.Vat_Type_ID,
    ap.Status,

    -- Payment details
    apd.Number AS Item_Number,
    apd.Detail AS Item_Detail,
    apd.Amount AS Item_Amount,

    -- Categorization
    CONVERT(DATE, ap.Created_Date) AS Payment_Date,
    YEAR(ap.Created_Date) AS Payment_Year,
    MONTH(ap.Created_Date) AS Payment_Month

FROM Account_Payment ap
LEFT JOIN Vendor v ON ap.Vendor_ID = v.ID
LEFT JOIN Account_Payment_Detail apd ON ap.ID = apd.Payment_ID
WHERE ap.Status = 'Normal';
GO

PRINT '   ✓ vw_Expense_Summary view created'
GO

-- =============================================
-- 4. CREATE REVENUE CATEGORIZATION FUNCTION
-- =============================================

PRINT '4. Creating fn_Categorize_Receipt function...'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[fn_Categorize_Receipt]') AND type in (N'FN', N'IF', N'TF'))
BEGIN
    DROP FUNCTION [dbo].[fn_Categorize_Receipt];
    PRINT '   → Dropped existing function'
END
GO

CREATE FUNCTION [dbo].[fn_Categorize_Receipt]
(
    @ReceiptID NVARCHAR(50),
    @StartDate DATETIME,
    @EndDate DATETIME
)
RETURNS INT
AS
BEGIN
    DECLARE @Category INT = 0;
    DECLARE @ReservationID BIGINT;
    DECLARE @CheckinDate DATETIME;
    DECLARE @CreatedDate DATETIME;
    DECLARE @IsDeposit BIT;

    -- Get receipt info
    SELECT
        @ReservationID = Reservation_ID,
        @CreatedDate = Created_Date,
        @IsDeposit = IsDeposit
    FROM Account_Receipt
    WHERE ID = @ReceiptID AND Status = 'Normal';

    -- If no reservation, check if it's product sale or other
    IF @ReservationID IS NULL OR @ReservationID = 0
    BEGIN
        -- Check if it's product sale (ProductType_ID = 3)
        IF EXISTS (
            SELECT 1 FROM Account_Receipt_Detail
            WHERE Receipt_ID = @ReceiptID AND ProductType_ID = 3
        )
            SET @Category = 3; -- Product sales
        ELSE
            SET @Category = 4; -- Others

        RETURN @Category;
    END

    -- Get checkin date
    SELECT @CheckinDate = CheckinDate
    FROM Reservation
    WHERE ID = @ReservationID;

    -- Categorize based on dates and deposit status
    IF @CheckinDate >= @StartDate AND @CheckinDate <= @EndDate
    BEGIN
        -- Category 1: Check-in within date range
        -- Exclude deposits (deposits are in Category 2)
        IF @IsDeposit = 0
            SET @Category = 1;
        ELSE
            SET @Category = 0; -- Deposit excluded from Category 1
    END
    ELSE IF @CreatedDate >= @StartDate AND @CreatedDate < DATEADD(DAY, 1, @EndDate)
    BEGIN
        -- Category 2: Receipt created in range, but check-in outside
        -- Only include deposits
        IF @IsDeposit = 1
            SET @Category = 2;
        ELSE
            SET @Category = 0;
    END

    RETURN @Category;
END
GO

PRINT '   ✓ fn_Categorize_Receipt function created'
GO

-- =============================================
-- 5. CREATE ENHANCED REVENUE SUMMARY VIEW
-- =============================================

PRINT '5. Creating vw_Revenue_By_Category view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Revenue_By_Category]'))
BEGIN
    DROP VIEW [dbo].[vw_Revenue_By_Category];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Revenue_By_Category]
AS
WITH Revenue_Items AS (
    SELECT
        ar.ID AS Receipt_ID,
        ar.Created_Date,
        ar.Reservation_ID,
        ar.IsDeposit,
        ar.Total_Amount,
        ar.Vat,
        ar.Paid_Type,
        r.CheckinDate,
        r.CheckoutDate,

        -- Item details
        ISNULL(SUM(ard.Price_Amount * ard.Quantity - ard.Discount_Amount), ar.Total_Amount) AS Items_Total,
        COUNT(ard.Number) AS Item_Count,

        -- Has product sales?
        MAX(CASE WHEN ard.ProductType_ID = 3 THEN 1 ELSE 0 END) AS Has_Product_Sale

    FROM Account_Receipt ar
    LEFT JOIN Reservation r ON ar.Reservation_ID = r.ID
    LEFT JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
    WHERE ar.Status = 'Normal'
    GROUP BY ar.ID, ar.Created_Date, ar.Reservation_ID, ar.IsDeposit,
             ar.Total_Amount, ar.Vat, ar.Paid_Type, r.CheckinDate, r.CheckoutDate
)
SELECT
    Receipt_ID,
    Created_Date,
    Reservation_ID,
    IsDeposit,
    Total_Amount,
    Vat,
    Paid_Type,
    CheckinDate,
    CheckoutDate,
    Items_Total,
    Item_Count,

    -- Determine category
    CASE
        WHEN Reservation_ID > 0 AND CheckinDate IS NOT NULL AND IsDeposit = 0 THEN 'CAT1_CHECKIN'
        WHEN Reservation_ID > 0 AND IsDeposit = 1 THEN 'CAT2_DEPOSIT'
        WHEN (Reservation_ID IS NULL OR Reservation_ID = 0) AND Has_Product_Sale = 1 THEN 'CAT3_PRODUCT'
        WHEN (Reservation_ID IS NULL OR Reservation_ID = 0) THEN 'CAT4_OTHER'
        ELSE 'UNCATEGORIZED'
    END AS Category,

    CONVERT(DATE, Created_Date) AS Receipt_Date,
    CONVERT(DATE, CheckinDate) AS Checkin_Date

FROM Revenue_Items;
GO

PRINT '   ✓ vw_Revenue_By_Category view created'
GO

-- =============================================
-- 6. CREATE PROFIT/LOSS SUMMARY VIEW
-- =============================================

PRINT '6. Creating vw_Profit_Loss_Summary view...'

IF EXISTS (SELECT * FROM sys.views WHERE object_id = OBJECT_ID(N'[dbo].[vw_Profit_Loss_Summary]'))
BEGIN
    DROP VIEW [dbo].[vw_Profit_Loss_Summary];
    PRINT '   → Dropped existing view'
END
GO

CREATE VIEW [dbo].[vw_Profit_Loss_Summary]
AS
WITH Daily_Revenue AS (
    SELECT
        CONVERT(DATE, Created_Date) AS Business_Date,
        SUM(Total_Amount) AS Total_Revenue,
        SUM(Vat) AS Total_VAT,
        COUNT(DISTINCT ID) AS Receipt_Count
    FROM Account_Receipt
    WHERE Status = 'Normal'
    GROUP BY CONVERT(DATE, Created_Date)
),
Daily_Expense AS (
    SELECT
        CONVERT(DATE, Created_Date) AS Business_Date,
        SUM(Total_Amount) AS Total_Expense,
        SUM(Vat) AS Total_VAT,
        COUNT(DISTINCT ID) AS Payment_Count
    FROM Account_Payment
    WHERE Status = 'Normal'
    GROUP BY CONVERT(DATE, Created_Date)
)
SELECT
    COALESCE(r.Business_Date, e.Business_Date) AS Business_Date,
    ISNULL(r.Total_Revenue, 0) AS Revenue,
    ISNULL(r.Total_VAT, 0) AS Revenue_VAT,
    ISNULL(r.Receipt_Count, 0) AS Receipt_Count,
    ISNULL(e.Total_Expense, 0) AS Expense,
    ISNULL(e.Total_VAT, 0) AS Expense_VAT,
    ISNULL(e.Payment_Count, 0) AS Payment_Count,
    ISNULL(r.Total_Revenue, 0) - ISNULL(e.Total_Expense, 0) AS Net_Profit,

    YEAR(COALESCE(r.Business_Date, e.Business_Date)) AS Year,
    MONTH(COALESCE(r.Business_Date, e.Business_Date)) AS Month

FROM Daily_Revenue r
FULL OUTER JOIN Daily_Expense e ON r.Business_Date = e.Business_Date;
GO

PRINT '   ✓ vw_Profit_Loss_Summary view created'
GO

-- =============================================
-- 7. CREATE STORED PROCEDURE FOR REVENUE REPORT
-- =============================================

PRINT '7. Creating sp_Get_Revenue_Report stored procedure...'

IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[sp_Get_Revenue_Report]') AND type in (N'P'))
BEGIN
    DROP PROCEDURE [dbo].[sp_Get_Revenue_Report];
    PRINT '   → Dropped existing procedure'
END
GO

CREATE PROCEDURE [dbo].[sp_Get_Revenue_Report]
    @StartDate DATETIME,
    @EndDate DATETIME
AS
BEGIN
    SET NOCOUNT ON;

    -- Category 1: Check-in in range (excluding deposits)
    SELECT 'Category 1' AS Category, 'Check-in in range' AS Description,
           ar.ID, ar.Created_Date, ar.Total_Amount, ar.Vat, ar.Paid_Type,
           r.CheckinDate, r.CheckoutDate, r.Customer_MobilePhone
    FROM Account_Receipt ar
    INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
    WHERE ar.Status = 'Normal'
      AND ar.IsDeposit = 0  -- Exclude deposits
      AND r.CheckinDate >= @StartDate
      AND r.CheckinDate <= @EndDate
    UNION ALL

    -- Category 2: Deposit paid in range, check-in outside
    SELECT 'Category 2' AS Category, 'Deposit in range, check-in outside' AS Description,
           ar.ID, ar.Created_Date, ar.Total_Amount, ar.Vat, ar.Paid_Type,
           r.CheckinDate, r.CheckoutDate, r.Customer_MobilePhone
    FROM Account_Receipt ar
    INNER JOIN Reservation r ON ar.Reservation_ID = r.ID
    WHERE ar.Status = 'Normal'
      AND ar.IsDeposit = 1  -- Only deposits
      AND ar.Created_Date >= @StartDate
      AND ar.Created_Date < DATEADD(DAY, 1, @EndDate)
      AND (r.CheckinDate < @StartDate OR r.CheckinDate > @EndDate OR r.CheckinDate IS NULL)
    UNION ALL

    -- Category 3: Product sales (no reservation)
    SELECT 'Category 3' AS Category, 'Product sales' AS Description,
           ar.ID, ar.Created_Date, ar.Total_Amount, ar.Vat, ar.Paid_Type,
           NULL AS CheckinDate, NULL AS CheckoutDate, NULL AS Customer_MobilePhone
    FROM Account_Receipt ar
    INNER JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
    WHERE ar.Status = 'Normal'
      AND (ar.Reservation_ID IS NULL OR ar.Reservation_ID = 0)
      AND ard.ProductType_ID = 3
      AND ar.Created_Date >= @StartDate
      AND ar.Created_Date < DATEADD(DAY, 1, @EndDate)
    UNION ALL

    -- Category 4: Others
    SELECT 'Category 4' AS Category, 'Others' AS Description,
           ar.ID, ar.Created_Date, ar.Total_Amount, ar.Vat, ar.Paid_Type,
           NULL AS CheckinDate, NULL AS CheckoutDate, NULL AS Customer_MobilePhone
    FROM Account_Receipt ar
    LEFT JOIN Account_Receipt_Detail ard ON ar.ID = ard.Receipt_ID
    WHERE ar.Status = 'Normal'
      AND (ar.Reservation_ID IS NULL OR ar.Reservation_ID = 0)
      AND (ard.ProductType_ID IS NULL OR ard.ProductType_ID != 3)
      AND ar.Created_Date >= @StartDate
      AND ar.Created_Date < DATEADD(DAY, 1, @EndDate)
    ORDER BY Category, Created_Date;
END
GO

PRINT '   ✓ sp_Get_Revenue_Report stored procedure created'
GO

-- =============================================
-- COMMIT TRANSACTION
-- =============================================

COMMIT TRANSACTION;

PRINT ''
PRINT '========================================='
PRINT 'PHASE4_Migration_01 completed successfully!'
PRINT '========================================='
PRINT ''
PRINT 'Summary of changes:'
PRINT '  ✓ vw_Revenue_Detail_By_Items - Detailed revenue with items'
PRINT '  ✓ vw_Deposit_Receipts - Deposit tracking'
PRINT '  ✓ vw_Expense_Summary - Expense summary from Payment Vouchers'
PRINT '  ✓ fn_Categorize_Receipt - Revenue categorization function'
PRINT '  ✓ vw_Revenue_By_Category - Revenue by category view'
PRINT '  ✓ vw_Profit_Loss_Summary - Profit/Loss summary'
PRINT '  ✓ sp_Get_Revenue_Report - Revenue report stored procedure'
PRINT ''
PRINT 'Usage:'
PRINT '  -- Get revenue report'
PRINT '  EXEC sp_Get_Revenue_Report @StartDate = ''2025-11-01'', @EndDate = ''2025-11-30'''
PRINT ''
PRINT '  -- View profit/loss'
PRINT '  SELECT * FROM vw_Profit_Loss_Summary'
PRINT '  WHERE Business_Date >= ''2025-11-01'' AND Business_Date <= ''2025-11-30'''
PRINT ''
GO
