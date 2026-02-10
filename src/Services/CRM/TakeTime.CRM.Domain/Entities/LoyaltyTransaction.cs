using TakeTime.Core.Domain.Base;
using TakeTime.CRM.Domain.Enums;

namespace TakeTime.CRM.Domain.Entities;

/// <summary>
/// Tracks individual loyalty point transactions for a customer.
/// </summary>
public class LoyaltyTransaction : Entity
{
    /// <summary>
    /// Identifier of the customer.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Type of loyalty transaction.
    /// </summary>
    public LoyaltyTransactionType TransactionType { get; private set; }

    /// <summary>
    /// Number of points involved in the transaction.
    /// Positive for Earn, negative for Redeem/Expire.
    /// </summary>
    public int Points { get; private set; }

    /// <summary>
    /// Description of the transaction.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Related reservation identifier, if applicable.
    /// </summary>
    public Guid? ReservationId { get; private set; }

    /// <summary>
    /// Expiry date for earned points.
    /// </summary>
    public DateTime? ExpiryDate { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private LoyaltyTransaction() { }

    /// <summary>
    /// Creates a new loyalty transaction.
    /// </summary>
    public LoyaltyTransaction(
        Guid customerId,
        LoyaltyTransactionType transactionType,
        int points,
        string description,
        Guid? reservationId = null,
        DateTime? expiryDate = null)
    {
        if (points == 0)
            throw new ArgumentException("Points cannot be zero.", nameof(points));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        CustomerId = customerId;
        TransactionType = transactionType;
        Points = transactionType switch
        {
            LoyaltyTransactionType.Redeem => -Math.Abs(points),
            LoyaltyTransactionType.Expire => -Math.Abs(points),
            _ => Math.Abs(points)
        };
        Description = description;
        ReservationId = reservationId;
        ExpiryDate = expiryDate;
    }

    /// <summary>
    /// Creates an earn transaction.
    /// </summary>
    public static LoyaltyTransaction Earn(
        Guid customerId, int points, string description,
        Guid? reservationId = null, DateTime? expiryDate = null)
    {
        return new LoyaltyTransaction(
            customerId, LoyaltyTransactionType.Earn, points, description, reservationId, expiryDate);
    }

    /// <summary>
    /// Creates a redeem transaction.
    /// </summary>
    public static LoyaltyTransaction Redeem(
        Guid customerId, int points, string description, Guid? reservationId = null)
    {
        return new LoyaltyTransaction(
            customerId, LoyaltyTransactionType.Redeem, points, description, reservationId);
    }
}
