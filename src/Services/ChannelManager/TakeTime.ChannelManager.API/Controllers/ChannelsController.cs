using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.ChannelManager.Application.Commands;
using TakeTime.ChannelManager.Application.DTOs;
using TakeTime.ChannelManager.Application.Queries;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.ChannelManager.API.Controllers;

/// <summary>
/// API controller for managing OTA channel connections, inventory synchronization,
/// and reservation imports from external booking platforms.
/// </summary>
[ApiController]
[Route("api/v1/channels")]
[Authorize]
[Produces("application/json")]
public sealed class ChannelsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IChannelManagerRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;

    public ChannelsController(
        IMediator mediator,
        IChannelManagerRepository repository,
        ICurrentTenantService currentTenantService)
    {
        _mediator = mediator;
        _repository = repository;
        _currentTenantService = currentTenantService;
    }

    /// <summary>
    /// Retrieves all channel connections for the current tenant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of channel connections.</returns>
    /// <response code="200">Connections retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(List<ChannelConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ChannelConnectionDto>>> GetConnections(
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var connections = await _repository.GetAllConnectionsAsync(tenantId, cancellationToken);

        var dtos = connections.Select(c => new ChannelConnectionDto
        {
            Id = c.Id,
            TenantId = c.TenantId,
            ChannelName = c.ChannelName,
            ChannelType = c.ChannelType,
            Status = c.Status,
            ApiEndpoint = c.ApiEndpoint,
            PropertyId = c.PropertyId,
            IsActive = c.IsActive,
            LastSyncAt = c.LastSyncAt,
            LastSyncStatus = c.LastSyncStatus,
            SyncErrorCount = c.SyncErrorCount,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Creates a new OTA channel connection.
    /// </summary>
    /// <param name="dto">The channel connection details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created channel connection.</returns>
    /// <response code="201">Connection created successfully.</response>
    /// <response code="400">Invalid connection data.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("connections")]
    [ProducesResponseType(typeof(ChannelConnectionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChannelConnectionDto>> CreateConnection(
        [FromBody] CreateChannelConnectionDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var entry = new ChannelConnectionEntry
        {
            TenantId = tenantId,
            ChannelName = dto.ChannelName,
            ChannelType = dto.ChannelType,
            ApiEndpoint = dto.ApiEndpoint,
            ApiKey = dto.ApiKey,
            ApiSecret = dto.ApiSecret,
            PropertyId = dto.PropertyId
        };

        var id = await _repository.CreateConnectionAsync(entry, cancellationToken);

        var result = new ChannelConnectionDto
        {
            Id = id,
            TenantId = tenantId,
            ChannelName = dto.ChannelName,
            ChannelType = dto.ChannelType,
            Status = "Active",
            ApiEndpoint = dto.ApiEndpoint,
            PropertyId = dto.PropertyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        return CreatedAtAction(nameof(GetConnections), null, result);
    }

    /// <summary>
    /// Updates a channel connection.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="request">The updated connection settings.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated channel connection.</returns>
    [HttpPut("connections/{connectionId:guid}")]
    [ProducesResponseType(typeof(ChannelConnectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ChannelConnectionDto>> UpdateConnection(
        Guid connectionId,
        [FromBody] UpdateChannelConnectionDto request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateChannelConnectionCommand
        {
            ConnectionId = connectionId,
            ChannelName = request.ChannelName,
            ApiEndpoint = request.ApiEndpoint,
            ApiKey = request.ApiKey,
            ApiSecret = request.ApiSecret,
            PropertyId = request.PropertyId,
            IsActive = request.IsActive
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes/disconnects a channel connection.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content on success.</returns>
    [HttpDelete("connections/{connectionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConnection(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var command = new DeleteChannelConnectionCommand { ConnectionId = connectionId };
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Synchronizes room inventory (availability and rates) to an OTA channel.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="startDate">Start date for the sync period.</param>
    /// <param name="endDate">End date for the sync period.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sync result summary.</returns>
    [HttpPost("connections/{connectionId:guid}/sync-inventory")]
    [ProducesResponseType(typeof(InventorySyncResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<InventorySyncResultDto>> SyncInventory(
        Guid connectionId,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        CancellationToken cancellationToken)
    {
        var command = new SyncInventoryCommand
        {
            ChannelConnectionId = connectionId,
            StartDate = startDate,
            EndDate = endDate
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Imports reservations from an OTA channel.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="since">Import reservations created or modified since this date.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of imported reservations.</returns>
    [HttpPost("connections/{connectionId:guid}/import-reservations")]
    [ProducesResponseType(typeof(List<ImportedReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ImportedReservationDto>>> ImportReservations(
        Guid connectionId,
        [FromQuery] DateTime? since,
        CancellationToken cancellationToken)
    {
        var command = new ImportReservationCommand
        {
            ChannelConnectionId = connectionId,
            Since = since
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves imported reservations with optional filtering.
    /// </summary>
    /// <param name="channelName">Filter by channel name.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of imported reservations.</returns>
    [HttpGet("imported-reservations")]
    [ProducesResponseType(typeof(List<ImportedReservationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ImportedReservationDto>>> GetImportedReservations(
        [FromQuery] string? channelName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var reservations = await _repository.GetImportedReservationsAsync(
            tenantId, channelName, page, pageSize, cancellationToken);

        var dtos = reservations.Select(r => new ImportedReservationDto
        {
            Id = r.Id,
            ChannelName = r.ChannelName,
            ExternalReservationId = r.ExternalReservationId,
            GuestName = r.GuestName,
            GuestEmail = r.GuestEmail,
            GuestPhone = r.GuestPhone,
            CheckInDate = r.CheckInDate,
            CheckOutDate = r.CheckOutDate,
            NumberOfGuests = r.NumberOfGuests,
            RoomType = r.RoomType,
            TotalAmount = r.TotalAmount,
            Currency = r.Currency,
            Status = r.Status,
            LocalReservationId = r.LocalReservationId,
            ImportedAt = r.ImportedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Gets room mappings for a channel connection.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of room type mappings.</returns>
    [HttpGet("connections/{connectionId:guid}/room-mappings")]
    [ProducesResponseType(typeof(List<RoomMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RoomMappingDto>>> GetRoomMappings(
        Guid connectionId,
        CancellationToken cancellationToken)
    {
        var query = new GetRoomMappingsQuery { ConnectionId = connectionId };
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates/updates room mapping for a channel.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="request">The room mappings to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated room mappings.</returns>
    [HttpPut("connections/{connectionId:guid}/room-mappings")]
    [ProducesResponseType(typeof(List<RoomMappingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<RoomMappingDto>>> UpdateRoomMappings(
        Guid connectionId,
        [FromBody] UpdateRoomMappingsDto request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateRoomMappingsCommand
        {
            ConnectionId = connectionId,
            Mappings = request.Mappings
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Gets sync history for a channel connection.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="page">Page number.</param>
    /// <param name="pageSize">Page size.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of sync history entries.</returns>
    [HttpGet("connections/{connectionId:guid}/sync-history")]
    [ProducesResponseType(typeof(List<ChannelSyncLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<ChannelSyncLogDto>>> GetSyncHistory(
        Guid connectionId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetSyncHistoryQuery
        {
            ConnectionId = connectionId,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Pushes rate update to a channel.
    /// </summary>
    /// <param name="connectionId">The channel connection identifier.</param>
    /// <param name="request">The rates to push.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The push rates result.</returns>
    [HttpPost("connections/{connectionId:guid}/push-rates")]
    [ProducesResponseType(typeof(PushRatesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PushRatesResultDto>> PushRates(
        Guid connectionId,
        [FromBody] PushRatesDto request,
        CancellationToken cancellationToken)
    {
        var command = new PushRatesCommand
        {
            ConnectionId = connectionId,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Rates = request.Rates
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
}
