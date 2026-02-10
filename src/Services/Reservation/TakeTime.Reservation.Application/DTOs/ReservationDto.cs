namespace TakeTime.Reservation.Application.DTOs;

/// <summary>
/// Full reservation data transfer object containing all reservation details.
/// </summary>
public sealed class ReservationDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;

    // Customer information
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }

    // Stay details
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }
    public int NumberOfNights { get; set; }

    // Status and source
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }

    // Financial
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "THB";
    public bool IsVATInclusive { get; set; }
    public decimal VATRate { get; set; }

    // Timestamps
    public DateTime? ActualCheckInTime { get; set; }
    public DateTime? ActualCheckOutTime { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    // Related entities
    public List<ReservationAccommodationDto> Accommodations { get; set; } = [];
    public List<ReservationItemDto> Items { get; set; } = [];
}

/// <summary>
/// Lightweight reservation DTO for list views and summaries.
/// </summary>
public sealed class ReservationSummaryDto
{
    public Guid Id { get; set; }
    public string ReservationNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public int NumberOfGuests { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "THB";
    public string? AccommodationNames { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for an accommodation assignment within a reservation.
/// </summary>
public sealed class ReservationAccommodationDto
{
    public Guid Id { get; set; }
    public Guid AccommodationId { get; set; }
    public string AccommodationName { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public decimal RatePerNight { get; set; }
    public decimal TotalRate { get; set; }
    public string Currency { get; set; } = "THB";
    public int Occupancy { get; set; }
}

/// <summary>
/// DTO for additional items or services on a reservation.
/// </summary>
public sealed class ReservationItemDto
{
    public Guid Id { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "THB";
    public DateTime? ServiceDate { get; set; }
}

/// <summary>
/// DTO for creating a new reservation.
/// </summary>
public sealed class CreateReservationDto
{
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }
    public string Source { get; set; } = "Direct";
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public List<CreateReservationAccommodationDto> Accommodations { get; set; } = [];
    public List<CreateReservationItemDto> Items { get; set; } = [];
}

/// <summary>
/// DTO for creating an accommodation assignment within a reservation.
/// </summary>
public sealed class CreateReservationAccommodationDto
{
    public Guid AccommodationId { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public decimal? RateOverride { get; set; }
    public int Occupancy { get; set; }
}

/// <summary>
/// DTO for creating an item within a reservation.
/// </summary>
public sealed class CreateReservationItemDto
{
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ItemType { get; set; } = "Service";
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public DateTime? ServiceDate { get; set; }
}

/// <summary>
/// DTO for updating an existing reservation.
/// </summary>
public sealed class UpdateReservationDto
{
    public Guid Id { get; set; }
    public DateTime? CheckInDate { get; set; }
    public DateTime? CheckOutDate { get; set; }
    public int? NumberOfGuests { get; set; }
    public int? NumberOfAdults { get; set; }
    public int? NumberOfChildren { get; set; }
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public List<CreateReservationAccommodationDto>? Accommodations { get; set; }
    public List<CreateReservationItemDto>? Items { get; set; }
}

/// <summary>
/// Search and filter criteria for querying reservations.
/// </summary>
public sealed class ReservationSearchCriteria
{
    public DateTime? CheckInDateFrom { get; set; }
    public DateTime? CheckInDateTo { get; set; }
    public DateTime? CheckOutDateFrom { get; set; }
    public DateTime? CheckOutDateTo { get; set; }
    public DateTime? CreatedDateFrom { get; set; }
    public DateTime? CreatedDateTo { get; set; }
    public string? Status { get; set; }
    public List<string>? Statuses { get; set; }
    public string? Source { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? ReservationNumber { get; set; }
    public Guid? AccommodationId { get; set; }
    public string? AccommodationType { get; set; }
    public decimal? MinTotalAmount { get; set; }
    public decimal? MaxTotalAmount { get; set; }
    public bool? HasBalance { get; set; }

    // Pagination
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "CheckInDate";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Paginated result wrapper for reservation queries.
/// </summary>
public sealed class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>
/// Dashboard summary DTO containing key operational metrics.
/// </summary>
public sealed class DashboardSummaryDto
{
    public DateTime Date { get; set; }
    public int TodayCheckIns { get; set; }
    public int TodayCheckOuts { get; set; }
    public int CurrentOccupancy { get; set; }
    public int TotalAccommodations { get; set; }
    public decimal OccupancyRate { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal MonthlyRevenue { get; set; }
    public int PendingReservations { get; set; }
    public int ConfirmedUpcoming { get; set; }
    public string Currency { get; set; } = "THB";
    public List<ReservationSummaryDto> UpcomingCheckIns { get; set; } = [];
    public List<ReservationSummaryDto> UpcomingCheckOuts { get; set; } = [];
}
