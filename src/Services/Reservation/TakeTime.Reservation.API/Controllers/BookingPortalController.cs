using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Reservation.Application.Commands;
using TakeTime.Reservation.Application.DTOs;
using TakeTime.Reservation.Application.Queries;

namespace TakeTime.Reservation.API.Controllers;

/// <summary>
/// Public-facing REST API controller for the customer booking portal.
/// Provides endpoints for browsing accommodations, checking availability,
/// viewing rates, and making/managing reservations without authentication.
/// </summary>
[ApiController]
[Route("api/v1/portal")]
[Produces("application/json")]
public class BookingPortalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BookingPortalController> _logger;

    public BookingPortalController(IMediator mediator, ILogger<BookingPortalController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// List available accommodations with photos and amenities for the public booking portal.
    /// </summary>
    /// <param name="type">Optional filter by accommodation type (e.g., "Deluxe", "Suite").</param>
    /// <param name="minOccupancy">Optional minimum occupancy filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of publicly available accommodations.</returns>
    [HttpGet("accommodations")]
    [ProducesResponseType(typeof(List<PublicAccommodationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccommodations(
        [FromQuery] string? type,
        [FromQuery] int? minOccupancy,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Portal: fetching public accommodations, type: {Type}, minOccupancy: {MinOccupancy}",
            type, minOccupancy);

        var query = new GetPublicAccommodationsQuery
        {
            AccommodationType = type,
            MinOccupancy = minOccupancy
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Check availability for a given date range. Returns available accommodations
    /// with calculated rates based on tenant pricing rules.
    /// </summary>
    /// <param name="checkInDate">Desired check-in date.</param>
    /// <param name="checkOutDate">Desired check-out date.</param>
    /// <param name="type">Optional filter by accommodation type.</param>
    /// <param name="minOccupancy">Optional minimum occupancy filter.</param>
    /// <param name="maxRate">Optional maximum rate per night filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available accommodations with pricing.</returns>
    [HttpGet("availability")]
    [ProducesResponseType(typeof(List<AccommodationAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckAvailability(
        [FromQuery] DateTime checkInDate,
        [FromQuery] DateTime checkOutDate,
        [FromQuery] string? type,
        [FromQuery] int? minOccupancy,
        [FromQuery] decimal? maxRate,
        CancellationToken cancellationToken)
    {
        if (checkOutDate <= checkInDate)
        {
            return BadRequest(new { message = "Check-out date must be after check-in date." });
        }

        _logger.LogDebug("Portal: checking availability from {CheckIn} to {CheckOut}",
            checkInDate, checkOutDate);

        var query = new GetAvailableAccommodationsQuery
        {
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            AccommodationType = type,
            MinOccupancy = minOccupancy,
            MaxRatePerNight = maxRate
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get public rates for given dates and optional room type filter.
    /// Returns calculated rates for all accommodations including availability status.
    /// </summary>
    /// <param name="checkInDate">Desired check-in date.</param>
    /// <param name="checkOutDate">Desired check-out date.</param>
    /// <param name="type">Optional filter by accommodation type.</param>
    /// <param name="minOccupancy">Optional minimum occupancy filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of rates for accommodations.</returns>
    [HttpGet("rates")]
    [ProducesResponseType(typeof(List<PublicRateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRates(
        [FromQuery] DateTime checkInDate,
        [FromQuery] DateTime checkOutDate,
        [FromQuery] string? type,
        [FromQuery] int? minOccupancy,
        CancellationToken cancellationToken)
    {
        if (checkOutDate <= checkInDate)
        {
            return BadRequest(new { message = "Check-out date must be after check-in date." });
        }

        _logger.LogDebug("Portal: fetching rates from {CheckIn} to {CheckOut}",
            checkInDate, checkOutDate);

        var query = new GetPublicRatesQuery
        {
            CheckInDate = checkInDate,
            CheckOutDate = checkOutDate,
            AccommodationType = type,
            MinOccupancy = minOccupancy
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Create a new booking as a guest through the public portal.
    /// Returns a confirmation code for self-service lookup and management.
    /// </summary>
    /// <param name="dto">Guest reservation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Booking confirmation with a confirmation code.</returns>
    [HttpPost("reservations")]
    [ProducesResponseType(typeof(GuestReservationConfirmationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReservation(
        [FromBody] CreateGuestReservationDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Portal: creating guest reservation for {GuestName}, {CheckIn} to {CheckOut}",
            dto.GuestName, dto.CheckInDate, dto.CheckOutDate);

        var command = new CreateGuestReservationCommand
        {
            GuestName = dto.GuestName,
            GuestEmail = dto.GuestEmail,
            GuestPhone = dto.GuestPhone,
            CheckInDate = dto.CheckInDate,
            CheckOutDate = dto.CheckOutDate,
            NumberOfGuests = dto.NumberOfGuests,
            NumberOfAdults = dto.NumberOfAdults,
            NumberOfChildren = dto.NumberOfChildren,
            SpecialRequests = dto.SpecialRequests,
            AccommodationIds = dto.AccommodationIds
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Portal: reservation {ConfirmationCode} created for {GuestName}",
            result.ConfirmationCode, dto.GuestName);

        return CreatedAtAction(
            nameof(GetReservationByCode),
            new { confirmationCode = result.ConfirmationCode },
            result);
    }

    /// <summary>
    /// Look up a booking by its confirmation code. Allows guests to view
    /// their reservation details without authentication.
    /// </summary>
    /// <param name="confirmationCode">The confirmation code received at booking time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The guest-safe reservation details.</returns>
    [HttpGet("reservations/{confirmationCode}")]
    [ProducesResponseType(typeof(GuestReservationLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReservationByCode(
        string confirmationCode,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Portal: looking up reservation by code {Code}", confirmationCode);

        var query = new GetReservationByCodeQuery
        {
            ConfirmationCode = confirmationCode
        };

        var result = await _mediator.Send(query, cancellationToken);

        if (result is null)
        {
            return NotFound(new { message = $"Reservation with confirmation code '{confirmationCode}' not found." });
        }

        return Ok(result);
    }

    /// <summary>
    /// Cancel a booking by the guest using their confirmation code.
    /// The tenant's cancellation policy is applied automatically.
    /// </summary>
    /// <param name="confirmationCode">The confirmation code of the reservation to cancel.</param>
    /// <param name="dto">Cancellation details including optional email verification and reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated reservation details showing cancelled status.</returns>
    [HttpPost("reservations/{confirmationCode}/cancel")]
    [ProducesResponseType(typeof(GuestReservationLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CancelReservation(
        string confirmationCode,
        [FromBody] CancelGuestReservationDto? dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Portal: guest cancellation request for {Code}", confirmationCode);

        var command = new CancelGuestReservationCommand
        {
            ConfirmationCode = confirmationCode,
            GuestEmail = dto?.GuestEmail,
            Reason = dto?.Reason
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Portal: reservation {Code} cancelled by guest", confirmationCode);

        return Ok(result);
    }
}
