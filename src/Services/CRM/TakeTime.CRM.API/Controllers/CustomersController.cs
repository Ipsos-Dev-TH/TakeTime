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

    /// <summary>Search customers with filtering.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CustomerDto>>> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? tier,
        [FromQuery] string? source,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new SearchCustomersQuery
        {
            Keyword = keyword,
            Tier = tier,
            Source = source,
            Page = page,
            PageSize = pageSize
        }, ct);

        return Ok(result);
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

    /// <summary>Deletes a customer record (soft-delete).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteCustomerCommand
        {
            CustomerId = id
        }, ct);

        return result ? NoContent() : NotFound();
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<CustomerAnalyticsDto>> Analytics(
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerAnalyticsQuery { FromDate = from, ToDate = to }, ct);
        return Ok(result);
    }

    /// <summary>Gets reservation history for a customer.</summary>
    [HttpGet("{id:guid}/reservations")]
    public async Task<ActionResult<CustomerReservationHistoryDto>> GetReservationHistory(
        Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerReservationsQuery
        {
            CustomerId = id
        }, ct);

        return Ok(result);
    }

    /// <summary>Gets a customer's loyalty point balance and tier.</summary>
    [HttpGet("{id:guid}/loyalty")]
    public async Task<IActionResult> GetLoyaltyStatus(Guid id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetCustomerLoyaltyQuery
        {
            CustomerId = id
        }, ct);

        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>Merges duplicate customer records.</summary>
    [HttpPost("merge")]
    public async Task<ActionResult<CustomerDto>> MergeCustomers(
        [FromBody] MergeCustomersRequest request, CancellationToken ct)
    {
        var result = await _mediator.Send(new MergeCustomersCommand
        {
            PrimaryCustomerId = request.PrimaryCustomerId,
            DuplicateCustomerIds = request.DuplicateCustomerIds
        }, ct);

        return Ok(result);
    }

    /// <summary>Exports customer data.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "csv",
        [FromQuery] string? tier = null,
        [FromQuery] string? source = null,
        CancellationToken ct = default)
    {
        var result = await _mediator.Send(new ExportCustomersQuery
        {
            Format = format,
            Tier = tier,
            Source = source
        }, ct);

        return File(result.Data, result.ContentType, result.FileName);
    }
}

public record MergeCustomersRequest(Guid PrimaryCustomerId, List<Guid> DuplicateCustomerIds);
