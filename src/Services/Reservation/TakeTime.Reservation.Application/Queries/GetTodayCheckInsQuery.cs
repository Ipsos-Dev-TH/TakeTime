using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve all reservations scheduled to check in today.
/// Supports optional filtering by status (e.g., only confirmed).
/// </summary>
public sealed class GetTodayCheckInsQuery : IRequest<List<ReservationSummaryDto>>
{
    /// <summary>
    /// Optional date override. Defaults to today (UTC).
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// If true, include only confirmed reservations. If false, include all statuses
    /// that are eligible for check-in (Confirmed, Pending).
    /// </summary>
    public bool ConfirmedOnly { get; set; }

    /// <summary>
    /// If true, also include reservations that have already been checked in today.
    /// </summary>
    public bool IncludeAlreadyCheckedIn { get; set; }
}

/// <summary>
/// Handler for <see cref="GetTodayCheckInsQuery"/>. Retrieves reservations
/// with a check-in date matching today (or the specified date) for the current tenant.
/// </summary>
public sealed class GetTodayCheckInsQueryHandler : IRequestHandler<GetTodayCheckInsQuery, List<ReservationSummaryDto>>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetTodayCheckInsQueryHandler> _logger;

    public GetTodayCheckInsQueryHandler(
        IReservationRepository reservationRepository,
        ITenantContext tenantContext,
        ILogger<GetTodayCheckInsQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<List<ReservationSummaryDto>> Handle(
        GetTodayCheckInsQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var targetDate = request.Date?.Date ?? DateTime.UtcNow.Date;

        var statuses = new List<string>();

        if (request.ConfirmedOnly)
        {
            statuses.Add("Confirmed");
        }
        else
        {
            statuses.AddRange(["Confirmed", "Pending"]);
        }

        if (request.IncludeAlreadyCheckedIn)
        {
            statuses.Add("CheckedIn");
        }

        _logger.LogDebug(
            "Retrieving check-ins for {Date} with statuses [{Statuses}] for tenant {TenantId}",
            targetDate, string.Join(", ", statuses), tenantSettings.TenantId);

        var checkIns = await _reservationRepository.GetReservationsByCheckInDateAsync(
            targetDate, statuses, cancellationToken);

        _logger.LogInformation(
            "Found {Count} check-ins for {Date} for tenant {TenantId}",
            checkIns.Count, targetDate, tenantSettings.TenantId);

        return checkIns;
    }
}
