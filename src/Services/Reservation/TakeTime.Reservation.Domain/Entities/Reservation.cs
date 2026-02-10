using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Domain.Enums;
using TakeTime.Reservation.Domain.Events;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Aggregate root representing a guest reservation. Manages the full reservation
/// lifecycle from creation through check-in, check-out, or cancellation.
/// </summary>
public class Reservation : AggregateRoot
{
    private readonly List<ReservationAccommodation> _accommodations = [];
    private readonly List<ReservationItem> _items = [];
    private readonly List<ReservationPayment> _payments = [];

    /// <summary>
    /// Auto-generated unique reservation number for display and reference.
    /// </summary>
    public string ReservationNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Identifier of the customer who made the reservation.
    /// </summary>
    public Guid CustomerId { get; private set; }

    /// <summary>
    /// Full name of the customer for quick reference.
    /// </summary>
    public string CustomerName { get; private set; } = string.Empty;

    /// <summary>
    /// Customer phone number for contact.
    /// </summary>
    public string CustomerPhone { get; private set; } = string.Empty;

    /// <summary>
    /// Customer email address for communication.
    /// </summary>
    public string CustomerEmail { get; private set; } = string.Empty;

    /// <summary>
    /// Scheduled check-in date.
    /// </summary>
    public DateTime CheckInDate { get; private set; }

    /// <summary>
    /// Scheduled check-out date.
    /// </summary>
    public DateTime CheckOutDate { get; private set; }

    /// <summary>
    /// Total number of nights for the stay.
    /// </summary>
    public int NumberOfNights { get; private set; }

    /// <summary>
    /// Current status of the reservation in its lifecycle.
    /// </summary>
    public ReservationStatus Status { get; private set; }

    /// <summary>
    /// Channel through which the reservation was created.
    /// </summary>
    public BookingSource Source { get; private set; }

    /// <summary>
    /// Total amount for the reservation including all accommodations and items.
    /// </summary>
    public Money TotalAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Deposit amount received.
    /// </summary>
    public Money DepositAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Outstanding balance to be paid.
    /// </summary>
    public Money BalanceAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Special requests from the guest (e.g., extra pillows, late check-out).
    /// </summary>
    public string? SpecialRequests { get; private set; }

    /// <summary>
    /// Internal notes visible only to staff.
    /// </summary>
    public string? InternalNotes { get; private set; }

    /// <summary>
    /// Identifier of the affiliate partner, if the reservation was made through one.
    /// </summary>
    public Guid? AffiliateId { get; private set; }

    /// <summary>
    /// Reservation identifier from the originating channel (OTA, channel manager).
    /// </summary>
    public string? ChannelReservationId { get; private set; }

    /// <summary>
    /// Accommodation units booked in this reservation.
    /// </summary>
    public IReadOnlyCollection<ReservationAccommodation> Accommodations => _accommodations.AsReadOnly();

    /// <summary>
    /// Additional items (rental items, services) included in the reservation.
    /// </summary>
    public IReadOnlyCollection<ReservationItem> Items => _items.AsReadOnly();

    /// <summary>
    /// Payment records associated with this reservation.
    /// </summary>
    public IReadOnlyCollection<ReservationPayment> Payments => _payments.AsReadOnly();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Reservation() { }

    /// <summary>
    /// Creates a new reservation.
    /// </summary>
    public Reservation(
        Guid customerId,
        string customerName,
        string customerPhone,
        string customerEmail,
        DateTime checkInDate,
        DateTime checkOutDate,
        BookingSource source,
        string? specialRequests = null,
        Guid? affiliateId = null,
        string? channelReservationId = null)
    {
        if (checkOutDate <= checkInDate)
            throw new ArgumentException("Check-out date must be after check-in date.");

        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));

        CustomerId = customerId;
        CustomerName = customerName;
        CustomerPhone = customerPhone;
        CustomerEmail = customerEmail;
        CheckInDate = checkInDate.Date;
        CheckOutDate = checkOutDate.Date;
        NumberOfNights = (CheckOutDate - CheckInDate).Days;
        Status = ReservationStatus.Pending;
        Source = source;
        SpecialRequests = specialRequests;
        AffiliateId = affiliateId;
        ChannelReservationId = channelReservationId;

        ReservationNumber = GenerateReservationNumber();

        AddDomainEvent(new ReservationCreatedEvent(
            Id,
            ReservationNumber,
            CustomerName,
            CheckInDate,
            CheckOutDate,
            TotalAmount));
    }

    /// <summary>
    /// Confirms the reservation, typically after deposit is received.
    /// </summary>
    public void Confirm()
    {
        if (Status != ReservationStatus.Pending && Status != ReservationStatus.PostponedCheckIn)
            throw new InvalidOperationException(
                $"Cannot confirm reservation in '{Status}' status. Only Pending or PostponedCheckIn reservations can be confirmed.");

        Status = ReservationStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ReservationConfirmedEvent(Id, ReservationNumber, CustomerName, CheckInDate));
    }

    /// <summary>
    /// Cancels the reservation.
    /// </summary>
    public void Cancel(string? reason = null)
    {
        if (Status == ReservationStatus.CheckedIn)
            throw new InvalidOperationException("Cannot cancel a reservation after check-in. Perform check-out instead.");

        if (Status == ReservationStatus.CheckedOut)
            throw new InvalidOperationException("Cannot cancel a reservation that has already been checked out.");

        if (Status == ReservationStatus.Cancelled)
            throw new InvalidOperationException("Reservation is already cancelled.");

        Status = ReservationStatus.Cancelled;
        if (!string.IsNullOrWhiteSpace(reason))
        {
            InternalNotes = string.IsNullOrWhiteSpace(InternalNotes)
                ? $"Cancellation reason: {reason}"
                : $"{InternalNotes}\nCancellation reason: {reason}";
        }

        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new ReservationCancelledEvent(Id, ReservationNumber, CustomerName, reason));
    }

    /// <summary>
    /// Checks the guest in. Reservation must be in Confirmed status.
    /// </summary>
    public void CheckIn()
    {
        if (Status != ReservationStatus.Confirmed)
            throw new InvalidOperationException(
                $"Cannot check in reservation in '{Status}' status. Reservation must be Confirmed.");

        Status = ReservationStatus.CheckedIn;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new GuestCheckedInEvent(Id, ReservationNumber, CustomerName, DateTime.UtcNow));
    }

    /// <summary>
    /// Checks the guest out and finalizes the reservation.
    /// </summary>
    public void CheckOut()
    {
        if (Status != ReservationStatus.CheckedIn)
            throw new InvalidOperationException(
                $"Cannot check out reservation in '{Status}' status. Guest must be checked in first.");

        Status = ReservationStatus.CheckedOut;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new GuestCheckedOutEvent(
            Id, ReservationNumber, CustomerName, DateTime.UtcNow, TotalAmount, BalanceAmount));
    }

    /// <summary>
    /// Postpones the check-in date to a later date.
    /// </summary>
    public void PostponeCheckIn(DateTime newCheckInDate)
    {
        if (Status != ReservationStatus.Confirmed && Status != ReservationStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot postpone check-in for reservation in '{Status}' status.");

        if (newCheckInDate.Date <= CheckInDate)
            throw new ArgumentException("New check-in date must be after the current check-in date.");

        if (newCheckInDate.Date >= CheckOutDate)
            throw new ArgumentException("New check-in date must be before the check-out date.");

        CheckInDate = newCheckInDate.Date;
        NumberOfNights = (CheckOutDate - CheckInDate).Days;
        Status = ReservationStatus.PostponedCheckIn;
        UpdatedAt = DateTime.UtcNow;

        CalculateTotal();
    }

    /// <summary>
    /// Adds an accommodation unit to the reservation.
    /// </summary>
    public void AddAccommodation(ReservationAccommodation accommodation)
    {
        ArgumentNullException.ThrowIfNull(accommodation);

        if (Status == ReservationStatus.CheckedOut || Status == ReservationStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot add accommodation to reservation in '{Status}' status.");

        accommodation.SetReservationId(Id);
        _accommodations.Add(accommodation);

        CalculateTotal();
    }

    /// <summary>
    /// Removes an accommodation unit from the reservation.
    /// </summary>
    public void RemoveAccommodation(Guid accommodationId)
    {
        if (Status == ReservationStatus.CheckedOut || Status == ReservationStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot remove accommodation from reservation in '{Status}' status.");

        var accommodation = _accommodations.FirstOrDefault(a => a.AccommodationId == accommodationId);
        if (accommodation is null)
            throw new InvalidOperationException($"Accommodation '{accommodationId}' not found in this reservation.");

        _accommodations.Remove(accommodation);

        CalculateTotal();
    }

    /// <summary>
    /// Adds an additional item (rental, service) to the reservation.
    /// </summary>
    public void AddItem(ReservationItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (Status == ReservationStatus.CheckedOut || Status == ReservationStatus.Cancelled)
            throw new InvalidOperationException(
                $"Cannot add items to reservation in '{Status}' status.");

        item.SetReservationId(Id);
        _items.Add(item);

        CalculateTotal();
    }

    /// <summary>
    /// Removes an item from the reservation.
    /// </summary>
    public void RemoveItem(Guid itemId)
    {
        var item = _items.FirstOrDefault(i => i.ItemId == itemId);
        if (item is null)
            throw new InvalidOperationException($"Item '{itemId}' not found in this reservation.");

        _items.Remove(item);

        CalculateTotal();
    }

    /// <summary>
    /// Records a payment against this reservation.
    /// </summary>
    public void AddPayment(ReservationPayment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        payment.SetReservationId(Id);
        _payments.Add(payment);

        RecalculateBalance();
    }

    /// <summary>
    /// Sets special requests from the guest.
    /// </summary>
    public void SetSpecialRequests(string? requests)
    {
        SpecialRequests = requests;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds or replaces internal notes.
    /// </summary>
    public void SetInternalNotes(string? notes)
    {
        InternalNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Recalculates the total amount based on all accommodations and items.
    /// </summary>
    public void CalculateTotal()
    {
        var accommodationTotal = _accommodations.Aggregate(
            Money.ZeroBaht(),
            (sum, a) => sum + a.TotalPrice);

        var itemTotal = _items.Aggregate(
            Money.ZeroBaht(),
            (sum, i) => sum + i.TotalPrice);

        TotalAmount = accommodationTotal + itemTotal;

        RecalculateBalance();
        UpdatedAt = DateTime.UtcNow;
    }

    private void RecalculateBalance()
    {
        var totalPaid = _payments
            .Where(p => p.Status == "Verified")
            .Aggregate(Money.ZeroBaht(), (sum, p) => sum + p.Amount);

        DepositAmount = totalPaid;
        BalanceAmount = TotalAmount - totalPaid;
    }

    private static string GenerateReservationNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"RES-{timestamp}-{random}";
    }
}
