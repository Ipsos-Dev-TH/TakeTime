using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.GuestExperience.Application.Queries;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

/// <summary>
/// REST API controller for managing housekeeping tasks.
/// Provides task creation, assignment, status tracking, and daily schedule management.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "Housekeeping")]
[Produces("application/json")]
public class HousekeepingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<HousekeepingController> _logger;

    public HousekeepingController(IMediator mediator, ILogger<HousekeepingController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new housekeeping task for a room.
    /// </summary>
    /// <param name="command">The housekeeping task creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created housekeeping task.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateHousekeepingTaskCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating housekeeping task for room {RoomNumber}, type: {TaskType}, priority: {Priority}",
            command.RoomNumber, command.TaskType, command.Priority);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// List housekeeping tasks with optional filtering by status and date.
    /// </summary>
    /// <param name="status">Filter by task status (Pending, Assigned, InProgress, Completed, Verified).</param>
    /// <param name="date">Filter by scheduled date.</param>
    /// <param name="priority">Filter by priority (Low, Normal, High, Urgent).</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of housekeeping tasks.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<HousekeepingTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] DateTime? date,
        [FromQuery] string? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing housekeeping tasks. Status: {Status}, Date: {Date}, Priority: {Priority}",
            status, date, priority);

        var query = new GetHousekeepingTasksQuery
        {
            Status = status,
            Date = date,
            Priority = priority,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific housekeeping task by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the housekeeping task.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The housekeeping task details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting housekeeping task {TaskId}", id);

        var query = new GetHousekeepingTaskByIdQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Assign a housekeeping task to a staff member.
    /// </summary>
    /// <param name="id">The unique identifier of the task to assign.</param>
    /// <param name="request">The assignment details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated housekeeping task.</returns>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignHousekeepingTaskRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Assigning housekeeping task {TaskId} to staff {StaffId}",
            id, request.AssignedTo);

        var command = new AssignHousekeepingTaskCommand
        {
            TaskId = id,
            AssignedTo = request.AssignedTo,
            AssignedToName = request.AssignedToName
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark a housekeeping task as started (in progress).
    /// </summary>
    /// <param name="id">The unique identifier of the task to start.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated housekeeping task.</returns>
    [HttpPut("{id:guid}/start")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting housekeeping task {TaskId}", id);

        var command = new StartHousekeepingTaskCommand { TaskId = id };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Mark a housekeeping task as completed.
    /// </summary>
    /// <param name="id">The unique identifier of the task to complete.</param>
    /// <param name="request">Completion details including optional notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated housekeeping task.</returns>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteHousekeepingTaskRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing housekeeping task {TaskId}", id);

        var command = new CompleteHousekeepingTaskCommand
        {
            TaskId = id,
            Notes = request.Notes,
            CompletedBy = request.CompletedBy
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Verify a completed housekeeping task (inspection by supervisor).
    /// </summary>
    /// <param name="id">The unique identifier of the task to verify.</param>
    /// <param name="request">Verification details including inspection rating and notes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated housekeeping task.</returns>
    [HttpPut("{id:guid}/verify")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Verify(
        Guid id,
        [FromBody] VerifyHousekeepingTaskRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Verifying housekeeping task {TaskId}, rating: {Rating}",
            id, request.InspectionRating);

        var command = new VerifyHousekeepingTaskCommand
        {
            TaskId = id,
            InspectionRating = request.InspectionRating,
            InspectionNotes = request.InspectionNotes
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get today's housekeeping schedule with summary statistics.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Today's housekeeping dashboard with task list and room status counts.</returns>
    [HttpGet("today")]
    [ProducesResponseType(typeof(HousekeepingDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodaySchedule(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting today's housekeeping schedule");

        var query = new GetTodayHousekeepingScheduleQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

/// <summary>
/// Request model for assigning a housekeeping task to a staff member.
/// </summary>
public sealed class AssignHousekeepingTaskRequest
{
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
}

/// <summary>
/// Request model for completing a housekeeping task.
/// </summary>
public sealed class CompleteHousekeepingTaskRequest
{
    public string? Notes { get; set; }
    public string? CompletedBy { get; set; }
}

/// <summary>
/// Request model for verifying (inspecting) a completed housekeeping task.
/// </summary>
public sealed class VerifyHousekeepingTaskRequest
{
    public int InspectionRating { get; set; }
    public string? InspectionNotes { get; set; }
}
