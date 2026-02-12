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
    public async Task<ActionResult<PaymentDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetPaymentByIdQuery { PaymentId = id }, ct);
        return Ok(result);
    }

    /// <summary>Searches payments with filtering.</summary>
    [HttpGet]
    public async Task<ActionResult<PaginatedPaymentResult>> Search(
        [FromQuery] string? status, [FromQuery] string? method,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchPaymentsQuery
        {
            Status = status,
            PaymentMethod = method,
            FromDate = from,
            ToDate = to,
            Page = page,
            PageSize = pageSize
        }, ct);
        return Ok(result);
    }

    /// <summary>Refunds a payment.</summary>
    [HttpPost("{id:guid}/refund")]
    public async Task<ActionResult<PaymentDto>> Refund(
        Guid id, [FromBody] RefundRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new RefundPaymentCommand
        {
            PaymentId = id,
            Amount = request.Amount,
            Reason = request.Reason
        }, ct);
        return Ok(result);
    }

    /// <summary>Voids a payment.</summary>
    [HttpPost("{id:guid}/void")]
    public async Task<ActionResult<PaymentDto>> VoidPayment(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new VoidPaymentCommand { PaymentId = id }, ct);
        return Ok(result);
    }

    /// <summary>Gets daily payment summary.</summary>
    [HttpGet("summary/daily")]
    public async Task<ActionResult<DailyPaymentSummaryDto>> GetDailySummary(
        [FromQuery] DateTime? date, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetDailyPaymentSummaryQuery { Date = date }, ct);
        return Ok(result);
    }
}

public record RefundRequest(decimal? Amount, string? Reason);
