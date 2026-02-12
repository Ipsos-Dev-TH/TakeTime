using TakeTime.Core.Domain.Base;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Aggregate root representing a promotion or discount code that can be
/// applied to reservations. Supports both percentage and fixed-amount discounts
/// with configurable validity periods and usage limits.
/// </summary>
public class Promotion : AggregateRoot
{
    /// <summary>
    /// Display name of the promotion.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional discount code that guests can enter to apply this promotion.
    /// </summary>
    public string? Code { get; private set; }

    /// <summary>
    /// Type of discount: "Percentage" or "FixedAmount".
    /// </summary>
    public string DiscountType { get; private set; } = "Percentage";

    /// <summary>
    /// Discount value. For percentage type, this is the percentage (e.g., 10 for 10%).
    /// For fixed amount, this is the monetary value.
    /// </summary>
    public decimal DiscountValue { get; private set; }

    /// <summary>
    /// Start date from which the promotion is valid.
    /// </summary>
    public DateTime? ValidFrom { get; private set; }

    /// <summary>
    /// End date until which the promotion is valid.
    /// </summary>
    public DateTime? ValidTo { get; private set; }

    /// <summary>
    /// Maximum number of times this promotion can be used. Null means unlimited.
    /// </summary>
    public int? MaxUses { get; private set; }

    /// <summary>
    /// Current number of times this promotion has been used.
    /// </summary>
    public int CurrentUses { get; private set; }

    /// <summary>
    /// Minimum booking amount required to use this promotion.
    /// </summary>
    public decimal? MinimumBookingAmount { get; private set; }

    /// <summary>
    /// Optional description of the promotion.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether this promotion is currently active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Promotion() { }

    /// <summary>
    /// Creates a new promotion.
    /// </summary>
    public Promotion(
        string name,
        string discountType,
        decimal discountValue,
        string? code = null,
        DateTime? validFrom = null,
        DateTime? validTo = null,
        int? maxUses = null,
        decimal? minimumBookingAmount = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Promotion name is required.", nameof(name));

        if (discountValue <= 0)
            throw new ArgumentException("Discount value must be greater than zero.", nameof(discountValue));

        if (discountType != "Percentage" && discountType != "FixedAmount")
            throw new ArgumentException("Discount type must be 'Percentage' or 'FixedAmount'.", nameof(discountType));

        if (discountType == "Percentage" && discountValue > 100)
            throw new ArgumentException("Percentage discount cannot exceed 100%.", nameof(discountValue));

        if (validFrom.HasValue && validTo.HasValue && validTo.Value <= validFrom.Value)
            throw new ArgumentException("Valid-to date must be after valid-from date.");

        Name = name;
        Code = code;
        DiscountType = discountType;
        DiscountValue = discountValue;
        ValidFrom = validFrom;
        ValidTo = validTo;
        MaxUses = maxUses;
        MinimumBookingAmount = minimumBookingAmount;
        Description = description;
        IsActive = true;
        CurrentUses = 0;
    }

    /// <summary>
    /// Updates the promotion details.
    /// </summary>
    public void Update(
        string name,
        string? code,
        string discountType,
        decimal discountValue,
        DateTime? validFrom,
        DateTime? validTo,
        int? maxUses,
        decimal? minimumBookingAmount,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Promotion name is required.", nameof(name));

        if (discountValue <= 0)
            throw new ArgumentException("Discount value must be greater than zero.", nameof(discountValue));

        Name = name;
        Code = code;
        DiscountType = discountType;
        DiscountValue = discountValue;
        ValidFrom = validFrom;
        ValidTo = validTo;
        MaxUses = maxUses;
        MinimumBookingAmount = minimumBookingAmount;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the promotion.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the promotion.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the active status.
    /// </summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Increments the usage counter.
    /// </summary>
    public void IncrementUses()
    {
        CurrentUses++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Validates whether this promotion can be applied for the given booking parameters.
    /// </summary>
    /// <param name="checkInDate">The check-in date of the booking.</param>
    /// <param name="bookingAmount">The total booking amount.</param>
    /// <returns>True if the promotion is valid for the given parameters.</returns>
    public bool IsValid(DateTime checkInDate, decimal? bookingAmount = null)
    {
        if (!IsActive) return false;
        if (ValidFrom.HasValue && checkInDate.Date < ValidFrom.Value.Date) return false;
        if (ValidTo.HasValue && checkInDate.Date > ValidTo.Value.Date) return false;
        if (MaxUses.HasValue && CurrentUses >= MaxUses.Value) return false;
        if (MinimumBookingAmount.HasValue && bookingAmount.HasValue
            && bookingAmount.Value < MinimumBookingAmount.Value) return false;
        return true;
    }

    /// <summary>
    /// Calculates the discount amount for a given booking amount.
    /// </summary>
    /// <param name="amount">The booking amount to calculate discount for.</param>
    /// <returns>The discount amount.</returns>
    public decimal CalculateDiscount(decimal amount)
    {
        if (DiscountType == "Percentage")
        {
            return Math.Round(amount * DiscountValue / 100m, 2);
        }

        // Fixed amount discount should not exceed the booking amount
        return Math.Min(DiscountValue, amount);
    }
}
