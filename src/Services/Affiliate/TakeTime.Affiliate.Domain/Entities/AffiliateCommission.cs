using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Affiliate.Domain.Enums;

namespace TakeTime.Affiliate.Domain.Entities;

/// <summary>
/// Tracks a commission earned by an affiliate partner for a specific reservation.
/// </summary>
public class AffiliateCommission : Entity
{
    /// <summary>
    /// Identifier of the affiliate partner.
    /// </summary>
    public Guid AffiliateId { get; private set; }

    /// <summary>
    /// Identifier of the referred reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Total reservation amount used as the commission base.
    /// </summary>
    public Money ReservationAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Commission rate applied (as a percentage).
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>
    /// Calculated commission amount.
    /// </summary>
    public Money CommissionAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Current status of the commission.
    /// </summary>
    public CommissionStatus Status { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private AffiliateCommission() { }

    /// <summary>
    /// Creates a new affiliate commission.
    /// </summary>
    public AffiliateCommission(
        Guid affiliateId,
        Guid reservationId,
        Money reservationAmount,
        decimal commissionRate)
    {
        if (commissionRate <= 0 || commissionRate > 100)
            throw new ArgumentException("Commission rate must be between 0 and 100.", nameof(commissionRate));

        AffiliateId = affiliateId;
        ReservationId = reservationId;
        ReservationAmount = reservationAmount;
        CommissionRate = commissionRate;
        CommissionAmount = reservationAmount.Percentage(commissionRate).Round();
        Status = CommissionStatus.Pending;
    }

    /// <summary>
    /// Approves the commission for payment.
    /// </summary>
    public void Approve()
    {
        if (Status != CommissionStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve commission in '{Status}' status.");

        Status = CommissionStatus.Approved;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the commission as paid.
    /// </summary>
    public void MarkAsPaid()
    {
        if (Status != CommissionStatus.Approved)
            throw new InvalidOperationException(
                $"Cannot mark commission as paid in '{Status}' status. Commission must be Approved first.");

        Status = CommissionStatus.Paid;
        UpdatedAt = DateTime.UtcNow;
    }
}
