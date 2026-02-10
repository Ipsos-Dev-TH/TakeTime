using TakeTime.Core.Domain.Base;

namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Configuration for a single loyalty program tier.
/// Defines the points threshold, discount rate, and associated benefits for the tier.
/// </summary>
public class LoyaltyTierConfig : ValueObject
{
    /// <summary>
    /// Display name of the tier (e.g., "Silver", "Gold", "Platinum").
    /// </summary>
    public string TierName { get; private set; } = string.Empty;

    /// <summary>
    /// Minimum accumulated points required to reach this tier.
    /// </summary>
    public int MinPoints { get; private set; }

    /// <summary>
    /// Discount percentage applied for members in this tier (0-100).
    /// </summary>
    public decimal DiscountPercentage { get; private set; }

    /// <summary>
    /// Free-form list of benefit descriptions for this tier
    /// (e.g., "Late checkout", "Free breakfast", "Room upgrade when available").
    /// </summary>
    public List<string> Benefits { get; private set; } = [];

    /// <summary>
    /// Optional multiplier applied to points earned while in this tier.
    /// For example, 1.5 means the member earns 50% more points per transaction.
    /// </summary>
    public decimal PointsMultiplier { get; private set; } = 1.0m;

    private LoyaltyTierConfig() { }

    public LoyaltyTierConfig(
        string tierName,
        int minPoints,
        decimal discountPercentage,
        List<string>? benefits = null,
        decimal pointsMultiplier = 1.0m)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tierName);
        ArgumentOutOfRangeException.ThrowIfNegative(minPoints);
        ArgumentOutOfRangeException.ThrowIfNegative(discountPercentage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(discountPercentage, 100m);
        ArgumentOutOfRangeException.ThrowIfLessThan(pointsMultiplier, 1.0m);

        TierName = tierName;
        MinPoints = minPoints;
        DiscountPercentage = discountPercentage;
        Benefits = benefits ?? [];
        PointsMultiplier = pointsMultiplier;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return TierName;
        yield return MinPoints;
        yield return DiscountPercentage;
        yield return PointsMultiplier;

        foreach (var benefit in Benefits.OrderBy(b => b))
        {
            yield return benefit;
        }
    }
}
