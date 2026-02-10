-- =============================================================================
-- Migration 007: Seed Default Tenant - "TakeTime BangPhra"
-- =============================================================================
-- Target Database: TakeTime_TenantManagement
-- Description: Seeds the default tenant with full Thai resort configuration:
--   - VAT enabled at 7%
--   - Thai Baht (THB) currency
--   - Default check-in 14:00, check-out 12:00
--   - Sample bank accounts (KBank, SCB, Bangkok Bank)
--   - Loyalty program enabled
--   - All notification channels enabled (Email, SMS, LINE)
--   - Full feature set enabled
-- =============================================================================

-- =============================================
-- Step 1: Insert Default Tenant
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM Tenants WHERE Id = @TenantId)
BEGIN
    INSERT INTO Tenants (Id, Identifier, Name, Subdomain, Timezone, Locale, CurrencyCode, CurrencySymbol, IsActive, CreatedBy)
    VALUES (
        @TenantId,
        'bangphra',
        N'TakeTime BangPhra',
        'bangphra',
        'Asia/Bangkok',
        'th-TH',
        'THB',
        N'฿',
        1,
        'Migrator'
    );
    PRINT 'Default tenant "TakeTime BangPhra" created.';
END
ELSE
BEGIN
    PRINT 'Default tenant already exists. Skipping.';
END
GO

-- =============================================
-- Step 2: Tenant Settings - Tax & Financial
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

-- VAT Configuration
IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Tax' AND [Key] = 'VatEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Tax', 'VatEnabled', 'true', 'Boolean', 'Enable VAT calculation'),
        (@TenantId, 'Tax', 'VatRate', '7.00', 'Decimal', 'VAT rate percentage'),
        (@TenantId, 'Tax', 'VatRegistrationNumber', '', 'String', 'VAT registration number (Tax ID)'),
        (@TenantId, 'Tax', 'VatIncludedInPrice', 'true', 'Boolean', 'Whether displayed prices include VAT'),
        (@TenantId, 'Tax', 'ServiceChargeEnabled', 'true', 'Boolean', 'Enable service charge'),
        (@TenantId, 'Tax', 'ServiceChargeRate', '10.00', 'Decimal', 'Service charge percentage'),
        (@TenantId, 'Tax', 'WithholdingTaxEnabled', 'false', 'Boolean', 'Enable withholding tax for corporate clients'),
        (@TenantId, 'Tax', 'WithholdingTaxRate', '3.00', 'Decimal', 'Default withholding tax rate');
END
GO

-- Currency & Financial Settings
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Financial' AND [Key] = 'BaseCurrency')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Financial', 'BaseCurrency', 'THB', 'String', 'Base operating currency'),
        (@TenantId, 'Financial', 'AcceptedCurrencies', 'THB,USD,EUR,GBP,CNY,JPY', 'String', 'Comma-separated accepted currencies'),
        (@TenantId, 'Financial', 'DecimalPlaces', '2', 'Integer', 'Currency decimal places'),
        (@TenantId, 'Financial', 'RoundingMethod', 'Standard', 'String', 'Rounding method: Standard, Up, Down'),
        (@TenantId, 'Financial', 'FiscalYearStartMonth', '1', 'Integer', 'Fiscal year start month (January)'),
        (@TenantId, 'Financial', 'InvoicePrefix', 'INV', 'String', 'Invoice number prefix'),
        (@TenantId, 'Financial', 'ReceiptPrefix', 'RCP', 'String', 'Receipt number prefix'),
        (@TenantId, 'Financial', 'PaymentNumberPrefix', 'PAY', 'String', 'Payment number prefix');
END
GO

-- =============================================
-- Step 3: Tenant Settings - Property Operations
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Property' AND [Key] = 'PropertyName')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Property', 'PropertyName', N'TakeTime BangPhra Resort', 'String', 'Full property name'),
        (@TenantId, 'Property', 'PropertyNameThai', N'เทคไทม์ บางพระ รีสอร์ท', 'String', 'Property name in Thai'),
        (@TenantId, 'Property', 'PropertyType', 'Resort', 'String', 'Property classification'),
        (@TenantId, 'Property', 'StarRating', '4', 'Integer', 'Star rating'),
        (@TenantId, 'Property', 'Address', N'123 Moo 4, Bang Phra, Si Racha, Chonburi 20110, Thailand', 'String', 'Property address'),
        (@TenantId, 'Property', 'AddressThai', N'123 หมู่ 4 ตำบลบางพระ อำเภอศรีราชา จังหวัดชลบุรี 20110', 'String', 'Address in Thai'),
        (@TenantId, 'Property', 'Phone', '+66-38-341-234', 'String', 'Main phone number'),
        (@TenantId, 'Property', 'Email', 'info@taketime-bangphra.com', 'String', 'Main email'),
        (@TenantId, 'Property', 'Website', 'https://www.taketime-bangphra.com', 'String', 'Website URL'),
        (@TenantId, 'Property', 'Latitude', '13.2234', 'Decimal', 'GPS latitude'),
        (@TenantId, 'Property', 'Longitude', '100.9267', 'Decimal', 'GPS longitude');
END
GO

-- Check-in/Check-out Configuration
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Operations' AND [Key] = 'DefaultCheckInTime')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Operations', 'DefaultCheckInTime', '14:00', 'Time', 'Default check-in time'),
        (@TenantId, 'Operations', 'DefaultCheckOutTime', '12:00', 'Time', 'Default check-out time'),
        (@TenantId, 'Operations', 'EarlyCheckInTime', '10:00', 'Time', 'Earliest allowed early check-in'),
        (@TenantId, 'Operations', 'LateCheckOutTime', '18:00', 'Time', 'Latest allowed late check-out'),
        (@TenantId, 'Operations', 'EarlyCheckInFeePercent', '50.00', 'Decimal', 'Early check-in fee as % of room rate'),
        (@TenantId, 'Operations', 'LateCheckOutFeePercent', '50.00', 'Decimal', 'Late check-out fee as % of room rate'),
        (@TenantId, 'Operations', 'DayUseEnabled', 'true', 'Boolean', 'Allow day-use bookings'),
        (@TenantId, 'Operations', 'DayUseCheckInTime', '09:00', 'Time', 'Day use check-in time'),
        (@TenantId, 'Operations', 'DayUseCheckOutTime', '18:00', 'Time', 'Day use check-out time'),
        (@TenantId, 'Operations', 'AutoNoShowHours', '24', 'Integer', 'Hours after check-in to auto-mark no-show'),
        (@TenantId, 'Operations', 'OverbookingAllowedPercent', '5.00', 'Decimal', 'Overbooking percentage allowed'),
        (@TenantId, 'Operations', 'ConfirmationNumberPrefix', 'TT', 'String', 'Reservation confirmation prefix');
END
GO

-- =============================================
-- Step 4: Tenant Settings - Notification Channels
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Notifications' AND [Key] = 'EmailEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Notifications', 'EmailEnabled', 'true', 'Boolean', 'Enable email notifications'),
        (@TenantId, 'Notifications', 'SmsEnabled', 'true', 'Boolean', 'Enable SMS notifications'),
        (@TenantId, 'Notifications', 'LineEnabled', 'true', 'Boolean', 'Enable LINE notifications'),
        (@TenantId, 'Notifications', 'PushEnabled', 'true', 'Boolean', 'Enable push notifications'),
        (@TenantId, 'Notifications', 'WhatsAppEnabled', 'false', 'Boolean', 'Enable WhatsApp notifications'),
        (@TenantId, 'Notifications', 'PreArrivalEmailHours', '24', 'Integer', 'Hours before arrival to send pre-arrival email'),
        (@TenantId, 'Notifications', 'PostDepartureEmailHours', '24', 'Integer', 'Hours after departure to send feedback email'),
        (@TenantId, 'Notifications', 'BookingConfirmationAuto', 'true', 'Boolean', 'Auto-send booking confirmation'),
        (@TenantId, 'Notifications', 'PaymentConfirmationAuto', 'true', 'Boolean', 'Auto-send payment confirmation'),
        (@TenantId, 'Notifications', 'SenderEmail', 'noreply@taketime-bangphra.com', 'String', 'Sender email address'),
        (@TenantId, 'Notifications', 'SenderName', N'TakeTime BangPhra', 'String', 'Sender display name');
END
GO

-- =============================================
-- Step 5: Tenant Settings - Loyalty & CRM
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Loyalty' AND [Key] = 'LoyaltyEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Loyalty', 'LoyaltyEnabled', 'true', 'Boolean', 'Enable loyalty program'),
        (@TenantId, 'Loyalty', 'DefaultProgramCode', 'TAKETIME_REWARDS', 'String', 'Default loyalty program code'),
        (@TenantId, 'Loyalty', 'PointsPerBaseCurrency', '1', 'Decimal', 'Points earned per 1 THB spent'),
        (@TenantId, 'Loyalty', 'PointRedemptionValue', '0.50', 'Decimal', 'THB value per redeemed point'),
        (@TenantId, 'Loyalty', 'PointsExpiryMonths', '24', 'Integer', 'Points expire after N months'),
        (@TenantId, 'Loyalty', 'AutoEnrollGuests', 'true', 'Boolean', 'Auto-enroll guests in loyalty program'),
        (@TenantId, 'Loyalty', 'WelcomeBonusPoints', '500', 'Integer', 'Welcome bonus points for new members');
END
GO

-- =============================================
-- Step 6: Tenant Settings - Payment Methods
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSettings WHERE TenantId = @TenantId AND Category = 'Payment' AND [Key] = 'CashEnabled')
BEGIN
    INSERT INTO TenantSettings (TenantId, Category, [Key], Value, ValueType, Description)
    VALUES
        (@TenantId, 'Payment', 'CashEnabled', 'true', 'Boolean', 'Accept cash payments'),
        (@TenantId, 'Payment', 'CreditCardEnabled', 'true', 'Boolean', 'Accept credit card payments'),
        (@TenantId, 'Payment', 'BankTransferEnabled', 'true', 'Boolean', 'Accept bank transfer payments'),
        (@TenantId, 'Payment', 'PromptPayEnabled', 'true', 'Boolean', 'Accept PromptPay payments'),
        (@TenantId, 'Payment', 'QrPaymentEnabled', 'true', 'Boolean', 'Accept QR code payments'),
        (@TenantId, 'Payment', 'DepositRequired', 'true', 'Boolean', 'Require deposit for reservations'),
        (@TenantId, 'Payment', 'DepositPercent', '30.00', 'Decimal', 'Default deposit percentage'),
        (@TenantId, 'Payment', 'DepositDeadlineHours', '48', 'Integer', 'Hours to pay deposit before auto-cancel'),
        (@TenantId, 'Payment', 'SlipVerificationRequired', 'true', 'Boolean', 'Require slip verification for transfers'),
        (@TenantId, 'Payment', 'AutoVerifyThreshold', '0', 'Decimal', 'Auto-verify transfers below this amount (0=disabled)');
END
GO

-- =============================================
-- Step 7: Bank Accounts
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantBankAccounts WHERE TenantId = @TenantId)
BEGIN
    -- Kasikorn Bank (KBank) - Primary
    INSERT INTO TenantBankAccounts (TenantId, BankCode, BankName, AccountName, AccountNumber, BranchName, AccountType, IsActive, IsDefault, PromptPayId, PromptPayType, SortOrder)
    VALUES (@TenantId, 'KBANK', N'ธนาคารกสิกรไทย (Kasikorn Bank)', N'บจ. เทคไทม์ บางพระ', '123-4-56789-0', N'ศรีราชา', 'Savings', 1, 1, '0123456789012', 'TaxId', 1);

    -- Siam Commercial Bank (SCB)
    INSERT INTO TenantBankAccounts (TenantId, BankCode, BankName, AccountName, AccountNumber, BranchName, AccountType, IsActive, IsDefault, SortOrder)
    VALUES (@TenantId, 'SCB', N'ธนาคารไทยพาณิชย์ (SCB)', N'บจ. เทคไทม์ บางพระ', '987-6-54321-0', N'ศรีราชา', 'Current', 1, 0, 2);

    -- Bangkok Bank (BBL)
    INSERT INTO TenantBankAccounts (TenantId, BankCode, BankName, AccountName, AccountNumber, BranchName, AccountType, IsActive, IsDefault, SortOrder)
    VALUES (@TenantId, 'BBL', N'ธนาคารกรุงเทพ (Bangkok Bank)', N'บจ. เทคไทม์ บางพระ', '456-7-89012-3', N'ศรีราชา', 'Savings', 1, 0, 3);

    PRINT 'Bank accounts seeded for TakeTime BangPhra.';
END
GO

-- =============================================
-- Step 8: Feature Flags - Full Feature Set
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantFeatures WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TenantFeatures (TenantId, FeatureCode, IsEnabled)
    VALUES
        -- Core Features
        (@TenantId, 'RESERVATIONS', 1),
        (@TenantId, 'FRONT_DESK', 1),
        (@TenantId, 'HOUSEKEEPING', 1),
        (@TenantId, 'PAYMENTS', 1),
        (@TenantId, 'INVOICING', 1),
        (@TenantId, 'GUEST_FOLIOS', 1),

        -- Inventory & F&B
        (@TenantId, 'INVENTORY', 1),
        (@TenantId, 'PURCHASE_ORDERS', 1),
        (@TenantId, 'STOCK_COUNTS', 1),
        (@TenantId, 'FNB_POS', 1),

        -- HR & Payroll
        (@TenantId, 'HR_MANAGEMENT', 1),
        (@TenantId, 'ATTENDANCE', 1),
        (@TenantId, 'LEAVE_MANAGEMENT', 1),
        (@TenantId, 'PAYROLL', 1),

        -- CRM & Marketing
        (@TenantId, 'GUEST_PROFILES', 1),
        (@TenantId, 'LOYALTY_PROGRAM', 1),
        (@TenantId, 'MARKETING_CAMPAIGNS', 1),
        (@TenantId, 'GUEST_SEGMENTS', 1),

        -- Channel Management
        (@TenantId, 'CHANNEL_MANAGER', 1),
        (@TenantId, 'BOOKING_ENGINE', 1),
        (@TenantId, 'RATE_MANAGER', 1),

        -- Guest Experience
        (@TenantId, 'ONLINE_CHECKIN', 1),
        (@TenantId, 'GUEST_APP', 1),
        (@TenantId, 'GUEST_REQUESTS', 1),
        (@TenantId, 'GUEST_FEEDBACK', 1),

        -- Accounting & Finance
        (@TenantId, 'ACCOUNTING', 1),
        (@TenantId, 'FINANCIAL_REPORTS', 1),
        (@TenantId, 'DAILY_RECONCILIATION', 1),

        -- Notifications
        (@TenantId, 'EMAIL_NOTIFICATIONS', 1),
        (@TenantId, 'SMS_NOTIFICATIONS', 1),
        (@TenantId, 'LINE_NOTIFICATIONS', 1),
        (@TenantId, 'PUSH_NOTIFICATIONS', 1),

        -- Affiliate
        (@TenantId, 'AFFILIATE_PROGRAM', 1),
        (@TenantId, 'COMMISSION_TRACKING', 1),

        -- Advanced
        (@TenantId, 'MULTI_LANGUAGE', 1),
        (@TenantId, 'MULTI_CURRENCY', 1),
        (@TenantId, 'REPORTING_ADVANCED', 1),
        (@TenantId, 'API_ACCESS', 1),
        (@TenantId, 'WEBHOOKS', 1);

    PRINT 'Full feature set enabled for TakeTime BangPhra.';
END
GO

-- =============================================
-- Step 9: Subscription
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM TenantSubscriptions WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO TenantSubscriptions (TenantId, PlanCode, PlanName, Status, BillingCycle, PricePerMonth, CurrencyCode, StartDate, MaxRooms, MaxUsers)
    VALUES (@TenantId, 'Enterprise', N'Enterprise Plan', 'Active', 'Annual', 0.00, 'THB', SYSUTCDATETIME(), 999, 999);

    PRINT 'Enterprise subscription created for TakeTime BangPhra.';
END
GO

-- =============================================
-- Step 10: Link admin user to tenant
-- =============================================
DECLARE @TenantId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

IF EXISTS (SELECT 1 FROM Users WHERE Role = 'SuperAdmin')
BEGIN
    DECLARE @AdminId UNIQUEIDENTIFIER;
    SELECT TOP 1 @AdminId = Id FROM Users WHERE Role = 'SuperAdmin' ORDER BY CreatedAt;

    IF @AdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TenantUsers WHERE TenantId = @TenantId AND UserId = @AdminId)
    BEGIN
        INSERT INTO TenantUsers (TenantId, UserId, Role, Department, Title, IsActive)
        VALUES (@TenantId, @AdminId, 'Admin', 'Management', 'System Administrator', 1);

        PRINT 'Admin user linked to TakeTime BangPhra tenant.';
    END
END
GO
