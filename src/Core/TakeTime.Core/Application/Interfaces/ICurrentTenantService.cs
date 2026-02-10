namespace TakeTime.Core.Application.Interfaces;

/// <summary>
/// Provides access to the current tenant's context information.
/// Implementations typically resolve tenant details from the HTTP request
/// (e.g., subdomain, header, or JWT claim) and cache them per-request.
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// Unique identifier of the current tenant.
    /// Returns null if no tenant context has been established.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// Display name of the current tenant.
    /// </summary>
    string? TenantName { get; }

    /// <summary>
    /// Tenant-specific database connection string.
    /// Used in database-per-tenant isolation strategies.
    /// </summary>
    string? ConnectionString { get; }

    /// <summary>
    /// Dictionary of tenant-specific settings and feature flags.
    /// </summary>
    IDictionary<string, string> TenantSettings { get; }

    /// <summary>
    /// Whether a valid tenant context has been established for the current request.
    /// </summary>
    bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);

    /// <summary>
    /// Sets the tenant context for the current scope.
    /// Typically called by middleware during request initialization.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="tenantName">The tenant display name.</param>
    /// <param name="connectionString">Optional tenant-specific connection string.</param>
    Task SetTenantAsync(string tenantId, string? tenantName = null, string? connectionString = null);

    /// <summary>
    /// Retrieves a specific tenant setting by key.
    /// Returns null if the key does not exist.
    /// </summary>
    string? GetSetting(string key);
}
