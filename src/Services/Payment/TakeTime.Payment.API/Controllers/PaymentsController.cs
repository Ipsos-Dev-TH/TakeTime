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
}
