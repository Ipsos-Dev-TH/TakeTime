using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.Commands;
using TakeTime.ChannelManager.Application.DTOs;

namespace TakeTime.ChannelManager.Application.Queries;

/// <summary>
/// Query to retrieve synchronization history for a channel connection.
/// </summary>
public sealed class GetSyncHistoryQuery : IRequest<List<ChannelSyncLogDto>>
{
    public Guid ConnectionId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetSyncHistoryQuery"/>. Retrieves the sync history log
/// entries for the specified channel connection.
/// </summary>
public sealed class GetSyncHistoryQueryHandler : IRequestHandler<GetSyncHistoryQuery, List<ChannelSyncLogDto>>
{
    private readonly IChannelManagerRepository _repository;
    private readonly ILogger<GetSyncHistoryQueryHandler> _logger;

    public GetSyncHistoryQueryHandler(
        IChannelManagerRepository repository,
        ILogger<GetSyncHistoryQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<List<ChannelSyncLogDto>> Handle(GetSyncHistoryQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Retrieving sync history for connection {ConnectionId}, page {Page}",
            request.ConnectionId, request.Page);

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        var logs = await _repository.GetSyncHistoryAsync(
            request.ConnectionId, request.Page, request.PageSize, cancellationToken);

        var result = logs.Select(l => new ChannelSyncLogDto
        {
            Id = l.Id,
            ChannelConnectionId = l.ChannelConnectionId,
            SyncType = l.SyncType,
            Direction = l.Direction,
            Status = l.Status,
            ItemCount = l.ItemCount,
            ErrorMessage = l.ErrorMessage,
            StartedAt = l.StartedAt,
            CompletedAt = l.CompletedAt
        }).ToList();

        _logger.LogInformation(
            "Retrieved {Count} sync history entries for connection {ConnectionId}",
            result.Count, request.ConnectionId);

        return result;
    }
}
