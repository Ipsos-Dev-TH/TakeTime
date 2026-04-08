-- ================================================================
-- PHASE 9 Migration 04: Fix Account Mapping Codes to match NextAcc
-- ================================================================
-- Updates Nexaacc_AccountCode values to match the actual chart of
-- accounts configured in NextAcc system.
--
-- The original seed data used placeholder codes (e.g., 1110 for CASH).
-- This migration corrects them to match the real NextAcc account codes
-- (e.g., 111 for CASH, 411 for ROOM_REVENUE, 21510 for ADVANCE_DEPOSIT).
-- ================================================================

PRINT 'Starting PHASE 9 Migration 04: Fix Account Mapping Codes...';

-- Update known code mismatches based on actual NextAcc chart of accounts
-- Assets (หมวด 1)
UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = '111'   WHERE TakeTime_Code = 'CASH'             AND Nexaacc_AccountCode = '1110';
UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = '11310' WHERE TakeTime_Code = 'ROOM_AR'          AND Nexaacc_AccountCode = '1121';

-- Liabilities (หมวด 2)
UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = '21510' WHERE TakeTime_Code = 'ADVANCE_DEPOSIT'  AND Nexaacc_AccountCode = '2130';

-- Revenue (หมวด 4)
UPDATE Accounting_Account_Mapping SET Nexaacc_AccountCode = '411'   WHERE TakeTime_Code = 'ROOM_REVENUE'     AND Nexaacc_AccountCode = '4100';

-- Clear Nexaacc_AccountId for updated rows (will be re-resolved on next Fetch Chart of Accounts)
UPDATE Accounting_Account_Mapping SET Nexaacc_AccountId = NULL WHERE Nexaacc_AccountId IS NOT NULL
    AND TakeTime_Code IN ('CASH', 'ROOM_AR', 'ADVANCE_DEPOSIT', 'ROOM_REVENUE');

PRINT 'Updated account codes: CASH→111, ROOM_AR→11310, ADVANCE_DEPOSIT→21510, ROOM_REVENUE→411';
PRINT '';
PRINT 'IMPORTANT: After running this migration, go to Admin > Accounting Integration';
PRINT 'and click "ดึง Chart of Accounts" to re-resolve Account IDs from NextAcc.';
PRINT '';
PRINT 'PHASE 9 Migration 04 completed.';
