using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

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
    private readonly CRMDbContext _db;
    private readonly ILogger<GetReviewsHandler> _logger;

    public GetReviewsHandler(CRMDbContext db, ILogger<GetReviewsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ReviewDto>> Handle(GetReviewsQuery request, CancellationToken ct)
    {
        var query = _db.GuestReviews.AsQueryable();

        // Filter by customer ID if provided
        if (request.CustomerId.HasValue)
            query = query.Where(r => r.CustomerId == request.CustomerId.Value);

        // Filter by status if provided
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            Enum.TryParse<ReviewStatus>(request.Status, true, out var statusFilter))
        {
            query = query.Where(r => r.Status == statusFilter);
        }

        // Filter by rating range
        if (request.MinRating.HasValue)
            query = query.Where(r => r.Rating >= request.MinRating.Value);

        if (request.MaxRating.HasValue)
            query = query.Where(r => r.Rating <= request.MaxRating.Value);

        // Order by created date descending
        query = query.OrderByDescending(r => r.CreatedAt);

        // Apply pagination
        var skip = (Math.Max(1, request.Page) - 1) * request.PageSize;
        var reviews = await query
            .Skip(skip)
            .Take(request.PageSize)
            .ToListAsync(ct);

        if (reviews.Count == 0)
            return [];

        // Load customer names for all reviews in a single query
        var customerIds = reviews.Select(r => r.CustomerId).Distinct().ToList();
        var customers = await _db.Customers
            .Where(c => customerIds.Contains(c.Id))
            .Select(c => new { c.Id, Name = c.FirstName + " " + c.LastName })
            .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

        return reviews.Select(r =>
        {
            customers.TryGetValue(r.CustomerId, out var customerName);

            return new ReviewDto
            {
                Id = r.Id,
                CustomerId = r.CustomerId,
                CustomerName = customerName ?? string.Empty,
                ReservationId = r.ReservationId == Guid.Empty ? null : r.ReservationId,
                Rating = r.Rating,
                Comment = r.Comment,
                Response = r.Response,
                Status = r.Status.ToString(),
                CreatedAt = r.CreatedAt
            };
        }).ToList();
    }
}
