namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Status of a tenant's subscription in its lifecycle.
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Tenant is on a free trial period.</summary>
    Trial = 0,

    /// <summary>Subscription is active and in good standing.</summary>
    Active = 1,

    /// <summary>Payment is overdue but subscription is still accessible.</summary>
    PastDue = 2,

    /// <summary>Subscription has been temporarily paused by the tenant.</summary>
    Paused = 3,

    /// <summary>Subscription has been cancelled.</summary>
    Cancelled = 4,

    /// <summary>Subscription has expired (trial or billing period ended).</summary>
    Expired = 5
}

/// <summary>
/// Billing cycle for recurring subscriptions.
/// </summary>
public enum BillingCycle
{
    /// <summary>Billed every month.</summary>
    Monthly = 0,

    /// <summary>Billed once per year (typically with a discount).</summary>
    Yearly = 1
}

/// <summary>
/// Tracks the subscription lifecycle for a tenant, including plan selection,
/// billing cycle, trial usage, pricing, and renewal configuration.
/// </summary>
public class TenantSubscription
{
    /// <summary>
    /// Unique identifier for this subscription record.
    /// </summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The tenant this subscription belongs to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The subscription plan the tenant is subscribed to.
    /// </summary>
    public Guid PlanId { get; set; }

    /// <summary>
    /// Current status of the subscription.
    /// </summary>
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;

    /// <summary>
    /// Whether this is a monthly or yearly billing cycle.
    /// </summary>
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;

    /// <summary>
    /// Start date of the subscription (after trial, or immediately if no trial).
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the current billing period.
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Start date of the trial period (null if no trial was used).
    /// </summary>
    public DateTime? TrialStartDate { get; set; }

    /// <summary>
    /// End date of the trial period.
    /// </summary>
    public DateTime? TrialEndDate { get; set; }

    /// <summary>
    /// Whether the tenant has already used a trial for this plan tier.
    /// </summary>
    public bool IsTrialUsed { get; set; }

    /// <summary>
    /// Timestamp when the subscription was cancelled (null if not cancelled).
    /// </summary>
    public DateTime? CancelledAt { get; set; }

    /// <summary>
    /// Reason provided for cancellation.
    /// </summary>
    public string? CancellationReason { get; set; }

    /// <summary>
    /// The price locked in at the time of subscription (snapshot of plan price).
    /// </summary>
    public decimal PriceAtSubscription { get; set; }

    /// <summary>
    /// Discount percentage applied (e.g., 10 for 10% off).
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// Discount/promo code applied, if any.
    /// </summary>
    public string? DiscountCode { get; set; }

    /// <summary>
    /// Final price after discount.
    /// </summary>
    public decimal FinalPrice { get; set; }

    /// <summary>
    /// Currency code (default THB).
    /// </summary>
    public string Currency { get; set; } = "THB";

    /// <summary>
    /// Whether the subscription should auto-renew at the end of the billing period.
    /// </summary>
    public bool AutoRenew { get; set; } = true;

    /// <summary>
    /// Date of the next scheduled billing.
    /// </summary>
    public DateTime? NextBillingDate { get; set; }

    /// <summary>
    /// UTC timestamp when the subscription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp when the subscription was last updated.
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    // ─── Navigation Properties ────────────────────────────────────────

    /// <summary>
    /// Navigation property to the subscription plan.
    /// </summary>
    public SubscriptionPlan? Plan { get; set; }
}
