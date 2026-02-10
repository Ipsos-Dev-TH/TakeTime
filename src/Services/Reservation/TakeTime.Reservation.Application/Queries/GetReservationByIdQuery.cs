using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve a single reservation by its unique identifier,
/// including all accommodation assignments and items.
/// </summary>
public sealed class GetReservationByIdQuery : IRequest<ReservationDto?>
{
    public Guid ReservationId { get; set; }

    public GetReservationByIdQuery()
    {
    }

    public GetReservationByIdQuery(Guid reservationId)
    {
        ReservationId = reservationId;
    }
}

/// <summary>
/// Handler for <see cref="GetReservationByIdQuery"/>. Retrieves the full reservation
/// details including accommodations, items, and financial summary scoped to the
/// current tenant.
/// </summary>
public sealed class GetReservationByIdQueryHandler : IRequestHandler<GetReservationByIdQuery, ReservationDto?>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetReservationByIdQueryHandler> _logger;

    public GetReservationByIdQueryHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        ILogger<GetReservationByIdQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<ReservationDto?> Handle(GetReservationByIdQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogDebug(
            "Retrieving reservation {ReservationId} for tenant {TenantId}",
            request.ReservationId, tenantSettings.TenantId);

        var reservation = await _reservationRepository.GetReservationDtoByIdAsync(
            request.ReservationId, cancellationToken);

        if (reservation is null)
        {
            _logger.LogWarning(
                "Reservation {ReservationId} not found for tenant {TenantId}",
                request.ReservationId, tenantSettings.TenantId);
            return null;
        }

        return reservation;
    }
}
