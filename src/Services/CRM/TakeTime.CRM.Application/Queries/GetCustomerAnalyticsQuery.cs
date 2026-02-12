using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerAnalyticsQuery : IRequest<CustomerAnalyticsDto>
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class GetCustomerAnalyticsHandler : IRequestHandler<GetCustomerAnalyticsQuery, CustomerAnalyticsDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<GetCustomerAnalyticsHandler> _logger;

    public GetCustomerAnalyticsHandler(CRMDbContext db, ILogger<GetCustomerAnalyticsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CustomerAnalyticsDto> Handle(GetCustomerAnalyticsQuery request, CancellationToken ct)
    {
        var query = _db.Customers.AsQueryable();

        // Total customers
        var totalCustomers = await query.CountAsync(ct);

        // New customers this month
        var startOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var newCustomersThisMonth = await query
            .Where(c => c.CreatedAt >= startOfMonth)
            .CountAsync(ct);

        // Average spend
        var averageSpend = totalCustomers > 0
            ? await query.AverageAsync(c => c.TotalSpent.Amount, ct)
            : 0m;

        // Tier distribution
        var tiers = await query
            .GroupBy(c => c.LoyaltyTier)
            .Select(g => new { Tier = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var tierDistribution = tiers.ToDictionary(
            t => t.Tier.ToString(),
            t => t.Count);

        // Top customers by total spent
        var topCustomers = await query
            .OrderByDescending(c => c.TotalSpent.Amount)
            .Take(10)
            .Select(c => new CustomerSummaryDto
            {
                Id = c.Id,
                FullName = c.FirstName + " " + c.LastName,
                Phone = c.Phone,
                LoyaltyTier = c.LoyaltyTier.ToString(),
                TotalVisits = c.TotalVisits,
                TotalSpent = c.TotalSpent.Amount
            })
            .ToListAsync(ct);

        // Average review rating
        var averageRating = await _db.GuestReviews.AnyAsync(ct)
            ? await _db.GuestReviews.AverageAsync(r => r.Rating, ct)
            : 0.0;

        return new CustomerAnalyticsDto
        {
            TotalCustomers = totalCustomers,
            NewCustomersThisMonth = newCustomersThisMonth,
            AverageSpend = averageSpend,
            TierDistribution = tierDistribution,
            TopCustomers = topCustomers,
            AverageRating = averageRating
        };
    }
}
