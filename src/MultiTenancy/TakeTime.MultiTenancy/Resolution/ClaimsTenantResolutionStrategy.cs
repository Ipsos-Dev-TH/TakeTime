using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TakeTime.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant identifier from the authenticated user's JWT claims.
/// Looks for a "tenant_id" or "tenantId" claim in the ClaimsPrincipal.
/// This strategy ensures that authenticated users are always routed to
/// their assigned tenant, regardless of which endpoint they call.
/// </summary>
public class ClaimsTenantResolutionStrategy : ITenantResolutionStrategy
{
    /// <summary>
    /// Standard claim type used to carry the tenant identifier in JWTs.
    /// </summary>
    public const string TenantIdClaimType = "tenant_id";

    /// <summary>
    /// Alternative camelCase claim type.
    /// </summary>
    public const string TenantIdClaimTypeAlt = "tenantId";

    /// <summary>
    /// Claim type carrying the tenant code (friendly name).
    /// </summary>
    public const string TenantCodeClaimType = "tenant_code";

    private readonly ILogger<ClaimsTenantResolutionStrategy> _logger;

    public string Name => "Claims";
    public int Priority => 200;

    public ClaimsTenantResolutionStrategy(ILogger<ClaimsTenantResolutionStrategy> logger)
    {
        _logger = logger;
    }

    public Task<string?> ResolveAsync(HttpContext context)
    {
        var user = context.User;

        if (user.Identity is null || !user.Identity.IsAuthenticated)
            return Task.FromResult<string?>(null);

        // Try the standard tenant_id claim first
        var tenantClaim = user.FindFirst(TenantIdClaimType)
                          ?? user.FindFirst(TenantIdClaimTypeAlt)
                          ?? user.FindFirst(TenantCodeClaimType);

        if (tenantClaim is null || string.IsNullOrWhiteSpace(tenantClaim.Value))
        {
            _logger.LogDebug(
                "Authenticated user '{User}' does not have a tenant claim",
                user.Identity.Name);
            return Task.FromResult<string?>(null);
        }

        _logger.LogDebug(
            "Resolved tenant from claim '{ClaimType}': {TenantId}",
            tenantClaim.Type, tenantClaim.Value);

        return Task.FromResult<string?>(tenantClaim.Value.Trim());
    }
}
