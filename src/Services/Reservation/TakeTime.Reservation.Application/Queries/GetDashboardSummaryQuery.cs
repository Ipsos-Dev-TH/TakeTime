using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Queries;

/// <summary>
/// Query to retrieve the dashboard summary, including today's check-ins/outs,
/// occupancy rate, revenue summary, and pending reservations for the current tenant.
/// </summary>
public sealed class GetDashboardSummaryQuery : IRequest<DashboardSummaryDto>
{
    /// <summary>
    /// Optional date override. Defaults to today (UTC).
    /// </summary>
    public DateTime? Date { get; set; }

    /// <summary>
    /// Number of upcoming check-ins/outs to include in the summary.
    /// </summary>
    public int UpcomingDays { get; set; } = 7;
}

/// <summary>
/// Handler for <see cref="GetDashboardSummaryQuery"/>. Aggregates operational metrics
/// including occupancy, revenue, and reservation counts for the dashboard view.
/// </summary>
public sealed class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    private readonly IReservationRepository _reservationRepository;
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<GetDashboardSummaryQueryHandler> _logger;

    public GetDashboardSummaryQueryHandler(
        IReservationRepository reservationRepository,
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<GetDashboardSummaryQueryHandler> logger)
    {
        _reservationRepository = reservationRepository;
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<DashboardSummaryDto> Handle(
        GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);
        var today = request.Date?.Date ?? DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        _logger.LogDebug(
            "Building dashboard summary for {Date} for tenant {TenantId}",
            today, tenantSettings.TenantId);

        // Get today's check-ins
        var todayCheckIns = await _reservationRepository.GetReservationsByCheckInDateAsync(
            today, ["Confirmed", "Pending"], cancellationToken);

        // Get today's check-outs
        var todayCheckOuts = await _reservationRepository.GetReservationsByCheckOutDateAsync(
            today, ["CheckedIn"], cancellationToken);

        // Get total active accommodations
        var totalAccommodations = await _accommodationRepository.GetActiveCountAsync(cancellationToken);

        // Get current occupancy (checked-in reservations)
        var currentOccupancy = await _reservationRepository.GetCurrentOccupancyCountAsync(today, cancellationToken);

        // Calculate occupancy rate
        var occupancyRate = totalAccommodations > 0
            ? Math.Round((decimal)currentOccupancy / totalAccommodations * 100, 1)
            : 0;

        // Get today's revenue (payments received today)
        var todayRevenue = await _reservationRepository.GetRevenueForDateAsync(today, cancellationToken);

        // Get monthly revenue
        var monthlyRevenue = await _reservationRepository.GetRevenueForPeriodAsync(
            monthStart, today.AddDays(1), cancellationToken);

        // Get pending reservations count
        var pendingReservations = await _reservationRepository.GetCountByStatusAsync(
            "Pending", cancellationToken);

        // Get confirmed upcoming reservations count
        var confirmedUpcoming = await _reservationRepository.GetUpcomingConfirmedCountAsync(
            today, today.AddDays(request.UpcomingDays), cancellationToken);

        // Get upcoming check-ins (next N days)
        var upcomingCheckIns = await _reservationRepository.GetUpcomingCheckInsAsync(
            today.AddDays(1), today.AddDays(request.UpcomingDays), 10, cancellationToken);

        // Get upcoming check-outs (next N days)
        var upcomingCheckOuts = await _reservationRepository.GetUpcomingCheckOutsAsync(
            today.AddDays(1), today.AddDays(request.UpcomingDays), 10, cancellationToken);

        var summary = new DashboardSummaryDto
        {
            Date = today,
            TodayCheckIns = todayCheckIns.Count,
            TodayCheckOuts = todayCheckOuts.Count,
            CurrentOccupancy = currentOccupancy,
            TotalAccommodations = totalAccommodations,
            OccupancyRate = occupancyRate,
            TodayRevenue = todayRevenue,
            MonthlyRevenue = monthlyRevenue,
            PendingReservations = pendingReservations,
            ConfirmedUpcoming = confirmedUpcoming,
            Currency = tenantSettings.Currency ?? "THB",
            UpcomingCheckIns = upcomingCheckIns,
            UpcomingCheckOuts = upcomingCheckOuts
        };

        _logger.LogInformation(
            "Dashboard summary for tenant {TenantId}: Occupancy {OccupancyRate}%, " +
            "CheckIns {CheckIns}, CheckOuts {CheckOuts}, Pending {Pending}",
            tenantSettings.TenantId, occupancyRate,
            todayCheckIns.Count, todayCheckOuts.Count, pendingReservations);

        return summary;
    }
}
