namespace TakeTime.Reservation.Application.DTOs;

/// <summary>
/// Public-facing accommodation DTO for the booking portal.
/// Excludes internal details and shows only guest-relevant information.
/// </summary>
public sealed class PublicAccommodationDto
{
    public Guid AccommodationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public int MaxOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string Currency { get; set; } = "THB";
    public string? Description { get; set; }
    public string? BedConfiguration { get; set; }
    public decimal? AreaSqMeters { get; set; }
    public List<string> Amenities { get; set; } = [];
    public List<string> ImageUrls { get; set; } = [];
    public int SortOrder { get; set; }
}

/// <summary>
/// Public rate information for a specific accommodation and date range.
/// </summary>
public sealed class PublicRateDto
{
    public Guid AccommodationId { get; set; }
    public string AccommodationName { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public decimal BaseRatePerNight { get; set; }
    public decimal CalculatedRatePerNight { get; set; }
    public int NumberOfNights { get; set; }
    public decimal TotalRate { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsAvailable { get; set; }
}

/// <summary>
/// DTO for creating a guest reservation through the booking portal.
/// </summary>
public sealed class CreateGuestReservationDto
{
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; } = 1;
    public int NumberOfAdults { get; set; } = 1;
    public int NumberOfChildren { get; set; }
    public string? SpecialRequests { get; set; }
    public List<Guid> AccommodationIds { get; set; } = [];
}

/// <summary>
/// Lightweight confirmation DTO returned after a guest creates a booking.
/// </summary>
public sealed class GuestReservationConfirmationDto
{
    public Guid ReservationId { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string ConfirmationCode { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int NumberOfGuests { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = "Pending";
    public List<string> AccommodationNames { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Public reservation lookup DTO returned when a guest looks up their booking.
/// </summary>
public sealed class GuestReservationLookupDto
{
    public string ReservationNumber { get; set; } = string.Empty;
    public string ConfirmationCode { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int NumberOfGuests { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "THB";
    public string? SpecialRequests { get; set; }
    public List<GuestReservationAccommodationDto> Accommodations { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Public-facing accommodation details within a guest reservation lookup.
/// </summary>
public sealed class GuestReservationAccommodationDto
{
    public string AccommodationName { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public decimal RatePerNight { get; set; }
    public int NumberOfNights { get; set; }
    public decimal TotalRate { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Request model for cancelling a guest reservation through the portal.
/// </summary>
public sealed class CancelGuestReservationDto
{
    public string? GuestEmail { get; set; }
    public string? Reason { get; set; }
}
