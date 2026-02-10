using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TakeTime.MultiTenancy.Configuration;

namespace TakeTime.MultiTenancy.Resolution;

/// <summary>
/// Resolves the tenant from the request's subdomain. For example, given a base
/// domain of "taketime.com", a request to "bangphra.taketime.com" resolves
/// the tenant code "bangphra".
///
/// This strategy is ideal for white-label / SaaS deployments where each
/// property gets its own subdomain.
/// </summary>
public class SubdomainTenantResolutionStrategy : ITenantResolutionStrategy
{
    private readonly ILogger<SubdomainTenantResolutionStrategy> _logger;
    private readonly TenantConfiguration _config;

    public string Name => "Subdomain";
    public int Priority => 300;

    public SubdomainTenantResolutionStrategy(
        ILogger<SubdomainTenantResolutionStrategy> logger,
        IOptions<TenantConfiguration> config)
    {
        _logger = logger;
        _config = config.Value;
    }

    public Task<string?> ResolveAsync(HttpContext context)
    {
        var host = context.Request.Host.Host;

        if (string.IsNullOrWhiteSpace(host))
            return Task.FromResult<string?>(null);

        // Skip resolution for IP addresses
        if (System.Net.IPAddress.TryParse(host, out _))
            return Task.FromResult<string?>(null);

        // Skip localhost
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult<string?>(null);

        var baseDomain = _config.BaseDomain;

        if (string.IsNullOrWhiteSpace(baseDomain))
        {
            _logger.LogWarning(
                "Subdomain resolution skipped: BaseDomain is not configured");
            return Task.FromResult<string?>(null);
        }

        // Ensure the host ends with the base domain
        if (!host.EndsWith($".{baseDomain}", StringComparison.OrdinalIgnoreCase))
        {
            // Check if it's the base domain itself (no subdomain)
            if (host.Equals(baseDomain, StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<string?>(null);

            _logger.LogDebug(
                "Host '{Host}' does not match base domain '{BaseDomain}'",
                host, baseDomain);
            return Task.FromResult<string?>(null);
        }

        // Extract the subdomain portion
        var subdomain = host[..^(baseDomain.Length + 1)];

        // Ignore common non-tenant subdomains
        if (IsReservedSubdomain(subdomain))
        {
            _logger.LogDebug(
                "Subdomain '{Subdomain}' is reserved and will not be treated as a tenant",
                subdomain);
            return Task.FromResult<string?>(null);
        }

        if (string.IsNullOrWhiteSpace(subdomain))
            return Task.FromResult<string?>(null);

        _logger.LogDebug(
            "Resolved tenant code from subdomain: {TenantCode}", subdomain);

        return Task.FromResult<string?>(subdomain.ToLowerInvariant());
    }

    private static bool IsReservedSubdomain(string subdomain)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "www", "api", "admin", "app", "mail", "smtp", "ftp",
            "staging", "dev", "test", "demo", "docs", "status",
            "cdn", "assets", "static", "media"
        };

        return reserved.Contains(subdomain);
    }
}
