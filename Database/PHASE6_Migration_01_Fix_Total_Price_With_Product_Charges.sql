-- =============================================
-- Phase 6 Migration 01: Fix Total Price to Include Product Charges
-- Purpose: Create functions to calculate total price including product charges
-- Author: Claude AI
-- Date: 2025-01-08
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'Phase 6 Migration 01: Fix Total Price With Product Charges';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Create fn_GetTotalProductCharges Function
-- Purpose: Get total product charges (all statuses except CANCELLED)
-- =============================================
IF OBJECT_ID('dbo.fn_GetTotalProductCharges', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetTotalProductCharges];
GO

CREATE FUNCTION [dbo].[fn_GetTotalProductCharges]
(
    @ReservationID bigint
)
RETURNS decimal(18,2)
AS
BEGIN
    DECLARE @TotalCharges decimal(18,2) = 0;

    -- Sum all product charges from Reservation_Product_Charges (except CANCELLED)
    SELECT @TotalCharges = ISNULL(SUM(TotalAmount), 0)
    FROM [dbo].[Reservation_Product_Charges]
    WHERE Reservation_ID = @ReservationID
      AND Status <> 'CANCELLED';

    RETURN @TotalCharges;
END
GO

PRINT '✅ Created fn_GetTotalProductCharges function';
GO

-- =============================================
-- 2. Create fn_GetTotalPriceWithCharges Function
-- Purpose: Get total price including all product charges
-- =============================================
IF OBJECT_ID('dbo.fn_GetTotalPriceWithCharges', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetTotalPriceWithCharges];
GO

CREATE FUNCTION [dbo].[fn_GetTotalPriceWithCharges]
(
    @ReservationID bigint
)
RETURNS decimal(18,2)
AS
BEGIN
    DECLARE @TotalPrice decimal(18,2);
    DECLARE @ProductCharges decimal(18,2);
    DECLARE @GrandTotal decimal(18,2);

    -- Get base total price from Reservation
    SELECT @TotalPrice = ISNULL(TotalPrice, 0)
    FROM [dbo].[Reservation]
    WHERE ID = @ReservationID;

    -- Get product charges
    SET @ProductCharges = dbo.fn_GetTotalProductCharges(@ReservationID);

    -- Calculate grand total
    SET @GrandTotal = @TotalPrice + @ProductCharges;

    RETURN @GrandTotal;
END
GO

PRINT '✅ Created fn_GetTotalPriceWithCharges function';
GO

-- =============================================
-- 3. Update fn_GetRemainingBalance to use new function
-- =============================================
IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    DROP FUNCTION [dbo].[fn_GetRemainingBalance];
GO

CREATE FUNCTION [dbo].[fn_GetRemainingBalance]
(
    @ReservationID bigint
)
RETURNS decimal(18,2)
AS
BEGIN
    DECLARE @TotalPrice decimal(18,2);
    DECLARE @TotalPaid decimal(18,2);
    DECLARE @RemainingBalance decimal(18,2);

    -- Get total price INCLUDING product charges
    SET @TotalPrice = dbo.fn_GetTotalPriceWithCharges(@ReservationID);

    -- Get total paid using fn_GetTotalPaid
    SET @TotalPaid = dbo.fn_GetTotalPaid(@ReservationID);

    -- Calculate remaining balance
    SET @RemainingBalance = @TotalPrice - @TotalPaid;

    RETURN @RemainingBalance;
END
GO

PRINT '✅ Updated fn_GetRemainingBalance to include product charges';
GO

-- =============================================
-- 4. Create/Update vw_ReservationPaymentSummary View
-- =============================================
IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    DROP VIEW [dbo].[vw_ReservationPaymentSummary];
GO

CREATE VIEW [dbo].[vw_ReservationPaymentSummary]
AS
SELECT
    r.ID as ReservationID,
    r.TotalPrice as BaseTotalPrice,
    dbo.fn_GetTotalProductCharges(r.ID) as ProductCharges,
    dbo.fn_GetTotalPriceWithCharges(r.ID) as TotalPriceWithCharges,
    dbo.fn_GetTotalPaid(r.ID) as TotalPaid,
    dbo.fn_GetRemainingBalance(r.ID) as RemainingBalance,
    r.Status,
    r.CheckinDate,
    r.CheckoutDate,
    c.Name as CustomerName,
    c.MobilePhone as CustomerPhone
FROM [dbo].[Reservation] r
LEFT JOIN [dbo].[Customer] c ON r.Customer_MobilePhone = c.MobilePhone;
GO

PRINT '✅ Created vw_ReservationPaymentSummary view';
GO

-- =============================================
-- Verification
-- =============================================
PRINT '';
PRINT 'Verification:';
PRINT '----------------------------------------';

IF OBJECT_ID('dbo.fn_GetTotalProductCharges', 'FN') IS NOT NULL
    PRINT '✅ fn_GetTotalProductCharges exists';
ELSE
    PRINT '❌ fn_GetTotalProductCharges missing';

IF OBJECT_ID('dbo.fn_GetTotalPriceWithCharges', 'FN') IS NOT NULL
    PRINT '✅ fn_GetTotalPriceWithCharges exists';
ELSE
    PRINT '❌ fn_GetTotalPriceWithCharges missing';

IF OBJECT_ID('dbo.fn_GetRemainingBalance', 'FN') IS NOT NULL
    PRINT '✅ fn_GetRemainingBalance updated';
ELSE
    PRINT '❌ fn_GetRemainingBalance missing';

IF OBJECT_ID('dbo.vw_ReservationPaymentSummary', 'V') IS NOT NULL
    PRINT '✅ vw_ReservationPaymentSummary exists';
ELSE
    PRINT '❌ vw_ReservationPaymentSummary missing';

PRINT '';
PRINT '========================================';
PRINT 'Migration completed successfully!';
PRINT '========================================';
GO
