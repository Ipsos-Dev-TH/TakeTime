using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.CRM.Application.Commands;
using TakeTime.CRM.Application.DTOs;
using TakeTime.CRM.Application.Queries;

namespace TakeTime.CRM.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly IMediator _mediator;

    public CustomersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> GetProfile(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerProfileQuery { CustomerId = id }, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Upsert(
        [FromBody] UpsertCustomerCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<CustomerAnalyticsDto>> Analytics(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerAnalyticsQuery { FromDate = from, ToDate = to }, ct);
        return Ok(result);
    }
}
