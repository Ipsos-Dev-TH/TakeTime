using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.HumanResources.Application.Commands;
using TakeTime.HumanResources.Application.DTOs;
using TakeTime.HumanResources.Application.Queries;

namespace TakeTime.HumanResources.API.Controllers;

/// <summary>
/// REST API controller for managing overtime records.
/// Provides creation, approval, and querying of employee overtime.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class OvertimeController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<OvertimeController> _logger;

    public OvertimeController(IMediator mediator, ILogger<OvertimeController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new overtime record for an employee.
    /// </summary>
    /// <param name="command">The overtime record data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created overtime record.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(OvertimeDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<OvertimeDto>> Create(
        [FromBody] CreateOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating overtime record for employee {EmployeeId} on {Date}",
            command.EmployeeId, command.Date);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByEmployee), new { employeeId = result.EmployeeId }, result);
    }

    /// <summary>
    /// Get overtime records for a specific employee.
    /// </summary>
    /// <param name="employeeId">The employee identifier.</param>
    /// <param name="fromDate">Optional start date filter.</param>
    /// <param name="toDate">Optional end date filter.</param>
    /// <param name="status">Optional status filter (Pending, Approved, Rejected).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of overtime records for the employee.</returns>
    [HttpGet("employee/{employeeId:guid}")]
    [ProducesResponseType(typeof(List<OvertimeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OvertimeDto>>> GetByEmployee(
        Guid employeeId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Getting overtime records for employee {EmployeeId}, From: {FromDate}, To: {ToDate}, Status: {Status}",
            employeeId, fromDate, toDate, status);

        var result = await _mediator.Send(new GetEmployeeOvertimeQuery
        {
            EmployeeId = employeeId,
            FromDate = fromDate,
            ToDate = toDate,
            Status = status
        }, cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Approve an overtime record.
    /// </summary>
    /// <param name="id">The overtime record identifier.</param>
    /// <param name="command">The approval data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated overtime record.</returns>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(OvertimeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OvertimeDto>> Approve(
        Guid id,
        [FromBody] ApproveOvertimeCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Approving overtime record {OvertimeRecordId}, IsApproved: {IsApproved}",
            id, command.IsApproved);

        command.OvertimeRecordId = id;
        command.IsApproved = true;
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get all pending overtime records awaiting approval.
    /// </summary>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of pending overtime records.</returns>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(List<OvertimeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<OvertimeDto>>> GetPending(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting pending overtime records, Page: {Page}, PageSize: {PageSize}", page, pageSize);

        var result = await _mediator.Send(new GetPendingOvertimeQuery
        {
            Page = page,
            PageSize = pageSize
        }, cancellationToken);

        return Ok(result);
    }
}
