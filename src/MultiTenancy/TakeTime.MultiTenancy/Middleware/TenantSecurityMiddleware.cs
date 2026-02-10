using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Resolution;

namespace TakeTime.MultiTenancy.Middleware;

/// <summary>
/// Security middleware that enforces tenant isolation. When strict isolation
/// is enabled, this middleware verifies that the authenticated user's tenant
/// claim matches the resolved tenant for the request, preventing cross-tenant
/// data access attacks.
///
/// Pipeline position: This middleware must run AFTER <see cref="TenantResolutionMiddleware"/>
/// and after authentication middleware.
/// </summary>
public class TenantSecurityMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantSecurityMiddleware> _logger;
    private readonly TenantConfiguration _config;

    /// <summary>
    /// Roles that are allowed to access any tenant (super-admin, platform-admin).
    /// These users can perform cross-tenant operations.
    /// </summary>
    private static readonly HashSet<string> CrossTenantRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "SuperAdmin",
        "PlatformAdmin",
        "SystemAdmin"
    };

    public TenantSecurityMiddleware(
        RequestDelegate next,
        ILogger<TenantSecurityMiddleware> logger,
        IOptions<TenantConfiguration> config)
    {
        _next = next;
        _logger = logger;
        _config = config.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip if strict isolation is not enforced
        if (!_config.EnforceStrictIsolation)
        {
            await _next(context);
            return;
        }

        // Skip if no tenant was resolved (public endpoints)
        if (!context.Items.TryGetValue(TenantResolutionMiddleware.TenantItemKey, out var tenantObj)
            || tenantObj is not Tenant resolvedTenant)
        {
            await _next(context);
            return;
        }

        // Skip if the user is not authenticated
        var user = context.User;
        if (user.Identity is null || !user.Identity.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        // Allow cross-tenant roles (super-admins) to bypass isolation
        if (HasCrossTenantRole(context))
        {
            _logger.LogDebug(
                "User '{User}' has cross-tenant role, bypassing tenant isolation check",
                user.Identity.Name);
            await _next(context);
            return;
        }

        // Get the user's tenant claim
        var userTenantClaim =
            user.FindFirst(ClaimsTenantResolutionStrategy.TenantIdClaimType)
            ?? user.FindFirst(ClaimsTenantResolutionStrategy.TenantIdClaimTypeAlt);

        if (userTenantClaim is null || string.IsNullOrWhiteSpace(userTenantClaim.Value))
        {
            // User has no tenant claim -- could be a platform-level user
            // or a misconfigured token. Log and allow through with a warning.
            _logger.LogWarning(
                "Authenticated user '{User}' has no tenant claim. " +
                "Consider adding tenant_id to the JWT. Request to tenant '{TenantCode}'.",
                user.Identity.Name, resolvedTenant.Code);

            // In strict mode, block users without tenant claims from accessing tenant resources
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Access denied. Your authentication token does not contain a tenant identifier."}""");
            return;
        }

        // Verify the user's tenant matches the resolved tenant
        var userTenantId = userTenantClaim.Value.Trim();
        var resolvedTenantId = resolvedTenant.Id.ToString();
        var resolvedTenantCode = resolvedTenant.Code;

        var isMatch = userTenantId.Equals(resolvedTenantId, StringComparison.OrdinalIgnoreCase)
                      || userTenantId.Equals(resolvedTenantCode, StringComparison.OrdinalIgnoreCase);

        if (!isMatch)
        {
            _logger.LogWarning(
                "TENANT ISOLATION VIOLATION: User '{User}' (tenant claim: {UserTenant}) " +
                "attempted to access tenant '{ResolvedTenant}' ({ResolvedTenantId}). " +
                "Request: {Method} {Path}",
                user.Identity.Name, userTenantId,
                resolvedTenantCode, resolvedTenantId,
                context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Access denied. You do not have permission to access this tenant's resources."}""");
            return;
        }

        _logger.LogDebug(
            "Tenant isolation check passed for user '{User}' on tenant '{TenantCode}'",
            user.Identity.Name, resolvedTenantCode);

        await _next(context);
    }

    private static bool HasCrossTenantRole(HttpContext context)
    {
        var user = context.User;

        foreach (var role in CrossTenantRoles)
        {
            if (user.IsInRole(role))
                return true;
        }

        // Also check for role claims directly (some JWT providers use different claim types)
        var roleClaims = user.FindAll("role")
            .Concat(user.FindAll(System.Security.Claims.ClaimTypes.Role));

        foreach (var claim in roleClaims)
        {
            if (CrossTenantRoles.Contains(claim.Value))
                return true;
        }

        return false;
    }
}
