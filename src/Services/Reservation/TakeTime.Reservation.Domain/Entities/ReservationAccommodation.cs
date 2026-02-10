using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Represents a specific accommodation unit booked within a reservation.
/// Tracks the room assignment, dates, pricing, and guest count.
/// </summary>
public class ReservationAccommodation : Entity
{
    /// <summary>
    /// Identifier of the parent reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Identifier of the accommodation unit.
    /// </summary>
    public Guid AccommodationId { get; private set; }

    /// <summary>
    /// Display name of the accommodation (e.g., "Deluxe Room").
    /// </summary>
    public string AccommodationName { get; private set; } = string.Empty;

    /// <summary>
    /// Room or unit number assigned.
    /// </summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Check-in date for this particular accommodation.
    /// </summary>
    public DateTime CheckInDate { get; private set; }

    /// <summary>
    /// Check-out date for this particular accommodation.
    /// </summary>
    public DateTime CheckOutDate { get; private set; }

    /// <summary>
    /// Nightly rate for this accommodation.
    /// </summary>
    public Money PricePerNight { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total price for the accommodation stay (PricePerNight x nights).
    /// </summary>
    public Money TotalPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Number of guests occupying this accommodation.
    /// </summary>
    public int GuestCount { get; private set; }

    /// <summary>
    /// Number of extra beds requested for this accommodation.
    /// </summary>
    public int ExtraBedCount { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private ReservationAccommodation() { }

    /// <summary>
    /// Creates a new reservation accommodation entry.
    /// </summary>
    public ReservationAccommodation(
        Guid accommodationId,
        string accommodationName,
        string roomNumber,
        DateTime checkInDate,
        DateTime checkOutDate,
        Money pricePerNight,
        int guestCount,
        int extraBedCount = 0)
    {
        if (checkOutDate <= checkInDate)
            throw new ArgumentException("Check-out date must be after check-in date.");

        if (guestCount <= 0)
            throw new ArgumentException("Guest count must be at least 1.", nameof(guestCount));

        if (extraBedCount < 0)
            throw new ArgumentException("Extra bed count cannot be negative.", nameof(extraBedCount));

        if (string.IsNullOrWhiteSpace(accommodationName))
            throw new ArgumentException("Accommodation name is required.", nameof(accommodationName));

        AccommodationId = accommodationId;
        AccommodationName = accommodationName;
        RoomNumber = roomNumber;
        CheckInDate = checkInDate.Date;
        CheckOutDate = checkOutDate.Date;
        PricePerNight = pricePerNight;
        GuestCount = guestCount;
        ExtraBedCount = extraBedCount;

        CalculateTotalPrice();
    }

    /// <summary>
    /// Sets the parent reservation identifier.
    /// </summary>
    internal void SetReservationId(Guid reservationId)
    {
        ReservationId = reservationId;
    }

    /// <summary>
    /// Updates the nightly rate and recalculates the total.
    /// </summary>
    public void UpdatePrice(Money newPricePerNight)
    {
        PricePerNight = newPricePerNight;
        CalculateTotalPrice();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the guest count and extra bed count.
    /// </summary>
    public void UpdateGuestCount(int guestCount, int extraBedCount = 0)
    {
        if (guestCount <= 0)
            throw new ArgumentException("Guest count must be at least 1.", nameof(guestCount));

        GuestCount = guestCount;
        ExtraBedCount = extraBedCount;
        UpdatedAt = DateTime.UtcNow;
    }

    private void CalculateTotalPrice()
    {
        var nights = (CheckOutDate - CheckInDate).Days;
        TotalPrice = PricePerNight * nights;
    }
}
