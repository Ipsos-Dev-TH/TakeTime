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
    /// <param name="type">Filter by accommodation type (e.g., Room, Villa, Tent).</param>
    /// <param name="status">Filter by status (e.g., Available, Maintenance, Cleaning).</param>
    /// <param name="minOccupancy">Filter by minimum guest occupancy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of accommodations matching the filter criteria.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<AccommodationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int? minOccupancy,
        CancellationToken cancellationToken)
    {
        // TODO: Implement GetAllAccommodationsQuery when available
        // Should support filtering by type, status, and minimum occupancy
        // Should return paginated results with sorting options
        _logger.LogDebug("Getting all accommodations. Type: {Type}, Status: {Status}, MinOccupancy: {MinOccupancy}",
            type, status, minOccupancy);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "List accommodations is not yet implemented. Awaiting GetAllAccommodationsQuery."
        });
    }

    /// <summary>
    /// Get a specific accommodation by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the accommodation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The accommodation details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement GetAccommodationByIdQuery when available
        // Should return full accommodation details including amenities, images, and pricing
        _logger.LogDebug("Getting accommodation {AccommodationId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Get accommodation by ID is not yet implemented. Awaiting GetAccommodationByIdQuery."
        });
    }

    /// <summary>
    /// Create a new accommodation.
    /// </summary>
    /// <param name="dto">The accommodation creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created accommodation.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAccommodationDto dto,
        CancellationToken cancellationToken)
    {
        // TODO: Implement CreateAccommodationCommand when available
        // Should validate room number uniqueness, set default status to Available,
        // and apply tenant-specific currency defaults
        _logger.LogInformation("Creating accommodation: {Name}, Type: {Type}", dto.Name, dto.AccommodationType);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Create accommodation is not yet implemented. Awaiting CreateAccommodationCommand."
        });
    }

    /// <summary>
    /// Update an existing accommodation.
    /// </summary>
    /// <param name="id">The unique identifier of the accommodation to update.</param>
    /// <param name="dto">The accommodation update data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated accommodation.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccommodationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAccommodationDto dto,
        CancellationToken cancellationToken)
    {
        // TODO: Implement UpdateAccommodationCommand when available
        // Should support partial updates, validate room number uniqueness on change,
        // and preserve audit trail
        dto.Id = id;
        _logger.LogInformation("Updating accommodation {AccommodationId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Update accommodation is not yet implemented. Awaiting UpdateAccommodationCommand."
        });
    }

    /// <summary>
    /// Delete (soft-delete) an accommodation.
    /// </summary>
    /// <param name="id">The unique identifier of the accommodation to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement DeleteAccommodationCommand when available
        // Should perform soft-delete (set IsActive = false)
        // Should check for active reservations before allowing deletion
        _logger.LogInformation("Deleting accommodation {AccommodationId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Delete accommodation is not yet implemented. Awaiting DeleteAccommodationCommand."
        });
    }

    /// <summary>
    /// Check availability of accommodations for a specific date range.
    /// Returns available accommodations with calculated rates based on tenant pricing rules.
    /// </summary>
    /// <param name="checkIn">The desired check-in date.</param>
    /// <param name="checkOut">The desired check-out date.</param>
    /// <param name="guests">Minimum number of guests the accommodation must support.</param>
    /// <param name="type">Filter by accommodation type (e.g., Room, Villa, Tent).</param>
    /// <param name="maxRate">Maximum rate per night to filter by.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available accommodations with pricing information.</returns>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(List<AccommodationAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] DateTime checkIn,
        [FromQuery] DateTime checkOut,
        [FromQuery] int? guests,
        [FromQuery] string? type,
        [FromQuery] decimal? maxRate,
        CancellationToken cancellationToken)
    {
        if (checkIn >= checkOut)
        {
            return BadRequest(new { message = "Check-in date must be before check-out date." });
        }

        if (checkIn.Date < DateTime.UtcNow.Date)
        {
            return BadRequest(new { message = "Check-in date cannot be in the past." });
        }

        _logger.LogDebug(
            "Checking availability from {CheckIn} to {CheckOut}, guests: {Guests}, type: {Type}",
            checkIn, checkOut, guests, type);

        var query = new GetAvailableAccommodationsQuery
        {
            CheckInDate = checkIn,
            CheckOutDate = checkOut,
            MinOccupancy = guests,
            AccommodationType = type,
            MaxRatePerNight = maxRate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Update the operational status of an accommodation (e.g., Available, Maintenance, Cleaning).
    /// </summary>
    /// <param name="id">The unique identifier of the accommodation.</param>
    /// <param name="request">The new status value.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success confirmation.</returns>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement UpdateAccommodationStatusCommand when available
        // Should validate status transitions (e.g., cannot go from Maintenance to Occupied directly)
        // Should integrate with housekeeping workflows
        _logger.LogInformation("Updating accommodation {AccommodationId} status to {Status}", id, request.Status);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Update accommodation status is not yet implemented. Awaiting UpdateAccommodationStatusCommand."
        });
    }
}

/// <summary>
/// Request model for updating accommodation status.
/// </summary>
public sealed class UpdateStatusRequest
{
    public string Status { get; set; } = string.Empty;
}
