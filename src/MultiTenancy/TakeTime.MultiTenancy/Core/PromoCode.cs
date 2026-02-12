namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Discount type for promo codes.
/// </summary>
public enum DiscountType
{
    /// <summary>Percentage discount (e.g., 10 = 10% off).</summary>
    Percentage = 0,

    /// <summary>Fixed amount discount in the plan's currency.</summary>
    FixedAmount = 1
}

/// <summary>
/// Database-backed promotional/discount code for subscription billing.
/// Supports expiration dates, usage limits, and plan-specific applicability.
/// </summary>
public class PromoCode
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The promo code string (e.g., "WELCOME10", "ANNUAL20").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Description of the promo code for admin reference.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this is a percentage or fixed amount discount.
    /// </summary>
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;

    /// <summary>
    /// Discount value: percentage (e.g., 10 for 10%) or fixed amount (e.g., 500 for ฿500).
    /// </summary>
    public decimal DiscountValue { get; set; }

    /// <summary>
    /// Maximum number of times this code can be used. Null = unlimited.
    /// </summary>
    public int? MaxUsageCount { get; set; }

    /// <summary>
    /// Current number of times this code has been redeemed.
    /// </summary>
    public int CurrentUsageCount { get; set; }

    /// <summary>
    /// Start date when the promo code becomes valid.
    /// </summary>
    public DateTime ValidFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expiration date. Null = no expiration.
    /// </summary>
    public DateTime? ValidUntil { get; set; }

    /// <summary>
    /// JSON list of plan IDs this code applies to. Empty/null = all plans.
    /// </summary>
    public List<Guid> ApplicablePlanIds { get; set; } = [];

    /// <summary>
    /// Minimum billing cycles the subscriber must commit to.
    /// </summary>
    public int MinBillingCycles { get; set; }

    /// <summary>
    /// Whether this promo code is currently active.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Checks if this promo code is currently valid and usable.
    /// </summary>
    public bool IsValid()
    {
        if (!IsActive) return false;
        var now = DateTime.UtcNow;
        if (now < ValidFrom) return false;
        if (ValidUntil.HasValue && now > ValidUntil.Value) return false;
        if (MaxUsageCount.HasValue && CurrentUsageCount >= MaxUsageCount.Value) return false;
        return true;
    }

    /// <summary>
    /// Checks if this promo code is applicable to the given plan.
    /// </summary>
    public bool IsApplicableToPlan(Guid planId)
    {
        if (ApplicablePlanIds.Count == 0) return true;
        return ApplicablePlanIds.Contains(planId);
    }
}
