using TakeTime.MultiTenancy.Features;

namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Pricing model that determines how a property charges guests.
/// </summary>
public enum PricingModel
{
    /// <summary>Per-night pricing (hotels, resorts).</summary>
    PerNight = 0,

    /// <summary>Per-hour pricing (meeting rooms, co-working spaces).</summary>
    PerHour = 1,

    /// <summary>Per-person pricing (hostels, tours, activities).</summary>
    PerPerson = 2,

    /// <summary>Flat-rate pricing (event venues, entire-property rentals).</summary>
    FlatRate = 3,

    /// <summary>Package-based pricing (bundled services).</summary>
    Package = 4
}

/// <summary>
/// Comprehensive business settings for a tenant. This is the heart of the
/// multi-tenant configuration system -- each property/business can have
/// completely different tax rules, pricing logic, payment terms, operational
/// hours, notification preferences, and feature flags.
/// </summary>
public class TenantBusinessSettings
{
    // ─── Tax / VAT ───────────────────────────────────────────────────

    /// <summary>Whether this tenant charges Value Added Tax.</summary>
    public bool EnableVAT { get; set; }

    /// <summary>VAT rate as a percentage (e.g., 7 for Thailand's standard rate).</summary>
    public decimal VATRate { get; set; } = 7m;

    /// <summary>Tax identification number issued by the revenue department.</summary>
    public string? TaxId { get; set; }

    /// <summary>VAT registration number (may differ from TaxId in some jurisdictions).</summary>
    public string? VATRegistrationNumber { get; set; }

    /// <summary>Whether VAT is included in displayed prices or added on top.</summary>
    public bool VATIncludedInPrice { get; set; } = true;

    /// <summary>Withholding tax rate when applicable (e.g., 3% in Thailand for certain services).</summary>
    public decimal WithholdingTaxRate { get; set; }

    // ─── Pricing ─────────────────────────────────────────────────────

    /// <summary>ISO 4217 currency code (e.g., "THB", "USD").</summary>
    public string DefaultCurrency { get; set; } = "THB";

    /// <summary>The primary pricing model for this business.</summary>
    public PricingModel PricingModel { get; set; } = PricingModel.PerNight;

    /// <summary>Enable algorithmic dynamic pricing based on demand.</summary>
    public bool EnableDynamicPricing { get; set; }

    /// <summary>Enable seasonal pricing with date-range-based rate adjustments.</summary>
    public bool EnableSeasonalPricing { get; set; }

    /// <summary>Minimum price floor for dynamic pricing (prevents race-to-bottom).</summary>
    public decimal? MinimumPrice { get; set; }

    /// <summary>Maximum price ceiling for dynamic pricing.</summary>
    public decimal? MaximumPrice { get; set; }

    /// <summary>Number of decimal places for price rounding.</summary>
    public int PriceDecimalPlaces { get; set; } = 2;

    /// <summary>Enable promotional/coupon code support.</summary>
    public bool EnablePromotions { get; set; } = true;

    // ─── Payment ─────────────────────────────────────────────────────

    /// <summary>
    /// Payment methods accepted by this tenant (e.g., "Cash", "BankTransfer",
    /// "CreditCard", "PromptPay", "LINE Pay", "TrueMoney").
    /// </summary>
    public List<string> AcceptedPaymentMethods { get; set; } = ["Cash", "BankTransfer", "PromptPay"];

    /// <summary>Whether the tenant allows partial/split payments.</summary>
    public bool EnablePartialPayment { get; set; }

    /// <summary>Deposit percentage required at booking time (0-100).</summary>
    public decimal DepositPercentage { get; set; }

    /// <summary>Number of days after invoice date by which payment is due.</summary>
    public int PaymentDueDays { get; set; } = 7;

    /// <summary>Bank accounts associated with this tenant for receiving payments.</summary>
    public List<BankAccount> BankAccounts { get; set; } = [];

    /// <summary>Enable automatic payment reminders before due date.</summary>
    public bool EnablePaymentReminders { get; set; } = true;

    /// <summary>Days before due date to send payment reminder.</summary>
    public int PaymentReminderDaysBefore { get; set; } = 2;

    /// <summary>Enable installment payment plans.</summary>
    public bool EnableInstallments { get; set; }

    /// <summary>Maximum number of installments allowed.</summary>
    public int MaxInstallments { get; set; } = 3;

    // ─── Reservation / Booking ───────────────────────────────────────

    /// <summary>Default check-in time (e.g., "14:00").</summary>
    public TimeOnly DefaultCheckInTime { get; set; } = new(14, 0);

    /// <summary>Default check-out time (e.g., "12:00").</summary>
    public TimeOnly DefaultCheckOutTime { get; set; } = new(12, 0);

    /// <summary>Maximum number of days in advance a booking can be made.</summary>
    public int MaxAdvanceBookingDays { get; set; } = 365;

    /// <summary>Minimum number of days in advance a booking must be made (0 = same-day allowed).</summary>
    public int MinAdvanceBookingDays { get; set; }

    /// <summary>Free cancellation window in hours before check-in.</summary>
    public int CancellationPolicyHours { get; set; } = 48;

    /// <summary>Whether new bookings are automatically confirmed without manual review.</summary>
    public bool EnableAutoConfirmation { get; set; } = true;

    /// <summary>Minimum length of stay in nights.</summary>
    public int MinStayNights { get; set; } = 1;

    /// <summary>Maximum length of stay in nights (0 = unlimited).</summary>
    public int MaxStayNights { get; set; }

    /// <summary>Enable overbooking protection.</summary>
    public bool EnableOverbookingProtection { get; set; } = true;

    /// <summary>Enable waitlist when fully booked.</summary>
    public bool EnableWaitlist { get; set; }

    /// <summary>Cancellation fee percentage if cancelled outside the free window.</summary>
    public decimal CancellationFeePercentage { get; set; }

    // ─── Receipt / Invoice ───────────────────────────────────────────

    /// <summary>Prefix for receipt numbers (e.g., "RCP-BP-").</summary>
    public string ReceiptPrefix { get; set; } = "RCP-";

    /// <summary>Prefix for invoice numbers (e.g., "INV-BP-").</summary>
    public string InvoicePrefix { get; set; } = "INV-";

    /// <summary>Prefix for tax invoice numbers.</summary>
    public string TaxInvoicePrefix { get; set; } = "TXINV-";

    /// <summary>Prefix for credit note numbers.</summary>
    public string CreditNotePrefix { get; set; } = "CN-";

    /// <summary>Whether receipts are generated automatically on payment.</summary>
    public bool EnableAutoReceipt { get; set; } = true;

    /// <summary>Next receipt sequence number (auto-incremented).</summary>
    public long NextReceiptNumber { get; set; } = 1;

    /// <summary>Next invoice sequence number (auto-incremented).</summary>
    public long NextInvoiceNumber { get; set; } = 1;

    /// <summary>Footer text for receipts and invoices.</summary>
    public string? DocumentFooterText { get; set; }

    // ─── Notification ────────────────────────────────────────────────

    /// <summary>Enable email notifications for bookings, payments, etc.</summary>
    public bool EnableEmailNotification { get; set; } = true;

    /// <summary>Enable SMS notifications.</summary>
    public bool EnableSMSNotification { get; set; }

    /// <summary>Enable LINE messaging notifications (popular in Thailand).</summary>
    public bool EnableLINENotification { get; set; }

    /// <summary>Enable Telegram bot notifications.</summary>
    public bool EnableTelegramNotification { get; set; }

    /// <summary>LINE Notify access token for sending notifications.</summary>
    public string? LINENotifyToken { get; set; }

    /// <summary>Telegram bot token for sending notifications.</summary>
    public string? TelegramBotToken { get; set; }

    /// <summary>Telegram chat ID for receiving notifications.</summary>
    public string? TelegramChatId { get; set; }

    /// <summary>SMS provider configuration key.</summary>
    public string? SMSProviderKey { get; set; }

    // ─── Loyalty Program ─────────────────────────────────────────────

    /// <summary>Whether the loyalty/rewards program is active.</summary>
    public bool EnableLoyaltyProgram { get; set; }

    /// <summary>Number of loyalty points earned per unit of currency spent.</summary>
    public decimal PointsPerCurrencyUnit { get; set; } = 1m;

    /// <summary>Configured loyalty tiers with ascending point thresholds.</summary>
    public List<LoyaltyTierConfig> LoyaltyTiers { get; set; } = [];

    /// <summary>Points expiry in days (0 = never expire).</summary>
    public int PointsExpiryDays { get; set; }

    /// <summary>Minimum points required before redemption is allowed.</summary>
    public int MinRedemptionPoints { get; set; } = 100;

    // ─── HR / Payroll ────────────────────────────────────────────────

    /// <summary>Enable overtime calculation for staff.</summary>
    public bool EnableOTCalculation { get; set; }

    /// <summary>Overtime rate multiplier (e.g., 1.5 for time-and-a-half).</summary>
    public decimal OTRateMultiplier { get; set; } = 1.5m;

    /// <summary>Standard working hours per day.</summary>
    public decimal WorkingHoursPerDay { get; set; } = 8m;

    /// <summary>Day of the month for payroll cutoff (1-31).</summary>
    public int PayrollCutoffDay { get; set; } = 25;

    /// <summary>Enable holiday overtime premium.</summary>
    public bool EnableHolidayOTPremium { get; set; }

    /// <summary>Holiday overtime rate multiplier.</summary>
    public decimal HolidayOTRateMultiplier { get; set; } = 3.0m;

    /// <summary>Working days per week.</summary>
    public int WorkingDaysPerWeek { get; set; } = 6;

    // ─── Localization ────────────────────────────────────────────────

    /// <summary>Default language code (e.g., "th", "en").</summary>
    public string DefaultLanguage { get; set; } = "th";

    /// <summary>Supported languages for this tenant.</summary>
    public List<string> SupportedLanguages { get; set; } = ["th", "en"];

    /// <summary>IANA time zone identifier (e.g., "Asia/Bangkok").</summary>
    public string TimeZone { get; set; } = "Asia/Bangkok";

    /// <summary>Date display format (e.g., "dd/MM/yyyy").</summary>
    public string DateFormat { get; set; } = "dd/MM/yyyy";

    /// <summary>Currency display format pattern.</summary>
    public string CurrencyFormat { get; set; } = "#,##0.00";

    /// <summary>Time display format (e.g., "HH:mm" for 24-hour).</summary>
    public string TimeFormat { get; set; } = "HH:mm";

    // ─── Feature Flags ───────────────────────────────────────────────

    /// <summary>Enable the affiliate/referral program.</summary>
    public bool EnableAffiliateProgram { get; set; }

    /// <summary>Enable integration with external channel managers (OTAs).</summary>
    public bool EnableChannelManager { get; set; }

    /// <summary>Enable the point-of-sale module.</summary>
    public bool EnablePOS { get; set; }

    /// <summary>Enable the room service ordering module.</summary>
    public bool EnableRoomService { get; set; }

    /// <summary>Enable the housekeeping management module.</summary>
    public bool EnableHousekeeping { get; set; }

    /// <summary>Enable the maintenance request/tracking module.</summary>
    public bool EnableMaintenanceTracking { get; set; }

    /// <summary>Enable inventory/stock management.</summary>
    public bool EnableInventoryManagement { get; set; }

    /// <summary>Enable reporting and analytics dashboard.</summary>
    public bool EnableReporting { get; set; } = true;

    /// <summary>Enable guest self-service portal.</summary>
    public bool EnableGuestPortal { get; set; }

    /// <summary>Enable multi-property management from a single tenant.</summary>
    public bool EnableMultiProperty { get; set; }

    /// <summary>Enable API access for third-party integrations.</summary>
    public bool EnableAPIAccess { get; set; }

    // ─── Feature & Menu Configuration ────────────────────────────────

    /// <summary>
    /// Granular feature and menu configuration for this tenant.
    /// Controls which modules, sub-features, and individual menu items
    /// are available. When null, the basic feature flags above are used.
    /// </summary>
    public TenantFeatureConfiguration? FeatureConfiguration { get; set; }

    /// <summary>
    /// Creates a deep copy of these settings. Useful when provisioning a new
    /// tenant from a template or when making speculative changes.
    /// </summary>
    public TenantBusinessSettings Clone()
    {
        var clone = (TenantBusinessSettings)MemberwiseClone();

        // Deep-copy mutable reference-type collections
        clone.AcceptedPaymentMethods = [.. AcceptedPaymentMethods];
        clone.BankAccounts = BankAccounts.Select(ba =>
            new BankAccount(ba.BankName, ba.AccountNumber, ba.AccountName, ba.BranchName, ba.IsDefault, ba.SwiftCode, ba.AccountType)
        ).ToList();
        clone.LoyaltyTiers = LoyaltyTiers.Select(lt =>
            new LoyaltyTierConfig(lt.TierName, lt.MinPoints, lt.DiscountPercentage, [.. lt.Benefits], lt.PointsMultiplier)
        ).ToList();
        clone.SupportedLanguages = [.. SupportedLanguages];

        return clone;
    }

    /// <summary>
    /// Creates a default settings object suitable for a typical Thai hospitality business.
    /// </summary>
    public static TenantBusinessSettings CreateThaiDefault()
    {
        return new TenantBusinessSettings
        {
            EnableVAT = true,
            VATRate = 7m,
            VATIncludedInPrice = true,
            DefaultCurrency = "THB",
            PricingModel = PricingModel.PerNight,
            DefaultCheckInTime = new TimeOnly(14, 0),
            DefaultCheckOutTime = new TimeOnly(12, 0),
            AcceptedPaymentMethods = ["Cash", "BankTransfer", "PromptPay", "CreditCard"],
            DefaultLanguage = "th",
            SupportedLanguages = ["th", "en"],
            TimeZone = "Asia/Bangkok",
            DateFormat = "dd/MM/yyyy",
            EnableAutoConfirmation = true,
            EnableAutoReceipt = true,
            EnableEmailNotification = true,
            EnableLINENotification = true,
            EnableReporting = true,
            WorkingDaysPerWeek = 6,
            WorkingHoursPerDay = 8m,
            OTRateMultiplier = 1.5m,
            PayrollCutoffDay = 25,
            FeatureConfiguration = TenantFeatureConfiguration.CreateThaiDefault()
        };
    }
}
