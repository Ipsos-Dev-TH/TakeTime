using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve publicly available accommodations with photos and amenities
/// for the customer-facing booking portal. Excludes internal-only accommodation details.
/// </summary>
public sealed class GetPublicAccommodationsQuery : IRequest<List<PublicAccommodationDto>>
{
    public string? AccommodationType { get; set; }
    public int? MinOccupancy { get; set; }
}

/// <summary>
/// Handler for <see cref="GetPublicAccommodationsQuery"/>. Retrieves all active
/// accommodations and maps them to the public-facing DTO, stripping out
/// internal fields like room numbers and floor assignments.
/// </summary>
public sealed class GetPublicAccommodationsQueryHandler
    : IRequestHandler<GetPublicAccommodationsQuery, List<PublicAccommodationDto>>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetPublicAccommodationsQueryHandler> _logger;

    public GetPublicAccommodationsQueryHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<GetPublicAccommodationsQueryHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<PublicAccommodationDto>> Handle(
        GetPublicAccommodationsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Fetching public accommodations for tenant {TenantId}, type filter: {Type}",
            tenantSettings.TenantId, request.AccommodationType);

        var accommodations = await _accommodationRepository.GetActiveAccommodationsAsync(
            request.AccommodationType, request.MinOccupancy, cancellationToken);

        var result = accommodations.Select(a => new PublicAccommodationDto
        {
            AccommodationId = a.Id,
            Name = a.Name,
            AccommodationType = a.AccommodationType,
            MaxOccupancy = a.MaxOccupancy,
            BaseRatePerNight = a.BaseRatePerNight,
            Currency = tenantSettings.Currency ?? "THB",
            BedConfiguration = a.BedConfiguration,
            Amenities = a.Amenities
        }).OrderBy(a => a.AccommodationType).ThenBy(a => a.BaseRatePerNight).ToList();

        _logger.LogInformation(
            "Returning {Count} public accommodations for tenant {TenantId}",
            result.Count, tenantSettings.TenantId);

        return result;
    }
}
