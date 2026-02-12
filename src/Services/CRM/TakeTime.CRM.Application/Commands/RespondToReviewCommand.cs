using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Commands;

public class RespondToReviewCommand : IRequest<ReviewDto>
{
    public Guid ReviewId { get; set; }
    public string Response { get; set; } = string.Empty;
    public string RespondedBy { get; set; } = string.Empty;
}

public class RespondToReviewHandler : IRequestHandler<RespondToReviewCommand, ReviewDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<RespondToReviewHandler> _logger;

    public RespondToReviewHandler(CRMDbContext db, ILogger<RespondToReviewHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReviewDto> Handle(RespondToReviewCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Response))
            throw new InvalidOperationException("Response text is required.");

        if (string.IsNullOrWhiteSpace(request.RespondedBy))
            throw new InvalidOperationException("Responded by user is required.");

        var review = await _db.GuestReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct);
        if (review is null)
            throw new InvalidOperationException($"Review with ID '{request.ReviewId}' not found.");

        // Apply response via domain method
        review.AddResponse(request.Response, request.RespondedBy);

        await _db.SaveChangesAsync(ct);

        // Load customer name
        var customerName = await _db.Customers
            .Where(c => c.Id == review.CustomerId)
            .Select(c => c.FirstName + " " + c.LastName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        _logger.LogInformation(
            "Responded to review {ReviewId} by {RespondedBy}",
            review.Id, request.RespondedBy);

        return new ReviewDto
        {
            Id = review.Id,
            CustomerId = review.CustomerId,
            CustomerName = customerName,
            ReservationId = review.ReservationId == Guid.Empty ? null : review.ReservationId,
            Rating = review.Rating,
            Comment = review.Comment,
            Response = review.Response,
            Status = review.Status.ToString(),
            CreatedAt = review.CreatedAt
        };
    }
}
