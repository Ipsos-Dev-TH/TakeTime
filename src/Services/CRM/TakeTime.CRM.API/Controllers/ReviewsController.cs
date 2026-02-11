using Microsoft.AspNetCore.Mvc;

namespace TakeTime.CRM.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReviewsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmitReviewRequest request, CancellationToken ct)
    {
        // Placeholder: create GuestReview entity and save
        return await Task.FromResult(Ok(new { Id = Guid.NewGuid(), request.Rating, Status = "Pending" }));
    }

    [HttpPost("{id:guid}/respond")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] RespondToReviewRequest request, CancellationToken ct)
    {
        return await Task.FromResult(Ok(new { Id = id, Response = request.Response }));
    }
}

public class SubmitReviewRequest
{
    public Guid CustomerId { get; set; }
    public Guid? ReservationId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class RespondToReviewRequest
{
    public string Response { get; set; } = string.Empty;
    public string RespondedBy { get; set; } = string.Empty;
}
