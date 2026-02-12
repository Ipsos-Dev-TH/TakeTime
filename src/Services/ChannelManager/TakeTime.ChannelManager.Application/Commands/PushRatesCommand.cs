using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to push rates to an OTA channel.
/// </summary>
public sealed class PushRatesCommand : IRequest<PushRatesResultDto>
{
    public Guid ConnectionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<RateEntryDto> Rates { get; set; } = [];
}

/// <summary>
/// Handler for <see cref="PushRatesCommand"/>. Pushes rate updates to the specified
/// channel connection's OTA API.
/// </summary>
public sealed class PushRatesCommandHandler : IRequestHandler<PushRatesCommand, PushRatesResultDto>
{
    private readonly IChannelManagerRepository _repository;
    private readonly IChannelApiClientFactory _channelApiClientFactory;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<PushRatesCommandHandler> _logger;

    public PushRatesCommandHandler(
        IChannelManagerRepository repository,
        IChannelApiClientFactory channelApiClientFactory,
        ICurrentTenantService currentTenantService,
        ILogger<PushRatesCommandHandler> logger)
    {
        _repository = repository;
        _channelApiClientFactory = channelApiClientFactory;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<PushRatesResultDto> Handle(PushRatesCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Pushing rates to channel connection {ConnectionId} for {StartDate} to {EndDate}",
            request.ConnectionId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        var connection = await _repository.GetChannelConnectionAsync(request.ConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ConnectionId} not found.");

        if (!connection.IsActive)
        {
            throw new InvalidOperationException($"Channel connection '{connection.ChannelName}' is not active.");
        }

        // Log the sync
        var syncLogId = await _repository.CreateSyncLogAsync(new ChannelSyncLogEntry
        {
            ChannelConnectionId = request.ConnectionId,
            TenantId = tenantId,
            SyncType = "Rates",
            Direction = "Outbound",
            Status = "InProgress"
        }, cancellationToken);

        try
        {
            // Store rate update entries
            foreach (var rate in request.Rates)
            {
                await _repository.CreateRateUpdateAsync(new RateUpdateEntry
                {
                    ChannelConnectionId = request.ConnectionId,
                    TenantId = tenantId,
                    RoomType = rate.RoomType,
                    StartDate = request.StartDate,
                    EndDate = request.EndDate,
                    Rate = rate.Rate,
                    Currency = rate.Currency,
                    Availability = rate.Availability,
                    MinStay = rate.MinStay,
                    Status = "Pending"
                }, cancellationToken);
            }

            // Push rates via API client
            var apiClient = _channelApiClientFactory.CreateClient(connection.ChannelType);
            var availability = request.Rates.Select(r => new RoomAvailabilityEntry
            {
                RoomType = r.RoomType,
                Date = request.StartDate,
                Rate = r.Rate,
                Currency = r.Currency,
                AvailableCount = r.Availability,
                MinStayNights = r.MinStay ?? 1
            }).ToList();

            var ratesPushed = await apiClient.PushRatesAsync(connection, availability, cancellationToken);

            await _repository.UpdateSyncLogAsync(syncLogId, "Completed", ratesPushed, null, cancellationToken);
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ConnectionId, DateTime.UtcNow, "Success", cancellationToken);

            _logger.LogInformation(
                "Pushed {Count} rates to {ChannelName}",
                ratesPushed, connection.ChannelName);

            return new PushRatesResultDto
            {
                ChannelConnectionId = request.ConnectionId,
                ChannelName = connection.ChannelName,
                Success = true,
                RatesPushed = ratesPushed,
                PushedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rate push failed for channel connection {ConnectionId}", request.ConnectionId);

            await _repository.UpdateSyncLogAsync(syncLogId, "Failed", 0, ex.Message, cancellationToken);
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ConnectionId, DateTime.UtcNow, "Failed", cancellationToken);

            return new PushRatesResultDto
            {
                ChannelConnectionId = request.ConnectionId,
                ChannelName = connection.ChannelName,
                Success = false,
                ErrorMessage = ex.Message,
                PushedAt = DateTime.UtcNow
            };
        }
    }
}
