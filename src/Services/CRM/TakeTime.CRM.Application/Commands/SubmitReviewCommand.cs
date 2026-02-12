using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Domain.Entities;
using TakeTime.CRM.Domain.Enums;
using TakeTime.CRM.Infrastructure.Repositories;

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
    private readonly CRMDbContext _db;
    private readonly ILogger<SubmitReviewHandler> _logger;

    public SubmitReviewHandler(CRMDbContext db, ILogger<SubmitReviewHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReviewDto> Handle(SubmitReviewCommand request, CancellationToken ct)
    {
        if (request.Rating < 1 || request.Rating > 5)
            throw new InvalidOperationException("Rating must be between 1 and 5.");

        // Verify customer exists
        var customer = await _db.Customers
            .Where(c => c.Id == request.CustomerId)
            .Select(c => new { c.Id, c.FirstName, c.LastName })
            .FirstOrDefaultAsync(ct);

        if (customer is null)
            throw new InvalidOperationException($"Customer with ID '{request.CustomerId}' not found.");

        // Parse review categories
        var categories = new List<ReviewCategory>();
        if (request.Categories is not null)
        {
            foreach (var cat in request.Categories)
            {
                if (Enum.TryParse<ReviewCategory>(cat, true, out var parsed))
                    categories.Add(parsed);
            }
        }

        // Create GuestReview entity via domain constructor
        var review = new GuestReview(
            customerId: request.CustomerId,
            reservationId: request.ReservationId ?? Guid.Empty,
            rating: request.Rating,
            comment: request.Comment,
            categories: categories
        );

        _db.GuestReviews.Add(review);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Submitted review {ReviewId} by customer {CustomerName} with rating {Rating}",
            review.Id, $"{customer.FirstName} {customer.LastName}", request.Rating);

        return new ReviewDto
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = $"{customer.FirstName} {customer.LastName}",
            ReservationId = review.ReservationId == Guid.Empty ? null : review.ReservationId,
            Rating = review.Rating,
            Comment = review.Comment,
            Status = review.Status.ToString(),
            CreatedAt = review.CreatedAt
        };
    }
}
