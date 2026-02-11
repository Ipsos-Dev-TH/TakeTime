using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Application.Queries;

namespace TakeTime.Payment.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PaymentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentDto>> ProcessPayment(
        [FromBody] ProcessPaymentCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(ProcessPayment), new { id = result.Id }, result);
    }

    [HttpPost("{id:guid}/verify")]
    public async Task<ActionResult<PaymentDto>> VerifyPayment(
        Guid id, [FromBody] VerifyPaymentCommand command, CancellationToken ct)
    {
        command.PaymentId = id;
        var result = await _mediator.Send(command, ct);
        return Ok(result);
    }

    [HttpGet("reservation/{reservationId:guid}")]
    public async Task<ActionResult<List<PaymentDto>>> GetByReservation(
        Guid reservationId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentsByReservationQuery { ReservationId = reservationId }, ct);
        return Ok(result);
    }

    [HttpGet("reservation/{reservationId:guid}/balance")]
    public async Task<ActionResult<ReservationBalanceDto>> GetBalance(
        Guid reservationId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetReservationBalanceQuery { ReservationId = reservationId }, ct);
        return Ok(result);
    }

    /// <summary>Gets a payment by ID.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        // TODO: Implement GetPaymentByIdQuery
        return StatusCode(501, new { message = "Get payment by ID not yet implemented." });
    }

    /// <summary>Searches payments with filtering.</summary>
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? status, [FromQuery] string? method,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // TODO: Implement SearchPaymentsQuery
        return StatusCode(501, new { message = "Search payments not yet implemented." });
    }

    /// <summary>Refunds a payment.</summary>
    [HttpPost("{id:guid}/refund")]
    public IActionResult Refund(Guid id, [FromBody] RefundRequest request)
    {
        // TODO: Implement RefundPaymentCommand
        return StatusCode(501, new { message = "Refund not yet implemented." });
    }

    /// <summary>Voids a payment.</summary>
    [HttpPost("{id:guid}/void")]
    public IActionResult VoidPayment(Guid id)
    {
        // TODO: Implement VoidPaymentCommand
        return StatusCode(501, new { message = "Void payment not yet implemented." });
    }

    /// <summary>Gets daily payment summary.</summary>
    [HttpGet("summary/daily")]
    public IActionResult GetDailySummary([FromQuery] DateTime? date)
    {
        // TODO: Implement GetDailyPaymentSummaryQuery
        return StatusCode(501, new { message = "Daily payment summary not yet implemented." });
    }
}

public record RefundRequest(decimal? Amount, string? Reason);
