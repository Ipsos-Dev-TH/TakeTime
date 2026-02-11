using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/room-service")]
public class RoomServiceController : ControllerBase
{
    private readonly IMediator _mediator;

    public RoomServiceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomServiceOrderCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Create), result);
    }
}
