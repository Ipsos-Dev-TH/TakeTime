using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve a single accommodation by its unique identifier.
/// Returns the full accommodation details including amenities, images, and pricing.
/// </summary>
public sealed class GetAccommodationByIdQuery : IRequest<AccommodationDto?>
{
    public Guid AccommodationId { get; set; }

    public GetAccommodationByIdQuery()
    {
    }

    public GetAccommodationByIdQuery(Guid accommodationId)
    {
        AccommodationId = accommodationId;
    }
}

/// <summary>
/// Handler for <see cref="GetAccommodationByIdQuery"/>. Retrieves accommodation details
/// by ID scoped to the current tenant.
/// </summary>
public sealed class GetAccommodationByIdQueryHandler
    : IRequestHandler<GetAccommodationByIdQuery, AccommodationDto?>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetAccommodationByIdQueryHandler> _logger;

    public GetAccommodationByIdQueryHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<GetAccommodationByIdQueryHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AccommodationDto?> Handle(
        GetAccommodationByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving accommodation {AccommodationId} for tenant {TenantId}",
            request.AccommodationId, tenantSettings.TenantId);

        var accommodation = await _accommodationRepository.GetAccommodationDtoByIdAsync(
            request.AccommodationId, cancellationToken);

        if (accommodation is null)
        {
            _logger.LogWarning(
                "Accommodation {AccommodationId} not found for tenant {TenantId}",
                request.AccommodationId, tenantSettings.TenantId);
            return null;
        }

        return accommodation;
    }
}
