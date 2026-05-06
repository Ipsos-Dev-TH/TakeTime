-- =============================================
-- PHASE 12: Add WHT (Withholding Tax) columns to Account_Payment
-- =============================================
-- Adds WHT_Rate and WHT_Amount to support ภาษีหัก ณ ที่จ่าย
-- on payment vouchers, synced correctly to NextAcc.
-- =============================================

-- 1. Add WHT columns to Account_Payment
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'WHT_Rate')
BEGIN
    ALTER TABLE Account_Payment ADD WHT_Rate DECIMAL(5,2) NULL DEFAULT 0;
    PRINT 'Added WHT_Rate column to Account_Payment';
END

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Account_Payment' AND COLUMN_NAME = 'WHT_Amount')
BEGIN
    ALTER TABLE Account_Payment ADD WHT_Amount DECIMAL(18,2) NULL DEFAULT 0;
    PRINT 'Added WHT_Amount column to Account_Payment';
END

PRINT 'PHASE12 Migration 01 completed successfully.';
