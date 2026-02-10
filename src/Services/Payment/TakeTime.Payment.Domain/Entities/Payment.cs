using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Domain.Enums;
using TakeTime.Payment.Domain.Events;
using TakeTime.Payment.Domain.ValueObjects;

namespace TakeTime.Payment.Domain.Entities;

/// <summary>
/// Aggregate root representing a payment transaction.
/// Supports bank transfer verification, refunds, and partial refunds.
/// </summary>
public class Payment : AggregateRoot
{
    /// <summary>
    /// Auto-generated unique payment number for display and reference.
    /// </summary>
    public string PaymentNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the associated reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Identifier of the customer who made the payment.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Payment amount.
    /// </summary>
    public Money Amount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Method of payment used.
    /// </summary>
    public PaymentMethod PaymentMethod { get; private set; }

    /// <summary>
    /// Current status of the payment.
    /// </summary>
    public PaymentStatus Status { get; private set; }

    /// <summary>
    /// Bank transfer details, populated when payment method is BankTransfer.
    /// </summary>
    public BankTransferDetails? BankTransferDetails { get; private set; }

    /// <summary>
    /// URL of the uploaded bank transfer slip image.
    /// </summary>
    public string? SlipImageUrl { get; private set; }

    /// <summary>
    /// Identifier of the staff member who verified the payment.
    /// </summary>
    public string? VerifiedBy { get; private set; }

    /// <summary>
    /// Timestamp when the payment was verified.
    /// </summary>
    public DateTime? VerifiedAt { get; private set; }

    /// <summary>
    /// Additional notes about the payment.
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Amount that has been refunded from this payment.
    /// </summary>
    public Money RefundedAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Payment() { }

    /// <summary>
    /// Creates a new payment.
    /// </summary>
    public Payment(
        Guid reservationId,
        Guid customerId,
        Money amount,
        PaymentMethod paymentMethod,
        BankTransferDetails? bankTransferDetails = null,
        string? slipImageUrl = null,
        string? notes = null)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        ReservationId = reservationId;
        CustomerId = customerId;
        Amount = amount;
        PaymentMethod = paymentMethod;
        Status = PaymentStatus.Pending;
        BankTransferDetails = bankTransferDetails;
        SlipImageUrl = slipImageUrl;
        Notes = notes;

        PaymentNumber = GeneratePaymentNumber();

        AddDomainEvent(new PaymentReceivedEvent(
            Id, PaymentNumber, ReservationId, CustomerId, Amount, PaymentMethod));
    }

    /// <summary>
    /// Verifies the payment (e.g., after checking bank transfer slip).
    /// </summary>
    public void Verify(string verifiedBy)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot verify payment in '{Status}' status. Only Pending payments can be verified.");

        if (string.IsNullOrWhiteSpace(verifiedBy))
            throw new ArgumentException("Verified by user is required.", nameof(verifiedBy));

        Status = PaymentStatus.Verified;
        VerifiedBy = verifiedBy;
        VerifiedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new PaymentVerifiedEvent(
            Id, PaymentNumber, ReservationId, Amount, verifiedBy));
    }

    /// <summary>
    /// Rejects the payment (e.g., invalid transfer slip).
    /// </summary>
    public void Reject(string rejectedBy, string? reason = null)
    {
        if (Status != PaymentStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject payment in '{Status}' status. Only Pending payments can be rejected.");

        if (string.IsNullOrWhiteSpace(rejectedBy))
            throw new ArgumentException("Rejected by user is required.", nameof(rejectedBy));

        Status = PaymentStatus.Rejected;
        VerifiedBy = rejectedBy;
        VerifiedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"Rejection reason: {reason}"
                : $"{Notes}\nRejection reason: {reason}";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Fully refunds the payment.
    /// </summary>
    public void Refund(string refundedBy, string? reason = null)
    {
        if (Status != PaymentStatus.Verified)
            throw new InvalidOperationException(
                $"Cannot refund payment in '{Status}' status. Only Verified payments can be refunded.");

        Status = PaymentStatus.Refunded;
        RefundedAmount = Amount;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"Refund reason: {reason}. Refunded by: {refundedBy}"
                : $"{Notes}\nRefund reason: {reason}. Refunded by: {refundedBy}";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Partially refunds a specific amount from the payment.
    /// </summary>
    public void PartialRefund(Money refundAmount, string refundedBy, string? reason = null)
    {
        if (Status != PaymentStatus.Verified && Status != PaymentStatus.PartiallyRefunded)
            throw new InvalidOperationException(
                $"Cannot partially refund payment in '{Status}' status.");

        if (refundAmount.Amount <= 0)
            throw new ArgumentException("Refund amount must be positive.", nameof(refundAmount));

        var remainingAmount = Amount - RefundedAmount;
        if (refundAmount > remainingAmount)
            throw new InvalidOperationException(
                $"Refund amount ({refundAmount}) exceeds remaining amount ({remainingAmount}).");

        RefundedAmount = RefundedAmount + refundAmount;

        if (RefundedAmount == Amount)
        {
            Status = PaymentStatus.Refunded;
        }
        else
        {
            Status = PaymentStatus.PartiallyRefunded;
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            Notes = string.IsNullOrWhiteSpace(Notes)
                ? $"Partial refund: {refundAmount}. Reason: {reason}. By: {refundedBy}"
                : $"{Notes}\nPartial refund: {refundAmount}. Reason: {reason}. By: {refundedBy}";
        }

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the bank transfer slip image URL.
    /// </summary>
    public void SetSlipImage(string imageUrl)
    {
        SlipImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string GeneratePaymentNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"PAY-{timestamp}-{random}";
    }
}
