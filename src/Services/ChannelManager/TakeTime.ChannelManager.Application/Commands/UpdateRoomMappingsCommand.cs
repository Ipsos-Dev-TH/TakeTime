using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.DTOs;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to update room type mappings for a channel connection.
/// </summary>
public sealed class UpdateRoomMappingsCommand : IRequest<List<RoomMappingDto>>
{
    public Guid ConnectionId { get; set; }
    public List<RoomMappingDto> Mappings { get; set; } = [];
}

/// <summary>
/// Handler for <see cref="UpdateRoomMappingsCommand"/>. Updates the room type mappings
/// between local and channel room types for the specified connection.
/// </summary>
public sealed class UpdateRoomMappingsCommandHandler : IRequestHandler<UpdateRoomMappingsCommand, List<RoomMappingDto>>
{
    private readonly IChannelManagerRepository _repository;
    private readonly ILogger<UpdateRoomMappingsCommandHandler> _logger;

    public UpdateRoomMappingsCommandHandler(
        IChannelManagerRepository repository,
        ILogger<UpdateRoomMappingsCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<RoomMappingDto>> Handle(UpdateRoomMappingsCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Updating {Count} room mappings for connection {ConnectionId}",
            request.Mappings.Count, request.ConnectionId);

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        var entries = request.Mappings.Select(m => new RoomMappingEntry
        {
            Id = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id,
            ChannelConnectionId = request.ConnectionId,
            LocalRoomType = m.LocalRoomType,
            ChannelRoomType = m.ChannelRoomType,
            ChannelRoomId = m.ChannelRoomId,
            IsActive = m.IsActive
        }).ToList();

        await _repository.UpdateRoomMappingsAsync(request.ConnectionId, entries, cancellationToken);

        _logger.LogInformation(
            "Room mappings updated for connection {ConnectionId} ({ChannelName})",
            request.ConnectionId, connection.ChannelName);

        return entries.Select(e => new RoomMappingDto
        {
            Id = e.Id,
            ChannelConnectionId = e.ChannelConnectionId,
            LocalRoomType = e.LocalRoomType,
            ChannelRoomType = e.ChannelRoomType,
            ChannelRoomId = e.ChannelRoomId,
            IsActive = e.IsActive
        }).ToList();
    }
}
