using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve the analytics dashboard overview including occupancy,
/// revenue, and booking summary metrics for the current date.
/// </summary>
public sealed class GetAnalyticsOverviewQuery : IRequest<AnalyticsOverviewDto>
{
    public DateTime? Date { get; set; }
}

/// <summary>
/// Handler for <see cref="GetAnalyticsOverviewQuery"/>. Aggregates occupancy, revenue,
/// and booking data from the accounting repository to produce a comprehensive
/// dashboard overview.
/// </summary>
public sealed class GetAnalyticsOverviewQueryHandler
    : IRequestHandler<GetAnalyticsOverviewQuery, AnalyticsOverviewDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetAnalyticsOverviewQueryHandler> _logger;

    public GetAnalyticsOverviewQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetAnalyticsOverviewQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<AnalyticsOverviewDto> Handle(
        GetAnalyticsOverviewQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var date = request.Date?.Date ?? DateTime.UtcNow.Date;
        var weekStart = date.AddDays(-(int)date.DayOfWeek);
        var monthStart = new DateTime(date.Year, date.Month, 1);

        _logger.LogInformation(
            "Generating analytics overview for tenant {TenantId} on {Date}",
            tenantId, date.ToString("yyyy-MM-dd"));

        // Occupancy data
        var totalRooms = await _repository.GetTotalRoomsAsync(tenantId, cancellationToken);
        var occupiedRooms = await _repository.GetOccupiedRoomsCountByDateAsync(tenantId, date, cancellationToken);
        var occupancyPercent = totalRooms > 0 ? Math.Round((decimal)occupiedRooms / totalRooms * 100, 2) : 0;

        // Revenue data
        var todayTransactions = await _repository.GetTransactionsByDateAsync(tenantId, date, cancellationToken);
        var todayRevenue = todayTransactions.Sum(t => t.Amount);

        var weekTransactions = await _repository.GetTransactionsByDateRangeAsync(
            tenantId, weekStart, date, cancellationToken);
        var weekRevenue = weekTransactions.Sum(t => t.Amount);

        var monthTransactions = await _repository.GetTransactionsByDateRangeAsync(
            tenantId, monthStart, date, cancellationToken);
        var monthRevenue = monthTransactions.Sum(t => t.Amount);

        // Check-in/out counts
        var todayCheckIns = await _repository.GetCheckInCountByDateAsync(tenantId, date, cancellationToken);
        var todayCheckOuts = await _repository.GetCheckOutCountByDateAsync(tenantId, date, cancellationToken);

        // Reservation counts
        var pendingReservations = await _repository.GetReservationCountByMonthAsync(
            tenantId, date.Year, date.Month, cancellationToken);

        // Calculate ADR and RevPAR
        var adr = occupiedRooms > 0 ? Math.Round(todayRevenue / occupiedRooms, 2) : 0;
        var revPar = totalRooms > 0 ? Math.Round(todayRevenue / totalRooms, 2) : 0;

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new AnalyticsOverviewDto
        {
            AsOfDate = date,
            TenantId = tenantId,
            TotalRooms = totalRooms,
            OccupiedRooms = occupiedRooms,
            OccupancyPercent = occupancyPercent,
            TodayRevenue = todayRevenue,
            WeekRevenue = weekRevenue,
            MonthRevenue = monthRevenue,
            TodayCheckIns = todayCheckIns,
            TodayCheckOuts = todayCheckOuts,
            PendingReservations = pendingReservations,
            ConfirmedReservations = 0, // TODO: Add GetConfirmedCountAsync to repository
            AverageDailyRate = adr,
            RevPAR = revPar,
            Currency = currency
        };
    }
}
