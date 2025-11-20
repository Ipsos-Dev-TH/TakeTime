-- =============================================
-- HOTFIX: Remove Reserve_Detail from SQL Functions
-- Purpose: Fix "Invalid object name 'dbo.Reserve_Detail'" error in checkout
-- Date: 2025-01-08
-- =============================================

USE [Taketime]
GO

PRINT '========================================';
PRINT 'HOTFIX: Remove Reserve_Detail References';
PRINT '========================================';
PRINT '';

-- =============================================
-- 1. Update fn_GetTotalProductCharges
-- Remove Reserve_Detail query
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
    -- ✅ FIX: Removed Reserve_Detail query to prevent "Invalid object name" error
    SELECT @TotalCharges = ISNULL(SUM(TotalAmount), 0)
    FROM [dbo].[Reservation_Product_Charges]
    WHERE Reservation_ID = @ReservationID
      AND Status <> 'CANCELLED';

    RETURN @TotalCharges;
END
GO

PRINT '✅ Updated fn_GetTotalProductCharges (removed Reserve_Detail)';
GO

-- =============================================
-- Verification
-- =============================================
PRINT '';
PRINT 'Verification:';
PRINT '----------------------------------------';

IF OBJECT_ID('dbo.fn_GetTotalProductCharges', 'FN') IS NOT NULL
    PRINT '✅ fn_GetTotalProductCharges exists and updated';
ELSE
    PRINT '❌ fn_GetTotalProductCharges missing';

-- Test the function
DECLARE @TestResult decimal(18,2);
PRINT '';
PRINT 'Testing function with sample ReservationID = 1:';
SELECT @TestResult = dbo.fn_GetTotalProductCharges(1);
PRINT 'Result: ' + CAST(@TestResult AS nvarchar(20));

PRINT '';
PRINT '========================================';
PRINT '✅ HOTFIX completed successfully!';
PRINT '========================================';
PRINT '';
PRINT 'NOTE: You can now checkout without "Reserve_Detail" error';
PRINT '========================================';
GO
