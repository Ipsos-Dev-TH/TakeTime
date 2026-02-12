using MediatR;
using Microsoft.Extensions.Logging;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to deactivate/delete a channel connection.
/// </summary>
public sealed class DeleteChannelConnectionCommand : IRequest<bool>
{
    public Guid ConnectionId { get; set; }
}

/// <summary>
/// Handler for <see cref="DeleteChannelConnectionCommand"/>. Deactivates the channel
/// connection and marks it as inactive rather than performing a hard delete.
/// </summary>
public sealed class DeleteChannelConnectionCommandHandler : IRequestHandler<DeleteChannelConnectionCommand, bool>
{
    private readonly IChannelManagerRepository _repository;
    private readonly ILogger<DeleteChannelConnectionCommandHandler> _logger;

    public DeleteChannelConnectionCommandHandler(
        IChannelManagerRepository repository,
        ILogger<DeleteChannelConnectionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeleteChannelConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating channel connection {ConnectionId}", request.ConnectionId);

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        await _repository.DeactivateConnectionAsync(request.ConnectionId, cancellationToken);

        _logger.LogInformation(
            "Channel connection {ConnectionId} ({ChannelName}) deactivated",
            request.ConnectionId, connection.ChannelName);

        return true;
    }
}
