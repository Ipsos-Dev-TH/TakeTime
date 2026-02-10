using System.Linq.Expressions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Infrastructure.Database;

/// <summary>
/// Abstract EF Core DbContext providing multi-tenant isolation, soft-delete filtering,
/// automatic audit field population, and domain event dispatching.
/// All service-specific DbContexts should inherit from this class.
/// </summary>
public abstract class BaseDbContext : DbContext
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly IMediator? _mediator;
    private readonly ILogger<BaseDbContext>? _logger;

    protected string? CurrentTenantId => _currentTenantService.TenantId;

    protected BaseDbContext(
        DbContextOptions options,
        ICurrentTenantService currentTenantService,
        IMediator? mediator = null,
        ILogger<BaseDbContext>? logger = null)
        : base(options)
    {
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
        _mediator = mediator;
        _logger = logger;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ApplyGlobalFilters(modelBuilder);
        ApplyEntityConfigurations(modelBuilder);
    }

    /// <summary>
    /// Override in derived contexts to apply service-specific entity configurations.
    /// </summary>
    protected virtual void ApplyEntityConfigurations(ModelBuilder modelBuilder)
    {
    }

    /// <summary>
    /// Applies global query filters for multi-tenancy and soft-delete to all Entity-derived types.
    /// </summary>
    private void ApplyGlobalFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(Entity).IsAssignableFrom(entityType.ClrType))
                continue;

            var method = typeof(BaseDbContext)
                .GetMethod(nameof(ApplyGlobalFilterToEntity),
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);

            method.Invoke(this, [modelBuilder]);
        }
    }

    private void ApplyGlobalFilterToEntity<TEntity>(ModelBuilder modelBuilder)
        where TEntity : Entity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(CreateFilterExpression<TEntity>());
    }

    /// <summary>
    /// Creates a combined filter expression for TenantId and IsDeleted.
    /// </summary>
    private Expression<Func<TEntity, bool>> CreateFilterExpression<TEntity>()
        where TEntity : Entity
    {
        Expression<Func<TEntity, bool>> filter = e =>
            (CurrentTenantId == null || e.TenantId == CurrentTenantId) &&
            !e.IsDeleted;

        return filter;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        ApplyConcurrencyVersioning();

        var domainEvents = CollectDomainEvents();

        var result = await base.SaveChangesAsync(cancellationToken);

        await DispatchDomainEvents(domainEvents, cancellationToken);

        return result;
    }

    public override int SaveChanges()
    {
        ApplyAuditFields();
        ApplyConcurrencyVersioning();
        return base.SaveChanges();
    }

    /// <summary>
    /// Sets TenantId, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy on tracked entities.
    /// </summary>
    private void ApplyAuditFields()
    {
        var entries = ChangeTracker.Entries<Entity>();

        foreach (var entry in entries)
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.TenantId ??= CurrentTenantId;
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    // Prevent TenantId from being changed after creation
                    entry.Property(nameof(Entity.TenantId)).IsModified = false;
                    entry.Property(nameof(Entity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(Entity.CreatedBy)).IsModified = false;
                    break;

                case EntityState.Deleted:
                    // Convert hard deletes to soft deletes by default
                    entry.State = EntityState.Modified;
                    entry.Entity.MarkAsDeleted(entry.Entity.UpdatedBy);
                    break;
            }
        }
    }

    /// <summary>
    /// Increments version on modified aggregate roots for optimistic concurrency.
    /// </summary>
    private void ApplyConcurrencyVersioning()
    {
        var aggregateRoots = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in aggregateRoots)
        {
            entry.Entity.IncrementVersion();
        }
    }

    /// <summary>
    /// Collects domain events from all tracked entities before saving,
    /// then clears them from the entities.
    /// </summary>
    private List<IDomainEvent> CollectDomainEvents()
    {
        var entities = ChangeTracker.Entries<Entity>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .OrderBy(e => e.OccurredOn)
            .ToList();

        foreach (var entity in entities)
        {
            entity.ClearDomainEvents();
        }

        return domainEvents;
    }

    /// <summary>
    /// Dispatches collected domain events through MediatR after a successful save.
    /// </summary>
    private async Task DispatchDomainEvents(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        if (_mediator is null || domainEvents.Count == 0)
            return;

        foreach (var domainEvent in domainEvents)
        {
            _logger?.LogDebug(
                "Dispatching domain event {EventType} with Id {EventId}",
                domainEvent.EventType,
                domainEvent.Id);

            try
            {
                await _mediator.Publish(domainEvent, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex,
                    "Error dispatching domain event {EventType} with Id {EventId}",
                    domainEvent.EventType,
                    domainEvent.Id);
                throw;
            }
        }
    }

    /// <summary>
    /// Configures the database provider based on the tenant's connection string.
    /// Supports both SQL Server and PostgreSQL. The provider is determined by
    /// the "DbProvider" tenant setting or defaults to SQL Server.
    /// </summary>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (optionsBuilder.IsConfigured)
            return;

        var connectionString = _currentTenantService.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
            return;

        var dbProvider = _currentTenantService.GetSetting("DbProvider") ?? "SqlServer";

        switch (dbProvider.ToLowerInvariant())
        {
            case "postgresql":
            case "postgres":
            case "npgsql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                });
                break;

            case "sqlserver":
            case "mssql":
            default:
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
                break;
        }
    }
}
