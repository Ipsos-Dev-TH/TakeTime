using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Infrastructure.Database;

/// <summary>
/// Factory that creates DbContext instances with the correct connection string
/// per tenant, implementing a database-per-tenant isolation strategy.
/// </summary>
public class TenantDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : BaseDbContext
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<TenantDbContextFactory<TContext>> _logger;

    public TenantDbContextFactory(
        IServiceProvider serviceProvider,
        ICurrentTenantService currentTenantService,
        ILogger<TenantDbContextFactory<TContext>> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _currentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates a new DbContext configured for the current tenant's database.
    /// </summary>
    /// <returns>A configured DbContext instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no tenant context is established.</exception>
    public TContext CreateDbContext()
    {
        if (!_currentTenantService.HasTenant)
        {
            throw new InvalidOperationException(
                "Cannot create a tenant-scoped DbContext without an active tenant context. " +
                "Ensure the tenant resolution middleware has executed before accessing the database.");
        }

        var connectionString = _currentTenantService.ConnectionString;
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                $"No connection string configured for tenant '{_currentTenantService.TenantId}'. " +
                "Verify the tenant configuration includes a valid database connection string.");
        }

        _logger.LogDebug(
            "Creating DbContext of type {ContextType} for tenant {TenantId}",
            typeof(TContext).Name,
            _currentTenantService.TenantId);

        var dbProvider = _currentTenantService.GetSetting("DbProvider") ?? "SqlServer";
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();

        ConfigureProvider(optionsBuilder, connectionString, dbProvider);

        var context = (TContext)ActivatorUtilities.CreateInstance(
            _serviceProvider,
            typeof(TContext),
            new object[] { optionsBuilder.Options });

        return context;
    }

    private static void ConfigureProvider(
        DbContextOptionsBuilder optionsBuilder,
        string connectionString,
        string dbProvider)
    {
        switch (dbProvider.ToLowerInvariant())
        {
            case "postgresql":
            case "postgres":
            case "npgsql":
                optionsBuilder.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorCodesToAdd: null);
                    npgsqlOptions.CommandTimeout(30);
                });
                break;

            case "sqlserver":
            case "mssql":
            default:
                optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                    sqlOptions.CommandTimeout(30);
                });
                break;
        }
    }
}

/// <summary>
/// Provides tenant-aware connection string resolution for design-time DbContext creation
/// and migration tooling.
/// </summary>
public static class TenantConnectionResolver
{
    /// <summary>
    /// Resolves the connection string for a given tenant identifier.
    /// Used primarily for migration tooling and administrative operations.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="tenantConfigurations">Dictionary mapping tenant IDs to connection strings.</param>
    /// <returns>The tenant's database connection string.</returns>
    public static string ResolveConnectionString(
        string tenantId,
        IDictionary<string, string> tenantConfigurations)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        if (!tenantConfigurations.TryGetValue(tenantId, out var connectionString))
        {
            throw new InvalidOperationException(
                $"No database configuration found for tenant '{tenantId}'. " +
                $"Available tenants: {string.Join(", ", tenantConfigurations.Keys)}");
        }

        return connectionString;
    }
}
