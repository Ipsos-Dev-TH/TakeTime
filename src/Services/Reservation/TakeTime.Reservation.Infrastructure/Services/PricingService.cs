using Microsoft.EntityFrameworkCore;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Infrastructure.Repositories;

namespace TakeTime.Reservation.Infrastructure.Services;

/// <summary>
/// Implements pricing calculations for accommodations using tenant-specific rules
/// including base rates, seasonal adjustments, dynamic pricing, and VAT.
/// </summary>
public sealed class PricingService : IPricingService
{
    private readonly ReservationDbContext _dbContext;

    public PricingService(ReservationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<decimal> CalculateAccommodationPriceAsync(
        Guid accommodationId,
        DateTime checkInDate,
        DateTime checkOutDate,
        TenantSettings tenantSettings,
        CancellationToken cancellationToken = default)
    {
        var accommodation = await _dbContext.Accommodations
            .FirstOrDefaultAsync(a => a.Id == accommodationId, cancellationToken);

        if (accommodation is null)
            throw new InvalidOperationException(
                $"Accommodation with ID '{accommodationId}' not found.");

        var baseRate = accommodation.BasePrice.Amount;
        var nights = (checkOutDate.Date - checkInDate.Date).Days;

        if (nights <= 0)
            throw new ArgumentException("Check-out date must be after check-in date.");

        // Calculate total by summing each night's adjusted rate
        var total = 0m;
        for (var i = 0; i < nights; i++)
        {
            var nightDate = checkInDate.Date.AddDays(i);
            var nightlyRate = ApplySeasonalAdjustment(baseRate, nightDate, tenantSettings);
            total += nightlyRate;
        }

        return Math.Round(total, 2);
    }

    /// <inheritdoc />
    public decimal ApplyDynamicPricing(
        decimal basePrice,
        decimal occupancyRate,
        TenantSettings tenantSettings)
    {
        if (!tenantSettings.DynamicPricingEnabled)
            return basePrice;

        if (occupancyRate >= tenantSettings.DynamicPricingHighOccupancyThreshold)
        {
            return Math.Round(
                basePrice * tenantSettings.DynamicPricingHighOccupancyMultiplier, 2);
        }

        if (occupancyRate <= tenantSettings.DynamicPricingLowOccupancyThreshold)
        {
            return Math.Round(
                basePrice * tenantSettings.DynamicPricingLowOccupancyMultiplier, 2);
        }

        return basePrice;
    }

    /// <inheritdoc />
    public decimal ApplySeasonalAdjustment(
        decimal basePrice,
        DateTime date,
        TenantSettings tenantSettings)
    {
        if (tenantSettings.SeasonalRates.Count == 0)
            return basePrice;

        // Find the applicable seasonal rate for the given date.
        // If multiple seasons overlap, take the one with the highest multiplier.
        var applicableRate = tenantSettings.SeasonalRates
            .Where(sr => date.Date >= sr.StartDate.Date && date.Date <= sr.EndDate.Date)
            .OrderByDescending(sr => sr.RateMultiplier)
            .FirstOrDefault();

        if (applicableRate is null)
            return basePrice;

        return Math.Round(basePrice * applicableRate.RateMultiplier, 2);
    }

    /// <inheritdoc />
    public decimal CalculateVAT(decimal amount, TenantSettings tenantSettings)
    {
        if (!tenantSettings.IsVATRegistered || tenantSettings.VATRate <= 0)
            return 0m;

        if (tenantSettings.IsVATInclusive)
        {
            // VAT is already included in the amount.
            // Extract the VAT component: VAT = amount - (amount / (1 + rate/100))
            var vatAmount = amount - (amount / (1 + tenantSettings.VATRate / 100m));
            return Math.Round(vatAmount, 2);
        }

        // VAT is exclusive: add on top
        return Math.Round(amount * tenantSettings.VATRate / 100m, 2);
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
            afterDiscount = 0;

        var vatAmount = CalculateVAT(afterDiscount, tenantSettings);
        var isVATInclusive = tenantSettings.IsVATRegistered && tenantSettings.IsVATInclusive;
        var vatRate = tenantSettings.IsVATRegistered ? tenantSettings.VATRate : 0m;

        decimal totalAmount;
        if (!tenantSettings.IsVATRegistered)
        {
            // No VAT
            totalAmount = afterDiscount;
        }
        else if (tenantSettings.IsVATInclusive)
        {
            // VAT is already included in the prices, total stays the same
            totalAmount = afterDiscount;
        }
        else
        {
            // VAT exclusive: add VAT on top
            totalAmount = afterDiscount + vatAmount;
        }

        return new PriceCalculationResult
        {
            AccommodationSubTotal = Math.Round(accommodationTotal, 2),
            ItemsSubTotal = Math.Round(itemsTotal, 2),
            SubTotal = Math.Round(subTotal, 2),
            DiscountAmount = Math.Round(discountAmount, 2),
            AfterDiscount = Math.Round(afterDiscount, 2),
            VATRate = vatRate,
            VATAmount = vatAmount,
            IsVATInclusive = isVATInclusive,
            TotalAmount = Math.Round(totalAmount, 2),
            Currency = tenantSettings.Currency ?? "THB"
        };
    }
}
