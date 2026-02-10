using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve available accommodations for a given date range.
/// Returns accommodations with availability information and calculated rates
/// based on tenant-specific pricing rules.
/// </summary>
public sealed class GetAvailableAccommodationsQuery : IRequest<List<AccommodationAvailabilityDto>>
{
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int? MinOccupancy { get; set; }
    public string? AccommodationType { get; set; }
    public decimal? MaxRatePerNight { get; set; }

    /// <summary>
    /// Optional reservation ID to exclude from availability check (for modification scenarios).
    /// </summary>
    public Guid? ExcludeReservationId { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAvailableAccommodationsQuery"/>. Checks availability
/// across all accommodations, calculates seasonal and dynamic pricing, and returns
/// a list of available options.
/// </summary>
public sealed class GetAvailableAccommodationsQueryHandler
    : IRequestHandler<GetAvailableAccommodationsQuery, List<AccommodationAvailabilityDto>>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAvailableAccommodationsQueryHandler> _logger;

    public GetAvailableAccommodationsQueryHandler(
        IAccommodationRepository accommodationRepository,
        IReservationRepository reservationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        ILogger<GetAvailableAccommodationsQueryHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _reservationRepository = reservationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<AccommodationAvailabilityDto>> Handle(
        GetAvailableAccommodationsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        if (request.CheckOutDate <= request.CheckInDate)
        {
            throw new InvalidOperationException("Check-out date must be after check-in date.");
        }

        _logger.LogDebug(
            "Searching available accommodations for {CheckIn} to {CheckOut} for tenant {TenantId}",
            request.CheckInDate, request.CheckOutDate, tenantSettings.TenantId);

        // Get all active accommodations matching criteria
        var accommodations = await _accommodationRepository.GetActiveAccommodationsAsync(
            request.AccommodationType, request.MinOccupancy, cancellationToken);

        // Get existing bookings for the date range
        var existingBookings = await _reservationRepository.GetBookedAccommodationsAsync(
            request.CheckInDate, request.CheckOutDate, request.ExcludeReservationId, cancellationToken);

        var bookedAccommodationIds = existingBookings
            .Select(b => b.AccommodationId)
            .ToHashSet();

        // Calculate current occupancy rate for dynamic pricing
        var totalAccommodations = accommodations.Count;
        var occupiedCount = bookedAccommodationIds.Count;
        var occupancyRate = totalAccommodations > 0
            ? (decimal)occupiedCount / totalAccommodations * 100
            : 0;

        var availableAccommodations = new List<AccommodationAvailabilityDto>();

        foreach (var accommodation in accommodations)
        {
            var isAvailable = !bookedAccommodationIds.Contains(accommodation.Id);

            if (!isAvailable)
            {
                continue;
            }

            // Calculate rate using tenant pricing rules
            var calculatedRate = await _pricingService.CalculateAccommodationPriceAsync(
                accommodation.Id, request.CheckInDate, request.CheckOutDate, tenantSettings, cancellationToken);

            // Apply dynamic pricing if enabled
            if (tenantSettings.DynamicPricingEnabled)
            {
                calculatedRate = _pricingService.ApplyDynamicPricing(
                    calculatedRate, occupancyRate, tenantSettings);
            }

            // Filter by max rate if specified
            if (request.MaxRatePerNight.HasValue && calculatedRate > request.MaxRatePerNight.Value)
            {
                continue;
            }

            // Get booked date ranges for this accommodation (for availability calendar)
            var bookedRanges = existingBookings
                .Where(b => b.AccommodationId == accommodation.Id)
                .Select(b => new AvailabilityDateRange
                {
                    StartDate = b.CheckInDate,
                    EndDate = b.CheckOutDate,
                    ReservationNumber = b.ReservationNumber,
                    GuestName = b.GuestName
                })
                .ToList();

            availableAccommodations.Add(new AccommodationAvailabilityDto
            {
                AccommodationId = accommodation.Id,
                Name = accommodation.Name,
                AccommodationType = accommodation.AccommodationType,
                RoomNumber = accommodation.RoomNumber,
                MaxOccupancy = accommodation.MaxOccupancy,
                BaseRatePerNight = accommodation.BaseRatePerNight,
                Currency = tenantSettings.Currency ?? "THB",
                IsAvailable = true,
                CalculatedRate = calculatedRate,
                SeasonalAdjustedRate = _pricingService.ApplySeasonalAdjustment(
                    accommodation.BaseRatePerNight, request.CheckInDate, tenantSettings),
                BedConfiguration = accommodation.BedConfiguration,
                Amenities = accommodation.Amenities,
                BookedDateRanges = bookedRanges
            });
        }

        _logger.LogInformation(
            "Found {Count} available accommodations out of {Total} for {CheckIn} to {CheckOut} for tenant {TenantId}",
            availableAccommodations.Count, totalAccommodations,
            request.CheckInDate, request.CheckOutDate, tenantSettings.TenantId);

        return availableAccommodations.OrderBy(a => a.CalculatedRate).ToList();
    }
}
