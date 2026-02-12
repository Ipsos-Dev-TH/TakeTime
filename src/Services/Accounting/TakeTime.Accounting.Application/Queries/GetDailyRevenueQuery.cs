using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Accounting.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Accounting.Application.Queries;

/// <summary>
/// Query to retrieve daily revenue report for a specific date.
/// </summary>
public sealed class GetDailyRevenueQuery : IRequest<DailyRevenueDto>
{
    public DateTime Date { get; set; }
}

/// <summary>
/// Handler for <see cref="GetDailyRevenueQuery"/>. Aggregates revenue data from
/// transactions, payments, and reservation charges for the specified date.
/// </summary>
public sealed class GetDailyRevenueQueryHandler : IRequestHandler<GetDailyRevenueQuery, DailyRevenueDto>
{
    private readonly IAccountingQueryRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetDailyRevenueQueryHandler> _logger;

    public GetDailyRevenueQueryHandler(
        IAccountingQueryRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetDailyRevenueQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<DailyRevenueDto> Handle(GetDailyRevenueQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving daily revenue for tenant {TenantId} on {Date}",
            tenantId, request.Date.ToString("yyyy-MM-dd"));

        var date = request.Date.Date;

        // Retrieve revenue aggregations from the repository
        var transactions = await _repository.GetTransactionsByDateAsync(tenantId, date, cancellationToken);

        decimal accommodationRevenue = 0, fbRevenue = 0, serviceRevenue = 0, otherRevenue = 0;
        var breakdown = new List<RevenueBreakdownDto>();

        foreach (var group in transactions.GroupBy(t => t.Category))
        {
            var categoryTotal = group.Sum(t => t.Amount);
            var categoryCount = group.Count();

            switch (group.Key?.ToLowerInvariant())
            {
                case "accommodation":
                case "room":
                    accommodationRevenue += categoryTotal;
                    break;
                case "food":
                case "beverage":
                case "food_beverage":
                case "f&b":
                    fbRevenue += categoryTotal;
                    break;
                case "service":
                case "spa":
                case "laundry":
                    serviceRevenue += categoryTotal;
                    break;
                default:
                    otherRevenue += categoryTotal;
                    break;
            }

            breakdown.Add(new RevenueBreakdownDto
            {
                Category = group.Key ?? "Other",
                Amount = categoryTotal,
                TransactionCount = categoryCount
            });
        }

        var totalRevenue = accommodationRevenue + fbRevenue + serviceRevenue + otherRevenue;

        // Calculate percentages
        foreach (var item in breakdown)
        {
            item.PercentageOfTotal = totalRevenue > 0
                ? Math.Round(item.Amount / totalRevenue * 100, 2)
                : 0;
        }

        // Get check-in/check-out counts
        var checkInCount = await _repository.GetCheckInCountByDateAsync(tenantId, date, cancellationToken);
        var checkOutCount = await _repository.GetCheckOutCountByDateAsync(tenantId, date, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        return new DailyRevenueDto
        {
            Date = date,
            TenantId = tenantId,
            TotalRevenue = totalRevenue,
            AccommodationRevenue = accommodationRevenue,
            FoodBeverageRevenue = fbRevenue,
            ServiceRevenue = serviceRevenue,
            OtherRevenue = otherRevenue,
            TransactionCount = transactions.Count,
            CheckInsCount = checkInCount,
            CheckOutsCount = checkOutCount,
            Currency = currency,
            Breakdown = breakdown
        };
    }
}

/// <summary>
/// Repository interface for accounting query operations.
/// </summary>
public interface IAccountingQueryRepository
{
    Task<IReadOnlyList<TransactionRecord>> GetTransactionsByDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionRecord>> GetTransactionsByDateRangeAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseRecord>> GetExpensesByDateRangeAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<int> GetCheckInCountByDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<int> GetCheckOutCountByDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<int> GetTotalRoomsAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<int> GetOccupiedRoomsCountByDateAsync(string tenantId, DateTime date, CancellationToken cancellationToken = default);
    Task<int> GetReservationCountByMonthAsync(string tenantId, int year, int month, CancellationToken cancellationToken = default);
    Task<int> GetCancellationCountByMonthAsync(string tenantId, int year, int month, CancellationToken cancellationToken = default);
    Task<decimal> GetAverageStayDurationAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TransactionRecord>> GetTransactionsByMonthAsync(string tenantId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExpenseRecord>> GetExpensesByMonthAsync(string tenantId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomTypeRevenueRecord>> GetRevenueByRoomTypeAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<TransactionRecord> transactions, int totalCount)> GetTransactionsPagedAsync(string tenantId, DateTime? startDate, DateTime? endDate, string? category, int page, int pageSize, CancellationToken cancellationToken = default);
}

/// <summary>
/// Revenue record grouped by room type for reporting.
/// </summary>
public sealed class RoomTypeRevenueRecord
{
    public string RoomType { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int BookingCount { get; set; }
    public decimal AverageRate { get; set; }
}

/// <summary>
/// Internal transaction record used for revenue calculations.
/// </summary>
public sealed class TransactionRecord
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? Reference { get; set; }
}

/// <summary>
/// Internal expense record used for P&L calculations.
/// </summary>
public sealed class ExpenseRecord
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime ExpenseDate { get; set; }
    public string? Description { get; set; }
}
