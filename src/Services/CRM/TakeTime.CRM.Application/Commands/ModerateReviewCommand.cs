using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Commands;

public class ModerateReviewCommand : IRequest<ReviewDto>
{
    public Guid ReviewId { get; set; }
    public bool IsApproved { get; set; }
}

public class ModerateReviewHandler : IRequestHandler<ModerateReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(ModerateReviewCommand request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Load GuestReview from CRMDbContext by ID
        // 2. If not found, throw NotFoundException
        // 3. Call review.Approve() or review.Hide() based on IsApproved
        // 4. Save changes

        var status = request.IsApproved ? "Approved" : "Hidden";

        return await Task.FromResult(new ReviewDto
        {
            Id = request.ReviewId,
            Status = status,
            CreatedAt = DateTime.UtcNow
        });
    }
}
