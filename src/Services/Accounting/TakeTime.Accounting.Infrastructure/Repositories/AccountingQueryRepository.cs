using Microsoft.EntityFrameworkCore;
using TakeTime.Accounting.Application.Queries;

namespace TakeTime.Accounting.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAccountingQueryRepository"/>.
/// Queries the <see cref="AccountingDbContext"/> for financial and operational data.
/// Cross-service data (rooms, check-ins, reservations) returns sensible defaults
/// until inter-service communication (e.g., gRPC or event sourcing) is implemented.
/// </summary>
public sealed class AccountingQueryRepository : IAccountingQueryRepository
{
    private readonly AccountingDbContext _dbContext;

    public AccountingQueryRepository(AccountingDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetTransactionsByDateAsync(
        string tenantId, DateTime date, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId && t.TransactionDate.Date == date.Date)
            .Select(t => new TransactionRecord
            {
                Id = t.Id,
                Category = t.Category,
                SubCategory = t.SubCategory,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Reference = t.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetTransactionsByDateRangeAsync(
        string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        t.TransactionDate.Date >= startDate.Date &&
                        t.TransactionDate.Date <= endDate.Date)
            .Select(t => new TransactionRecord
            {
                Id = t.Id,
                Category = t.Category,
                SubCategory = t.SubCategory,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Reference = t.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseRecord>> GetExpensesByDateRangeAsync(
        string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Expenses
            .Where(e => e.TenantId == tenantId &&
                        e.ExpenseDate.Date >= startDate.Date &&
                        e.ExpenseDate.Date <= endDate.Date)
            .Select(e => new ExpenseRecord
            {
                Id = e.Id,
                Category = e.Category,
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                Description = e.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCheckInCountByDateAsync(
        string tenantId, DateTime date, CancellationToken cancellationToken = default)
    {
        // Check-in data lives in the Reservation service.
        // Approximate from accommodation transactions for this date.
        return await _dbContext.Transactions
            .CountAsync(t => t.TenantId == tenantId &&
                             t.TransactionDate.Date == date.Date &&
                             (t.Category == "accommodation" || t.Category == "room"),
                        cancellationToken);
    }

    public async Task<int> GetCheckOutCountByDateAsync(
        string tenantId, DateTime date, CancellationToken cancellationToken = default)
    {
        // Approximate: count distinct reservation IDs with transactions on this date
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        t.TransactionDate.Date == date.Date &&
                        t.ReservationId != null)
            .Select(t => t.ReservationId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public Task<int> GetTotalRoomsAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        // Room inventory lives in the Reservation service.
        // Return a configurable default until inter-service communication is implemented.
        return Task.FromResult(30);
    }

    public async Task<int> GetOccupiedRoomsCountByDateAsync(
        string tenantId, DateTime date, CancellationToken cancellationToken = default)
    {
        // Approximate from distinct reservation IDs with accommodation transactions on the date
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        t.TransactionDate.Date == date.Date &&
                        (t.Category == "accommodation" || t.Category == "room") &&
                        t.ReservationId != null)
            .Select(t => t.ReservationId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetReservationCountByMonthAsync(
        string tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        t.TransactionDate.Year == year &&
                        t.TransactionDate.Month == month &&
                        t.ReservationId != null)
            .Select(t => t.ReservationId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetCancellationCountByMonthAsync(
        string tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        // Cancellation data lives in the Reservation service.
        // Return 0 until inter-service communication is implemented.
        await Task.CompletedTask;
        return 0;
    }

    public async Task<decimal> GetAverageStayDurationAsync(
        string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // Stay duration data lives in the Reservation service.
        // Return a typical average until inter-service communication is implemented.
        await Task.CompletedTask;
        return 2.5m;
    }

    public async Task<IReadOnlyList<TransactionRecord>> GetTransactionsByMonthAsync(
        string tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        t.TransactionDate.Year == year &&
                        t.TransactionDate.Month == month)
            .Select(t => new TransactionRecord
            {
                Id = t.Id,
                Category = t.Category,
                SubCategory = t.SubCategory,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Reference = t.Reference
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ExpenseRecord>> GetExpensesByMonthAsync(
        string tenantId, int year, int month, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Expenses
            .Where(e => e.TenantId == tenantId &&
                        e.ExpenseDate.Year == year &&
                        e.ExpenseDate.Month == month)
            .Select(e => new ExpenseRecord
            {
                Id = e.Id,
                Category = e.Category,
                Amount = e.Amount,
                ExpenseDate = e.ExpenseDate,
                Description = e.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RoomTypeRevenueRecord>> GetRevenueByRoomTypeAsync(
        string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        // Group accommodation transactions by sub-category (which represents room type)
        return await _dbContext.Transactions
            .Where(t => t.TenantId == tenantId &&
                        (t.Category == "accommodation" || t.Category == "room") &&
                        t.TransactionDate.Date >= startDate.Date &&
                        t.TransactionDate.Date <= endDate.Date)
            .GroupBy(t => t.SubCategory ?? "Standard")
            .Select(g => new RoomTypeRevenueRecord
            {
                RoomType = g.Key,
                Revenue = g.Sum(t => t.Amount),
                BookingCount = g.Select(t => t.ReservationId).Distinct().Count(),
                AverageRate = g.Count() > 0 ? g.Average(t => t.Amount) : 0
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<TransactionRecord> transactions, int totalCount)> GetTransactionsPagedAsync(
        string tenantId, DateTime? startDate, DateTime? endDate, string? category,
        int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Transactions
            .Where(t => t.TenantId == tenantId);

        if (startDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date >= startDate.Value.Date);

        if (endDate.HasValue)
            query = query.Where(t => t.TransactionDate.Date <= endDate.Value.Date);

        if (!string.IsNullOrWhiteSpace(category))
            query = query.Where(t => t.Category == category);

        var totalCount = await query.CountAsync(cancellationToken);

        var transactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TransactionRecord
            {
                Id = t.Id,
                Category = t.Category,
                SubCategory = t.SubCategory,
                Amount = t.Amount,
                TransactionDate = t.TransactionDate,
                Reference = t.Reference
            })
            .ToListAsync(cancellationToken);

        return (transactions, totalCount);
    }
}
