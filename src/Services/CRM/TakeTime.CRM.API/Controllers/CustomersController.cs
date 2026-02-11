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

    /// <summary>Search customers with filtering.</summary>
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? keyword, [FromQuery] string? tier,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // TODO: Implement SearchCustomersQuery
        return StatusCode(501, new { message = "Customer search not yet implemented." });
    }

    /// <summary>Deletes a customer record.</summary>
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        // TODO: Implement DeleteCustomerCommand
        return StatusCode(501, new { message = "Delete customer not yet implemented." });
    }

    /// <summary>Gets reservation history for a customer.</summary>
    [HttpGet("{id:guid}/reservations")]
    public IActionResult GetReservationHistory(Guid id)
    {
        // TODO: Implement GetCustomerReservationsQuery
        return StatusCode(501, new { message = "Customer reservation history not yet implemented." });
    }

    /// <summary>Gets a customer's loyalty point balance and tier.</summary>
    [HttpGet("{id:guid}/loyalty")]
    public IActionResult GetLoyaltyStatus(Guid id)
    {
        // TODO: Implement GetCustomerLoyaltyQuery
        return StatusCode(501, new { message = "Customer loyalty status not yet implemented." });
    }

    /// <summary>Merges duplicate customer records.</summary>
    [HttpPost("merge")]
    public IActionResult MergeCustomers([FromBody] MergeCustomersRequest request)
    {
        // TODO: Implement MergeCustomersCommand
        return StatusCode(501, new { message = "Merge customers not yet implemented." });
    }

    /// <summary>Exports customer data.</summary>
    [HttpGet("export")]
    public IActionResult Export([FromQuery] string format = "csv")
    {
        // TODO: Implement ExportCustomersQuery
        return StatusCode(501, new { message = "Customer export not yet implemented." });
    }
}

public record MergeCustomersRequest(Guid PrimaryCustomerId, List<Guid> DuplicateCustomerIds);
