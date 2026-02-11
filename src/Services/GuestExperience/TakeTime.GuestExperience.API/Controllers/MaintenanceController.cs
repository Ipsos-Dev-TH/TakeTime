using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "MaintenanceTracking")]
public class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;

    public MaintenanceController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Reports a new maintenance issue.</summary>
    [HttpPost]
    public async Task<IActionResult> Report(
        [FromBody] CreateMaintenanceRequestCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Report), result);
    }

    /// <summary>Lists maintenance requests with filtering.</summary>
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? status, [FromQuery] string? priority,
        [FromQuery] string? assignedTo, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        return StatusCode(501, new { message = "List maintenance requests not yet implemented." });
    }

    /// <summary>Gets a maintenance request by ID.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return StatusCode(501, new { message = "Get request by ID not yet implemented." });
    }

    /// <summary>Assigns a request to a technician.</summary>
    [HttpPut("{id:guid}/assign")]
    public IActionResult Assign(Guid id, [FromBody] AssignMaintenanceRequest request)
    {
        return StatusCode(501, new { message = "Assign request not yet implemented." });
    }

    /// <summary>Starts work on a maintenance request.</summary>
    [HttpPut("{id:guid}/start")]
    public IActionResult Start(Guid id)
    {
        return StatusCode(501, new { message = "Start work not yet implemented." });
    }

    /// <summary>Completes a maintenance request.</summary>
    [HttpPut("{id:guid}/complete")]
    public IActionResult Complete(Guid id, [FromBody] CompleteMaintenanceRequest? request = null)
    {
        return StatusCode(501, new { message = "Complete work not yet implemented." });
    }

    /// <summary>Gets all pending maintenance requests.</summary>
    [HttpGet("pending")]
    public IActionResult GetPending()
    {
        return StatusCode(501, new { message = "Pending requests not yet implemented." });
    }

    /// <summary>Gets maintenance summary/statistics.</summary>
    [HttpGet("summary")]
    public IActionResult GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        return StatusCode(501, new { message = "Maintenance summary not yet implemented." });
    }
}

public record AssignMaintenanceRequest(Guid TechnicianId, string? Notes = null);
public record CompleteMaintenanceRequest(string? ResolutionNotes = null, decimal? Cost = null);
