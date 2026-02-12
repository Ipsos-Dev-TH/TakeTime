using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to get public rates for given dates and optional room type filter.
/// Returns calculated rates based on tenant pricing rules including seasonal
/// and dynamic pricing adjustments.
/// </summary>
public sealed class GetPublicRatesQuery : IRequest<List<PublicRateDto>>
{
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public string? AccommodationType { get; set; }
    public int? MinOccupancy { get; set; }
}

/// <summary>
/// Handler for <see cref="GetPublicRatesQuery"/>. Calculates public-facing rates
/// for all available accommodations matching the criteria, applying tenant-specific
/// pricing rules and availability checks.
/// </summary>
public sealed class GetPublicRatesQueryHandler
    : IRequestHandler<GetPublicRatesQuery, List<PublicRateDto>>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly IReservationRepository _reservationRepository;
    private readonly IPricingService _pricingService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPublicRatesQueryHandler> _logger;

    public GetPublicRatesQueryHandler(
        IAccommodationRepository accommodationRepository,
        IReservationRepository reservationRepository,
        IPricingService pricingService,
        ITenantContext tenantContext,
        ILogger<GetPublicRatesQueryHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _reservationRepository = reservationRepository;
        _pricingService = pricingService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<PublicRateDto>> Handle(
        GetPublicRatesQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        if (request.CheckOutDate <= request.CheckInDate)
        {
            throw new InvalidOperationException("Check-out date must be after check-in date.");
        }

        var numberOfNights = (request.CheckOutDate.Date - request.CheckInDate.Date).Days;

        _logger.LogDebug(
            "Fetching public rates for tenant {TenantId}, {CheckIn} to {CheckOut} ({Nights} nights)",
            tenantSettings.TenantId, request.CheckInDate, request.CheckOutDate, numberOfNights);

        // Get all active accommodations matching criteria
        var accommodations = await _accommodationRepository.GetActiveAccommodationsAsync(
            request.AccommodationType, request.MinOccupancy, cancellationToken);

        // Check which accommodations are booked
        var existingBookings = await _reservationRepository.GetBookedAccommodationsAsync(
            request.CheckInDate, request.CheckOutDate, null, cancellationToken);

        var bookedIds = existingBookings.Select(b => b.AccommodationId).ToHashSet();

        var rates = new List<PublicRateDto>();

        foreach (var accommodation in accommodations)
        {
            var isAvailable = !bookedIds.Contains(accommodation.Id);

            // Calculate rate using tenant pricing rules
            var calculatedRate = await _pricingService.CalculateAccommodationPriceAsync(
                accommodation.Id, request.CheckInDate, request.CheckOutDate,
                tenantSettings, cancellationToken);

            rates.Add(new PublicRateDto
            {
                AccommodationId = accommodation.Id,
                AccommodationName = accommodation.Name,
                AccommodationType = accommodation.AccommodationType,
                BaseRatePerNight = accommodation.BaseRatePerNight,
                CalculatedRatePerNight = calculatedRate,
                NumberOfNights = numberOfNights,
                TotalRate = calculatedRate * numberOfNights,
                Currency = tenantSettings.Currency ?? "THB",
                IsAvailable = isAvailable
            });
        }

        _logger.LogInformation(
            "Returning {Count} public rates for tenant {TenantId}, {Available} available",
            rates.Count, tenantSettings.TenantId, rates.Count(r => r.IsAvailable));

        return rates.OrderBy(r => r.CalculatedRatePerNight).ToList();
    }
}
