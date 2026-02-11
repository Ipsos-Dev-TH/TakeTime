using MediatR;
using TakeTime.CRM.Application.DTOs;

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
    public async Task<LoyaltyDto> Handle(AdjustLoyaltyPointsCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load customer
        // 2. Adjust points (add for Earn, subtract for Redeem)
        // 3. Recalculate loyalty tier based on tenant's tier config
        // 4. Save transaction log
        // 5. Return updated loyalty info

        return await Task.FromResult(new LoyaltyDto
        {
            CustomerId = request.CustomerId,
            Tier = "Member",
            Points = request.Points
        });
    }

    public static string CalculateTier(int totalPoints)
    {
        return totalPoints switch
        {
            >= 50000 => "VIP",
            >= 15000 => "Platinum",
            >= 5000 => "Gold",
            >= 1000 => "Silver",
            _ => "Member"
        };
    }
}
