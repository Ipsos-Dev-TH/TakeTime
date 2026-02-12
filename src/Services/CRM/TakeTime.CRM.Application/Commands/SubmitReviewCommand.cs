using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Commands;

public class SubmitReviewCommand : IRequest<ReviewDto>
{
    public Guid CustomerId { get; set; }
    public Guid? ReservationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Categories { get; set; }
}

public class SubmitReviewHandler : IRequestHandler<SubmitReviewCommand, ReviewDto>
{
    public async Task<ReviewDto> Handle(SubmitReviewCommand request, CancellationToken ct)
    {
        // Validate rating
        if (request.Rating < 1 || request.Rating > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        // In real implementation:
        // 1. Verify customer exists in CRMDbContext
        // 2. Optionally verify reservation exists and belongs to customer
        // 3. Create GuestReview entity via domain constructor
        // 4. Save to CRMDbContext
        // 5. Return ReviewDto

        var id = Guid.NewGuid();

        return await Task.FromResult(new ReviewDto
        {
            Id = id,
            CustomerId = request.CustomerId,
            ReservationId = request.ReservationId,
            Rating = request.Rating,
            Comment = request.Comment,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });
    }
}
