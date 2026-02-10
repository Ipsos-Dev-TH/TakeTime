using Microsoft.AspNetCore.Http;

namespace TakeTime.MultiTenancy.Resolution;

/// <summary>
/// Defines a strategy for resolving the current tenant identifier from an HTTP request.
/// Multiple strategies can be registered and evaluated in priority order by the
/// <see cref="Middleware.TenantResolutionMiddleware"/>.
/// </summary>
public interface ITenantResolutionStrategy
{
    /// <summary>
    /// Display name of the strategy (used for logging and diagnostics).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Priority of this strategy. Lower values are evaluated first.
    /// Default strategies use: Header=100, Claim=200, Subdomain=300, QueryString=400.
    /// </summary>
    int Priority { get; }

    /// <summary>
    /// Attempts to resolve a tenant identifier from the given HTTP context.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <returns>
    /// The resolved tenant identifier (GUID string or tenant code), or null
    /// if this strategy cannot resolve a tenant from the current request.
    /// </returns>
    Task<string?> ResolveAsync(HttpContext context);
}
