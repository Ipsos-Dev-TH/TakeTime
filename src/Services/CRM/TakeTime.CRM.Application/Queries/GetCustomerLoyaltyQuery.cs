using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerLoyaltyQuery : IRequest<LoyaltyDto?>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerLoyaltyHandler : IRequestHandler<GetCustomerLoyaltyQuery, LoyaltyDto?>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<GetCustomerLoyaltyHandler> _logger;

    public GetCustomerLoyaltyHandler(CRMDbContext db, ILogger<GetCustomerLoyaltyHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LoyaltyDto?> Handle(GetCustomerLoyaltyQuery request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
        {
            _logger.LogWarning("Customer with ID '{CustomerId}' not found for loyalty query.", request.CustomerId);
            return null;
        }

        // Load recent loyalty transactions
        var recentTransactions = await _db.LoyaltyTransactions
            .Where(lt => lt.CustomerId == request.CustomerId)
            .OrderByDescending(lt => lt.CreatedAt)
            .Take(20)
            .Select(lt => new LoyaltyTransactionDto
            {
                Id = lt.Id,
                Type = lt.TransactionType.ToString(),
                Points = lt.Points,
                Description = lt.Description,
                CreatedAt = lt.CreatedAt
            })
            .ToListAsync(ct);

        // Calculate points to next tier based on total spending thresholds
        var pointsToNextTier = CalculatePointsToNextTier(customer.LoyaltyTier, customer.TotalSpent.Amount);

        // Calculate discount percentage based on current tier
        var discountPercentage = GetTierDiscount(customer.LoyaltyTier);

        return new LoyaltyDto
        {
            CustomerId = customer.Id,
            Tier = customer.LoyaltyTier.ToString(),
            Points = customer.LoyaltyPoints,
            PointsToNextTier = pointsToNextTier,
            DiscountPercentage = discountPercentage,
            RecentTransactions = recentTransactions
        };
    }

    private static int CalculatePointsToNextTier(LoyaltyTier currentTier, decimal totalSpent)
    {
        var nextTierThreshold = currentTier switch
        {
            LoyaltyTier.Bronze => 20000m,   // Silver threshold
            LoyaltyTier.Silver => 50000m,   // Gold threshold
            LoyaltyTier.Gold => 100000m,    // Platinum threshold
            LoyaltyTier.Platinum => 0m,     // Already at max
            _ => 20000m
        };

        if (nextTierThreshold == 0m) return 0;
        return (int)Math.Max(0, nextTierThreshold - totalSpent);
    }

    private static decimal GetTierDiscount(LoyaltyTier tier) => tier switch
    {
        LoyaltyTier.Bronze => 0m,
        LoyaltyTier.Silver => 5m,
        LoyaltyTier.Gold => 10m,
        LoyaltyTier.Platinum => 15m,
        _ => 0m
    };
}
