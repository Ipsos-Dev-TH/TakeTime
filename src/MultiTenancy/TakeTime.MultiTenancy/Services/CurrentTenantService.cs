using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Middleware;
using CoreTenantService = TakeTime.Core.Application.Interfaces.ICurrentTenantService;

namespace TakeTime.MultiTenancy.Services;

/// <summary>
/// Scoped service that provides the current tenant context by reading from
/// <see cref="HttpContext.Items"/> where it was placed by the
/// <see cref="TenantResolutionMiddleware"/>.
///
/// This service is registered as scoped because it is tied to a single HTTP
/// request. Non-HTTP scenarios (background jobs, message handlers) should use
/// a different mechanism to set the tenant (e.g., <see cref="SetTenant"/>).
///
/// Implements both the MultiTenancy and Core ICurrentTenantService interfaces
/// so that a single scoped instance can serve BaseDbContext, MediatR handlers,
/// and any other consumer that depends on either contract.
/// </summary>
public class CurrentTenantService : ICurrentTenantService, CoreTenantService
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

    // ─── Core ICurrentTenantService members ─────────────────────────

    /// <summary>
    /// Returns tenant settings as a flat dictionary, combining the tenant's
    /// Metadata dictionary with well-known BusinessSettings properties.
    /// </summary>
    IDictionary<string, string> CoreTenantService.TenantSettings => GetTenantSettingsDictionary();

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

    /// <summary>
    /// Implements Core ICurrentTenantService.SetTenantAsync by creating a
    /// minimal Tenant object and calling the existing SetTenant method.
    /// </summary>
    public Task SetTenantAsync(string tenantId, string? tenantName = null, string? connectionString = null)
    {
        var tenant = new Tenant
        {
            Id = Guid.TryParse(tenantId, out var guid) ? guid : Guid.NewGuid(),
            Code = tenantId,
            Name = tenantName ?? tenantId,
            DatabaseConnectionString = connectionString
        };

        SetTenant(tenant);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves a tenant setting by key. Checks the tenant's Metadata dictionary
    /// first, then falls back to well-known BusinessSettings properties.
    /// </summary>
    public string? GetSetting(string key)
    {
        var tenant = CurrentTenant;
        if (tenant is null)
            return null;

        // 1. Check Metadata dictionary first
        if (tenant.Metadata.TryGetValue(key, out var metaValue))
            return metaValue;

        // 2. Map well-known keys to BusinessSettings properties
        var settings = tenant.BusinessSettings;
        if (settings is null)
            return null;

        return key switch
        {
            "Currency" => settings.DefaultCurrency,
            "DefaultCurrency" => settings.DefaultCurrency,
            "DbProvider" => null, // No specific BusinessSettings property; rely on Metadata
            "VATRate" => settings.VATRate.ToString(),
            "EnableVAT" => settings.EnableVAT.ToString(),
            "TimeZone" => settings.TimeZone,
            "DefaultLanguage" => settings.DefaultLanguage,
            "DateFormat" => settings.DateFormat,
            "PricingModel" => settings.PricingModel.ToString(),
            _ => null
        };
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

    private Dictionary<string, string> GetTenantSettingsDictionary()
    {
        var tenant = CurrentTenant;
        if (tenant is null)
            return new Dictionary<string, string>();

        // Start with Metadata
        var dict = new Dictionary<string, string>(tenant.Metadata);

        // Add well-known BusinessSettings values
        var settings = tenant.BusinessSettings;
        if (settings is not null)
        {
            dict.TryAdd("Currency", settings.DefaultCurrency);
            dict.TryAdd("TimeZone", settings.TimeZone);
            dict.TryAdd("DefaultLanguage", settings.DefaultLanguage);
            dict.TryAdd("VATRate", settings.VATRate.ToString());
            dict.TryAdd("EnableVAT", settings.EnableVAT.ToString());
        }

        return dict;
    }
}
