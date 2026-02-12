using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;
using TakeTime.GuestExperience.Application.Queries;
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
    public async Task<IActionResult> GetAll(
        [FromQuery] string? status,
        [FromQuery] string? roomNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing room service orders. Status: {Status}, Room: {RoomNumber}",
            status, roomNumber);

        var query = new GetRoomServiceOrdersQuery
        {
            Status = status,
            RoomNumber = roomNumber,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
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
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Getting room service order {OrderId}", id);

        var query = new GetRoomServiceOrderByIdQuery { Id = id };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
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
    public async Task<IActionResult> Accept(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Accepting room service order {OrderId}", id);

        var command = new AcceptRoomServiceOrderCommand { OrderId = id };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
    public async Task<IActionResult> Prepare(
        Guid id,
        [FromBody] PrepareRoomServiceRequest? request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking room service order {OrderId} as preparing", id);

        var command = new PrepareRoomServiceOrderCommand
        {
            OrderId = id,
            EstimatedDeliveryTime = request?.EstimatedDeliveryTime,
            Notes = request?.Notes
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
    public async Task<IActionResult> Deliver(Guid id, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Marking room service order {OrderId} as delivered", id);

        var command = new DeliverRoomServiceOrderCommand { OrderId = id };
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelRoomServiceRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Cancelling room service order {OrderId}, reason: {Reason}",
            id, request.Reason);

        var command = new CancelRoomServiceOrderCommand
        {
            OrderId = id,
            Reason = request.Reason
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
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
