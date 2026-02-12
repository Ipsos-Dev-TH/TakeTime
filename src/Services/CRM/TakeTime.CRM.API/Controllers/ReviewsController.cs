using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.CRM.Application.Commands;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Application.Queries;

namespace TakeTime.CRM.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReviewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReviewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>List reviews with optional filters.</summary>
    [HttpGet]
    public async Task<ActionResult<List<ReviewDto>>> GetReviews(
        [FromQuery] Guid? customerId,
        [FromQuery] string? status,
        [FromQuery] int? minRating,
        [FromQuery] int? maxRating,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetReviewsQuery
        {
            CustomerId = customerId,
            Status = status,
            MinRating = minRating,
            MaxRating = maxRating,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(result);
    }

    /// <summary>Submit a guest review.</summary>
    [HttpPost]
    public async Task<ActionResult<ReviewDto>> Submit(
        [FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new SubmitReviewCommand
        {
            CustomerId = request.CustomerId,
            ReservationId = request.ReservationId,
            Rating = request.Rating,
            Comment = request.Comment,
            Categories = request.Categories
        }, ct);

        return CreatedAtAction(nameof(GetReviews), null, result);
    }

    /// <summary>Hotel responds to a review.</summary>
    [HttpPost("{id:guid}/respond")]
    public async Task<ActionResult<ReviewDto>> Respond(
        Guid id, [FromBody] RespondToReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RespondToReviewCommand
        {
            ReviewId = id,
            Response = request.Response,
            RespondedBy = request.RespondedBy
        }, ct);

        return Ok(result);
    }

    /// <summary>Approve or reject a review for public display.</summary>
    [HttpPost("{id:guid}/moderate")]
    public async Task<ActionResult<ReviewDto>> Moderate(
        Guid id, [FromBody] ModerateReviewRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new ModerateReviewCommand
        {
            ReviewId = id,
            IsApproved = request.IsApproved
        }, ct);

        return Ok(result);
    }
}

public class SubmitReviewRequest
{
    public Guid CustomerId { get; set; }
    public Guid? ReservationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public List<string>? Categories { get; set; }
}

public class RespondToReviewRequest
{
    public string Response { get; set; } = string.Empty;
    public string RespondedBy { get; set; } = string.Empty;
}

public class ModerateReviewRequest
{
    public bool IsApproved { get; set; }
}
