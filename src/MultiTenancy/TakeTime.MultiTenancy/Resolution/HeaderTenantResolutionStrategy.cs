using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TakeTime.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant identifier from an HTTP request header.
/// Supports both <c>X-Tenant-Id</c> (GUID) and <c>X-Tenant-Code</c> (friendly code).
/// This is the highest-priority strategy and is typically used by API clients
/// and service-to-service communication.
/// </summary>
public class HeaderTenantResolutionStrategy : ITenantResolutionStrategy
{
    public const string TenantIdHeader = "X-Tenant-Id";
    public const string TenantCodeHeader = "X-Tenant-Code";

    private readonly ILogger<HeaderTenantResolutionStrategy> _logger;

    public string Name => "Header";
    public int Priority => 100;

    public HeaderTenantResolutionStrategy(ILogger<HeaderTenantResolutionStrategy> logger)
    {
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context)
    {
        // First try X-Tenant-Id header (GUID-based)
        if (context.Request.Headers.TryGetValue(TenantIdHeader, out var tenantIdValues))
        {
            var tenantId = tenantIdValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                _logger.LogDebug(
                    "Resolved tenant from {Header} header: {TenantId}",
                    TenantIdHeader, tenantId);
                return Task.FromResult<string?>(tenantId.Trim());
            }
        }

        // Fall back to X-Tenant-Code header (code-based)
        if (context.Request.Headers.TryGetValue(TenantCodeHeader, out var tenantCodeValues))
        {
            var tenantCode = tenantCodeValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                _logger.LogDebug(
                    "Resolved tenant from {Header} header: {TenantCode}",
                    TenantCodeHeader, tenantCode);
                return Task.FromResult<string?>(tenantCode.Trim());
            }
        }

        return Task.FromResult<string?>(null);
    }
}
