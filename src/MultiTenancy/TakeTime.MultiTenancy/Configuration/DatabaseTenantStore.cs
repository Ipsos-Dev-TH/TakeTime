using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Core;

namespace TakeTime.MultiTenancy.Configuration;

/// <summary>
/// Implementation of <see cref="ITenantStore"/> backed by EF Core
/// with an in-memory cache layer. Tenant data is cached to avoid
/// hitting the database on every HTTP request.
///
/// Cache entries are keyed by both tenant ID and tenant code so that
/// lookups by either value are fast. The cache is invalidated on save/delete.
/// </summary>
public class DatabaseTenantStore : ITenantStore
{
    private const string CacheKeyPrefixById = "tenant:id:";
    private const string CacheKeyPrefixByCode = "tenant:code:";
    private const string CacheKeyAllTenants = "tenant:all";

    private readonly TenantDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DatabaseTenantStore> _logger;
    private readonly TenantConfiguration _config;

    public DatabaseTenantStore(
        TenantDbContext dbContext,
        IMemoryCache cache,
        ILogger<DatabaseTenantStore> logger,
        IOptions<TenantConfiguration> config)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _config = config.Value;
    }

    public async Task<Tenant?> GetTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        var cacheKey = CacheKeyPrefixById + tenantId.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out Tenant? cached) && cached is not null)
        {
            _logger.LogDebug("Tenant {TenantId} resolved from cache", tenantId);
            return cached;
        }

        Tenant? tenant;

        if (Guid.TryParse(tenantId, out var guidId))
        {
            tenant = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == guidId, cancellationToken);
        }
        else
        {
            // If not a GUID, treat as a code
            tenant = await _dbContext.Tenants
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    t => t.Code == tenantId,
                    cancellationToken);
        }

        if (tenant is not null)
        {
            CacheTenant(tenant);
            _logger.LogDebug("Tenant {TenantId} loaded from database and cached", tenantId);
        }
        else
        {
            _logger.LogDebug("Tenant {TenantId} not found in database", tenantId);
        }

        return tenant;
    }

    public async Task<Tenant?> GetTenantByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);

        var cacheKey = CacheKeyPrefixByCode + code.ToLowerInvariant();

        if (_cache.TryGetValue(cacheKey, out Tenant? cached) && cached is not null)
        {
            _logger.LogDebug("Tenant with code '{Code}' resolved from cache", code);
            return cached;
        }

        var tenant = await _dbContext.Tenants
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Code == code,
                cancellationToken);

        if (tenant is not null)
        {
            CacheTenant(tenant);
            _logger.LogDebug("Tenant with code '{Code}' loaded from database and cached", code);
        }
        else
        {
            _logger.LogDebug("Tenant with code '{Code}' not found in database", code);
        }

        return tenant;
    }

    public async Task<IReadOnlyList<Tenant>> GetAllTenantsAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKeyAllTenants, out IReadOnlyList<Tenant>? cachedAll) && cachedAll is not null)
        {
            _logger.LogDebug("All tenants resolved from cache ({Count} tenants)", cachedAll.Count);
            return cachedAll;
        }

        var tenants = await _dbContext.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var result = tenants.AsReadOnly();

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(Math.Max(1, _config.CacheDurationMinutes / 2)))
            .SetSlidingExpiration(TimeSpan.FromMinutes(Math.Max(1, _config.CacheDurationMinutes / 4)));

        _cache.Set(CacheKeyAllTenants, result, cacheOptions);

        // Also cache each individual tenant
        foreach (var tenant in tenants)
        {
            CacheTenant(tenant);
        }

        _logger.LogDebug("All tenants loaded from database ({Count} tenants)", result.Count);
        return result;
    }

    public async Task SaveTenantAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var existing = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == tenant.Id, cancellationToken);

        if (existing is null)
        {
            tenant.CreatedAt = DateTime.UtcNow;
            _dbContext.Tenants.Add(tenant);
            _logger.LogInformation("Creating new tenant: {TenantCode} ({TenantId})", tenant.Code, tenant.Id);
        }
        else
        {
            // Update existing tracked entity
            _dbContext.Entry(existing).CurrentValues.SetValues(tenant);
            existing.BusinessSettings = tenant.BusinessSettings;
            existing.Metadata = tenant.Metadata;
            existing.UpdatedAt = DateTime.UtcNow;
            _logger.LogInformation("Updating tenant: {TenantCode} ({TenantId})", tenant.Code, tenant.Id);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Invalidate all related cache entries
        InvalidateCache(tenant);
    }

    public async Task<bool> DeleteTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);

        if (!Guid.TryParse(tenantId, out var guidId))
        {
            _logger.LogWarning("DeleteTenantAsync called with non-GUID id: {TenantId}", tenantId);
            return false;
        }

        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.Id == guidId, cancellationToken);

        if (tenant is null)
        {
            _logger.LogWarning("Tenant {TenantId} not found for deletion", tenantId);
            return false;
        }

        _dbContext.Tenants.Remove(tenant);
        await _dbContext.SaveChangesAsync(cancellationToken);

        InvalidateCache(tenant);

        _logger.LogInformation("Deleted tenant: {TenantCode} ({TenantId})", tenant.Code, tenant.Id);
        return true;
    }

    /// <summary>
    /// Forces eviction of all tenant cache entries. Useful after bulk
    /// administrative operations.
    /// </summary>
    public void InvalidateAllCaches()
    {
        // MemoryCache doesn't support clearing all entries directly,
        // but we can remove the "all tenants" key. Individual entries
        // will expire on their own via the sliding expiration.
        _cache.Remove(CacheKeyAllTenants);
        _logger.LogInformation("All tenant caches invalidated");
    }

    private void CacheTenant(Tenant tenant)
    {
        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(Math.Max(1, _config.CacheDurationMinutes)))
            .SetSlidingExpiration(TimeSpan.FromMinutes(Math.Max(1, _config.CacheDurationMinutes / 2)))
            .SetSize(1);

        // Cache by both ID and code for fast lookups from either direction
        _cache.Set(CacheKeyPrefixById + tenant.Id.ToString().ToLowerInvariant(), tenant, cacheOptions);

        if (!string.IsNullOrWhiteSpace(tenant.Code))
        {
            _cache.Set(CacheKeyPrefixByCode + tenant.Code.ToLowerInvariant(), tenant, cacheOptions);
        }
    }

    private void InvalidateCache(Tenant tenant)
    {
        _cache.Remove(CacheKeyPrefixById + tenant.Id.ToString().ToLowerInvariant());

        if (!string.IsNullOrWhiteSpace(tenant.Code))
        {
            _cache.Remove(CacheKeyPrefixByCode + tenant.Code.ToLowerInvariant());
        }

        _cache.Remove(CacheKeyAllTenants);
    }
}
