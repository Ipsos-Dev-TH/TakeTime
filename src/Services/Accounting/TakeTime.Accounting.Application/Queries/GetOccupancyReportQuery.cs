using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve an occupancy report for a specified date range, including
/// daily breakdowns, ADR (Average Daily Rate), and RevPAR.
/// </summary>
public sealed class GetOccupancyReportQuery : IRequest<OccupancyReportDto>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

/// <summary>
/// Handler for <see cref="GetOccupancyReportQuery"/>. Calculates occupancy metrics
/// by iterating over each day in the range and aggregating room utilization data.
/// </summary>
public sealed class GetOccupancyReportQueryHandler : IRequestHandler<GetOccupancyReportQuery, OccupancyReportDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetOccupancyReportQueryHandler> _logger;

    public GetOccupancyReportQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetOccupancyReportQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<OccupancyReportDto> Handle(GetOccupancyReportQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Generating occupancy report for tenant {TenantId} from {StartDate} to {EndDate}",
            tenantId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        var startDate = request.StartDate.Date;
        var endDate = request.EndDate.Date;

        var totalRooms = await _repository.GetTotalRoomsAsync(tenantId, cancellationToken);
        if (totalRooms == 0)
        {
            return new OccupancyReportDto
            {
                StartDate = startDate,
                EndDate = endDate,
                TenantId = tenantId
            };
        }

        var totalDays = (endDate - startDate).Days + 1;
        var totalRoomNightsAvailable = totalRooms * totalDays;
        var totalRoomNightsSold = 0;
        var totalAccommodationRevenue = 0m;

        var dailyBreakdown = new List<DailyOccupancyDto>();

        // Get accommodation revenue for ADR/RevPAR calculation
        var transactions = await _repository.GetTransactionsByDateRangeAsync(
            tenantId, startDate, endDate, cancellationToken);

        var accommodationTransactions = transactions
            .Where(t => t.Category?.Equals("accommodation", StringComparison.OrdinalIgnoreCase) == true ||
                        t.Category?.Equals("room", StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        totalAccommodationRevenue = accommodationTransactions.Sum(t => t.Amount);

        // Calculate daily breakdown
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var occupied = await _repository.GetOccupiedRoomsCountByDateAsync(tenantId, date, cancellationToken);
            totalRoomNightsSold += occupied;

            var dailyRevenue = accommodationTransactions
                .Where(t => t.TransactionDate.Date == date)
                .Sum(t => t.Amount);

            dailyBreakdown.Add(new DailyOccupancyDto
            {
                Date = date,
                OccupiedRooms = occupied,
                AvailableRooms = totalRooms - occupied,
                OccupancyRatePercent = Math.Round((decimal)occupied / totalRooms * 100, 2),
                Revenue = dailyRevenue
            });
        }

        var occupancyRate = totalRoomNightsAvailable > 0
            ? Math.Round((decimal)totalRoomNightsSold / totalRoomNightsAvailable * 100, 2)
            : 0;

        var averageDailyRate = totalRoomNightsSold > 0
            ? Math.Round(totalAccommodationRevenue / totalRoomNightsSold, 2)
            : 0;

        var revPar = totalRoomNightsAvailable > 0
            ? Math.Round(totalAccommodationRevenue / totalRoomNightsAvailable, 2)
            : 0;

        var averageStay = await _repository.GetAverageStayDurationAsync(tenantId, startDate, endDate, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new OccupancyReportDto
        {
            StartDate = startDate,
            EndDate = endDate,
            TenantId = tenantId,
            TotalRooms = totalRooms,
            TotalRoomNightsAvailable = totalRoomNightsAvailable,
            TotalRoomNightsSold = totalRoomNightsSold,
            OccupancyRatePercent = occupancyRate,
            AverageDailyRate = averageDailyRate,
            RevPAR = revPar,
            AverageStayDuration = averageStay,
            Currency = currency,
            DailyBreakdown = dailyBreakdown
        };
    }
}
