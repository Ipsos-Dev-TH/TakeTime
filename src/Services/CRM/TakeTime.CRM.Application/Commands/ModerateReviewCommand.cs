using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Infrastructure.Repositories;

namespace TakeTime.CRM.Application.Commands;

public class ModerateReviewCommand : IRequest<ReviewDto>
{
    public Guid ReviewId { get; set; }
    public bool IsApproved { get; set; }
}

public class ModerateReviewHandler : IRequestHandler<ModerateReviewCommand, ReviewDto>
{
    private readonly CRMDbContext _db;
    private readonly ILogger<ModerateReviewHandler> _logger;

    public ModerateReviewHandler(CRMDbContext db, ILogger<ModerateReviewHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReviewDto> Handle(ModerateReviewCommand request, CancellationToken ct)
    {
        var review = await _db.GuestReviews.FirstOrDefaultAsync(r => r.Id == request.ReviewId, ct);
        if (review is null)
            throw new InvalidOperationException($"Review with ID '{request.ReviewId}' not found.");

        // Apply moderation via domain methods
        if (request.IsApproved)
        {
            review.Approve();
        }
        else
        {
            review.Hide();
        }

        await _db.SaveChangesAsync(ct);

        // Load customer name
        var customerName = await _db.Customers
            .Where(c => c.Id == review.CustomerId)
            .Select(c => c.FirstName + " " + c.LastName)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var action = request.IsApproved ? "Approved" : "Hidden";
        _logger.LogInformation("{Action} review {ReviewId}", action, review.Id);

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
