namespace TakeTime.Payment.Domain.Enums;

/// <summary>
/// Lifecycle status of a payment transaction.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Payment has been submitted but not yet verified.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Payment has been verified by staff.
    /// </summary>
    Verified = 1,

    /// <summary>
    /// Payment was rejected (e.g., invalid transfer slip).
    /// </summary>
    Rejected = 2,

    /// <summary>
    /// Payment has been fully refunded.
    /// </summary>
    Refunded = 3,

    /// <summary>
    /// Payment has been partially refunded.
    /// </summary>
    PartiallyRefunded = 4,

    /// <summary>
    /// Payment has been voided (same-day cancellation).
    /// </summary>
    Voided = 5
}
