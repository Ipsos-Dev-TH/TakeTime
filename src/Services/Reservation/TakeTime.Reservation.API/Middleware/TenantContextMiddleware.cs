using Serilog.Context;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Reservation.API.Middleware;

/// <summary>
/// Middleware that ensures tenant context is resolved and available for every request.
/// Extracts tenant ID from the X-Tenant-Id header, subdomain, or route parameter,
/// and pushes it into the Serilog LogContext for structured logging.
/// </summary>
public class TenantContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantContextMiddleware> _logger;

    private const string TenantHeaderName = "X-Tenant-Id";

    public TenantContextMiddleware(
        RequestDelegate next,
        ILogger<TenantContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentTenantService currentTenantService)
    {
        var tenantId = ResolveTenantId(context);

        if (string.IsNullOrEmpty(tenantId))
        {
            // Allow health check and swagger endpoints without tenant context
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";
            if (path.StartsWith("/health") ||
                path.StartsWith("/swagger") ||
                path.StartsWith("/api-docs") ||
                path == "/" ||
                path == "/favicon.ico")
            {
                await _next(context);
                return;
            }

            _logger.LogWarning(
                "No tenant context found for request {Method} {Path}. " +
                "Provide tenant ID via {Header} header, subdomain, or route parameter.",
                context.Request.Method,
                context.Request.Path,
                TenantHeaderName);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Tenant context is required.",
                detail = $"Provide the tenant identifier via the '{TenantHeaderName}' request header."
            });
            return;
        }

        // Set the tenant context for the current request scope
        currentTenantService.SetTenant(tenantId);

        // Push tenant ID into Serilog LogContext for structured logging
        using (LogContext.PushProperty("TenantId", tenantId))
        {
            _logger.LogDebug("Tenant context set to {TenantId} for {Method} {Path}",
                tenantId, context.Request.Method, context.Request.Path);

            await _next(context);
        }
    }

    /// <summary>
    /// Resolves the tenant ID from various sources in priority order:
    /// 1. X-Tenant-Id header
    /// 2. Subdomain (first segment before first dot)
    /// 3. Route parameter "tenantId"
    /// 4. JWT claim "tenant_id"
    /// </summary>
    private static string? ResolveTenantId(HttpContext context)
    {
        // 1. From header
        if (context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValue) &&
            !string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.ToString().Trim();
        }

        // 2. From subdomain
        var host = context.Request.Host.Host;
        if (!string.IsNullOrEmpty(host) && host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (!string.IsNullOrEmpty(subdomain) &&
                subdomain != "www" &&
                subdomain != "api" &&
                subdomain != "localhost")
            {
                return subdomain;
            }
        }

        // 3. From route parameter
        if (context.Request.RouteValues.TryGetValue("tenantId", out var routeValue) &&
            routeValue is string routeTenantId &&
            !string.IsNullOrWhiteSpace(routeTenantId))
        {
            return routeTenantId;
        }

        // 4. From JWT claims
        var tenantClaim = context.User?.FindFirst("tenant_id")?.Value;
        if (!string.IsNullOrWhiteSpace(tenantClaim))
        {
            return tenantClaim;
        }

        return null;
    }
}

/// <summary>
/// Extension method for registering the tenant context middleware.
/// </summary>
public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<TenantContextMiddleware>();
    }
}
