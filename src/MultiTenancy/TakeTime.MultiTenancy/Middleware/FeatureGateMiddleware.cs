using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TakeTime.MultiTenancy.Features;

namespace TakeTime.MultiTenancy.Middleware;

/// <summary>
/// Middleware that performs route-level feature gating based on the current
/// tenant's feature configuration. Maps API route prefixes to feature modules
/// and returns 403 if the module is disabled.
///
/// This provides a coarse-grained safety net in addition to the fine-grained
/// <see cref="FeatureGateAttribute"/> on individual controllers/actions.
/// </summary>
public class FeatureGateMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<FeatureGateMiddleware> _logger;

    /// <summary>
    /// Maps API route segments to their corresponding feature modules.
    /// </summary>
    private static readonly Dictionary<string, FeatureModule> RouteModuleMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["reservations"] = FeatureModule.Reservation,
        ["accommodations"] = FeatureModule.Reservation,
        ["payments"] = FeatureModule.Payment,
        ["receipts"] = FeatureModule.Payment,
        ["tax-invoices"] = FeatureModule.Payment,
        ["products"] = FeatureModule.Inventory,
        ["sales"] = FeatureModule.Inventory,
        ["inventory"] = FeatureModule.Inventory,
        ["employees"] = FeatureModule.HumanResources,
        ["leave"] = FeatureModule.HumanResources,
        ["payroll"] = FeatureModule.HumanResources,
        ["customers"] = FeatureModule.CRM,
        ["loyalty"] = FeatureModule.CRM,
        ["reviews"] = FeatureModule.CRM,
        ["channels"] = FeatureModule.ChannelManager,
        ["housekeeping"] = FeatureModule.GuestExperience,
        ["room-service"] = FeatureModule.GuestExperience,
        ["maintenance"] = FeatureModule.GuestExperience,
        ["reports"] = FeatureModule.Accounting,
        ["notifications"] = FeatureModule.Notification,
        ["affiliates"] = FeatureModule.Affiliate
    };

    public FeatureGateMiddleware(
        RequestDelegate next,
        ILogger<FeatureGateMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // Skip non-API routes and health checks
        if (string.IsNullOrEmpty(path)
            || !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            || path.Contains("/health", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Extract the resource segment from the path (e.g., /api/v1/reservations -> reservations)
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string? resourceSegment = null;

        for (int i = 0; i < segments.Length; i++)
        {
            // Skip "api" and version segments (e.g., "v1", "v2")
            if (segments[i].Equals("api", StringComparison.OrdinalIgnoreCase))
                continue;
            if (segments[i].StartsWith('v') && segments[i].Length <= 3
                && char.IsDigit(segments[i][1]))
                continue;

            resourceSegment = segments[i];
            break;
        }

        if (resourceSegment is null || !RouteModuleMap.TryGetValue(resourceSegment, out var module))
        {
            await _next(context);
            return;
        }

        // Check if the module is enabled for the current tenant
        var featureManager = context.RequestServices.GetService(typeof(IFeatureManager)) as IFeatureManager;
        if (featureManager is null)
        {
            await _next(context);
            return;
        }

        var isEnabled = await featureManager.IsModuleEnabledAsync(module, context.RequestAborted);

        if (!isEnabled)
        {
            _logger.LogWarning(
                "Feature gate blocked request to {Path}: Module '{Module}' is disabled for current tenant",
                path, module);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Feature not available",
                module = module.ToString(),
                message = $"The '{module}' module is not enabled for your organization."
            });
            return;
        }

        await _next(context);
    }
}
