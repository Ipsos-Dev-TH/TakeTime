using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve all accommodations with optional filtering by type, status,
/// and minimum occupancy. Returns full accommodation DTOs scoped to the current tenant.
/// </summary>
public sealed class GetAllAccommodationsQuery : IRequest<List<AccommodationDto>>
{
    public string? Type { get; set; }
    public string? Status { get; set; }
    public int? MinOccupancy { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAllAccommodationsQuery"/>. Retrieves all accommodations
/// for the current tenant with optional filtering applied.
/// </summary>
public sealed class GetAllAccommodationsQueryHandler
    : IRequestHandler<GetAllAccommodationsQuery, List<AccommodationDto>>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAllAccommodationsQueryHandler> _logger;

    public GetAllAccommodationsQueryHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<GetAllAccommodationsQueryHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<AccommodationDto>> Handle(
        GetAllAccommodationsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Getting all accommodations for tenant {TenantId}. Type: {Type}, Status: {Status}, MinOccupancy: {MinOccupancy}",
            tenantSettings.TenantId, request.Type, request.Status, request.MinOccupancy);

        var accommodations = await _accommodationRepository.GetAllAccommodationDtosAsync(
            request.Type, request.Status, request.MinOccupancy, cancellationToken);

        _logger.LogDebug(
            "Found {Count} accommodations for tenant {TenantId}",
            accommodations.Count, tenantSettings.TenantId);

        return accommodations;
    }
}
