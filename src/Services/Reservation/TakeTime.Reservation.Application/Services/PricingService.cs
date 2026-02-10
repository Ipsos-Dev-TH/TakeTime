using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Services;

/// <summary>
/// Implementation of <see cref="IPricingService"/>. Calculates accommodation prices
/// using tenant-specific rules including seasonal adjustments, dynamic pricing based
/// on occupancy, and VAT calculation.
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ILogger<PricingService> _logger;

    public PricingService(
        IAccommodationRepository accommodationRepository,
        ILogger<PricingService> logger)
    {
        _accommodationRepository = accommodationRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateAccommodationPriceAsync(
        Guid accommodationId,
        DateTime checkInDate,
        DateTime checkOutDate,
        TenantSettings tenantSettings,
        CancellationToken cancellationToken = default)
    {
        var baseRate = await _accommodationRepository.GetBaseRateAsync(accommodationId, cancellationToken);

        if (baseRate <= 0)
        {
            _logger.LogWarning(
                "Base rate for accommodation {AccommodationId} is {BaseRate}. Using zero.",
                accommodationId, baseRate);
            return 0;
        }

        // Calculate the average nightly rate considering seasonal adjustments across the stay
        var numberOfNights = (checkOutDate.Date - checkInDate.Date).Days;
        if (numberOfNights <= 0)
        {
            return baseRate;
        }

        decimal totalRate = 0;
        for (var i = 0; i < numberOfNights; i++)
        {
            var nightDate = checkInDate.Date.AddDays(i);
            var nightRate = ApplySeasonalAdjustment(baseRate, nightDate, tenantSettings);
            totalRate += nightRate;
        }

        var averageNightlyRate = Math.Round(totalRate / numberOfNights, 2);

        _logger.LogDebug(
            "Calculated average nightly rate for accommodation {AccommodationId}: {Rate} (base: {BaseRate})",
            accommodationId, averageNightlyRate, baseRate);

        return averageNightlyRate;
    }

    /// <inheritdoc />
    public decimal ApplyDynamicPricing(decimal basePrice, decimal occupancyRate, TenantSettings tenantSettings)
    {
        if (!tenantSettings.DynamicPricingEnabled)
        {
            return basePrice;
        }

        decimal multiplier;

        if (occupancyRate >= tenantSettings.DynamicPricingHighOccupancyThreshold)
        {
            // High demand - increase price
            multiplier = tenantSettings.DynamicPricingHighOccupancyMultiplier;
            _logger.LogDebug(
                "High occupancy dynamic pricing applied: {OccupancyRate}% >= {Threshold}%, multiplier: {Multiplier}",
                occupancyRate, tenantSettings.DynamicPricingHighOccupancyThreshold, multiplier);
        }
        else if (occupancyRate <= tenantSettings.DynamicPricingLowOccupancyThreshold)
        {
            // Low demand - decrease price
            multiplier = tenantSettings.DynamicPricingLowOccupancyMultiplier;
            _logger.LogDebug(
                "Low occupancy dynamic pricing applied: {OccupancyRate}% <= {Threshold}%, multiplier: {Multiplier}",
                occupancyRate, tenantSettings.DynamicPricingLowOccupancyThreshold, multiplier);
        }
        else
        {
            // Normal demand - no adjustment
            return basePrice;
        }

        return Math.Round(basePrice * multiplier, 2);
    }

    /// <inheritdoc />
    public decimal ApplySeasonalAdjustment(decimal basePrice, DateTime date, TenantSettings tenantSettings)
    {
        if (tenantSettings.SeasonalRates.Count == 0)
        {
            return basePrice;
        }

        // Find the seasonal rate that applies to the given date
        var applicableSeason = tenantSettings.SeasonalRates
            .FirstOrDefault(sr =>
                date.Date >= sr.StartDate.Date && date.Date <= sr.EndDate.Date);

        if (applicableSeason is null)
        {
            return basePrice;
        }

        var adjustedPrice = Math.Round(basePrice * applicableSeason.RateMultiplier, 2);

        _logger.LogDebug(
            "Seasonal adjustment applied for {Date}: season '{SeasonName}', multiplier {Multiplier}, " +
            "base {BasePrice} -> adjusted {AdjustedPrice}",
            date, applicableSeason.SeasonName, applicableSeason.RateMultiplier, basePrice, adjustedPrice);

        return adjustedPrice;
    }

    /// <inheritdoc />
    public decimal CalculateVAT(decimal amount, TenantSettings tenantSettings)
    {
        if (!tenantSettings.IsVATRegistered || tenantSettings.VATRate <= 0)
        {
            return 0;
        }

        decimal vatAmount;

        if (tenantSettings.IsVATInclusive)
        {
            // VAT is already included in the amount, extract it
            // Formula: VAT = amount - (amount / (1 + rate/100))
            vatAmount = amount - (amount / (1 + tenantSettings.VATRate / 100m));
        }
        else
        {
            // VAT is added on top
            // Formula: VAT = amount * rate/100
            vatAmount = amount * tenantSettings.VATRate / 100m;
        }

        return Math.Round(vatAmount, 2);
    }

    /// <inheritdoc />
    public PriceCalculationResult CalculateTotal(
        decimal accommodationTotal,
        decimal itemsTotal,
        decimal discountAmount,
        TenantSettings tenantSettings)
    {
        var subTotal = accommodationTotal + itemsTotal;
        var afterDiscount = subTotal - discountAmount;

        if (afterDiscount < 0)
        {
            afterDiscount = 0;
        }

        var vatAmount = CalculateVAT(afterDiscount, tenantSettings);

        decimal totalAmount;
        if (tenantSettings.IsVATInclusive)
        {
            // VAT is already part of the prices, total stays the same
            totalAmount = afterDiscount;
        }
        else
        {
            // VAT is added on top
            totalAmount = afterDiscount + vatAmount;
        }

        return new PriceCalculationResult
        {
            AccommodationSubTotal = accommodationTotal,
            ItemsSubTotal = itemsTotal,
            SubTotal = subTotal,
            DiscountAmount = discountAmount,
            AfterDiscount = afterDiscount,
            VATRate = tenantSettings.VATRate,
            VATAmount = vatAmount,
            IsVATInclusive = tenantSettings.IsVATInclusive,
            TotalAmount = Math.Round(totalAmount, 2),
            Currency = tenantSettings.Currency ?? "THB"
        };
    }
}
