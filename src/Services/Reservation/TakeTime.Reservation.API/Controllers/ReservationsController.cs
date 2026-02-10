using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Reservation.Application.Commands;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Queries;

namespace TakeTime.Reservation.API.Controllers;

/// <summary>
/// REST API controller for managing reservations.
/// Provides endpoints for the full reservation lifecycle including creation,
/// confirmation, check-in, check-out, cancellation, and search.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class ReservationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ReservationsController> _logger;

    public ReservationsController(IMediator mediator, ILogger<ReservationsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Search reservations with filtering, pagination, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResult<ReservationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] ReservationSearchCriteria criteria,
        CancellationToken cancellationToken)
    {
        var query = new SearchReservationsQuery(criteria);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a reservation by ID with full details.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetReservationByIdQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
            return NotFound(new { message = $"Reservation with ID '{id}' not found." });

        return Ok(result);
    }

    /// <summary>
    /// Create a new reservation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateReservationDto dto,
        CancellationToken cancellationToken)
    {
        var command = new CreateReservationCommand
        {
            CustomerId = dto.CustomerId,
            CustomerName = dto.CustomerName,
            CustomerEmail = dto.CustomerEmail,
            CustomerPhone = dto.CustomerPhone,
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            NumberOfGuests = dto.NumberOfGuests,
            NumberOfAdults = dto.NumberOfAdults,
            NumberOfChildren = dto.NumberOfChildren,
            Source = dto.Source,
            SpecialRequests = dto.SpecialRequests,
            InternalNotes = dto.InternalNotes,
            DiscountAmount = dto.DiscountAmount,
            DiscountReason = dto.DiscountReason,
            Accommodations = dto.Accommodations,
            Items = dto.Items,
            CreatedBy = User.Identity?.Name
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Reservation {ReservationNumber} created successfully",
            result.ReservationNumber);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update an existing reservation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateReservationDto dto,
        CancellationToken cancellationToken)
    {
        dto.Id = id;

        // TODO: Implement UpdateReservationCommand when available
        // For now, return method not allowed until the command handler is implemented
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Update reservation is not yet implemented." });
    }

    /// <summary>
    /// Confirm a pending reservation.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Confirm(
        Guid id,
        CancellationToken cancellationToken)
    {
        var command = new ConfirmReservationCommand
        {
            ReservationId = id,
            ConfirmedBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Reservation {ReservationId} confirmed", id);

        return Ok(new { message = "Reservation confirmed successfully." });
    }

    /// <summary>
    /// Cancel a reservation.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelReservationRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CancelReservationCommand
        {
            ReservationId = id,
            Reason = request.Reason,
            CancelledBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Reservation {ReservationId} cancelled. Reason: {Reason}", id, request.Reason);

        return Ok(new { message = "Reservation cancelled successfully." });
    }

    /// <summary>
    /// Check in a guest for a confirmed reservation.
    /// </summary>
    [HttpPost("{id:guid}/check-in")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(
        Guid id,
        [FromBody] CheckInRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CheckInCommand
        {
            ReservationId = id,
            ActualGuests = request?.ActualGuests,
            Notes = request?.Notes,
            CheckedInBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Guest checked in for reservation {ReservationId}", id);

        return Ok(new { message = "Guest checked in successfully." });
    }

    /// <summary>
    /// Check out a guest from a checked-in reservation.
    /// </summary>
    [HttpPost("{id:guid}/check-out")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckOut(
        Guid id,
        [FromBody] CheckOutRequest? request,
        CancellationToken cancellationToken)
    {
        var command = new CheckOutCommand
        {
            ReservationId = id,
            Notes = request?.Notes,
            AdditionalItems = request?.AdditionalItems,
            CheckedOutBy = User.Identity?.Name
        };

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Guest checked out for reservation {ReservationId}", id);

        return Ok(new { message = "Guest checked out successfully." });
    }

    /// <summary>
    /// Get today's expected check-ins.
    /// </summary>
    [HttpGet("today/check-ins")]
    [ProducesResponseType(typeof(List<ReservationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodayCheckIns(CancellationToken cancellationToken)
    {
        var query = new GetTodayCheckInsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get today's expected check-outs.
    /// </summary>
    [HttpGet("today/check-outs")]
    [ProducesResponseType(typeof(List<ReservationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodayCheckOuts(CancellationToken cancellationToken)
    {
        // Reuse the check-ins query with a different date filter
        var criteria = new ReservationSearchCriteria
        {
            CheckOutDateFrom = DateTime.UtcNow.Date,
            CheckOutDateTo = DateTime.UtcNow.Date.AddDays(1),
            Statuses = ["CheckedIn"],
            PageSize = 100
        };
        var result = await _mediator.Send(new SearchReservationsQuery(criteria), cancellationToken);
        return Ok(result.Items);
    }

    /// <summary>
    /// Get the operational dashboard summary.
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] DateTime? date,
        [FromQuery] int upcomingDays = 7,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDashboardSummaryQuery
        {
            Date = date,
            UpcomingDays = upcomingDays
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}

/// <summary>
/// Request model for cancelling a reservation.
/// </summary>
public sealed class CancelReservationRequest
{
    public string? Reason { get; set; }
}

/// <summary>
/// Request model for checking in a guest.
/// </summary>
public sealed class CheckInRequest
{
    public int? ActualGuests { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for checking out a guest.
/// </summary>
public sealed class CheckOutRequest
{
    public string? Notes { get; set; }
    public List<CheckOutAdditionalItem>? AdditionalItems { get; set; }
}
