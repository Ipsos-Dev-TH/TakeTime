using Microsoft.Extensions.Logging;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Service for reading and updating specific business settings for a tenant.
/// Provides granular access to individual setting categories (VAT, pricing,
/// payment, reservations, etc.) without requiring the caller to manage the
/// full <see cref="TenantBusinessSettings"/> object.
/// </summary>
public class TenantSettingsService
{
    private readonly ITenantStore _tenantStore;
    private readonly ILogger<TenantSettingsService> _logger;

    public TenantSettingsService(
        ITenantStore tenantStore,
        ILogger<TenantSettingsService> logger)
    {
        _tenantStore = tenantStore;
        _logger = logger;
    }

    /// <summary>
    /// Gets the full business settings for a tenant.
    /// </summary>
    public async Task<TenantBusinessSettings?> GetSettingsAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        return tenant?.BusinessSettings;
    }

    /// <summary>
    /// Replaces the full business settings for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateSettingsAsync(
        string tenantId,
        TenantBusinessSettings settings,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        tenant.BusinessSettings = settings;
        tenant.MarkUpdated(updatedBy);

        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated full business settings for tenant {TenantCode} by {UpdatedBy}",
            tenant.Code, updatedBy);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── VAT / Tax Settings ─────────────────────────────────────────

    /// <summary>
    /// Updates VAT/tax configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateVATSettingsAsync(
        string tenantId,
        bool enableVAT,
        decimal vatRate,
        string? taxId = null,
        string? vatRegistrationNumber = null,
        bool vatIncludedInPrice = true,
        decimal withholdingTaxRate = 0m,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (vatRate < 0 || vatRate > 100)
            return TenantOperationResult.Fail("VAT rate must be between 0 and 100.");

        var settings = tenant.BusinessSettings;
        settings.EnableVAT = enableVAT;
        settings.VATRate = vatRate;
        settings.TaxId = taxId;
        settings.VATRegistrationNumber = vatRegistrationNumber;
        settings.VATIncludedInPrice = vatIncludedInPrice;
        settings.WithholdingTaxRate = withholdingTaxRate;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated VAT settings for tenant {TenantCode}: VAT={EnableVAT}, Rate={Rate}%",
            tenant.Code, enableVAT, vatRate);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Pricing Settings ───────────────────────────────────────────

    /// <summary>
    /// Updates pricing configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdatePricingSettingsAsync(
        string tenantId,
        string defaultCurrency,
        PricingModel pricingModel,
        bool enableDynamicPricing = false,
        bool enableSeasonalPricing = false,
        decimal? minimumPrice = null,
        decimal? maximumPrice = null,
        bool enablePromotions = true,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (minimumPrice.HasValue && maximumPrice.HasValue && minimumPrice > maximumPrice)
            return TenantOperationResult.Fail("Minimum price cannot exceed maximum price.");

        var settings = tenant.BusinessSettings;
        settings.DefaultCurrency = defaultCurrency;
        settings.PricingModel = pricingModel;
        settings.EnableDynamicPricing = enableDynamicPricing;
        settings.EnableSeasonalPricing = enableSeasonalPricing;
        settings.MinimumPrice = minimumPrice;
        settings.MaximumPrice = maximumPrice;
        settings.EnablePromotions = enablePromotions;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated pricing settings for tenant {TenantCode}: Currency={Currency}, Model={Model}",
            tenant.Code, defaultCurrency, pricingModel);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Payment Settings ───────────────────────────────────────────

    /// <summary>
    /// Updates payment configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdatePaymentSettingsAsync(
        string tenantId,
        List<string>? acceptedMethods = null,
        bool enablePartialPayment = false,
        decimal depositPercentage = 0,
        int paymentDueDays = 7,
        List<BankAccount>? bankAccounts = null,
        bool enableInstallments = false,
        int maxInstallments = 3,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (depositPercentage < 0 || depositPercentage > 100)
            return TenantOperationResult.Fail("Deposit percentage must be between 0 and 100.");

        if (paymentDueDays < 0)
            return TenantOperationResult.Fail("Payment due days cannot be negative.");

        var settings = tenant.BusinessSettings;
        if (acceptedMethods is not null)
            settings.AcceptedPaymentMethods = acceptedMethods;
        settings.EnablePartialPayment = enablePartialPayment;
        settings.DepositPercentage = depositPercentage;
        settings.PaymentDueDays = paymentDueDays;
        if (bankAccounts is not null)
            settings.BankAccounts = bankAccounts;
        settings.EnableInstallments = enableInstallments;
        settings.MaxInstallments = maxInstallments;

        // Ensure only one bank account is marked as default
        EnsureSingleDefaultBankAccount(settings.BankAccounts);

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated payment settings for tenant {TenantCode}: Methods={Methods}, DueDays={DueDays}",
            tenant.Code, string.Join(", ", settings.AcceptedPaymentMethods), paymentDueDays);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Reservation Settings ───────────────────────────────────────

    /// <summary>
    /// Updates reservation/booking configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateReservationSettingsAsync(
        string tenantId,
        TimeOnly? checkInTime = null,
        TimeOnly? checkOutTime = null,
        int maxAdvanceBookingDays = 365,
        int minAdvanceBookingDays = 0,
        int cancellationPolicyHours = 48,
        bool enableAutoConfirmation = true,
        int minStayNights = 1,
        int maxStayNights = 0,
        decimal cancellationFeePercentage = 0,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (minAdvanceBookingDays > maxAdvanceBookingDays)
            return TenantOperationResult.Fail("Minimum advance booking days cannot exceed maximum.");

        if (maxStayNights > 0 && minStayNights > maxStayNights)
            return TenantOperationResult.Fail("Minimum stay cannot exceed maximum stay.");

        var settings = tenant.BusinessSettings;
        if (checkInTime.HasValue) settings.DefaultCheckInTime = checkInTime.Value;
        if (checkOutTime.HasValue) settings.DefaultCheckOutTime = checkOutTime.Value;
        settings.MaxAdvanceBookingDays = maxAdvanceBookingDays;
        settings.MinAdvanceBookingDays = minAdvanceBookingDays;
        settings.CancellationPolicyHours = cancellationPolicyHours;
        settings.EnableAutoConfirmation = enableAutoConfirmation;
        settings.MinStayNights = minStayNights;
        settings.MaxStayNights = maxStayNights;
        settings.CancellationFeePercentage = cancellationFeePercentage;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated reservation settings for tenant {TenantCode}: CheckIn={CheckIn}, CheckOut={CheckOut}",
            tenant.Code, settings.DefaultCheckInTime, settings.DefaultCheckOutTime);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Notification Settings ──────────────────────────────────────

    /// <summary>
    /// Updates notification configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateNotificationSettingsAsync(
        string tenantId,
        bool enableEmail = true,
        bool enableSMS = false,
        bool enableLINE = false,
        bool enableTelegram = false,
        string? lineNotifyToken = null,
        string? telegramBotToken = null,
        string? telegramChatId = null,
        string? smsProviderKey = null,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        var settings = tenant.BusinessSettings;
        settings.EnableEmailNotification = enableEmail;
        settings.EnableSMSNotification = enableSMS;
        settings.EnableLINENotification = enableLINE;
        settings.EnableTelegramNotification = enableTelegram;
        settings.LINENotifyToken = lineNotifyToken;
        settings.TelegramBotToken = telegramBotToken;
        settings.TelegramChatId = telegramChatId;
        settings.SMSProviderKey = smsProviderKey;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated notification settings for tenant {TenantCode}: Email={Email}, SMS={SMS}, LINE={LINE}, Telegram={Telegram}",
            tenant.Code, enableEmail, enableSMS, enableLINE, enableTelegram);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Loyalty Program Settings ───────────────────────────────────

    /// <summary>
    /// Updates loyalty program configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateLoyaltySettingsAsync(
        string tenantId,
        bool enableLoyaltyProgram,
        decimal pointsPerCurrencyUnit = 1m,
        List<LoyaltyTierConfig>? loyaltyTiers = null,
        int pointsExpiryDays = 0,
        int minRedemptionPoints = 100,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (pointsPerCurrencyUnit <= 0)
            return TenantOperationResult.Fail("Points per currency unit must be positive.");

        var settings = tenant.BusinessSettings;
        settings.EnableLoyaltyProgram = enableLoyaltyProgram;
        settings.PointsPerCurrencyUnit = pointsPerCurrencyUnit;
        if (loyaltyTiers is not null)
            settings.LoyaltyTiers = loyaltyTiers;
        settings.PointsExpiryDays = pointsExpiryDays;
        settings.MinRedemptionPoints = minRedemptionPoints;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated loyalty settings for tenant {TenantCode}: Enabled={Enabled}, Points/Unit={Points}",
            tenant.Code, enableLoyaltyProgram, pointsPerCurrencyUnit);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── HR / Payroll Settings ──────────────────────────────────────

    /// <summary>
    /// Updates HR/payroll configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateHRSettingsAsync(
        string tenantId,
        bool enableOTCalculation = false,
        decimal otRateMultiplier = 1.5m,
        decimal workingHoursPerDay = 8m,
        int payrollCutoffDay = 25,
        bool enableHolidayOTPremium = false,
        decimal holidayOTRateMultiplier = 3.0m,
        int workingDaysPerWeek = 6,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        if (payrollCutoffDay < 1 || payrollCutoffDay > 31)
            return TenantOperationResult.Fail("Payroll cutoff day must be between 1 and 31.");

        if (workingHoursPerDay <= 0 || workingHoursPerDay > 24)
            return TenantOperationResult.Fail("Working hours per day must be between 0 and 24.");

        if (workingDaysPerWeek < 1 || workingDaysPerWeek > 7)
            return TenantOperationResult.Fail("Working days per week must be between 1 and 7.");

        var settings = tenant.BusinessSettings;
        settings.EnableOTCalculation = enableOTCalculation;
        settings.OTRateMultiplier = otRateMultiplier;
        settings.WorkingHoursPerDay = workingHoursPerDay;
        settings.PayrollCutoffDay = payrollCutoffDay;
        settings.EnableHolidayOTPremium = enableHolidayOTPremium;
        settings.HolidayOTRateMultiplier = holidayOTRateMultiplier;
        settings.WorkingDaysPerWeek = workingDaysPerWeek;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated HR settings for tenant {TenantCode}: OT={EnableOT}, Rate={OTRate}x, CutoffDay={CutoffDay}",
            tenant.Code, enableOTCalculation, otRateMultiplier, payrollCutoffDay);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Localization Settings ──────────────────────────────────────

    /// <summary>
    /// Updates localization configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateLocalizationSettingsAsync(
        string tenantId,
        string defaultLanguage = "th",
        List<string>? supportedLanguages = null,
        string timeZone = "Asia/Bangkok",
        string dateFormat = "dd/MM/yyyy",
        string currencyFormat = "#,##0.00",
        string timeFormat = "HH:mm",
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        // Validate timezone
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TenantOperationResult.Fail($"Invalid time zone: '{timeZone}'.");
        }

        var settings = tenant.BusinessSettings;
        settings.DefaultLanguage = defaultLanguage;
        if (supportedLanguages is not null)
            settings.SupportedLanguages = supportedLanguages;
        settings.TimeZone = timeZone;
        settings.DateFormat = dateFormat;
        settings.CurrencyFormat = currencyFormat;
        settings.TimeFormat = timeFormat;

        // Ensure default language is in supported languages
        if (!settings.SupportedLanguages.Contains(defaultLanguage))
        {
            settings.SupportedLanguages.Add(defaultLanguage);
        }

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated localization settings for tenant {TenantCode}: Lang={Lang}, TZ={TimeZone}",
            tenant.Code, defaultLanguage, timeZone);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Feature Flags ──────────────────────────────────────────────

    /// <summary>
    /// Updates feature flag configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateFeatureFlagsAsync(
        string tenantId,
        Action<TenantBusinessSettings> updateFlags,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        updateFlags(tenant.BusinessSettings);
        tenant.MarkUpdated(updatedBy);

        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated feature flags for tenant {TenantCode} by {UpdatedBy}",
            tenant.Code, updatedBy);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Receipt / Invoice Settings ─────────────────────────────────

    /// <summary>
    /// Updates receipt and invoice configuration for a tenant.
    /// </summary>
    public async Task<TenantOperationResult> UpdateReceiptSettingsAsync(
        string tenantId,
        string? receiptPrefix = null,
        string? invoicePrefix = null,
        string? taxInvoicePrefix = null,
        string? creditNotePrefix = null,
        bool enableAutoReceipt = true,
        string? documentFooterText = null,
        string? updatedBy = null,
        CancellationToken cancellationToken = default)
    {
        var tenant = await ResolveTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
            return TenantOperationResult.Fail($"Tenant '{tenantId}' not found.");

        var settings = tenant.BusinessSettings;
        if (receiptPrefix is not null) settings.ReceiptPrefix = receiptPrefix;
        if (invoicePrefix is not null) settings.InvoicePrefix = invoicePrefix;
        if (taxInvoicePrefix is not null) settings.TaxInvoicePrefix = taxInvoicePrefix;
        if (creditNotePrefix is not null) settings.CreditNotePrefix = creditNotePrefix;
        settings.EnableAutoReceipt = enableAutoReceipt;
        settings.DocumentFooterText = documentFooterText;

        tenant.MarkUpdated(updatedBy);
        await _tenantStore.SaveTenantAsync(tenant, cancellationToken);

        _logger.LogInformation(
            "Updated receipt settings for tenant {TenantCode}: ReceiptPrefix={Prefix}",
            tenant.Code, settings.ReceiptPrefix);

        return TenantOperationResult.Ok(tenant);
    }

    // ─── Helpers ─────────────────────────────────────────────────────

    private async Task<Tenant?> ResolveTenantAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var tenant = await _tenantStore.GetTenantAsync(tenantId, cancellationToken);
        if (tenant is null)
        {
            tenant = await _tenantStore.GetTenantByCodeAsync(tenantId, cancellationToken);
        }
        return tenant;
    }

    private static void EnsureSingleDefaultBankAccount(List<BankAccount> accounts)
    {
        if (accounts.Count == 0)
            return;

        var defaults = accounts.Where(a => a.IsDefault).ToList();

        if (defaults.Count > 1)
        {
            // Keep only the first default, unmark the rest
            for (int i = 1; i < defaults.Count; i++)
            {
                var index = accounts.IndexOf(defaults[i]);
                accounts[index] = defaults[i].WithDefault(false);
            }
        }
        else if (defaults.Count == 0)
        {
            // Mark the first account as default
            accounts[0] = accounts[0].WithDefault(true);
        }
    }
}
