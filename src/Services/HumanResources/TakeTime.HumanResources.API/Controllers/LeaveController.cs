using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.HumanResources.Application.Commands;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LeaveController : ControllerBase
{
    private readonly IMediator _mediator;

    public LeaveController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<LeaveRequestDto>> Submit(
        [FromBody] SubmitLeaveRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Submit), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<LeaveRequestDto>> Approve(
        Guid id, [FromBody] ApproveLeaveRequestCommand command, CancellationToken ct)
    {
        command.LeaveRequestId = id;
        command.IsApproved = true;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<LeaveRequestDto>> Reject(
        Guid id, [FromBody] ApproveLeaveRequestCommand command, CancellationToken ct)
    {
        command.LeaveRequestId = id;
        command.IsApproved = false;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
