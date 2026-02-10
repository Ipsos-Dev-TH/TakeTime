namespace TakeTime.Reservation.Application.DTOs;

/// <summary>
/// Full accommodation data transfer object.
/// </summary>
public sealed class AccommodationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public string? Description { get; set; }
    public int MaxOccupancy { get; set; }
    public int DefaultOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Amenities { get; set; } = [];
    public List<string> ImageUrls { get; set; } = [];
    public decimal? AreaSqMeters { get; set; }
    public string? BedConfiguration { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Accommodation availability DTO with date-specific availability data.
/// </summary>
public sealed class AccommodationAvailabilityDto
{
    public Guid AccommodationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public int MaxOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsAvailable { get; set; }
    public List<AvailabilityDateRange> AvailableDateRanges { get; set; } = [];
    public List<AvailabilityDateRange> BookedDateRanges { get; set; } = [];
    public decimal CalculatedRate { get; set; }
    public decimal? SeasonalAdjustedRate { get; set; }
    public string? BedConfiguration { get; set; }
    public List<string> Amenities { get; set; } = [];
}

/// <summary>
/// Represents a contiguous date range for availability tracking.
/// </summary>
public sealed class AvailabilityDateRange
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? ReservationNumber { get; set; }
    public string? GuestName { get; set; }
}

/// <summary>
/// DTO for creating a new accommodation.
/// </summary>
public sealed class CreateAccommodationDto
{
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public string? Description { get; set; }
    public int MaxOccupancy { get; set; }
    public int DefaultOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string Currency { get; set; } = "THB";
    public List<string> Amenities { get; set; } = [];
    public List<string> ImageUrls { get; set; } = [];
    public decimal? AreaSqMeters { get; set; }
    public string? BedConfiguration { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// DTO for updating an existing accommodation.
/// </summary>
public sealed class UpdateAccommodationDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? AccommodationType { get; set; }
    public string? RoomNumber { get; set; }
    public string? Floor { get; set; }
    public string? Building { get; set; }
    public string? Description { get; set; }
    public int? MaxOccupancy { get; set; }
    public int? DefaultOccupancy { get; set; }
    public decimal? BaseRatePerNight { get; set; }
    public string? Currency { get; set; }
    public bool? IsActive { get; set; }
    public List<string>? Amenities { get; set; }
    public List<string>? ImageUrls { get; set; }
    public decimal? AreaSqMeters { get; set; }
    public string? BedConfiguration { get; set; }
    public int? SortOrder { get; set; }
}
