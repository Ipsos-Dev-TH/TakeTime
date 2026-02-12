using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.DTOs;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.ChannelManager.Application.Commands;

/// <summary>
/// Command to synchronize room availability, rates, and restrictions to an OTA channel.
/// Pushes the current inventory state from the property management system to the specified
/// channel connection.
/// </summary>
public sealed class SyncInventoryCommand : IRequest<InventorySyncResultDto>
{
    public Guid ChannelConnectionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool SyncRates { get; set; } = true;
    public bool SyncAvailability { get; set; } = true;
    public bool SyncRestrictions { get; set; } = true;
}

/// <summary>
/// Handler for <see cref="SyncInventoryCommand"/>. Retrieves current room availability
/// and rates, formats them according to the channel's requirements, and pushes the data
/// to the OTA's API.
/// </summary>
public sealed class SyncInventoryCommandHandler : IRequestHandler<SyncInventoryCommand, InventorySyncResultDto>
{
    private readonly IChannelManagerRepository _repository;
    private readonly IChannelApiClientFactory _channelApiClientFactory;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<SyncInventoryCommandHandler> _logger;

    public SyncInventoryCommandHandler(
        IChannelManagerRepository repository,
        IChannelApiClientFactory channelApiClientFactory,
        ICurrentTenantService currentTenantService,
        ILogger<SyncInventoryCommandHandler> logger)
    {
        _repository = repository;
        _channelApiClientFactory = channelApiClientFactory;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<InventorySyncResultDto> Handle(SyncInventoryCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Starting inventory sync for channel connection {ChannelConnectionId} from {StartDate} to {EndDate}",
            request.ChannelConnectionId, request.StartDate.ToString("yyyy-MM-dd"), request.EndDate.ToString("yyyy-MM-dd"));

        // Retrieve channel connection details
        var connection = await _repository.GetChannelConnectionAsync(request.ChannelConnectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {request.ChannelConnectionId} not found.");

        if (!connection.IsActive)
        {
            throw new InvalidOperationException($"Channel connection '{connection.ChannelName}' is not active.");
        }

        // Log the sync start
        var syncLogId = await _repository.CreateSyncLogAsync(new ChannelSyncLogEntry
        {
            ChannelConnectionId = request.ChannelConnectionId,
            TenantId = tenantId,
            SyncType = "Inventory",
            Direction = "Outbound",
            Status = "InProgress"
        }, cancellationToken);

        try
        {
            // Retrieve current availability data
            var availability = await _repository.GetRoomAvailabilityAsync(
                tenantId, request.StartDate, request.EndDate, cancellationToken);

            // Get the API client for this channel
            var apiClient = _channelApiClientFactory.CreateClient(connection.ChannelType);

            // Push inventory to the channel
            int roomsSynced = 0, ratesSynced = 0, availabilityDaysSynced = 0;

            if (request.SyncAvailability)
            {
                availabilityDaysSynced = await apiClient.PushAvailabilityAsync(
                    connection, availability, cancellationToken);
            }

            if (request.SyncRates)
            {
                ratesSynced = await apiClient.PushRatesAsync(
                    connection, availability, cancellationToken);
            }

            roomsSynced = availability.Select(a => a.RoomType).Distinct().Count();

            // Update sync log
            await _repository.UpdateSyncLogAsync(syncLogId, "Completed", availabilityDaysSynced, null, cancellationToken);

            // Update connection last sync time
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ChannelConnectionId, DateTime.UtcNow, "Success", cancellationToken);

            _logger.LogInformation(
                "Inventory sync completed for {ChannelName}: {Rooms} rooms, {Rates} rates, {Days} days",
                connection.ChannelName, roomsSynced, ratesSynced, availabilityDaysSynced);

            return new InventorySyncResultDto
            {
                ChannelConnectionId = request.ChannelConnectionId,
                ChannelName = connection.ChannelName,
                Success = true,
                RoomsSynced = roomsSynced,
                RatesSynced = ratesSynced,
                AvailabilityDaysSynced = availabilityDaysSynced,
                SyncedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Inventory sync failed for channel connection {ChannelConnectionId}", request.ChannelConnectionId);

            await _repository.UpdateSyncLogAsync(syncLogId, "Failed", 0, ex.Message, cancellationToken);
            await _repository.UpdateConnectionSyncStatusAsync(
                request.ChannelConnectionId, DateTime.UtcNow, "Failed", cancellationToken);

            return new InventorySyncResultDto
            {
                ChannelConnectionId = request.ChannelConnectionId,
                ChannelName = connection.ChannelName,
                Success = false,
                ErrorMessage = ex.Message,
                SyncedAt = DateTime.UtcNow
            };
        }
    }
}

/// <summary>
/// Repository interface for channel manager operations.
/// </summary>
public interface IChannelManagerRepository
{
    Task<ChannelConnectionEntry?> GetChannelConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelConnectionEntry>> GetAllConnectionsAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateConnectionAsync(ChannelConnectionEntry connection, CancellationToken cancellationToken = default);
    Task UpdateConnectionSyncStatusAsync(Guid connectionId, DateTime syncTime, string status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomAvailabilityEntry>> GetRoomAvailabilityAsync(string tenantId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    Task<Guid> CreateSyncLogAsync(ChannelSyncLogEntry log, CancellationToken cancellationToken = default);
    Task UpdateSyncLogAsync(Guid logId, string status, int itemCount, string? errorMessage, CancellationToken cancellationToken = default);
    Task<Guid> CreateImportedReservationAsync(ImportedReservationEntry reservation, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportedReservationEntry>> GetImportedReservationsAsync(string tenantId, string? channelName, int page, int pageSize, CancellationToken cancellationToken = default);
    Task UpdateConnectionAsync(ChannelConnectionEntry connection, CancellationToken cancellationToken = default);
    Task DeactivateConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomMappingEntry>> GetRoomMappingsAsync(Guid connectionId, CancellationToken cancellationToken = default);
    Task UpdateRoomMappingsAsync(Guid connectionId, IReadOnlyList<RoomMappingEntry> mappings, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChannelSyncLogEntry>> GetSyncHistoryAsync(Guid connectionId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<Guid> CreateRateUpdateAsync(RateUpdateEntry rateUpdate, CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating channel-specific API clients.
/// </summary>
public interface IChannelApiClientFactory
{
    IChannelApiClient CreateClient(string channelType);
}

/// <summary>
/// Interface for channel-specific API communication.
/// </summary>
public interface IChannelApiClient
{
    Task<int> PushAvailabilityAsync(ChannelConnectionEntry connection, IReadOnlyList<RoomAvailabilityEntry> availability, CancellationToken cancellationToken = default);
    Task<int> PushRatesAsync(ChannelConnectionEntry connection, IReadOnlyList<RoomAvailabilityEntry> availability, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportedReservationEntry>> FetchReservationsAsync(ChannelConnectionEntry connection, DateTime since, CancellationToken cancellationToken = default);
}

public sealed class ChannelConnectionEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ChannelType { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? PropertyId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncAt { get; set; }
    public string? LastSyncStatus { get; set; }
    public int SyncErrorCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class RoomAvailabilityEntry
{
    public Guid AccommodationId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public int AvailableCount { get; set; }
    public decimal Rate { get; set; }
    public string Currency { get; set; } = "THB";
    public int MinStayNights { get; set; } = 1;
    public bool IsClosed { get; set; }
}

public sealed class ChannelSyncLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelConnectionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string SyncType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int ItemCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public sealed class ImportedReservationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string ExternalReservationId { get; set; } = string.Empty;
    public string GuestName { get; set; } = string.Empty;
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public int NumberOfGuests { get; set; }
    public string? RoomType { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = "Imported";
    public Guid? LocalReservationId { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

public sealed class RoomMappingEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelConnectionId { get; set; }
    public string LocalRoomType { get; set; } = string.Empty;
    public string ChannelRoomType { get; set; } = string.Empty;
    public string? ChannelRoomId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class RateUpdateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChannelConnectionId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RoomType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Rate { get; set; }
    public string Currency { get; set; } = "THB";
    public int Availability { get; set; }
    public int? MinStay { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
