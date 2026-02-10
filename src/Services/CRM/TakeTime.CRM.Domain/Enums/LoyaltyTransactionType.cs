namespace TakeTime.CRM.Domain.Enums;

/// <summary>
/// Types of loyalty point transactions.
/// </summary>
public enum LoyaltyTransactionType
{
    /// <summary>
    /// Points earned from a stay or purchase.
    /// </summary>
    Earn = 0,

    /// <summary>
    /// Points redeemed for a reward.
    /// </summary>
    Redeem = 1,

    /// <summary>
    /// Manual adjustment by staff.
    /// </summary>
    Adjust = 2,

    /// <summary>
    /// Points expired due to inactivity.
    /// </summary>
    Expire = 3
}
