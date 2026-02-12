using MediatR;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.GuestExperience.Application.Queries;

namespace TakeTime.GuestExperience.API.Controllers;

/// <summary>
/// Guest self-service portal API controller for checked-in guests.
/// Provides endpoints for hotel information, housekeeping requests,
/// room service orders, maintenance reports, menu browsing, and reviews.
/// This is a public/semi-public controller accessible without full staff authentication.
/// </summary>
[ApiController]
[Route("api/v1/guest-portal")]
[Produces("application/json")]
public class GuestPortalController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<GuestPortalController> _logger;

    public GuestPortalController(IMediator mediator, ILogger<GuestPortalController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get hotel information including about, facilities, nearby places, and emergency contacts.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Hotel information for the guest portal.</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(HotelInfoDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHotelInfo(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Guest portal: fetching hotel info");

        var query = new GetHotelInfoQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Request housekeeping service for the guest's room.
    /// Delegates to the existing CreateHousekeepingTaskCommand.
    /// </summary>
    /// <param name="dto">Housekeeping request details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created housekeeping task.</returns>
    [HttpPost("housekeeping")]
    [ProducesResponseType(typeof(HousekeepingTaskDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RequestHousekeeping(
        [FromBody] GuestHousekeepingRequestDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Guest portal: housekeeping request for room {RoomNumber}", dto.RoomNumber);

        var command = new CreateHousekeepingTaskCommand
        {
            RoomNumber = dto.RoomNumber,
            TaskType = dto.RequestType ?? "GuestRequest",
            Priority = "Normal",
            Description = dto.Description ?? "Guest housekeeping request via portal"
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Guest portal: housekeeping task {TaskId} created for room {RoomNumber}",
            result.Id, dto.RoomNumber);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Order room service for the guest's room.
    /// Delegates to the existing CreateRoomServiceOrderCommand.
    /// </summary>
    /// <param name="dto">Room service order details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created room service order.</returns>
    [HttpPost("room-service")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OrderRoomService(
        [FromBody] GuestRoomServiceRequestDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Guest portal: room service order for room {RoomNumber}, {ItemCount} items",
            dto.RoomNumber, dto.Items.Count);

        var command = new CreateRoomServiceOrderCommand
        {
            RoomNumber = dto.RoomNumber,
            GuestName = dto.GuestName,
            Items = dto.Items.Select(i => new CreateRoomServiceItemDto
            {
                ItemName = i.ItemName,
                Category = i.Category,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                SpecialRequest = i.SpecialRequest
            }).ToList(),
            ChargeToRoom = true,
            SpecialInstructions = dto.SpecialInstructions
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Guest portal: room service order {OrderNumber} created for room {RoomNumber}",
            result.OrderNumber, dto.RoomNumber);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Report a maintenance issue in the guest's room.
    /// Delegates to the existing CreateMaintenanceRequestCommand.
    /// </summary>
    /// <param name="dto">Maintenance report details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created maintenance request.</returns>
    [HttpPost("maintenance")]
    [ProducesResponseType(typeof(MaintenanceRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportMaintenance(
        [FromBody] GuestMaintenanceRequestDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Guest portal: maintenance report for room {RoomNumber}, category: {Category}",
            dto.RoomNumber, dto.Category);

        var command = new CreateMaintenanceRequestCommand
        {
            RoomNumber = dto.RoomNumber,
            Category = dto.Category,
            Priority = "Normal",
            Description = dto.Description
        };

        var result = await _mediator.Send(command, cancellationToken);

        _logger.LogInformation(
            "Guest portal: maintenance request {RequestNumber} created for room {RoomNumber}",
            result.RequestNumber, dto.RoomNumber);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>
    /// Get the room service menu available for ordering through the guest portal.
    /// </summary>
    /// <param name="category">Optional filter by menu category (e.g., "Breakfast", "Main Course", "Beverages").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of available room service menu items.</returns>
    [HttpGet("menu")]
    [ProducesResponseType(typeof(List<RoomServiceMenuItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenu(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Guest portal: fetching room service menu, category: {Category}", category);

        var query = new GetRoomServiceMenuQuery
        {
            Category = category
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submit a review of the guest's stay. Reviews are submitted with a "Submitted"
    /// status and moderated by staff before publication.
    /// </summary>
    /// <param name="dto">Review details including ratings and comments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A confirmation of the submitted review.</returns>
    [HttpPost("reviews")]
    [ProducesResponseType(typeof(GuestReviewConfirmationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitReview(
        [FromBody] SubmitGuestReviewDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Guest portal: review submitted for room {RoomNumber} by {GuestName}, rating: {Rating}/5",
            dto.RoomNumber, dto.GuestName ?? "Anonymous", dto.OverallRating);

        var command = new SubmitGuestReviewCommand
        {
            RoomNumber = dto.RoomNumber,
            GuestName = dto.GuestName,
            OverallRating = dto.OverallRating,
            CleanlinessRating = dto.CleanlinessRating,
            ServiceRating = dto.ServiceRating,
            FacilitiesRating = dto.FacilitiesRating,
            LocationRating = dto.LocationRating,
            ValueRating = dto.ValueRating,
            Comments = dto.Comments
        };

        var result = await _mediator.Send(command, cancellationToken);

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
