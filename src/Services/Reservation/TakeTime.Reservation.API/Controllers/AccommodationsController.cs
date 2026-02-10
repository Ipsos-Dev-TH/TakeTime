using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Queries;

namespace TakeTime.Reservation.API.Controllers;

/// <summary>
/// REST API controller for managing accommodation units (rooms, villas, tents, etc.).
/// Provides CRUD operations and availability checking.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
[Produces("application/json")]
public class AccommodationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<AccommodationsController> _logger;

    public AccommodationsController(IMediator mediator, ILogger<AccommodationsController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get all accommodations with optional filtering by type and status.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<AccommodationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? minOccupancy,
        CancellationToken cancellationToken)
    {
        // TODO: Implement GetAllAccommodationsQuery
        // For now, return empty list as placeholder
        _logger.LogDebug("Getting all accommodations. Type: {Type}, Status: {Status}", type, status);
        return Ok(new List<AccommodationDto>());
    }

    /// <summary>
    /// Get a specific accommodation by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting accommodation {AccommodationId}", id);

        // TODO: Implement GetAccommodationByIdQuery
        return NotFound(new { message = $"Accommodation with ID '{id}' not found." });
    }

    /// <summary>
    /// Create a new accommodation.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccommodationDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating accommodation: {Name}, Type: {Type}", dto.Name, dto.AccommodationType);

        // TODO: Implement CreateAccommodationCommand
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Create accommodation is not yet implemented." });
    }

    /// <summary>
    /// Update an existing accommodation.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAccommodationDto dto,
        CancellationToken cancellationToken)
    {
        dto.Id = id;
        _logger.LogInformation("Updating accommodation {AccommodationId}", id);

        // TODO: Implement UpdateAccommodationCommand
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Update accommodation is not yet implemented." });
    }

    /// <summary>
    /// Delete (soft-delete) an accommodation.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deleting accommodation {AccommodationId}", id);

        // TODO: Implement DeleteAccommodationCommand
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Delete accommodation is not yet implemented." });
    }

    /// <summary>
    /// Check availability of accommodations for a specific date range.
    /// </summary>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(List<AccommodationAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] int? guests,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        if (checkIn >= checkOut)
            return BadRequest(new { message = "Check-in date must be before check-out date." });

        if (checkIn.Date < DateTime.UtcNow.Date)
            return BadRequest(new { message = "Check-in date cannot be in the past." });

        var query = new GetAvailableAccommodationsQuery
        {
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            Guests = guests,
            AccommodationType = type
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update the operational status of an accommodation (e.g., Available, Maintenance, Cleaning).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating accommodation {AccommodationId} status to {Status}", id, request.Status);

        // TODO: Implement UpdateAccommodationStatusCommand
        return StatusCode(StatusCodes.Status501NotImplemented,
            new { message = "Update accommodation status is not yet implemented." });
    }
}

/// <summary>
/// Request model for updating accommodation status.
/// </summary>
public sealed class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
