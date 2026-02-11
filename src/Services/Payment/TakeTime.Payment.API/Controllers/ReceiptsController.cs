using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ReceiptsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ReceiptsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<ReceiptDto>> GenerateReceipt(
        [FromBody] GenerateReceiptCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GenerateReceipt), new { id = result.Id }, result);
    }

    [HttpPost("tax-invoice")]
    public async Task<ActionResult<TaxInvoiceDto>> GenerateTaxInvoice(
        [FromBody] GenerateTaxInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(GenerateTaxInvoice), new { id = result.Id }, result);
    }
}
