using MediatR;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.Application.Queries;

public class GetReviewsQuery : IRequest<List<ReviewDto>>
{
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
    public int? MinRating { get; set; }
    public int? MaxRating { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetReviewsHandler : IRequestHandler<GetReviewsQuery, List<ReviewDto>>
{
    public async Task<List<ReviewDto>> Handle(GetReviewsQuery request, CancellationToken ct)
    {
        // In real implementation:
        // 1. Query GuestReviews from CRMDbContext
        // 2. Filter by customer ID if provided
        // 3. Filter by status if provided (Pending, Approved, Hidden)
        // 4. Filter by rating range if provided
        // 5. Include customer name
        // 6. Apply pagination
        // 7. Order by created date descending
        // 8. Map to ReviewDto

        return await Task.FromResult(new List<ReviewDto>());
    }
}
