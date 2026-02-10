using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Tracks a payment record associated with a reservation.
/// References the actual Payment entity in the Payment microservice.
/// </summary>
public class ReservationPayment : Entity
{
    /// <summary>
    /// Identifier of the parent reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Reference to the payment entity in the Payment microservice.
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Amount of this payment.
    /// </summary>
    public Money Amount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Date and time the payment was made.
    /// </summary>
    public DateTime PaymentDate { get; private set; }

    /// <summary>
    /// Method used for this payment (e.g., BankTransfer, Cash).
    /// </summary>
    public string PaymentMethod { get; private set; } = string.Empty;

    /// <summary>
    /// Current status of this payment (e.g., Pending, Verified).
    /// </summary>
    public string Status { get; private set; } = string.Empty;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private ReservationPayment() { }

    /// <summary>
    /// Creates a new reservation payment record.
    /// </summary>
    public ReservationPayment(
        Guid paymentId,
        Money amount,
        DateTime paymentDate,
        string paymentMethod,
        string status)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new ArgumentException("Payment method is required.", nameof(paymentMethod));

        PaymentId = paymentId;
        Amount = amount;
        PaymentDate = paymentDate;
        PaymentMethod = paymentMethod;
        Status = status;
    }

    /// <summary>
    /// Sets the parent reservation identifier.
    /// </summary>
    internal void SetReservationId(Guid reservationId)
    {
        ReservationId = reservationId;
    }

    /// <summary>
    /// Updates the payment status.
    /// </summary>
    public void UpdateStatus(string newStatus)
    {
        if (string.IsNullOrWhiteSpace(newStatus))
            throw new ArgumentException("Status is required.", nameof(newStatus));

        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}
