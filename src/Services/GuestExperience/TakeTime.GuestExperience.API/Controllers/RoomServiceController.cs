using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/room-service")]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "RoomService")]
public class RoomServiceController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomServiceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates a new room service order.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomServiceOrderCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Create), result);
    }

    /// <summary>Lists room service orders with filtering.</summary>
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? status, [FromQuery] string? roomNumber,
        [FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return StatusCode(501, new { message = "List room service orders not yet implemented." });
    }

    /// <summary>Gets a room service order by ID.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return StatusCode(501, new { message = "Get order by ID not yet implemented." });
    }

    /// <summary>Accepts a room service order.</summary>
    [HttpPut("{id:guid}/accept")]
    public IActionResult Accept(Guid id)
    {
        return StatusCode(501, new { message = "Accept order not yet implemented." });
    }

    /// <summary>Marks order as being prepared.</summary>
    [HttpPut("{id:guid}/prepare")]
    public IActionResult MarkPreparing(Guid id)
    {
        return StatusCode(501, new { message = "Mark as preparing not yet implemented." });
    }

    /// <summary>Marks order as delivered.</summary>
    [HttpPut("{id:guid}/deliver")]
    public IActionResult MarkDelivered(Guid id)
    {
        return StatusCode(501, new { message = "Mark as delivered not yet implemented." });
    }

    /// <summary>Cancels a room service order.</summary>
    [HttpPut("{id:guid}/cancel")]
    public IActionResult Cancel(Guid id, [FromBody] CancelOrderRequest? request = null)
    {
        return StatusCode(501, new { message = "Cancel order not yet implemented." });
    }

    /// <summary>Gets room service menu items.</summary>
    [HttpGet("menu")]
    public IActionResult GetMenu()
    {
        return StatusCode(501, new { message = "Room service menu not yet implemented." });
    }
}

public record CancelOrderRequest(string? Reason = null);
