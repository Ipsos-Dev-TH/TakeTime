using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;
using TakeTime.Inventory.Application.Queries;

namespace TakeTime.Inventory.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;

    public SalesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<SalesTransactionDto>> CreateTransaction(
        [FromBody] CreateSalesTransactionCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(CreateTransaction), new { id = result.Id }, result);
    }

    [HttpGet("report")]
    public async Task<IActionResult> GetSalesReport(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetSalesReportQuery
        {
            FromDate = from ?? DateTime.Today.AddMonths(-1),
            ToDate = to ?? DateTime.Today
        }, ct);
        return Ok(result);
    }
}
