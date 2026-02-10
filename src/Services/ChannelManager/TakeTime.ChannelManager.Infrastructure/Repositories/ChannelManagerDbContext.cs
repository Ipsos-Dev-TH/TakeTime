using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;

namespace TakeTime.ChannelManager.Infrastructure.Repositories;

/// <summary>
/// Entity Framework Core DbContext for the Channel Manager microservice.
/// Inherits from <see cref="BaseDbContext"/> for multi-tenant isolation,
/// soft-delete filtering, audit fields, and domain event dispatching.
/// </summary>
public sealed class ChannelManagerDbContext : BaseDbContext
{
    public ChannelManagerDbContext(
        DbContextOptions<ChannelManagerDbContext> options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<BaseDbContext>? logger = null)
        : base(options, currentTenantService, mediator, logger)
    {
    }

    /// <summary>
    /// OTA channel connections.
    /// </summary>
    public DbSet<ChannelConnectionEntity> ChannelConnections => Set<ChannelConnectionEntity>();

    /// <summary>
    /// Imported reservations from OTA channels.
    /// </summary>
    public DbSet<ImportedReservationEntity> ImportedReservations => Set<ImportedReservationEntity>();

    /// <summary>
    /// Channel synchronization logs.
    /// </summary>
    public DbSet<SyncLogEntity> SyncLogs => Set<SyncLogEntity>();

    /// <summary>
    /// Room type mappings between local and channel room types.
    /// </summary>
    public DbSet<RoomTypeMappingEntity> RoomTypeMappings => Set<RoomTypeMappingEntity>();

    protected override void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("channel_manager");

        modelBuilder.Entity<ChannelConnectionEntity>(entity =>
        {
            entity.ToTable("ChannelConnections");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ChannelName });
        });

        modelBuilder.Entity<ImportedReservationEntity>(entity =>
        {
            entity.ToTable("ImportedReservations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 4);
            entity.HasIndex(e => new { e.TenantId, e.ExternalReservationId }).IsUnique();
            entity.HasIndex(e => new { e.TenantId, e.ChannelName });
        });

        modelBuilder.Entity<SyncLogEntity>(entity =>
        {
            entity.ToTable("SyncLogs");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ChannelConnectionId });
        });

        modelBuilder.Entity<RoomTypeMappingEntity>(entity =>
        {
            entity.ToTable("RoomTypeMappings");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.ChannelConnectionId, e.LocalRoomType });
        });
    }
}

public sealed class ChannelConnectionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
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
    public string? SettingsJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public sealed class ImportedReservationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public Guid ChannelConnectionId { get; set; }
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
    public string? RawDataJson { get; set; }
    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;
}

public sealed class SyncLogEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public Guid ChannelConnectionId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int ItemCount { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public sealed class RoomTypeMappingEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TenantId { get; set; }
    public Guid ChannelConnectionId { get; set; }
    public string LocalRoomType { get; set; } = string.Empty;
    public string ChannelRoomType { get; set; } = string.Empty;
    public string? ChannelRoomId { get; set; }
    public bool IsActive { get; set; } = true;
}
