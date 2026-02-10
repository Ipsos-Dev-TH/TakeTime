using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Provides access to the current tenant context for the active HTTP request.
/// Inject this service wherever you need to know which tenant the current
/// request belongs to.
///
/// This interface is designed to be consumed by application services, repositories,
/// and domain handlers that need tenant-aware behavior (e.g., applying tenant
/// filters to queries, selecting the correct database connection, or reading
/// tenant-specific business rules).
/// </summary>
public interface ICurrentTenantService
{
    /// <summary>
    /// The unique identifier (GUID) of the current tenant, or null
    /// if no tenant has been resolved for this request.
    /// </summary>
    string? TenantId { get; }

    /// <summary>
    /// The short code of the current tenant (e.g., "taketime-bangphra"),
    /// or null if no tenant has been resolved.
    /// </summary>
    string? TenantCode { get; }

    /// <summary>
    /// The display name of the current tenant, or null if no tenant
    /// has been resolved.
    /// </summary>
    string? TenantName { get; }

    /// <summary>
    /// The full <see cref="Tenant"/> entity for the current request,
    /// or null if no tenant has been resolved.
    /// </summary>
    Tenant? CurrentTenant { get; }

    /// <summary>
    /// The connection string for the current tenant's database.
    /// Returns the tenant-specific connection string if configured,
    /// otherwise falls back to the shared/default connection string.
    /// </summary>
    string? ConnectionString { get; }

    /// <summary>
    /// The business settings for the current tenant, or null if no
    /// tenant has been resolved.
    /// </summary>
    TenantBusinessSettings? BusinessSettings { get; }

    /// <summary>
    /// Whether a tenant has been successfully resolved for the current request.
    /// </summary>
    bool HasTenant { get; }

    /// <summary>
    /// Sets the current tenant context. This is typically called by the
    /// <see cref="Middleware.TenantResolutionMiddleware"/> and should not
    /// be called by application code under normal circumstances.
    /// </summary>
    /// <param name="tenant">The resolved tenant.</param>
    void SetTenant(Tenant tenant);

    /// <summary>
    /// Clears the current tenant context.
    /// </summary>
    void ClearTenant();
}
