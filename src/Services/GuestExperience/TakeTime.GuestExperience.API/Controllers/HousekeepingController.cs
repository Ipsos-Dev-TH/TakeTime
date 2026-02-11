using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "Housekeeping")]
public class HousekeepingController : ControllerBase
{
    private readonly IMediator _mediator;

    public HousekeepingController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Creates a new housekeeping task.</summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateHousekeepingTaskCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return CreatedAtAction(nameof(Create), result);
    }

    /// <summary>Lists housekeeping tasks with filtering.</summary>
    [HttpGet]
    public IActionResult Search(
        [FromQuery] string? status, [FromQuery] string? assignedTo,
        [FromQuery] DateTime? date, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        // TODO: Implement GetHousekeepingTasksQuery
        return StatusCode(501, new { message = "List housekeeping tasks not yet implemented." });
    }

    /// <summary>Gets a housekeeping task by ID.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return StatusCode(501, new { message = "Get task by ID not yet implemented." });
    }

    /// <summary>Assigns a task to a staff member.</summary>
    [HttpPut("{id:guid}/assign")]
    public IActionResult Assign(Guid id, [FromBody] AssignTaskRequest request)
    {
        return StatusCode(501, new { message = "Assign task not yet implemented." });
    }

    /// <summary>Marks a task as started.</summary>
    [HttpPut("{id:guid}/start")]
    public IActionResult Start(Guid id)
    {
        return StatusCode(501, new { message = "Start task not yet implemented." });
    }

    /// <summary>Marks a task as completed.</summary>
    [HttpPut("{id:guid}/complete")]
    public IActionResult Complete(Guid id)
    {
        return StatusCode(501, new { message = "Complete task not yet implemented." });
    }

    /// <summary>Verifies a completed task (supervisor approval).</summary>
    [HttpPut("{id:guid}/verify")]
    public IActionResult Verify(Guid id, [FromBody] VerifyTaskRequest request)
    {
        return StatusCode(501, new { message = "Verify task not yet implemented." });
    }

    /// <summary>Gets today's housekeeping schedule.</summary>
    [HttpGet("today")]
    public IActionResult GetTodaySchedule()
    {
        return StatusCode(501, new { message = "Today's schedule not yet implemented." });
    }

    /// <summary>Gets housekeeping status summary by room.</summary>
    [HttpGet("room-status")]
    public IActionResult GetRoomStatus()
    {
        return StatusCode(501, new { message = "Room status not yet implemented." });
    }
}

public record AssignTaskRequest(Guid StaffId, string? Notes = null);
public record VerifyTaskRequest(bool Approved, string? Notes = null);
