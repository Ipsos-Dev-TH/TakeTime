namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Audit log entry tracking all subscription lifecycle changes.
/// Provides a complete history of plan changes, status transitions,
/// payments, and administrative actions.
/// </summary>
public class SubscriptionAuditLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The tenant this audit entry relates to.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// The subscription this entry relates to (null for tenant-level actions).
    /// </summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>
    /// Action performed (e.g., "TrialStarted", "Subscribed", "Upgraded",
    /// "Downgraded", "Cancelled", "Paused", "Resumed", "Renewed",
    /// "PaymentRecorded", "Refunded", "TrialExpired", "InvoiceGenerated").
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Previous subscription status before the action.
    /// </summary>
    public string? OldStatus { get; set; }

    /// <summary>
    /// New subscription status after the action.
    /// </summary>
    public string? NewStatus { get; set; }

    /// <summary>
    /// Previous plan ID (for upgrades/downgrades).
    /// </summary>
    public Guid? OldPlanId { get; set; }

    /// <summary>
    /// New plan ID (for upgrades/downgrades/new subscriptions).
    /// </summary>
    public Guid? NewPlanId { get; set; }

    /// <summary>
    /// JSON details providing additional context (amounts, codes, reasons).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Who performed the action (user ID, "system", or "auto-renewal").
    /// </summary>
    public string? PerformedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
