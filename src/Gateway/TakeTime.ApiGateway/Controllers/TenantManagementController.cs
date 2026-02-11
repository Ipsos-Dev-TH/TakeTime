using Microsoft.AspNetCore.Mvc;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// Admin API for managing tenants (properties/businesses).
/// Create, update, activate/deactivate tenants and their settings.
/// </summary>
[ApiController]
[Route("api/v1/admin/tenants")]
public class TenantManagementController : ControllerBase
{
    private readonly TenantManagementService _tenantService;
    private readonly TenantSettingsService _settingsService;

    public TenantManagementController(
        TenantManagementService tenantService,
        TenantSettingsService settingsService)
    {
        _tenantService = tenantService;
        _settingsService = settingsService;
    }

    /// <summary>Gets all registered tenants.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenants = await _tenantService.GetAllTenantsAsync(ct);
        return Ok(tenants);
    }

    /// <summary>Gets a tenant by ID or code.</summary>
    [HttpGet("{tenantId}")]
    public async Task<IActionResult> Get(string tenantId, CancellationToken ct)
    {
        var tenant = await _tenantService.GetTenantAsync(tenantId, ct);
        if (tenant is null)
            return NotFound(new { error = $"Tenant '{tenantId}' not found." });
        return Ok(tenant);
    }

    /// <summary>Creates a new tenant.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateTenantRequest request, CancellationToken ct)
    {
        var result = await _tenantService.CreateTenantAsync(
            request.Code, request.Name, request.ConnectionString,
            request.CreatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return CreatedAtAction(nameof(Get), new { tenantId = result.Tenant!.Id }, result.Tenant);
    }

    /// <summary>Updates tenant basic information.</summary>
    [HttpPut("{tenantId}")]
    public async Task<IActionResult> Update(
        string tenantId, [FromBody] UpdateTenantRequest request, CancellationToken ct)
    {
        var result = await _tenantService.UpdateTenantAsync(
            tenantId, request.Name, request.ContactEmail, request.ContactPhone,
            request.WebsiteUrl, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(result.Tenant);
    }

    /// <summary>Activates a tenant.</summary>
    [HttpPost("{tenantId}/activate")]
    public async Task<IActionResult> Activate(
        string tenantId, [FromBody] ActionRequest request, CancellationToken ct)
    {
        var result = await _tenantService.ActivateTenantAsync(
            tenantId, request.PerformedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Tenant activated successfully." });
    }

    /// <summary>Deactivates a tenant.</summary>
    [HttpPost("{tenantId}/deactivate")]
    public async Task<IActionResult> Deactivate(
        string tenantId, [FromBody] ActionRequest request, CancellationToken ct)
    {
        var result = await _tenantService.DeactivateTenantAsync(
            tenantId, request.PerformedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });

        return Ok(new { message = "Tenant deactivated successfully." });
    }

    /// <summary>Gets all business settings for a tenant.</summary>
    [HttpGet("{tenantId}/settings")]
    public async Task<IActionResult> GetSettings(string tenantId, CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(tenantId, ct);
        if (settings is null)
            return NotFound(new { error = $"Tenant '{tenantId}' not found." });
        return Ok(settings);
    }

    /// <summary>Updates full business settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings")]
    public async Task<IActionResult> UpdateSettings(
        string tenantId, [FromBody] TenantBusinessSettings settings,
        [FromQuery] string? updatedBy, CancellationToken ct)
    {
        var result = await _settingsService.UpdateSettingsAsync(tenantId, settings, updatedBy, ct);
        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Settings updated successfully." });
    }

    /// <summary>Updates VAT/tax settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/vat")]
    public async Task<IActionResult> UpdateVATSettings(
        string tenantId, [FromBody] UpdateVATRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateVATSettingsAsync(
            tenantId, request.EnableVAT, request.VATRate, request.TaxId,
            request.VATRegistrationNumber, request.VATIncludedInPrice,
            request.WithholdingTaxRate, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "VAT settings updated." });
    }

    /// <summary>Updates pricing settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/pricing")]
    public async Task<IActionResult> UpdatePricingSettings(
        string tenantId, [FromBody] UpdatePricingRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdatePricingSettingsAsync(
            tenantId, request.DefaultCurrency, request.PricingModel,
            request.EnableDynamicPricing, request.EnableSeasonalPricing,
            request.MinimumPrice, request.MaximumPrice, request.EnablePromotions,
            request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Pricing settings updated." });
    }

    /// <summary>Updates payment settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/payment")]
    public async Task<IActionResult> UpdatePaymentSettings(
        string tenantId, [FromBody] UpdatePaymentRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdatePaymentSettingsAsync(
            tenantId, request.AcceptedMethods, request.EnablePartialPayment,
            request.DepositPercentage, request.PaymentDueDays, request.BankAccounts,
            request.EnableInstallments, request.MaxInstallments,
            request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Payment settings updated." });
    }

    /// <summary>Updates reservation settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/reservation")]
    public async Task<IActionResult> UpdateReservationSettings(
        string tenantId, [FromBody] UpdateReservationSettingsRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateReservationSettingsAsync(
            tenantId, request.CheckInTime, request.CheckOutTime,
            request.MaxAdvanceBookingDays, request.MinAdvanceBookingDays,
            request.CancellationPolicyHours, request.EnableAutoConfirmation,
            request.MinStayNights, request.MaxStayNights,
            request.CancellationFeePercentage, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Reservation settings updated." });
    }

    /// <summary>Updates notification settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/notification")]
    public async Task<IActionResult> UpdateNotificationSettings(
        string tenantId, [FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateNotificationSettingsAsync(
            tenantId, request.EnableEmail, request.EnableSMS, request.EnableLINE,
            request.EnableTelegram, request.LINENotifyToken, request.TelegramBotToken,
            request.TelegramChatId, request.SMSProviderKey, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Notification settings updated." });
    }

    /// <summary>Updates loyalty program settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/loyalty")]
    public async Task<IActionResult> UpdateLoyaltySettings(
        string tenantId, [FromBody] UpdateLoyaltyRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateLoyaltySettingsAsync(
            tenantId, request.EnableLoyaltyProgram, request.PointsPerCurrencyUnit,
            request.LoyaltyTiers, request.PointsExpiryDays, request.MinRedemptionPoints,
            request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Loyalty settings updated." });
    }

    /// <summary>Updates HR/payroll settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/hr")]
    public async Task<IActionResult> UpdateHRSettings(
        string tenantId, [FromBody] UpdateHRSettingsRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateHRSettingsAsync(
            tenantId, request.EnableOTCalculation, request.OTRateMultiplier,
            request.WorkingHoursPerDay, request.PayrollCutoffDay,
            request.EnableHolidayOTPremium, request.HolidayOTRateMultiplier,
            request.WorkingDaysPerWeek, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "HR settings updated." });
    }

    /// <summary>Updates localization settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/localization")]
    public async Task<IActionResult> UpdateLocalizationSettings(
        string tenantId, [FromBody] UpdateLocalizationRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateLocalizationSettingsAsync(
            tenantId, request.DefaultLanguage, request.SupportedLanguages,
            request.TimeZone, request.DateFormat, request.CurrencyFormat,
            request.TimeFormat, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Localization settings updated." });
    }

    /// <summary>Updates receipt/invoice settings for a tenant.</summary>
    [HttpPut("{tenantId}/settings/receipt")]
    public async Task<IActionResult> UpdateReceiptSettings(
        string tenantId, [FromBody] UpdateReceiptSettingsRequest request, CancellationToken ct)
    {
        var result = await _settingsService.UpdateReceiptSettingsAsync(
            tenantId, request.ReceiptPrefix, request.InvoicePrefix,
            request.TaxInvoicePrefix, request.CreditNotePrefix,
            request.EnableAutoReceipt, request.DocumentFooterText,
            request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Receipt settings updated." });
    }

    /// <summary>Updates tenant branding (logo, colors, domain).</summary>
    [HttpPut("{tenantId}/branding")]
    public async Task<IActionResult> UpdateBranding(
        string tenantId, [FromBody] UpdateBrandingRequest request, CancellationToken ct)
    {
        var result = await _tenantService.UpdateBrandingAsync(
            tenantId, request.LogoUrl, request.FaviconUrl,
            request.PrimaryColor, request.SecondaryColor,
            request.CustomDomain, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Branding updated." });
    }

    /// <summary>Updates tenant subscription tier and limits.</summary>
    [HttpPut("{tenantId}/subscription")]
    public async Task<IActionResult> UpdateSubscription(
        string tenantId, [FromBody] UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _tenantService.UpdateSubscriptionAsync(
            tenantId, request.Tier, request.StartDate, request.EndDate,
            request.MaxRooms, request.MaxUsers, request.UpdatedBy, ct);

        if (!result.Success)
            return BadRequest(new { error = result.ErrorMessage });
        return Ok(new { message = "Subscription updated." });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

public record CreateTenantRequest(
    string Code, string Name, string? ConnectionString = null, string? CreatedBy = null);

public record UpdateTenantRequest(
    string? Name = null, string? ContactEmail = null, string? ContactPhone = null,
    string? WebsiteUrl = null, string? UpdatedBy = null);

public record ActionRequest(string? PerformedBy = null);

public record UpdateVATRequest(
    bool EnableVAT = false, decimal VATRate = 7m, string? TaxId = null,
    string? VATRegistrationNumber = null, bool VATIncludedInPrice = true,
    decimal WithholdingTaxRate = 0m, string? UpdatedBy = null);

public record UpdatePricingRequest(
    string DefaultCurrency = "THB", PricingModel PricingModel = PricingModel.PerNight,
    bool EnableDynamicPricing = false, bool EnableSeasonalPricing = false,
    decimal? MinimumPrice = null, decimal? MaximumPrice = null,
    bool EnablePromotions = true, string? UpdatedBy = null);

public record UpdatePaymentRequest(
    List<string>? AcceptedMethods = null, bool EnablePartialPayment = false,
    decimal DepositPercentage = 0, int PaymentDueDays = 7,
    List<BankAccount>? BankAccounts = null, bool EnableInstallments = false,
    int MaxInstallments = 3, string? UpdatedBy = null);

public record UpdateReservationSettingsRequest(
    TimeOnly? CheckInTime = null, TimeOnly? CheckOutTime = null,
    int MaxAdvanceBookingDays = 365, int MinAdvanceBookingDays = 0,
    int CancellationPolicyHours = 48, bool EnableAutoConfirmation = true,
    int MinStayNights = 1, int MaxStayNights = 0,
    decimal CancellationFeePercentage = 0, string? UpdatedBy = null);

public record UpdateNotificationSettingsRequest(
    bool EnableEmail = true, bool EnableSMS = false, bool EnableLINE = false,
    bool EnableTelegram = false, string? LINENotifyToken = null,
    string? TelegramBotToken = null, string? TelegramChatId = null,
    string? SMSProviderKey = null, string? UpdatedBy = null);

public record UpdateLoyaltyRequest(
    bool EnableLoyaltyProgram = false, decimal PointsPerCurrencyUnit = 1m,
    List<LoyaltyTierConfig>? LoyaltyTiers = null, int PointsExpiryDays = 0,
    int MinRedemptionPoints = 100, string? UpdatedBy = null);

public record UpdateHRSettingsRequest(
    bool EnableOTCalculation = false, decimal OTRateMultiplier = 1.5m,
    decimal WorkingHoursPerDay = 8m, int PayrollCutoffDay = 25,
    bool EnableHolidayOTPremium = false, decimal HolidayOTRateMultiplier = 3.0m,
    int WorkingDaysPerWeek = 6, string? UpdatedBy = null);

public record UpdateLocalizationRequest(
    string DefaultLanguage = "th", List<string>? SupportedLanguages = null,
    string TimeZone = "Asia/Bangkok", string DateFormat = "dd/MM/yyyy",
    string CurrencyFormat = "#,##0.00", string TimeFormat = "HH:mm",
    string? UpdatedBy = null);

public record UpdateReceiptSettingsRequest(
    string? ReceiptPrefix = null, string? InvoicePrefix = null,
    string? TaxInvoicePrefix = null, string? CreditNotePrefix = null,
    bool EnableAutoReceipt = true, string? DocumentFooterText = null,
    string? UpdatedBy = null);

public record UpdateBrandingRequest(
    string? LogoUrl = null, string? FaviconUrl = null,
    string? PrimaryColor = null, string? SecondaryColor = null,
    string? CustomDomain = null, string? UpdatedBy = null);

public record UpdateSubscriptionRequest(
    SubscriptionTier Tier, DateTime? StartDate = null, DateTime? EndDate = null,
    int MaxRooms = 10, int MaxUsers = 5, string? UpdatedBy = null);
