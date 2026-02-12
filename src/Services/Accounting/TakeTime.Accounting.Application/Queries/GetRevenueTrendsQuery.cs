using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve revenue and occupancy trends over a specified time period.
/// Supports daily, weekly, and monthly aggregation periods.
/// </summary>
public sealed class GetRevenueTrendsQuery : IRequest<TrendsDto>
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Period { get; set; } = "daily";
}

/// <summary>
/// Handler for <see cref="GetRevenueTrendsQuery"/>. Aggregates revenue and occupancy
/// data into time-series data points for trend visualization.
/// </summary>
public sealed class GetRevenueTrendsQueryHandler
    : IRequestHandler<GetRevenueTrendsQuery, TrendsDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetRevenueTrendsQueryHandler> _logger;

    public GetRevenueTrendsQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetRevenueTrendsQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<TrendsDto> Handle(
        GetRevenueTrendsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        if (request.EndDate < request.StartDate)
        {
            throw new InvalidOperationException("End date must be on or after start date.");
        }

        _logger.LogInformation(
            "Generating {Period} trends for tenant {TenantId} from {StartDate} to {EndDate}",
            request.Period, tenantId, request.StartDate.ToString("yyyy-MM-dd"),
            request.EndDate.ToString("yyyy-MM-dd"));

        var totalRooms = await _repository.GetTotalRoomsAsync(tenantId, cancellationToken);
        var transactions = await _repository.GetTransactionsByDateRangeAsync(
            tenantId, request.StartDate, request.EndDate, cancellationToken);

        var revenueData = new List<TrendDataPointDto>();
        var occupancyData = new List<TrendDataPointDto>();

        // Generate daily data points
        for (var date = request.StartDate.Date; date <= request.EndDate.Date; date = date.AddDays(1))
        {
            var dayRevenue = transactions
                .Where(t => t.TransactionDate.Date == date)
                .Sum(t => t.Amount);

            revenueData.Add(new TrendDataPointDto
            {
                Date = date,
                Label = date.ToString("yyyy-MM-dd"),
                Value = dayRevenue
            });

            var occupied = await _repository.GetOccupiedRoomsCountByDateAsync(tenantId, date, cancellationToken);
            var occupancyRate = totalRooms > 0 ? Math.Round((decimal)occupied / totalRooms * 100, 2) : 0;

            occupancyData.Add(new TrendDataPointDto
            {
                Date = date,
                Label = date.ToString("yyyy-MM-dd"),
                Value = occupancyRate
            });
        }

        // Aggregate by period if not daily
        if (request.Period.Equals("weekly", StringComparison.OrdinalIgnoreCase))
        {
            revenueData = AggregateByWeek(revenueData);
            occupancyData = AggregateByWeekAverage(occupancyData);
        }
        else if (request.Period.Equals("monthly", StringComparison.OrdinalIgnoreCase))
        {
            revenueData = AggregateByMonth(revenueData);
            occupancyData = AggregateByMonthAverage(occupancyData);
        }

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new TrendsDto
        {
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            TenantId = tenantId,
            Period = request.Period,
            RevenueData = revenueData,
            OccupancyData = occupancyData,
            Currency = currency
        };
    }

    private static List<TrendDataPointDto> AggregateByWeek(List<TrendDataPointDto> data)
    {
        return data
            .GroupBy(d => new { Year = d.Date.Year, Week = GetIsoWeekNumber(d.Date) })
            .Select(g => new TrendDataPointDto
            {
                Date = g.First().Date,
                Label = $"W{g.Key.Week} {g.Key.Year}",
                Value = g.Sum(d => d.Value)
            })
            .ToList();
    }

    private static List<TrendDataPointDto> AggregateByWeekAverage(List<TrendDataPointDto> data)
    {
        return data
            .GroupBy(d => new { Year = d.Date.Year, Week = GetIsoWeekNumber(d.Date) })
            .Select(g => new TrendDataPointDto
            {
                Date = g.First().Date,
                Label = $"W{g.Key.Week} {g.Key.Year}",
                Value = Math.Round(g.Average(d => d.Value), 2)
            })
            .ToList();
    }

    private static List<TrendDataPointDto> AggregateByMonth(List<TrendDataPointDto> data)
    {
        return data
            .GroupBy(d => new { d.Date.Year, d.Date.Month })
            .Select(g => new TrendDataPointDto
            {
                Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Value = g.Sum(d => d.Value)
            })
            .ToList();
    }

    private static List<TrendDataPointDto> AggregateByMonthAverage(List<TrendDataPointDto> data)
    {
        return data
            .GroupBy(d => new { d.Date.Year, d.Date.Month })
            .Select(g => new TrendDataPointDto
            {
                Date = new DateTime(g.Key.Year, g.Key.Month, 1),
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Value = Math.Round(g.Average(d => d.Value), 2)
            })
            .ToList();
    }

    private static int GetIsoWeekNumber(DateTime date)
    {
        return System.Globalization.CultureInfo.InvariantCulture.Calendar
            .GetWeekOfYear(date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }
}
