using Microsoft.Extensions.Logging;
using TakeTime.Payment.Application.Commands;

namespace TakeTime.Payment.Application.Services;

/// <summary>
/// Service for calculating VAT (Value Added Tax) amounts based on tenant-specific
/// settings. Supports both VAT-inclusive and VAT-exclusive pricing models,
/// reverse VAT calculation, and formatted display strings.
/// </summary>
public sealed class VATCalculationService
{
    private readonly ILogger<VATCalculationService> _logger;

    public VATCalculationService(ILogger<VATCalculationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculates the VAT breakdown for a given amount based on tenant settings.
    /// Returns both the VAT amount and the amount before VAT.
    /// </summary>
    /// <param name="amount">The amount to calculate VAT for.</param>
    /// <param name="tenantSettings">Tenant configuration with VAT settings.</param>
    /// <returns>VAT breakdown result.</returns>
    public VATBreakdown CalculateVAT(decimal amount, TenantSettings tenantSettings)
    {
        if (!IsVATRegistered(tenantSettings) || tenantSettings.VATRate <= 0 || amount == 0)
        {
            return new VATBreakdown
            {
                OriginalAmount = amount,
                AmountBeforeVAT = amount,
                VATAmount = 0,
                VATRate = 0,
                TotalAmount = amount,
                IsVATInclusive = false,
                IsVATRegistered = false
            };
        }

        decimal amountBeforeVAT;
        decimal vatAmount;
        decimal totalAmount;

        if (tenantSettings.IsVATInclusive)
        {
            // VAT is already included in the amount
            // Amount before VAT = amount / (1 + rate/100)
            amountBeforeVAT = Math.Round(amount / (1 + tenantSettings.VATRate / 100m), 2);
            vatAmount = amount - amountBeforeVAT;
            totalAmount = amount;
        }
        else
        {
            // VAT is added on top
            amountBeforeVAT = amount;
            vatAmount = Math.Round(amount * tenantSettings.VATRate / 100m, 2);
            totalAmount = amount + vatAmount;
        }

        _logger.LogDebug(
            "VAT calculated: Amount={Amount}, BeforeVAT={BeforeVAT}, VAT={VAT}, " +
            "Total={Total}, Rate={Rate}%, Inclusive={Inclusive}",
            amount, amountBeforeVAT, vatAmount, totalAmount,
            tenantSettings.VATRate, tenantSettings.IsVATInclusive);

        return new VATBreakdown
        {
            OriginalAmount = amount,
            AmountBeforeVAT = amountBeforeVAT,
            VATAmount = vatAmount,
            VATRate = tenantSettings.VATRate,
            TotalAmount = totalAmount,
            IsVATInclusive = tenantSettings.IsVATInclusive,
            IsVATRegistered = true
        };
    }

    /// <summary>
    /// Checks whether the tenant is VAT registered.
    /// </summary>
    public bool IsVATRegistered(TenantSettings tenantSettings)
    {
        return tenantSettings.IsVATRegistered;
    }

    /// <summary>
    /// Gets the VAT rate configured for the tenant.
    /// Returns 0 if tenant is not VAT registered.
    /// </summary>
    public decimal GetVATRate(TenantSettings tenantSettings)
    {
        return tenantSettings.IsVATRegistered ? tenantSettings.VATRate : 0;
    }

    /// <summary>
    /// Calculates the total amount including VAT from a base (pre-VAT) amount.
    /// </summary>
    /// <param name="baseAmount">The amount before VAT.</param>
    /// <param name="tenantSettings">Tenant configuration.</param>
    /// <returns>Amount with VAT included.</returns>
    public decimal CalculateAmountWithVAT(decimal baseAmount, TenantSettings tenantSettings)
    {
        if (!IsVATRegistered(tenantSettings) || tenantSettings.VATRate <= 0)
        {
            return baseAmount;
        }

        var vatAmount = Math.Round(baseAmount * tenantSettings.VATRate / 100m, 2);
        return baseAmount + vatAmount;
    }

    /// <summary>
    /// Performs reverse VAT calculation - extracts the pre-VAT amount from a
    /// VAT-inclusive total.
    /// </summary>
    /// <param name="totalAmount">The total amount including VAT.</param>
    /// <param name="tenantSettings">Tenant configuration.</param>
    /// <returns>The amount without VAT.</returns>
    public decimal CalculateAmountWithoutVAT(decimal totalAmount, TenantSettings tenantSettings)
    {
        if (!IsVATRegistered(tenantSettings) || tenantSettings.VATRate <= 0)
        {
            return totalAmount;
        }

        return Math.Round(totalAmount / (1 + tenantSettings.VATRate / 100m), 2);
    }

    /// <summary>
    /// Formats the VAT display for receipts and invoices, showing the amount
    /// breakdown in a tenant-appropriate format.
    /// </summary>
    /// <param name="amount">Amount before VAT.</param>
    /// <param name="vatAmount">VAT amount.</param>
    /// <param name="tenantSettings">Tenant configuration.</param>
    /// <returns>Formatted VAT display string.</returns>
    public VATDisplayInfo FormatVATDisplay(decimal amount, decimal vatAmount, TenantSettings tenantSettings)
    {
        var currency = tenantSettings.Currency ?? "THB";
        var currencySymbol = currency == "THB" ? "\u0E3F" : currency;

        if (!IsVATRegistered(tenantSettings))
        {
            return new VATDisplayInfo
            {
                ShowVAT = false,
                AmountLine = $"{currencySymbol}{amount + vatAmount:N2}",
                VATLine = string.Empty,
                TotalLine = $"{currencySymbol}{amount + vatAmount:N2}",
                VATNote = "Non-VAT registered business"
            };
        }

        var total = tenantSettings.IsVATInclusive
            ? amount + vatAmount
            : amount + vatAmount;

        if (tenantSettings.IsVATInclusive)
        {
            return new VATDisplayInfo
            {
                ShowVAT = true,
                AmountLine = $"Amount (before VAT): {currencySymbol}{amount:N2}",
                VATLine = $"VAT {tenantSettings.VATRate}% (included): {currencySymbol}{vatAmount:N2}",
                TotalLine = $"Total: {currencySymbol}{total:N2}",
                VATNote = $"* Prices include VAT {tenantSettings.VATRate}%",
                VATRegistrationNumber = tenantSettings.VATRegistrationNumber
            };
        }

        return new VATDisplayInfo
        {
            ShowVAT = true,
            AmountLine = $"Subtotal: {currencySymbol}{amount:N2}",
            VATLine = $"VAT {tenantSettings.VATRate}%: {currencySymbol}{vatAmount:N2}",
            TotalLine = $"Total (incl. VAT): {currencySymbol}{total:N2}",
            VATNote = $"VAT {tenantSettings.VATRate}% added",
            VATRegistrationNumber = tenantSettings.VATRegistrationNumber
        };
    }
}

/// <summary>
/// Result of a VAT calculation, containing the full breakdown.
/// </summary>
public sealed class VATBreakdown
{
    public decimal OriginalAmount { get; set; }
    public decimal AmountBeforeVAT { get; set; }
    public decimal VATAmount { get; set; }
    public decimal VATRate { get; set; }
    public decimal TotalAmount { get; set; }
    public bool IsVATInclusive { get; set; }
    public bool IsVATRegistered { get; set; }
}

/// <summary>
/// Formatted VAT display information for receipts and invoices.
/// </summary>
public sealed class VATDisplayInfo
{
    public bool ShowVAT { get; set; }
    public string AmountLine { get; set; } = string.Empty;
    public string VATLine { get; set; } = string.Empty;
    public string TotalLine { get; set; } = string.Empty;
    public string VATNote { get; set; } = string.Empty;
    public string? VATRegistrationNumber { get; set; }
}
