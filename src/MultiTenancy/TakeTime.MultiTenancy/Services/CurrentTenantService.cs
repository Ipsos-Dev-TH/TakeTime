using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Middleware;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Scoped service that provides the current tenant context by reading from
/// <see cref="HttpContext.Items"/> where it was placed by the
/// <see cref="TenantResolutionMiddleware"/>.
///
/// This service is registered as scoped because it is tied to a single HTTP
/// request. Non-HTTP scenarios (background jobs, message handlers) should use
/// a different mechanism to set the tenant (e.g., <see cref="SetTenant"/>).
/// </summary>
public class CurrentTenantService : ICurrentTenantService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TenantConfiguration _config;
    private readonly ILogger<CurrentTenantService> _logger;

    // Fallback tenant holder for non-HTTP scenarios (background tasks)
    private Tenant? _explicitTenant;

    public CurrentTenantService(
        IHttpContextAccessor httpContextAccessor,
        IOptions<TenantConfiguration> config,
        ILogger<CurrentTenantService> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _config = config.Value;
        _logger = logger;
    }

    public string? TenantId => CurrentTenant?.Id.ToString();

    public string? TenantCode => CurrentTenant?.Code;

    public string? TenantName => CurrentTenant?.Name;

    public Tenant? CurrentTenant
    {
        get
        {
            // First check for an explicitly set tenant (background scenarios)
            if (_explicitTenant is not null)
                return _explicitTenant;

            // Then try to read from HttpContext.Items
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
                return null;

            if (httpContext.Items.TryGetValue(TenantResolutionMiddleware.TenantItemKey, out var tenantObj)
                && tenantObj is Tenant tenant)
            {
                return tenant;
            }

            return null;
        }
    }

    public string? ConnectionString
    {
        get
        {
            var tenant = CurrentTenant;
            if (tenant is null)
                return null;

            // Return the tenant-specific connection string if available,
            // otherwise fall back to the shared connection string
            return !string.IsNullOrWhiteSpace(tenant.DatabaseConnectionString)
                ? tenant.DatabaseConnectionString
                : _config.ConnectionString;
        }
    }

    public TenantBusinessSettings? BusinessSettings => CurrentTenant?.BusinessSettings;

    public bool HasTenant => CurrentTenant is not null;

    public void SetTenant(Tenant tenant)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        _explicitTenant = tenant;

        // Also set in HttpContext if available (for consistency)
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            httpContext.Items[TenantResolutionMiddleware.TenantItemKey] = tenant;
            httpContext.Items[TenantResolutionMiddleware.TenantIdItemKey] = tenant.Id.ToString();
        }

        _logger.LogDebug(
            "Tenant explicitly set to '{TenantCode}' ({TenantId})",
            tenant.Code, tenant.Id);
    }

    public void ClearTenant()
    {
        _explicitTenant = null;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            httpContext.Items.Remove(TenantResolutionMiddleware.TenantItemKey);
            httpContext.Items.Remove(TenantResolutionMiddleware.TenantIdItemKey);
            httpContext.Items.Remove(TenantResolutionMiddleware.ResolutionStrategyItemKey);
        }

        _logger.LogDebug("Tenant context cleared");
    }
}
