namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Abstraction for tenant persistence. Implementations may use a database,
/// configuration files, or an external service to store and retrieve tenant information.
/// </summary>
public interface ITenantStore
{
    /// <summary>
    /// Retrieves a tenant by its unique identifier.
    /// </summary>
    /// <param name="tenantId">The tenant's GUID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant if found; otherwise null.</returns>
    Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a tenant by its unique code (e.g., "taketime-bangphra").
    /// </summary>
    /// <param name="code">The tenant code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tenant if found; otherwise null.</returns>
    Task<Tenant?> GetTenantByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all registered tenants.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all tenants.</returns>
    Task<IReadOnlyList<Tenant>> GetAllTenantsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists a tenant (insert or update). If the tenant already exists
    /// (matched by Id), it is updated; otherwise a new record is created.
    /// </summary>
    /// <param name="tenant">The tenant to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveTenantAsync(Tenant tenant, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a tenant by its unique identifier.
    /// </summary>
    /// <param name="tenantId">The tenant's GUID as a string.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the tenant was found and deleted; false if not found.</returns>
    Task<bool> DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default);
}
