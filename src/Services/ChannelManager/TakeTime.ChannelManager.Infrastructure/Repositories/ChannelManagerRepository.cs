using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.ChannelManager.Application.Commands;

namespace TakeTime.ChannelManager.Infrastructure.Repositories;

/// <summary>
/// Implementation of <see cref="IChannelManagerRepository"/> providing data access
/// for channel manager operations using Entity Framework Core.
/// Maps between application-level entry DTOs and EF entity types stored in the database.
/// </summary>
public sealed class ChannelManagerRepository : IChannelManagerRepository
{
    private readonly ChannelManagerDbContext _dbContext;
    private readonly ILogger<ChannelManagerRepository> _logger;

    public ChannelManagerRepository(
        ChannelManagerDbContext dbContext,
        ILogger<ChannelManagerRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<ChannelConnectionEntry?> GetChannelConnectionAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ChannelConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        return entity is null ? null : MapToConnectionEntry(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelConnectionEntry>> GetAllConnectionsAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.ChannelConnections
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToConnectionEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateConnectionAsync(
        ChannelConnectionEntry connection, CancellationToken cancellationToken = default)
    {
        var entity = new ChannelConnectionEntity
        {
            Id = connection.Id,
            TenantId = connection.TenantId,
            ChannelName = connection.ChannelName,
            ChannelType = connection.ChannelType,
            Status = connection.Status,
            ApiEndpoint = connection.ApiEndpoint,
            ApiKey = connection.ApiKey,
            ApiSecret = connection.ApiSecret,
            PropertyId = connection.PropertyId,
            IsActive = connection.IsActive,
            LastSyncAt = connection.LastSyncAt,
            LastSyncStatus = connection.LastSyncStatus,
            SyncErrorCount = connection.SyncErrorCount,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.ChannelConnections.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created channel connection {ConnectionId} for tenant {TenantId} ({ChannelName})",
            entity.Id, entity.TenantId, entity.ChannelName);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateConnectionAsync(
        ChannelConnectionEntry connection, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ChannelConnections
            .FirstOrDefaultAsync(c => c.Id == connection.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {connection.Id} not found.");

        entity.ChannelName = connection.ChannelName;
        entity.ChannelType = connection.ChannelType;
        entity.Status = connection.Status;
        entity.ApiEndpoint = connection.ApiEndpoint;
        entity.ApiKey = connection.ApiKey;
        entity.ApiSecret = connection.ApiSecret;
        entity.PropertyId = connection.PropertyId;
        entity.IsActive = connection.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated channel connection {ConnectionId} ({ChannelName})",
            entity.Id, entity.ChannelName);
    }

    /// <inheritdoc />
    public async Task UpdateConnectionSyncStatusAsync(
        Guid connectionId, DateTime syncTime, string status,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ChannelConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {connectionId} not found.");

        entity.LastSyncAt = syncTime;
        entity.LastSyncStatus = status;

        if (status == "Failed")
        {
            entity.SyncErrorCount++;
        }
        else if (status == "Success")
        {
            entity.SyncErrorCount = 0;
        }

        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeactivateConnectionAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.ChannelConnections
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken)
            ?? throw new InvalidOperationException($"Channel connection {connectionId} not found.");

        entity.IsActive = false;
        entity.Status = "Inactive";
        entity.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deactivated channel connection {ConnectionId}", connectionId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomAvailabilityEntry>> GetRoomAvailabilityAsync(
        string tenantId, DateTime startDate, DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Room availability data is owned by the Reservation service.
        // In a full implementation, this would either:
        //   1. Query a local read-model cache populated via domain events from the Reservation service, or
        //   2. Call the Reservation service API via an HTTP client.
        // For now, return an empty list until cross-service integration is wired up.
        _logger.LogWarning(
            "GetRoomAvailabilityAsync called for tenant {TenantId} ({StartDate} - {EndDate}), " +
            "but cross-service availability integration is not yet implemented. Returning empty list.",
            tenantId, startDate.ToString("yyyy-MM-dd"), endDate.ToString("yyyy-MM-dd"));

        return await Task.FromResult<IReadOnlyList<RoomAvailabilityEntry>>(
            Array.Empty<RoomAvailabilityEntry>());
    }

    /// <inheritdoc />
    public async Task<Guid> CreateSyncLogAsync(
        ChannelSyncLogEntry log, CancellationToken cancellationToken = default)
    {
        var entity = new SyncLogEntity
        {
            Id = log.Id,
            TenantId = log.TenantId,
            ChannelConnectionId = log.ChannelConnectionId,
            SyncType = log.SyncType,
            Direction = log.Direction,
            Status = log.Status,
            ItemCount = log.ItemCount,
            ErrorMessage = log.ErrorMessage,
            StartedAt = DateTime.UtcNow
        };

        await _dbContext.SyncLogs.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateSyncLogAsync(
        Guid logId, string status, int itemCount, string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SyncLogs
            .FirstOrDefaultAsync(s => s.Id == logId, cancellationToken)
            ?? throw new InvalidOperationException($"Sync log {logId} not found.");

        entity.Status = status;
        entity.ItemCount = itemCount;
        entity.ErrorMessage = errorMessage;
        entity.CompletedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChannelSyncLogEntry>> GetSyncHistoryAsync(
        Guid connectionId, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var entities = await _dbContext.SyncLogs
            .AsNoTracking()
            .Where(s => s.ChannelConnectionId == connectionId)
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToSyncLogEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateImportedReservationAsync(
        ImportedReservationEntry reservation, CancellationToken cancellationToken = default)
    {
        var entity = new ImportedReservationEntity
        {
            Id = reservation.Id,
            TenantId = reservation.TenantId,
            ChannelName = reservation.ChannelName,
            ExternalReservationId = reservation.ExternalReservationId,
            GuestName = reservation.GuestName,
            GuestEmail = reservation.GuestEmail,
            GuestPhone = reservation.GuestPhone,
            CheckInDate = reservation.CheckInDate,
            CheckOutDate = reservation.CheckOutDate,
            NumberOfGuests = reservation.NumberOfGuests,
            RoomType = reservation.RoomType,
            TotalAmount = reservation.TotalAmount,
            Currency = reservation.Currency,
            Status = reservation.Status,
            LocalReservationId = reservation.LocalReservationId,
            ImportedAt = DateTime.UtcNow
        };

        await _dbContext.ImportedReservations.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created imported reservation {ReservationId} from {ChannelName} (external: {ExternalId})",
            entity.Id, entity.ChannelName, entity.ExternalReservationId);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ImportedReservationEntry>> GetImportedReservationsAsync(
        string tenantId, string? channelName, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.ImportedReservations
            .AsNoTracking()
            .Where(r => r.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(channelName))
        {
            query = query.Where(r => r.ChannelName == channelName);
        }

        var entities = await query
            .OrderByDescending(r => r.ImportedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToImportedReservationEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RoomMappingEntry>> GetRoomMappingsAsync(
        Guid connectionId, CancellationToken cancellationToken = default)
    {
        var entities = await _dbContext.RoomTypeMappings
            .AsNoTracking()
            .Where(m => m.ChannelConnectionId == connectionId)
            .OrderBy(m => m.LocalRoomType)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToRoomMappingEntry).ToList();
    }

    /// <inheritdoc />
    public async Task UpdateRoomMappingsAsync(
        Guid connectionId, IReadOnlyList<RoomMappingEntry> mappings,
        CancellationToken cancellationToken = default)
    {
        // Remove existing mappings for this connection
        var existingMappings = await _dbContext.RoomTypeMappings
            .Where(m => m.ChannelConnectionId == connectionId)
            .ToListAsync(cancellationToken);

        _dbContext.RoomTypeMappings.RemoveRange(existingMappings);

        // Retrieve the connection to get its TenantId
        var connection = await _dbContext.ChannelConnections
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == connectionId, cancellationToken);

        // Add new mappings
        var newEntities = mappings.Select(m => new RoomTypeMappingEntity
        {
            Id = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id,
            TenantId = connection?.TenantId,
            ChannelConnectionId = connectionId,
            LocalRoomType = m.LocalRoomType,
            ChannelRoomType = m.ChannelRoomType,
            ChannelRoomId = m.ChannelRoomId,
            IsActive = m.IsActive
        }).ToList();

        await _dbContext.RoomTypeMappings.AddRangeAsync(newEntities, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Updated {Count} room mappings for connection {ConnectionId}",
            newEntities.Count, connectionId);
    }

    /// <inheritdoc />
    public async Task<Guid> CreateRateUpdateAsync(
        RateUpdateEntry rateUpdate, CancellationToken cancellationToken = default)
    {
        // Rate updates are tracked via a sync log entry since the ChannelManager DbContext
        // does not yet have a dedicated RateUpdates table. When a RateUpdates DbSet is added
        // to ChannelManagerDbContext, this method should be updated to persist to that table.
        var syncLog = new SyncLogEntity
        {
            Id = rateUpdate.Id,
            TenantId = rateUpdate.TenantId,
            ChannelConnectionId = rateUpdate.ChannelConnectionId,
            SyncType = "RateUpdate",
            Direction = "Outbound",
            Status = rateUpdate.Status,
            ItemCount = 1,
            StartedAt = rateUpdate.CreatedAt
        };

        await _dbContext.SyncLogs.AddAsync(syncLog, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created rate update {RateUpdateId} for connection {ConnectionId} ({RoomType}: {Rate} {Currency})",
            syncLog.Id, rateUpdate.ChannelConnectionId, rateUpdate.RoomType,
            rateUpdate.Rate, rateUpdate.Currency);

        return syncLog.Id;
    }

    #region Mapping Helpers

    private static ChannelConnectionEntry MapToConnectionEntry(ChannelConnectionEntity entity)
    {
        return new ChannelConnectionEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            ChannelName = entity.ChannelName,
            ChannelType = entity.ChannelType,
            Status = entity.Status,
            ApiEndpoint = entity.ApiEndpoint,
            ApiKey = entity.ApiKey,
            ApiSecret = entity.ApiSecret,
            PropertyId = entity.PropertyId,
            IsActive = entity.IsActive,
            LastSyncAt = entity.LastSyncAt,
            LastSyncStatus = entity.LastSyncStatus,
            SyncErrorCount = entity.SyncErrorCount,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    private static ChannelSyncLogEntry MapToSyncLogEntry(SyncLogEntity entity)
    {
        return new ChannelSyncLogEntry
        {
            Id = entity.Id,
            ChannelConnectionId = entity.ChannelConnectionId,
            TenantId = entity.TenantId ?? string.Empty,
            SyncType = entity.SyncType,
            Direction = entity.Direction,
            Status = entity.Status,
            ItemCount = entity.ItemCount,
            ErrorMessage = entity.ErrorMessage,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt
        };
    }

    private static ImportedReservationEntry MapToImportedReservationEntry(ImportedReservationEntity entity)
    {
        return new ImportedReservationEntry
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            ChannelName = entity.ChannelName,
            ExternalReservationId = entity.ExternalReservationId,
            GuestName = entity.GuestName,
            GuestEmail = entity.GuestEmail,
            GuestPhone = entity.GuestPhone,
            CheckInDate = entity.CheckInDate,
            CheckOutDate = entity.CheckOutDate,
            NumberOfGuests = entity.NumberOfGuests,
            RoomType = entity.RoomType,
            TotalAmount = entity.TotalAmount,
            Currency = entity.Currency,
            Status = entity.Status,
            LocalReservationId = entity.LocalReservationId,
            ImportedAt = entity.ImportedAt
        };
    }

    private static RoomMappingEntry MapToRoomMappingEntry(RoomTypeMappingEntity entity)
    {
        return new RoomMappingEntry
        {
            Id = entity.Id,
            ChannelConnectionId = entity.ChannelConnectionId,
            LocalRoomType = entity.LocalRoomType,
            ChannelRoomType = entity.ChannelRoomType,
            ChannelRoomId = entity.ChannelRoomId,
            IsActive = entity.IsActive
        };
    }

    #endregion
}
