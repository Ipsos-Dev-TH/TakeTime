using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.GuestExperience.Application.Queries;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

/// <summary>
/// REST API controller for managing maintenance requests.
/// Provides issue reporting, assignment, status tracking, and work completion management.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "MaintenanceTracking")]
[Produces("application/json")]
public class MaintenanceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<MaintenanceController> _logger;

    public MaintenanceController(IMediator mediator, ILogger<MaintenanceController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Report a new maintenance issue for a room or facility.
    /// Generates a unique request number and optionally assigns to maintenance staff.
    /// </summary>
    /// <param name="command">The maintenance request creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created maintenance request.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Report(
        [FromBody] CreateMaintenanceRequestCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Reporting maintenance issue for room {RoomNumber}, category: {Category}, priority: {Priority}",
            command.RoomNumber, command.Category, command.Priority);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// List maintenance requests with optional filtering by status and priority.
    /// </summary>
    /// <param name="status">Filter by request status (Open, Assigned, InProgress, Completed).</param>
    /// <param name="priority">Filter by priority (Low, Normal, High, Urgent).</param>
    /// <param name="category">Filter by maintenance category.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of maintenance requests.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<MaintenanceRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] string? category,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing maintenance requests. Status: {Status}, Priority: {Priority}, Category: {Category}",
            status, priority, category);

        var query = new GetMaintenanceRequestsQuery
        {
            Status = status,
            Priority = priority,
            Category = category,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a specific maintenance request by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the maintenance request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The maintenance request details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting maintenance request {RequestId}", id);

        var query = new GetMaintenanceRequestByIdQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Assign a maintenance request to a technician.
    /// </summary>
    /// <param name="id">The unique identifier of the request to assign.</param>
    /// <param name="request">The assignment details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated maintenance request.</returns>
    [HttpPut("{id:guid}/assign")]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Assign(
        Guid id,
        [FromBody] AssignMaintenanceRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Assigning maintenance request {RequestId} to technician {TechnicianId}",
            id, request.AssignedTo);

        var command = new AssignMaintenanceRequestCommand
        {
            RequestId = id,
            AssignedTo = request.AssignedTo,
            AssignedToName = request.AssignedToName
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Start work on a maintenance request.
    /// </summary>
    /// <param name="id">The unique identifier of the request to start.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated maintenance request.</returns>
    [HttpPut("{id:guid}/start")]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting work on maintenance request {RequestId}", id);

        var command = new StartMaintenanceWorkCommand { RequestId = id };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Complete work on a maintenance request.
    /// </summary>
    /// <param name="id">The unique identifier of the request to complete.</param>
    /// <param name="request">Completion details including resolution notes and actual cost.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated maintenance request.</returns>
    [HttpPut("{id:guid}/complete")]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteMaintenanceRequestDto request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing maintenance request {RequestId}", id);

        var command = new CompleteMaintenanceWorkCommand
        {
            RequestId = id,
            ResolutionNotes = request.ResolutionNotes,
            ActualCost = request.ActualCost
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all pending (open and unassigned) maintenance requests.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of pending maintenance requests.</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<MaintenanceRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting pending maintenance requests");

        var query = new GetPendingMaintenanceRequestsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

/// <summary>
/// Request model for assigning a maintenance request to a technician.
/// </summary>
public sealed class AssignMaintenanceRequestDto
{
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
}

/// <summary>
/// Request model for completing a maintenance request.
/// </summary>
public sealed class CompleteMaintenanceRequestDto
{
    public string? ResolutionNotes { get; set; }
    public decimal? ActualCost { get; set; }
}
