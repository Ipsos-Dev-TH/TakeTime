-- =============================================================================
-- Seed Script: Sample Tenant WITHOUT VAT
-- =============================================================================
-- Purpose: Creates a second sample tenant for testing multi-tenant scenarios
--          with different tax configurations. This tenant has:
--   - VAT DISABLED (0%)
--   - No service charge
--   - USD currency (international property simulation)
--   - Different check-in/check-out times
--   - Minimal feature set (basic plan)
--   - Different notification preferences
-- =============================================================================

DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

-- =============================================
-- Step 1: Insert Sample No-VAT Tenant
-- =============================================
IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @TenantId)
BEGIN
    INSERT INTO Tenants (Id, Identifier, Name, Subdomain, Timezone, Locale, CurrencyCode, CurrencySymbol, IsActive, CreatedBy)
    VALUES (
        @TenantId,
        'paradise-cove',
        N'Paradise Cove Resort',
        'paradise-cove',
        'Asia/Bangkok',
        'en-US',
        'USD',
        '$',
        1,
        'Migrator-SeedScript'
    );
    PRINT 'Sample no-VAT tenant "Paradise Cove Resort" created.';
END
ELSE
BEGIN
    PRINT 'Sample no-VAT tenant already exists. Skipping.';
END
GO

-- =============================================
-- Step 2: Tax Settings - VAT DISABLED
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Tax' AND [Key] = 'VatEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Tax', 'VatEnabled', 'false', 'Boolean', 'VAT calculation disabled'),
        (@TenantId, 'Tax', 'VatRate', '0.00', 'Decimal', 'No VAT applied'),
        (@TenantId, 'Tax', 'VatRegistrationNumber', '', 'String', 'Not VAT registered'),
        (@TenantId, 'Tax', 'VatIncludedInPrice', 'false', 'Boolean', 'No VAT in prices'),
        (@TenantId, 'Tax', 'ServiceChargeEnabled', 'false', 'Boolean', 'No service charge'),
        (@TenantId, 'Tax', 'ServiceChargeRate', '0.00', 'Decimal', 'Service charge rate'),
        (@TenantId, 'Tax', 'WithholdingTaxEnabled', 'false', 'Boolean', 'No withholding tax'),
        (@TenantId, 'Tax', 'WithholdingTaxRate', '0.00', 'Decimal', 'Withholding tax rate');

    PRINT 'Tax settings (no VAT) seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 3: Financial Settings - USD
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Financial' AND [Key] = 'BaseCurrency')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Financial', 'BaseCurrency', 'USD', 'String', 'Base operating currency'),
        (@TenantId, 'Financial', 'AcceptedCurrencies', 'USD,THB,EUR', 'String', 'Accepted currencies'),
        (@TenantId, 'Financial', 'DecimalPlaces', '2', 'Integer', 'Currency decimal places'),
        (@TenantId, 'Financial', 'RoundingMethod', 'Standard', 'String', 'Rounding method'),
        (@TenantId, 'Financial', 'FiscalYearStartMonth', '1', 'Integer', 'Fiscal year start'),
        (@TenantId, 'Financial', 'InvoicePrefix', 'PC-INV', 'String', 'Invoice number prefix'),
        (@TenantId, 'Financial', 'ReceiptPrefix', 'PC-RCP', 'String', 'Receipt number prefix'),
        (@TenantId, 'Financial', 'PaymentNumberPrefix', 'PC-PAY', 'String', 'Payment number prefix');

    PRINT 'Financial settings (USD) seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 4: Property Settings
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Property' AND [Key] = 'PropertyName')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Property', 'PropertyName', N'Paradise Cove Resort & Spa', 'String', 'Full property name'),
        (@TenantId, 'Property', 'PropertyNameThai', N'พาราไดซ์ โคฟ รีสอร์ท แอนด์ สปา', 'String', 'Property name in Thai'),
        (@TenantId, 'Property', 'PropertyType', 'Resort', 'String', 'Property classification'),
        (@TenantId, 'Property', 'StarRating', '5', 'Integer', 'Star rating'),
        (@TenantId, 'Property', 'Address', N'456 Beach Road, Hua Hin, Prachuap Khiri Khan 77110, Thailand', 'String', 'Property address'),
        (@TenantId, 'Property', 'Phone', '+66-32-555-678', 'String', 'Main phone number'),
        (@TenantId, 'Property', 'Email', 'info@paradise-cove.com', 'String', 'Main email'),
        (@TenantId, 'Property', 'Website', 'https://www.paradise-cove.com', 'String', 'Website URL'),
        (@TenantId, 'Property', 'Latitude', '12.5684', 'Decimal', 'GPS latitude'),
        (@TenantId, 'Property', 'Longitude', '99.9576', 'Decimal', 'GPS longitude');

    PRINT 'Property settings seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 5: Operations - Different Check-in/out Times
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Operations' AND [Key] = 'DefaultCheckInTime')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Operations', 'DefaultCheckInTime', '15:00', 'Time', 'Check-in at 3 PM'),
        (@TenantId, 'Operations', 'DefaultCheckOutTime', '11:00', 'Time', 'Check-out at 11 AM'),
        (@TenantId, 'Operations', 'EarlyCheckInTime', '11:00', 'Time', 'Earliest early check-in'),
        (@TenantId, 'Operations', 'LateCheckOutTime', '16:00', 'Time', 'Latest late check-out'),
        (@TenantId, 'Operations', 'EarlyCheckInFeePercent', '50.00', 'Decimal', 'Early check-in fee %'),
        (@TenantId, 'Operations', 'LateCheckOutFeePercent', '50.00', 'Decimal', 'Late check-out fee %'),
        (@TenantId, 'Operations', 'DayUseEnabled', 'false', 'Boolean', 'Day-use bookings disabled'),
        (@TenantId, 'Operations', 'AutoNoShowHours', '48', 'Integer', 'Hours to auto no-show'),
        (@TenantId, 'Operations', 'OverbookingAllowedPercent', '0.00', 'Decimal', 'No overbooking'),
        (@TenantId, 'Operations', 'ConfirmationNumberPrefix', 'PC', 'String', 'Reservation prefix');

    PRINT 'Operations settings seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 6: Notification Settings - Email Only
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Notifications' AND [Key] = 'EmailEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Notifications', 'EmailEnabled', 'true', 'Boolean', 'Email notifications enabled'),
        (@TenantId, 'Notifications', 'SmsEnabled', 'false', 'Boolean', 'SMS notifications disabled'),
        (@TenantId, 'Notifications', 'LineEnabled', 'false', 'Boolean', 'LINE notifications disabled'),
        (@TenantId, 'Notifications', 'PushEnabled', 'false', 'Boolean', 'Push notifications disabled'),
        (@TenantId, 'Notifications', 'WhatsAppEnabled', 'false', 'Boolean', 'WhatsApp disabled'),
        (@TenantId, 'Notifications', 'PreArrivalEmailHours', '48', 'Integer', 'Pre-arrival 48 hours'),
        (@TenantId, 'Notifications', 'PostDepartureEmailHours', '48', 'Integer', 'Post-departure 48 hours'),
        (@TenantId, 'Notifications', 'BookingConfirmationAuto', 'true', 'Boolean', 'Auto-send confirmation'),
        (@TenantId, 'Notifications', 'PaymentConfirmationAuto', 'true', 'Boolean', 'Auto-send payment confirmation'),
        (@TenantId, 'Notifications', 'SenderEmail', 'noreply@paradise-cove.com', 'String', 'Sender email'),
        (@TenantId, 'Notifications', 'SenderName', N'Paradise Cove Resort', 'String', 'Sender name');

    PRINT 'Notification settings (email only) seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 7: Loyalty Disabled
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Loyalty' AND [Key] = 'LoyaltyEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Loyalty', 'LoyaltyEnabled', 'false', 'Boolean', 'Loyalty program disabled'),
        (@TenantId, 'Loyalty', 'AutoEnrollGuests', 'false', 'Boolean', 'No auto-enrollment');

    PRINT 'Loyalty settings (disabled) seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 8: Payment Settings - Simpler Setup
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Payment' AND [Key] = 'CashEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Payment', 'CashEnabled', 'true', 'Boolean', 'Accept cash'),
        (@TenantId, 'Payment', 'CreditCardEnabled', 'true', 'Boolean', 'Accept credit card'),
        (@TenantId, 'Payment', 'BankTransferEnabled', 'true', 'Boolean', 'Accept bank transfer'),
        (@TenantId, 'Payment', 'PromptPayEnabled', 'false', 'Boolean', 'PromptPay disabled'),
        (@TenantId, 'Payment', 'QrPaymentEnabled', 'false', 'Boolean', 'QR payment disabled'),
        (@TenantId, 'Payment', 'DepositRequired', 'true', 'Boolean', 'Require deposit'),
        (@TenantId, 'Payment', 'DepositPercent', '50.00', 'Decimal', '50% deposit required'),
        (@TenantId, 'Payment', 'DepositDeadlineHours', '72', 'Integer', '72 hours to pay deposit'),
        (@TenantId, 'Payment', 'SlipVerificationRequired', 'true', 'Boolean', 'Slip verification required'),
        (@TenantId, 'Payment', 'AutoVerifyThreshold', '0', 'Decimal', 'No auto-verify');

    PRINT 'Payment settings seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 9: Bank Account (single)
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantBankAccounts WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TenantBankAccounts (TenantId, BankCode, BankName, AccountName, AccountNumber, BranchName, AccountType, IsActive, IsDefault, SortOrder)
    VALUES (@TenantId, 'KBANK', N'ธนาคารกสิกรไทย (Kasikorn Bank)', N'Paradise Cove Resort Co., Ltd.', '789-0-12345-6', N'หัวหิน', 'Current', 1, 1, 1);

    PRINT 'Bank account seeded for Paradise Cove.';
END
GO

-- =============================================
-- Step 10: Feature Flags - Basic Plan (fewer features)
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantFeatures WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TenantFeatures (TenantId, FeatureCode, IsEnabled)
    VALUES
        -- Core Features (enabled)
        (@TenantId, 'RESERVATIONS', 1),
        (@TenantId, 'FRONT_DESK', 1),
        (@TenantId, 'HOUSEKEEPING', 1),
        (@TenantId, 'PAYMENTS', 1),
        (@TenantId, 'INVOICING', 1),
        (@TenantId, 'GUEST_FOLIOS', 1),

        -- Inventory (enabled)
        (@TenantId, 'INVENTORY', 1),
        (@TenantId, 'PURCHASE_ORDERS', 1),
        (@TenantId, 'STOCK_COUNTS', 0),
        (@TenantId, 'FNB_POS', 0),

        -- HR (basic only)
        (@TenantId, 'HR_MANAGEMENT', 1),
        (@TenantId, 'ATTENDANCE', 1),
        (@TenantId, 'LEAVE_MANAGEMENT', 1),
        (@TenantId, 'PAYROLL', 0),

        -- CRM (basic only)
        (@TenantId, 'GUEST_PROFILES', 1),
        (@TenantId, 'LOYALTY_PROGRAM', 0),
        (@TenantId, 'MARKETING_CAMPAIGNS', 0),
        (@TenantId, 'GUEST_SEGMENTS', 0),

        -- Channel Management (basic)
        (@TenantId, 'CHANNEL_MANAGER', 1),
        (@TenantId, 'BOOKING_ENGINE', 1),
        (@TenantId, 'RATE_MANAGER', 0),

        -- Guest Experience (basic)
        (@TenantId, 'ONLINE_CHECKIN', 0),
        (@TenantId, 'GUEST_APP', 0),
        (@TenantId, 'GUEST_REQUESTS', 1),
        (@TenantId, 'GUEST_FEEDBACK', 1),

        -- Accounting (basic)
        (@TenantId, 'ACCOUNTING', 1),
        (@TenantId, 'FINANCIAL_REPORTS', 1),
        (@TenantId, 'DAILY_RECONCILIATION', 1),

        -- Notifications (email only)
        (@TenantId, 'EMAIL_NOTIFICATIONS', 1),
        (@TenantId, 'SMS_NOTIFICATIONS', 0),
        (@TenantId, 'LINE_NOTIFICATIONS', 0),
        (@TenantId, 'PUSH_NOTIFICATIONS', 0),

        -- Affiliate (disabled)
        (@TenantId, 'AFFILIATE_PROGRAM', 0),
        (@TenantId, 'COMMISSION_TRACKING', 0),

        -- Advanced (limited)
        (@TenantId, 'MULTI_LANGUAGE', 1),
        (@TenantId, 'MULTI_CURRENCY', 0),
        (@TenantId, 'REPORTING_ADVANCED', 0),
        (@TenantId, 'API_ACCESS', 1),
        (@TenantId, 'WEBHOOKS', 0);

    PRINT 'Basic feature set enabled for Paradise Cove.';
END
GO

-- =============================================
-- Step 11: Subscription - Standard Plan
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF NOT EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TenantSubscriptions (TenantId, PlanCode, PlanName, Status, BillingCycle, PricePerMonth, CurrencyCode, StartDate, MaxRooms, MaxUsers)
    VALUES (@TenantId, 'Standard', N'Standard Plan', 'Active', 'Monthly', 2999.00, 'THB', SYSUTCDATETIME(), 50, 20);

    PRINT 'Standard subscription created for Paradise Cove.';
END
GO

-- =============================================
-- Step 12: Link admin user to no-VAT tenant too
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000002-0002-0002-0002-000000000002';

IF EXISTS (SELECT 1 FROM Users WHERE Role = 'SuperAdmin')
BEGIN
    DECLARE @AdminId UNIQUEIDENTIFIER;
    SELECT TOP 1 @AdminId = Id FROM Users WHERE Role = 'SuperAdmin' ORDER BY CreatedAt;

    IF @AdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TenantUsers WHERE TenantId = @TenantId AND UserId = @AdminId)
    BEGIN
        INSERT INTO TenantUsers (TenantId, UserId, Role, Department, Title, IsActive)
        VALUES (@TenantId, @AdminId, 'Admin', 'Management', 'System Administrator', 1);

        PRINT 'Admin user linked to Paradise Cove tenant.';
    END
END
GO

PRINT '==========================================';
PRINT 'Sample no-VAT tenant seeding complete.';
PRINT 'Tenant: Paradise Cove Resort';
PRINT 'VAT: Disabled (0%)';
PRINT 'Currency: USD';
PRINT 'Plan: Standard (limited features)';
PRINT '==========================================';
GO
