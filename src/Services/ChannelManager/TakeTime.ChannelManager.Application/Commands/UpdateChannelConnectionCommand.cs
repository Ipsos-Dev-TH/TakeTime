using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to update an existing channel connection's settings.
/// </summary>
public sealed class UpdateChannelConnectionCommand : IRequest<ChannelConnectionDto>
{
    public Guid ConnectionId { get; set; }
    public string? ChannelName { get; set; }
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? PropertyId { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateChannelConnectionCommand"/>. Retrieves the connection,
/// applies the updates, and persists the changes.
/// </summary>
public sealed class UpdateChannelConnectionCommandHandler : IRequestHandler<UpdateChannelConnectionCommand, ChannelConnectionDto>
{
    private readonly IChannelManagerRepository _repository;
    private readonly ILogger<UpdateChannelConnectionCommandHandler> _logger;

    public UpdateChannelConnectionCommandHandler(
        IChannelManagerRepository repository,
        ILogger<UpdateChannelConnectionCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ChannelConnectionDto> Handle(UpdateChannelConnectionCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating channel connection {ConnectionId}", request.ConnectionId);

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        if (request.ChannelName != null) connection.ChannelName = request.ChannelName;
        if (request.ApiEndpoint != null) connection.ApiEndpoint = request.ApiEndpoint;
        if (request.ApiKey != null) connection.ApiKey = request.ApiKey;
        if (request.ApiSecret != null) connection.ApiSecret = request.ApiSecret;
        if (request.PropertyId != null) connection.PropertyId = request.PropertyId;
        if (request.IsActive.HasValue) connection.IsActive = request.IsActive.Value;
        connection.UpdatedAt = DateTime.UtcNow;

        await _repository.UpdateConnectionAsync(connection, cancellationToken);

        _logger.LogInformation("Channel connection {ConnectionId} updated", request.ConnectionId);

        return new ChannelConnectionDto
        {
            Id = connection.Id,
            TenantId = connection.TenantId,
            ChannelName = connection.ChannelName,
            ChannelType = connection.ChannelType,
            Status = connection.IsActive ? "Active" : "Inactive",
            ApiEndpoint = connection.ApiEndpoint,
            PropertyId = connection.PropertyId,
            IsActive = connection.IsActive,
            LastSyncAt = connection.LastSyncAt,
            LastSyncStatus = connection.LastSyncStatus,
            SyncErrorCount = connection.SyncErrorCount,
            CreatedAt = connection.CreatedAt,
            UpdatedAt = connection.UpdatedAt
        };
    }
}
