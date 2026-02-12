using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Entities;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Commands;

public class AdjustLoyaltyPointsCommand : IRequest<LoyaltyDto>
{
    public Guid CustomerId { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Earn, Redeem, Adjust
    public int Points { get; set; }
    public string? Description { get; set; }
    public Guid? ReservationId { get; set; }
}

public class AdjustLoyaltyPointsHandler : IRequestHandler<AdjustLoyaltyPointsCommand, LoyaltyDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<AdjustLoyaltyPointsHandler> _logger;

    public AdjustLoyaltyPointsHandler(CRMDbContext db, ILogger<AdjustLoyaltyPointsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<LoyaltyDto> Handle(AdjustLoyaltyPointsCommand request, CancellationToken ct)
    {
        var customer = await _db.Customers.FirstOrDefaultAsync(c => c.Id == request.CustomerId, ct);
        if (customer is null)
            throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' not found.");

        // Parse transaction type
        if (!Enum.TryParse<LoyaltyTransactionType>(request.TransactionType, true, out var txType))
            txType = LoyaltyTransactionType.Adjust;

        var description = request.Description ?? $"{txType} {request.Points} points";

        // Create loyalty transaction
        LoyaltyTransaction transaction;
        switch (txType)
        {
            case LoyaltyTransactionType.Earn:
                customer.AddLoyaltyPoints(request.Points);
                transaction = LoyaltyTransaction.Earn(
                    customer.Id, request.Points, description,
                    request.ReservationId, DateTime.UtcNow.AddYears(1));
                break;

            case LoyaltyTransactionType.Redeem:
                customer.RedeemLoyaltyPoints(request.Points);
                transaction = LoyaltyTransaction.Redeem(
                    customer.Id, request.Points, description, request.ReservationId);
                break;

            default: // Adjust
                if (request.Points > 0)
                    customer.AddLoyaltyPoints(request.Points);
                else if (request.Points < 0)
                    customer.RedeemLoyaltyPoints(Math.Abs(request.Points));

                transaction = new LoyaltyTransaction(
                    customer.Id, txType, request.Points, description,
                    request.ReservationId);
                break;
        }

        _db.LoyaltyTransactions.Add(transaction);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Adjusted loyalty points for customer {CustomerCode}: {Type} {Points} points. New balance: {Balance}",
            customer.CustomerCode, txType, request.Points, customer.LoyaltyPoints);

        // Load recent transactions for response
        var recentTransactions = await _db.LoyaltyTransactions
            .Where(lt => lt.CustomerId == request.CustomerId)
            .OrderByDescending(lt => lt.CreatedAt)
            .Take(10)
            .Select(lt => new LoyaltyTransactionDto
            {
                Id = lt.Id,
                Type = lt.TransactionType.ToString(),
                Points = lt.Points,
                Description = lt.Description,
                CreatedAt = lt.CreatedAt
            })
            .ToListAsync(ct);

        // Calculate points to next tier
        var pointsToNextTier = CalculatePointsToNextTier(customer.LoyaltyTier, customer.TotalSpent.Amount);

        // Calculate discount percentage based on tier
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
            LoyaltyTier.Bronze => 20000m,
            LoyaltyTier.Silver => 50000m,
            LoyaltyTier.Gold => 100000m,
            LoyaltyTier.Platinum => 0m, // Already at max
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
