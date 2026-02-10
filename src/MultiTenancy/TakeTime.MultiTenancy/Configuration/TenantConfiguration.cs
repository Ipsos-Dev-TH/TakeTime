namespace TakeTime.MultiTenancy.Configuration;

/// <summary>
/// Configuration options for the multi-tenancy system.
/// Bound from the "MultiTenancy" section of appsettings.json.
/// </summary>
public class TenantConfiguration
{
    /// <summary>
    /// Configuration section key in appsettings.json.
    /// </summary>
    public const string SectionName = "MultiTenancy";

    /// <summary>
    /// Connection string for the central tenant management database.
    /// This database stores all Tenant records and their configurations.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Base domain used for subdomain-based tenant resolution
    /// (e.g., "taketime.com"). Requests to "bangphra.taketime.com"
    /// will resolve tenant code "bangphra".
    /// </summary>
    public string? BaseDomain { get; set; }

    /// <summary>
    /// Whether tenant resolution from HTTP headers is enabled.
    /// Default: true (always on for API clients).
    /// </summary>
    public bool EnableHeaderResolution { get; set; } = true;

    /// <summary>
    /// Whether tenant resolution from JWT claims is enabled.
    /// Default: true.
    /// </summary>
    public bool EnableClaimsResolution { get; set; } = true;

    /// <summary>
    /// Whether tenant resolution from subdomains is enabled.
    /// Requires <see cref="BaseDomain"/> to be set.
    /// </summary>
    public bool EnableSubdomainResolution { get; set; }

    /// <summary>
    /// Whether tenant resolution from query string is enabled.
    /// Default: false. Enable only in development/testing environments.
    /// </summary>
    public bool EnableQueryStringResolution { get; set; }

    /// <summary>
    /// Whether a valid tenant is required for every request.
    /// When true, requests that cannot be resolved to a tenant will receive a 400 response.
    /// When false, unresolved requests proceed without a tenant context (useful for
    /// public/shared endpoints like health checks).
    /// </summary>
    public bool RequireTenant { get; set; }

    /// <summary>
    /// URL path prefixes that are excluded from tenant resolution.
    /// Requests to these paths will skip tenant resolution entirely.
    /// Default includes health check, swagger, and static file paths.
    /// </summary>
    public List<string> ExcludedPaths { get; set; } =
    [
        "/health",
        "/healthz",
        "/ready",
        "/swagger",
        "/hangfire",
        "/_framework",
        "/favicon.ico"
    ];

    /// <summary>
    /// Duration in minutes to cache resolved tenant data in memory.
    /// Set to 0 to disable caching (not recommended for production).
    /// </summary>
    public int CacheDurationMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum number of tenants to keep in the memory cache.
    /// Helps prevent unbounded memory growth.
    /// </summary>
    public int MaxCachedTenants { get; set; } = 500;

    /// <summary>
    /// Default tenant code to use when no tenant can be resolved
    /// and <see cref="RequireTenant"/> is false. Useful for
    /// single-tenant deployments or default landing pages.
    /// </summary>
    public string? DefaultTenantCode { get; set; }

    /// <summary>
    /// Whether to enforce strict tenant isolation security checks.
    /// When enabled, the <see cref="Middleware.TenantSecurityMiddleware"/>
    /// validates that authenticated users belong to the resolved tenant.
    /// </summary>
    public bool EnforceStrictIsolation { get; set; } = true;

    /// <summary>
    /// HTTP header name used to pass the resolved tenant ID downstream
    /// (e.g., to microservices behind a gateway). The middleware
    /// sets this header on the response for debugging purposes in development.
    /// </summary>
    public string TenantIdResponseHeader { get; set; } = "X-Resolved-Tenant-Id";

    /// <summary>
    /// Whether to include the resolved tenant ID in the response headers.
    /// Should be disabled in production for security.
    /// </summary>
    public bool IncludeTenantInResponse { get; set; }
}
