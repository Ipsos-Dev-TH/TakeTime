using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Commands;

public class RespondToReviewCommand : IRequest<ReviewDto>
{
    public Guid ReviewId { get; set; }
    public string Response { get; set; } = string.Empty;
    public string RespondedBy { get; set; } = string.Empty;
}

public class RespondToReviewHandler : IRequestHandler<RespondToReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(RespondToReviewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Response))
            throw new InvalidOperationException("Response text is required.");

        if (string.IsNullOrWhiteSpace(request.RespondedBy))
            throw new InvalidOperationException("Responded by user is required.");

        // In real implementation:
        // 1. Load GuestReview from CRMDbContext by ID
        // 2. If not found, throw NotFoundException
        // 3. Call review.AddResponse(response, respondedBy)
        // 4. Save changes

        return await Task.FromResult(new ReviewDto
        {
            Id = request.ReviewId,
            Response = request.Response,
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
    }
}
