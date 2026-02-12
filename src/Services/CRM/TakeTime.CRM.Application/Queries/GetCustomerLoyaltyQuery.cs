using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class GetCustomerLoyaltyQuery : IRequest<LoyaltyDto?>
{
    public Guid CustomerId { get; set; }
}

public class GetCustomerLoyaltyHandler : IRequestHandler<GetCustomerLoyaltyQuery, LoyaltyDto?>
{
    public async Task<LoyaltyDto?> Handle(GetCustomerLoyaltyQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load customer from CRMDbContext by ID
        // 2. If not found, return null
        // 3. Query LoyaltyTransactions for this customer (recent transactions)
        // 4. Calculate points to next tier based on tenant's tier configuration
        // 5. Calculate discount percentage based on current tier
        // 6. Map to LoyaltyDto

        return await Task.FromResult<LoyaltyDto?>(new LoyaltyDto
        {
            CustomerId = request.CustomerId,
            Tier = "Bronze",
            Points = 0,
            PointsToNextTier = 1000,
            DiscountPercentage = 0,
            RecentTransactions = []
        });
    }
}
