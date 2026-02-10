using TakeTime.Reservation.Application.DTOs;

namespace TakeTime.Reservation.Application.Interfaces;

/// <summary>
/// Service interface for calculating accommodation prices using tenant-specific rules,
/// including base rates, seasonal adjustments, dynamic pricing, and VAT.
/// </summary>
public interface IPricingService
{
    /// <summary>
    /// Calculates the nightly rate for an accommodation for the specified date range,
    /// factoring in tenant-specific pricing rules, seasonal adjustments, and any
    /// applicable rate overrides.
    /// </summary>
    /// <param name="accommodationId">The accommodation to price.</param>
    /// <param name="checkInDate">Start of the stay.</param>
    /// <param name="checkOutDate">End of the stay.</param>
    /// <param name="tenantSettings">The tenant's configuration and pricing rules.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Calculated nightly rate.</returns>
    Task<decimal> CalculateAccommodationPriceAsync(
        Guid accommodationId,
        DateTime checkInDate,
        DateTime checkOutDate,
        TenantSettings tenantSettings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies dynamic pricing adjustments to a base price based on the current
    /// occupancy rate and the tenant's dynamic pricing configuration.
    /// </summary>
    /// <param name="basePrice">The base price before dynamic adjustment.</param>
    /// <param name="occupancyRate">Current occupancy rate as a percentage (0-100).</param>
    /// <param name="tenantSettings">The tenant's configuration.</param>
    /// <returns>Dynamically adjusted price.</returns>
    decimal ApplyDynamicPricing(decimal basePrice, decimal occupancyRate, TenantSettings tenantSettings);

    /// <summary>
    /// Applies seasonal price adjustments to a base price for a specific date,
    /// using the tenant's configured seasonal rate multipliers.
    /// </summary>
    /// <param name="basePrice">The base price before seasonal adjustment.</param>
    /// <param name="date">The date to evaluate for seasonal pricing.</param>
    /// <param name="tenantSettings">The tenant's configuration.</param>
    /// <returns>Seasonally adjusted price.</returns>
    decimal ApplySeasonalAdjustment(decimal basePrice, DateTime date, TenantSettings tenantSettings);

    /// <summary>
    /// Calculates the VAT amount for a given amount based on the tenant's VAT
    /// registration status and rate.
    /// </summary>
    /// <param name="amount">The amount on which to calculate VAT.</param>
    /// <param name="tenantSettings">The tenant's configuration.</param>
    /// <returns>VAT amount (0 if tenant is not VAT registered).</returns>
    decimal CalculateVAT(decimal amount, TenantSettings tenantSettings);

    /// <summary>
    /// Calculates the complete total for a reservation including all items,
    /// accommodations, discounts, and optionally VAT.
    /// </summary>
    /// <param name="accommodationTotal">Total of all accommodation charges.</param>
    /// <param name="itemsTotal">Total of all additional item charges.</param>
    /// <param name="discountAmount">Discount to apply.</param>
    /// <param name="tenantSettings">The tenant's configuration.</param>
    /// <returns>A breakdown of the calculated total.</returns>
    PriceCalculationResult CalculateTotal(
        decimal accommodationTotal,
        decimal itemsTotal,
        decimal discountAmount,
        TenantSettings tenantSettings);
}

/// <summary>
/// Result of a full price calculation including subtotal, VAT, and final total.
/// </summary>
public sealed class PriceCalculationResult
{
    public decimal AccommodationSubTotal { get; set; }
    public decimal ItemsSubTotal { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal AfterDiscount { get; set; }
    public decimal VATRate { get; set; }
    public decimal VATAmount { get; set; }
    public bool IsVATInclusive { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Represents tenant-specific configuration and business rules used across
/// the Reservation application layer.
/// </summary>
public class TenantSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string? Currency { get; set; } = "THB";

    // VAT settings
    public bool IsVATRegistered { get; set; }
    public decimal VATRate { get; set; } = 7; // Default Thai VAT rate
    public bool IsVATInclusive { get; set; }
    public string? VATRegistrationNumber { get; set; }

    // Pricing settings
    public bool DynamicPricingEnabled { get; set; }
    public decimal DynamicPricingHighOccupancyThreshold { get; set; } = 80;
    public decimal DynamicPricingHighOccupancyMultiplier { get; set; } = 1.2m;
    public decimal DynamicPricingLowOccupancyThreshold { get; set; } = 30;
    public decimal DynamicPricingLowOccupancyMultiplier { get; set; } = 0.9m;

    // Seasonal pricing
    public List<SeasonalRate> SeasonalRates { get; set; } = [];

    // Cancellation policy
    public int FreeCancellationDays { get; set; } = 7;
    public int LateCancellationDays { get; set; } = 2;
    public decimal LateCancellationFeePercentage { get; set; } = 50;
    public decimal SameDayCancellationFeePercentage { get; set; } = 100;

    // Check-in/out settings
    public TimeSpan StandardCheckInTime { get; set; } = new(14, 0, 0);
    public TimeSpan StandardCheckOutTime { get; set; } = new(12, 0, 0);
    public int AllowEarlyCheckInDays { get; set; } = 0;
    public bool LateCheckOutFeeEnabled { get; set; }
    public decimal GracePeriodHours { get; set; } = 1;
    public decimal HalfDayLateCheckOutHours { get; set; } = 4;
    public decimal LateCheckOutFeeHalfDay { get; set; } = 500;
    public decimal LateCheckOutFeeFullDay { get; set; } = 1000;

    // HR / Payroll settings
    public decimal OvertimeRateMultiplier { get; set; } = 1.5m;
    public decimal HolidayOvertimeRateMultiplier { get; set; } = 3.0m;
    public decimal SocialSecurityRate { get; set; } = 5;
    public decimal MaxSocialSecurityContribution { get; set; } = 750;

    // Business info (for receipts/invoices)
    public string? BusinessName { get; set; }
    public string? BusinessAddress { get; set; }
    public string? BusinessTaxId { get; set; }
    public string? BusinessPhone { get; set; }
    public string? BusinessEmail { get; set; }
}

/// <summary>
/// Represents a seasonal rate period with a multiplier applied to base rates.
/// </summary>
public sealed class SeasonalRate
{
    public string SeasonName { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal RateMultiplier { get; set; } = 1.0m;
}

/// <summary>
/// Interface for accessing current tenant context and settings.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// Gets the current tenant's unique identifier.
    /// </summary>
    string TenantId { get; }

    /// <summary>
    /// Retrieves the full tenant settings for the current tenant.
    /// </summary>
    Task<TenantSettings> GetTenantSettingsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for reservation aggregate operations.
/// </summary>
public interface IReservationRepository
{
    Task<ReservationSummaryDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ReservationDto?> GetReservationDtoByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task CreateAsync(
        CreateReservationData reservationData,
        CancellationToken cancellationToken = default);

    Task<int> GetTodayReservationCountAsync(string tenantId, CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid reservationId, string newStatus, string? updatedBy, string? notes,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAccommodationsAvailableAsync(Guid reservationId, CancellationToken cancellationToken = default);

    Task CancelReservationAsync(
        Guid reservationId, string reason, decimal cancellationFee, decimal refundAmount,
        string? cancelledBy, CancellationToken cancellationToken = default);

    Task CheckInAsync(
        Guid reservationId, DateTime actualCheckInTime, int? actualGuests,
        string? checkedInBy, string? notes, CancellationToken cancellationToken = default);

    Task CheckOutAsync(
        Guid reservationId, DateTime actualCheckOutTime,
        decimal additionalCharges, decimal lateCheckOutFee, decimal vatOnAdditional,
        decimal finalTotal, decimal finalBalance,
        string? checkedOutBy, string? notes,
        List<CheckOutAdditionalItem>? additionalItems,
        CancellationToken cancellationToken = default);

    Task<PaginatedResult<ReservationSummaryDto>> SearchReservationsAsync(
        ReservationSearchCriteria criteria, CancellationToken cancellationToken = default);

    Task<List<ReservationSummaryDto>> GetReservationsByCheckInDateAsync(
        DateTime date, List<string> statuses, CancellationToken cancellationToken = default);

    Task<List<ReservationSummaryDto>> GetReservationsByCheckOutDateAsync(
        DateTime date, List<string> statuses, CancellationToken cancellationToken = default);

    Task<List<BookedAccommodationInfo>> GetBookedAccommodationsAsync(
        DateTime startDate, DateTime endDate, Guid? excludeReservationId,
        CancellationToken cancellationToken = default);

    Task<int> GetCurrentOccupancyCountAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<decimal> GetRevenueForDateAsync(DateTime date, CancellationToken cancellationToken = default);
    Task<decimal> GetRevenueForPeriodAsync(DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task<int> GetCountByStatusAsync(string status, CancellationToken cancellationToken = default);

    Task<int> GetUpcomingConfirmedCountAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    Task<List<ReservationSummaryDto>> GetUpcomingCheckInsAsync(
        DateTime fromDate, DateTime toDate, int take, CancellationToken cancellationToken = default);

    Task<List<ReservationSummaryDto>> GetUpcomingCheckOutsAsync(
        DateTime fromDate, DateTime toDate, int take, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for accommodation operations.
/// </summary>
public interface IAccommodationRepository
{
    Task<AccommodationInfo?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(
        Guid accommodationId, DateTime checkIn, DateTime checkOut,
        CancellationToken cancellationToken = default);

    Task<List<AccommodationInfo>> GetActiveAccommodationsAsync(
        string? type, int? minOccupancy, CancellationToken cancellationToken = default);

    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);

    Task<decimal> GetBaseRateAsync(Guid accommodationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight accommodation info returned by the repository.
/// </summary>
public sealed class AccommodationInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public int MaxOccupancy { get; set; }
    public int DefaultOccupancy { get; set; }
    public decimal BaseRatePerNight { get; set; }
    public string? BedConfiguration { get; set; }
    public List<string> Amenities { get; set; } = [];
}

/// <summary>
/// Information about a booked accommodation for a specific date range.
/// </summary>
public sealed class BookedAccommodationInfo
{
    public Guid AccommodationId { get; set; }
    public Guid ReservationId { get; set; }
    public string? ReservationNumber { get; set; }
    public string? GuestName { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
}

/// <summary>
/// Data model for creating a reservation through the repository.
/// </summary>
public sealed class CreateReservationData
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string ReservationNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public int NumberOfAdults { get; set; }
    public int NumberOfChildren { get; set; }
    public int NumberOfNights { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? SpecialRequests { get; set; }
    public string? InternalNotes { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal VATAmount { get; set; }
    public decimal VATRate { get; set; }
    public bool IsVATInclusive { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }
    public string Currency { get; set; } = "THB";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CreateReservationAccommodationData> Accommodations { get; set; } = [];
    public List<CreateReservationItemData> Items { get; set; } = [];
}

/// <summary>
/// Accommodation data for reservation creation.
/// </summary>
public sealed class CreateReservationAccommodationData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccommodationId { get; set; }
    public string AccommodationName { get; set; } = string.Empty;
    public string AccommodationType { get; set; } = string.Empty;
    public string? RoomNumber { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfNights { get; set; }
    public decimal RatePerNight { get; set; }
    public decimal TotalRate { get; set; }
    public int Occupancy { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Item data for reservation creation.
/// </summary>
public sealed class CreateReservationItemData
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ServiceDate { get; set; }
    public string Currency { get; set; } = "THB";
}

/// <summary>
/// Additional item added at check-out time.
/// </summary>
public sealed class CheckOutAdditionalItem
{
    public string ItemName { get; set; } = string.Empty;
    public string ItemType { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime? ServiceDate { get; set; }
    public string Currency { get; set; } = "THB";
}
