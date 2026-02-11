using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.HumanResources.Application.Commands;
using TakeTime.HumanResources.Application.DTOs;

namespace TakeTime.HumanResources.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PayrollController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<List<PayrollDto>>> Calculate(
        [FromBody] CalculatePayrollCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }
}
