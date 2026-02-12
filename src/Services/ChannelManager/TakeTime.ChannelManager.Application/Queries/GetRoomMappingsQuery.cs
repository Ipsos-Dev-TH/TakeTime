using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.Commands;
using TakeTime.ChannelManager.Application.DTOs;

namespace TakeTime.ChannelManager.Application.Queries;

/// <summary>
/// Query to retrieve room type mappings for a channel connection.
/// </summary>
public sealed class GetRoomMappingsQuery : IRequest<List<RoomMappingDto>>
{
    public Guid ConnectionId { get; set; }
}

/// <summary>
/// Handler for <see cref="GetRoomMappingsQuery"/>. Retrieves all room type mappings
/// for the specified channel connection.
/// </summary>
public sealed class GetRoomMappingsQueryHandler : IRequestHandler<GetRoomMappingsQuery, List<RoomMappingDto>>
{
    private readonly IChannelManagerRepository _repository;
    private readonly ILogger<GetRoomMappingsQueryHandler> _logger;

    public GetRoomMappingsQueryHandler(
        IChannelManagerRepository repository,
        ILogger<GetRoomMappingsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<RoomMappingDto>> Handle(GetRoomMappingsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving room mappings for connection {ConnectionId}", request.ConnectionId);

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        var mappings = await _repository.GetRoomMappingsAsync(request.ConnectionId, cancellationToken);

        var result = mappings.Select(m => new RoomMappingDto
        {
            Id = m.Id,
            ChannelConnectionId = m.ChannelConnectionId,
            LocalRoomType = m.LocalRoomType,
            ChannelRoomType = m.ChannelRoomType,
            ChannelRoomId = m.ChannelRoomId,
            IsActive = m.IsActive
        }).ToList();

        _logger.LogInformation(
            "Retrieved {Count} room mappings for connection {ConnectionId}",
            result.Count, request.ConnectionId);

        return result;
    }
}
