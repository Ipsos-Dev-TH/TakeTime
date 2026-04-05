-- ================================================================
-- PHASE 9: Accounting System Integration (Nexaacc)
-- Migration 03: Switch from Email/Password to API Key authentication
-- ================================================================
-- Nexaacc API ใช้ X-Api-Key header สำหรับ authentication
-- (ตรงตาม ApiKeyMiddleware.cs ใน Nexaacc source code)
-- Migration นี้จะ:
--   1. เพิ่ม config key Nexaacc_ApiKey_Encrypted
--   2. ลบ config keys เก่า (Nexaacc_Email, Nexaacc_Password_Encrypted)
-- ================================================================

-- Add new API Key config
IF NOT EXISTS (SELECT 1 FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_ApiKey_Encrypted')
BEGIN
    INSERT INTO Accounting_Integration_Config (ConfigKey, ConfigValue, Description)
    VALUES ('Nexaacc_ApiKey_Encrypted', '', 'Encrypted API Key for Nexaacc (use X-Api-Key header)');
    PRINT 'Added Nexaacc_ApiKey_Encrypted config key';
END
GO

-- Remove old Email/Password config keys (no longer used)
DELETE FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Email';
DELETE FROM Accounting_Integration_Config WHERE ConfigKey = 'Nexaacc_Password_Encrypted';
PRINT 'Removed old Nexaacc_Email and Nexaacc_Password_Encrypted config keys';
GO

-- Update description for BaseUrl
UPDATE Accounting_Integration_Config
SET Description = 'Base URL of Nexaacc Accounting API (e.g., https://accounting.example.com)'
WHERE ConfigKey = 'Nexaacc_BaseUrl';
GO

PRINT '================================================================';
PRINT 'PHASE 9 Migration 03 completed successfully.';
PRINT '';
PRINT 'Authentication changed: Email/Password (JWT) -> API Key (X-Api-Key header)';
PRINT '';
PRINT 'Next steps:';
PRINT '1. Generate an API Key from Nexaacc Settings page';
PRINT '2. Enter the API Key in TakeTime Accounting Integration Settings';
PRINT '================================================================';
