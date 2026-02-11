using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.CRM.Application.Commands;
using TakeTime.CRM.Application.DTOs;

namespace TakeTime.CRM.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;

    public LoyaltyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("earn")]
    public async Task<ActionResult<LoyaltyDto>> Earn(
        [FromBody] AdjustLoyaltyPointsCommand command, CancellationToken ct)
    {
        command.TransactionType = "Earn";
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpPost("redeem")]
    public async Task<ActionResult<LoyaltyDto>> Redeem(
        [FromBody] AdjustLoyaltyPointsCommand command, CancellationToken ct)
    {
        command.TransactionType = "Redeem";
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
