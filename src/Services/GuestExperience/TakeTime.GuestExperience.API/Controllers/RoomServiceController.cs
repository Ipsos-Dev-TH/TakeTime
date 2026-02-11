using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.GuestExperience.API.Controllers;

/// <summary>
/// REST API controller for managing room service orders.
/// Provides order creation, status tracking, and lifecycle management
/// (accept, prepare, deliver, cancel).
/// </summary>
[ApiController]
[Route("api/v1/room-service")]
[Authorize]
[FeatureGate(FeatureModule.GuestExperience, SubFeature = "RoomService")]
[Produces("application/json")]
public class RoomServiceController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<RoomServiceController> _logger;

    public RoomServiceController(IMediator mediator, ILogger<RoomServiceController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Create a new room service order.
    /// Calculates totals including service charge and VAT based on tenant settings.
    /// </summary>
    /// <param name="command">The room service order creation data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created room service order with calculated totals.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRoomServiceOrderCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating room service order for room {RoomNumber}, items: {ItemCount}, charge to room: {ChargeToRoom}",
            command.RoomNumber, command.Items.Count, command.ChargeToRoom);

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// List room service orders with optional filtering by status.
    /// </summary>
    /// <param name="status">Filter by order status (Received, Accepted, Preparing, Delivered, Cancelled).</param>
    /// <param name="roomNumber">Filter by room number.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of room service orders.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(List<RoomServiceOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? roomNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        // TODO: Implement GetRoomServiceOrdersQuery when available
        // Should support filtering by status, room number, and date range
        // Should return paginated results ordered by creation time (newest first)
        _logger.LogDebug("Listing room service orders. Status: {Status}, Room: {RoomNumber}",
            status, roomNumber);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "List room service orders is not yet implemented. Awaiting GetRoomServiceOrdersQuery."
        });
    }

    /// <summary>
    /// Get a specific room service order by ID.
    /// </summary>
    /// <param name="id">The unique identifier of the room service order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The room service order details.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement GetRoomServiceOrderByIdQuery when available
        // Should return full order details including items, totals, and delivery status
        _logger.LogDebug("Getting room service order {OrderId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Get room service order by ID is not yet implemented. Awaiting GetRoomServiceOrderByIdQuery."
        });
    }

    /// <summary>
    /// Accept a room service order and begin processing.
    /// </summary>
    /// <param name="id">The unique identifier of the order to accept.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated room service order.</returns>
    [HttpPut("{id:guid}/accept")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement AcceptRoomServiceOrderCommand when available
        // Should set status to Accepted, validate order is in Received status,
        // and optionally send notification to the guest
        _logger.LogInformation("Accepting room service order {OrderId}", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Accept room service order is not yet implemented. Awaiting AcceptRoomServiceOrderCommand."
        });
    }

    /// <summary>
    /// Mark a room service order as being prepared.
    /// </summary>
    /// <param name="id">The unique identifier of the order.</param>
    /// <param name="request">Optional preparation details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated room service order.</returns>
    [HttpPut("{id:guid}/prepare")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Prepare(
        Guid id,
        [FromBody] PrepareRoomServiceRequest? request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement PrepareRoomServiceOrderCommand when available
        // Should set status to Preparing, validate order is in Accepted status,
        // and update estimated delivery time if provided
        _logger.LogInformation("Marking room service order {OrderId} as preparing", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Prepare room service order is not yet implemented. Awaiting PrepareRoomServiceOrderCommand."
        });
    }

    /// <summary>
    /// Mark a room service order as delivered to the guest.
    /// </summary>
    /// <param name="id">The unique identifier of the order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated room service order.</returns>
    [HttpPut("{id:guid}/deliver")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Deliver(Guid id, CancellationToken cancellationToken)
    {
        // TODO: Implement DeliverRoomServiceOrderCommand when available
        // Should set status to Delivered, record DeliveredAt timestamp,
        // validate order is in Preparing status, and charge to room folio if applicable
        _logger.LogInformation("Marking room service order {OrderId} as delivered", id);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Deliver room service order is not yet implemented. Awaiting DeliverRoomServiceOrderCommand."
        });
    }

    /// <summary>
    /// Cancel a room service order. Only orders that have not been delivered can be cancelled.
    /// </summary>
    /// <param name="id">The unique identifier of the order to cancel.</param>
    /// <param name="request">Cancellation details including reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated room service order.</returns>
    [HttpPut("{id:guid}/cancel")]
    [ProducesResponseType(typeof(RoomServiceOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status501NotImplemented)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelRoomServiceRequest request,
        CancellationToken cancellationToken)
    {
        // TODO: Implement CancelRoomServiceOrderCommand when available
        // Should set status to Cancelled, validate order has not been delivered,
        // record cancellation reason, and reverse room folio charge if applicable
        _logger.LogInformation("Cancelling room service order {OrderId}, reason: {Reason}",
            id, request.Reason);

        return StatusCode(StatusCodes.Status501NotImplemented, new
        {
            message = "Cancel room service order is not yet implemented. Awaiting CancelRoomServiceOrderCommand."
        });
    }
}

// ─── Request DTOs ────────────────────────────────────────────────────

/// <summary>
/// Request model for updating preparation details of a room service order.
/// </summary>
public sealed class PrepareRoomServiceRequest
{
    public DateTime? EstimatedDeliveryTime { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Request model for cancelling a room service order.
/// </summary>
public sealed class CancelRoomServiceRequest
{
    public string Reason { get; set; } = string.Empty;
}
