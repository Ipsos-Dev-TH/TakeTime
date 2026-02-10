using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Resolution;

namespace TakeTime.MultiTenancy.Middleware;

/// <summary>
/// ASP.NET Core middleware that resolves the current tenant for each HTTP request.
///
/// Resolution flows through the registered <see cref="ITenantResolutionStrategy"/>
/// implementations in priority order (lowest number first). The first strategy
/// to return a non-null result wins. The resolved <see cref="Tenant"/> is placed
/// into <c>HttpContext.Items</c> for downstream consumption.
///
/// Pipeline position: This middleware should run early, after authentication
/// but before authorization and application logic, so that all downstream
/// components have access to the tenant context.
/// </summary>
public class TenantResolutionMiddleware
{
    /// <summary>
    /// Key used to store/retrieve the resolved Tenant from HttpContext.Items.
    /// </summary>
    public const string TenantItemKey = "CurrentTenant";

    /// <summary>
    /// Key used to store the raw resolved tenant identifier (before lookup).
    /// </summary>
    public const string TenantIdItemKey = "CurrentTenantId";

    /// <summary>
    /// Key used to store the name of the strategy that resolved the tenant.
    /// </summary>
    public const string ResolutionStrategyItemKey = "TenantResolutionStrategy";

    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;
    private readonly TenantConfiguration _config;

    public TenantResolutionMiddleware(
        RequestDelegate next,
        ILogger<TenantResolutionMiddleware> logger,
        IOptions<TenantConfiguration> config)
    {
        _next = next;
        _logger = logger;
        _config = config.Value;
    }

    public async Task InvokeAsync(
        HttpContext context,
        IEnumerable<ITenantResolutionStrategy> strategies,
        ITenantStore tenantStore)
    {
        // Skip tenant resolution for excluded paths
        if (IsExcludedPath(context.Request.Path))
        {
            _logger.LogDebug("Skipping tenant resolution for excluded path: {Path}", context.Request.Path);
            await _next(context);
            return;
        }

        // Attempt resolution through strategies in priority order
        var orderedStrategies = strategies.OrderBy(s => s.Priority);
        string? resolvedIdentifier = null;
        string? winningStrategy = null;

        foreach (var strategy in orderedStrategies)
        {
            try
            {
                resolvedIdentifier = await strategy.ResolveAsync(context);
                if (!string.IsNullOrWhiteSpace(resolvedIdentifier))
                {
                    winningStrategy = strategy.Name;
                    _logger.LogDebug(
                        "Tenant resolved by {Strategy} strategy: {TenantIdentifier}",
                        strategy.Name, resolvedIdentifier);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Tenant resolution strategy {Strategy} threw an exception",
                    strategy.Name);
            }
        }

        // If no strategy resolved a tenant, try the default
        if (string.IsNullOrWhiteSpace(resolvedIdentifier) &&
            !string.IsNullOrWhiteSpace(_config.DefaultTenantCode))
        {
            resolvedIdentifier = _config.DefaultTenantCode;
            winningStrategy = "Default";
            _logger.LogDebug(
                "Using default tenant code: {DefaultTenantCode}",
                _config.DefaultTenantCode);
        }

        // If still no tenant and it's required, return 400
        if (string.IsNullOrWhiteSpace(resolvedIdentifier))
        {
            if (_config.RequireTenant)
            {
                _logger.LogWarning(
                    "Tenant resolution failed for {Method} {Path} and RequireTenant is enabled",
                    context.Request.Method, context.Request.Path);

                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"error":"Tenant identification required. Provide X-Tenant-Id header, include tenant claim in JWT, or use a tenant subdomain."}""");
                return;
            }

            // Tenant not required -- proceed without tenant context
            _logger.LogDebug("No tenant resolved for {Path}, proceeding without tenant context",
                context.Request.Path);
            await _next(context);
            return;
        }

        // Look up the full tenant record from the store
        var tenant = await tenantStore.GetTenantAsync(resolvedIdentifier);

        // If resolved identifier didn't match by ID, try by code
        if (tenant is null)
        {
            tenant = await tenantStore.GetTenantByCodeAsync(resolvedIdentifier);
        }

        if (tenant is null)
        {
            _logger.LogWarning(
                "Tenant identifier '{TenantIdentifier}' resolved by {Strategy} but not found in tenant store",
                resolvedIdentifier, winningStrategy);

            if (_config.RequireTenant)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(
                    """{"error":"Tenant not found. The specified tenant does not exist or has been removed."}""");
                return;
            }

            await _next(context);
            return;
        }

        // Verify the tenant is active and operational
        if (!tenant.IsOperational())
        {
            _logger.LogWarning(
                "Tenant '{TenantCode}' ({TenantId}) resolved but is not operational (Active={IsActive}, SubscriptionValid={SubValid})",
                tenant.Code, tenant.Id, tenant.IsActive, tenant.IsSubscriptionActive());

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                """{"error":"Tenant is currently inactive or the subscription has expired. Please contact support."}""");
            return;
        }

        // Store tenant in HttpContext.Items for downstream access
        context.Items[TenantItemKey] = tenant;
        context.Items[TenantIdItemKey] = tenant.Id.ToString();
        context.Items[ResolutionStrategyItemKey] = winningStrategy;

        // Optionally include the tenant ID in the response header (for debugging)
        if (_config.IncludeTenantInResponse)
        {
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[_config.TenantIdResponseHeader] = tenant.Id.ToString();
                return Task.CompletedTask;
            });
        }

        _logger.LogInformation(
            "Request routed to tenant '{TenantName}' ({TenantCode}/{TenantId}) via {Strategy}",
            tenant.Name, tenant.Code, tenant.Id, winningStrategy);

        await _next(context);
    }

    private bool IsExcludedPath(PathString path)
    {
        if (!path.HasValue)
            return false;

        foreach (var excluded in _config.ExcludedPaths)
        {
            if (path.StartsWithSegments(excluded, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
