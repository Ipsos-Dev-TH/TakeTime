using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TakeTime.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant identifier from a query string parameter.
/// Supports <c>?tenantId=xxx</c> and <c>?tenant=xxx</c>.
///
/// This is the lowest-priority strategy and is primarily intended for
/// development, testing, and special administrative endpoints. It should
/// normally be disabled in production via <see cref="Configuration.TenantConfiguration.EnableQueryStringResolution"/>.
/// </summary>
public class QueryStringTenantResolutionStrategy : ITenantResolutionStrategy
{
    public const string TenantIdParam = "tenantId";
    public const string TenantParam = "tenant";

    private readonly ILogger<QueryStringTenantResolutionStrategy> _logger;

    public string Name => "QueryString";
    public int Priority => 400;

    public QueryStringTenantResolutionStrategy(ILogger<QueryStringTenantResolutionStrategy> logger)
    {
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context)
    {
        // Try "tenantId" parameter first
        if (context.Request.Query.TryGetValue(TenantIdParam, out var tenantIdValues))
        {
            var tenantId = tenantIdValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                _logger.LogDebug(
                    "Resolved tenant from query string parameter '{Param}': {TenantId}",
                    TenantIdParam, tenantId);
                return Task.FromResult<string?>(tenantId.Trim());
            }
        }

        // Fall back to "tenant" parameter
        if (context.Request.Query.TryGetValue(TenantParam, out var tenantValues))
        {
            var tenant = tenantValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenant))
            {
                _logger.LogDebug(
                    "Resolved tenant from query string parameter '{Param}': {Tenant}",
                    TenantParam, tenant);
                return Task.FromResult<string?>(tenant.Trim());
            }
        }

        return Task.FromResult<string?>(null);
    }
}
