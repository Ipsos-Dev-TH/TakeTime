namespace TakeTime.Affiliate.Domain.Enums;

/// <summary>
/// Status of an affiliate commission in its payment lifecycle.
/// </summary>
public enum CommissionStatus
{
    /// <summary>
    /// Commission is calculated but not yet approved.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Commission has been approved for payment.
    /// </summary>
    Approved = 1,

    /// <summary>
    /// Commission has been paid to the affiliate.
    /// </summary>
    Paid = 2
}
