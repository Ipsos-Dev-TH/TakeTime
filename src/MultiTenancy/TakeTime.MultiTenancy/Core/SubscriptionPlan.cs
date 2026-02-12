namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Represents a subscription plan available for tenants to subscribe to.
/// Each plan defines pricing, limits, and included features/modules.
/// </summary>
public class SubscriptionPlan
{
    /// <summary>
    /// Unique identifier for the subscription plan.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Short, unique code for the plan (e.g., "free", "starter", "professional", "enterprise").
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name in English (e.g., "Professional Plan").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Display name in Thai (e.g., "แพ็กเกจโปรเฟสชันนอล").
    /// </summary>
    public string? NameTh { get; set; }

    /// <summary>
    /// Description of the plan in English.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Description of the plan in Thai.
    /// </summary>
    public string? DescriptionTh { get; set; }

    /// <summary>
    /// The subscription tier this plan belongs to.
    /// </summary>
    public SubscriptionTier Tier { get; set; } = SubscriptionTier.Free;

    /// <summary>
    /// Monthly price in the specified currency.
    /// </summary>
    public decimal MonthlyPrice { get; set; }

    /// <summary>
    /// Yearly price in the specified currency (typically discounted vs 12x monthly).
    /// </summary>
    public decimal YearlyPrice { get; set; }

    /// <summary>
    /// Currency code for pricing (default THB for Thai Baht).
    /// </summary>
    public string Currency { get; set; } = "THB";

    /// <summary>
    /// Maximum number of rooms/units allowed under this plan.
    /// </summary>
    public int MaxRooms { get; set; }

    /// <summary>
    /// Maximum number of user accounts allowed under this plan.
    /// </summary>
    public int MaxUsers { get; set; }

    /// <summary>
    /// Maximum storage in gigabytes allowed under this plan.
    /// </summary>
    public int MaxStorageGB { get; set; }

    /// <summary>
    /// List of feature codes included in this plan (e.g., "reservation", "payment", "pos").
    /// </summary>
    public List<string> IncludedFeatures { get; set; } = [];

    /// <summary>
    /// List of module codes included in this plan (e.g., "Reservation", "Payment", "Inventory").
    /// </summary>
    public List<string> IncludedModules { get; set; } = [];

    /// <summary>
    /// Whether a free trial is available for this plan.
    /// </summary>
    public bool TrialAvailable { get; set; }

    /// <summary>
    /// Number of trial days available (e.g., 14, 30).
    /// </summary>
    public int TrialDays { get; set; }

    /// <summary>
    /// Whether this plan is currently active and available for new subscriptions.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Sort order for display purposes (lower = displayed first).
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// UTC timestamp when the plan was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the plan was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }
}
