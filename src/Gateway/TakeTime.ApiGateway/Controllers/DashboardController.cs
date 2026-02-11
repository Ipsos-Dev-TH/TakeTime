using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.MultiTenancy.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// System Dashboard API for platform administrators.
/// Provides overview statistics, health checks for all microservices,
/// and aggregated platform metrics.
/// </summary>
[ApiController]
[Route("api/v1/admin/dashboard")]
[Authorize(Roles = "Admin,SystemAdmin")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly TenantManagementService _tenantService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DashboardController> _logger;

    /// <summary>
    /// Microservice endpoints for health checks.
    /// </summary>
    private static readonly Dictionary<string, string> ServiceHealthEndpoints = new()
    {
        ["Reservation"] = "http://localhost:5001/health",
        ["Payment"] = "http://localhost:5002/health",
        ["Inventory"] = "http://localhost:5003/health",
        ["HumanResources"] = "http://localhost:5004/health",
        ["CRM"] = "http://localhost:5005/health",
        ["ChannelManager"] = "http://localhost:5006/health",
        ["GuestExperience"] = "http://localhost:5007/health",
        ["Accounting"] = "http://localhost:5008/health",
        ["Notification"] = "http://localhost:5009/health",
        ["Affiliate"] = "http://localhost:5010/health"
    };

    public DashboardController(
        TenantManagementService tenantService,
        IHttpClientFactory httpClientFactory,
        ILogger<DashboardController> logger)
    {
        _tenantService = tenantService ?? throw new ArgumentNullException(nameof(tenantService));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get system overview including total tenants, active tenants, and user counts.
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(typeof(SystemOverviewDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching system overview for admin dashboard");

        var tenants = await _tenantService.GetAllTenantsAsync(cancellationToken);

        var totalTenants = tenants.Count;
        var activeTenants = tenants.Count(t => t.IsActive);
        var inactiveTenants = totalTenants - activeTenants;

        // Aggregate user and room counts across all tenants
        var totalMaxUsers = tenants.Sum(t => t.MaxUsers);
        var totalMaxRooms = tenants.Sum(t => t.MaxRooms);

        // Group tenants by subscription tier
        var tenantsByTier = tenants
            .GroupBy(t => t.SubscriptionTier.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var overview = new SystemOverviewDto
        {
            TotalTenants = totalTenants,
            ActiveTenants = activeTenants,
            InactiveTenants = inactiveTenants,
            TotalMaxUsers = totalMaxUsers,
            TotalMaxRooms = totalMaxRooms,
            TenantsByTier = tenantsByTier,
            GeneratedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "System overview: {TotalTenants} tenants ({ActiveTenants} active)",
            totalTenants, activeTenants);

        return Ok(overview);
    }

    /// <summary>
    /// Check health status of all microservices by calling their /health endpoints.
    /// </summary>
    [HttpGet("health")]
    [ProducesResponseType(typeof(SystemHealthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Performing health check across all microservices");

        var healthResults = new List<ServiceHealthDto>();
        var client = _httpClientFactory.CreateClient("HealthCheck");
        client.Timeout = TimeSpan.FromSeconds(5);

        var healthCheckTasks = ServiceHealthEndpoints.Select(async kvp =>
        {
            var serviceName = kvp.Key;
            var healthUrl = kvp.Value;
            var serviceHealth = new ServiceHealthDto
            {
                ServiceName = serviceName,
                Url = healthUrl,
                CheckedAt = DateTime.UtcNow
            };

            try
            {
                var response = await client.GetAsync(healthUrl, cancellationToken);
                serviceHealth.StatusCode = (int)response.StatusCode;
                serviceHealth.IsHealthy = response.IsSuccessStatusCode;
                serviceHealth.Status = response.IsSuccessStatusCode ? "Healthy" : "Unhealthy";

                if (response.IsSuccessStatusCode)
                {
                    serviceHealth.ResponseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                }
            }
            catch (TaskCanceledException)
            {
                serviceHealth.IsHealthy = false;
                serviceHealth.Status = "Timeout";
                serviceHealth.Error = "Health check timed out after 5 seconds.";
                _logger.LogWarning("Health check timed out for {ServiceName} at {Url}", serviceName, healthUrl);
            }
            catch (HttpRequestException ex)
            {
                serviceHealth.IsHealthy = false;
                serviceHealth.Status = "Unreachable";
                serviceHealth.Error = ex.Message;
                _logger.LogWarning(ex, "Health check failed for {ServiceName} at {Url}", serviceName, healthUrl);
            }
            catch (Exception ex)
            {
                serviceHealth.IsHealthy = false;
                serviceHealth.Status = "Error";
                serviceHealth.Error = ex.Message;
                _logger.LogError(ex, "Unexpected error during health check for {ServiceName}", serviceName);
            }

            return serviceHealth;
        });

        var results = await Task.WhenAll(healthCheckTasks);
        healthResults.AddRange(results.OrderBy(r => r.ServiceName));

        var healthyCount = healthResults.Count(r => r.IsHealthy);
        var totalCount = healthResults.Count;

        var systemHealth = new SystemHealthDto
        {
            OverallStatus = healthyCount == totalCount ? "Healthy"
                : healthyCount == 0 ? "Unhealthy"
                : "Degraded",
            HealthyServices = healthyCount,
            TotalServices = totalCount,
            Services = healthResults,
            CheckedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "System health check complete: {Healthy}/{Total} services healthy. Overall: {Status}",
            healthyCount, totalCount, systemHealth.OverallStatus);

        return Ok(systemHealth);
    }

    /// <summary>
    /// Get aggregated platform statistics including tenant distribution and usage metrics.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(PlatformStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching platform statistics");

        var tenants = await _tenantService.GetAllTenantsAsync(cancellationToken);

        // Subscription tier distribution
        var tierDistribution = tenants
            .GroupBy(t => t.SubscriptionTier.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        // Active vs inactive
        var activeTenants = tenants.Where(t => t.IsActive).ToList();
        var inactiveTenants = tenants.Where(t => !t.IsActive).ToList();

        // Tenants with expiring subscriptions (within 30 days)
        var expiringThreshold = DateTime.UtcNow.AddDays(30);
        var expiringTenants = activeTenants
            .Where(t => t.SubscriptionEndDate.HasValue && t.SubscriptionEndDate.Value <= expiringThreshold)
            .Select(t => new TenantSummaryDto
            {
                TenantId = t.Id.ToString(),
                Code = t.Code,
                Name = t.Name,
                SubscriptionTier = t.SubscriptionTier.ToString(),
                SubscriptionEndDate = t.SubscriptionEndDate,
                IsActive = t.IsActive
            })
            .ToList();

        // Recently created tenants (last 30 days)
        var recentThreshold = DateTime.UtcNow.AddDays(-30);
        var recentlyCreated = tenants
            .Where(t => t.CreatedAt >= recentThreshold)
            .Count();

        // Capacity utilization
        var totalRoomCapacity = activeTenants.Sum(t => (long)t.MaxRooms);
        var totalUserCapacity = activeTenants.Sum(t => (long)t.MaxUsers);

        var stats = new PlatformStatsDto
        {
            TotalTenants = tenants.Count,
            ActiveTenants = activeTenants.Count,
            InactiveTenants = inactiveTenants.Count,
            TierDistribution = tierDistribution,
            ExpiringSubscriptions = expiringTenants,
            ExpiringSubscriptionCount = expiringTenants.Count,
            RecentlyCreatedCount = recentlyCreated,
            TotalRoomCapacity = totalRoomCapacity,
            TotalUserCapacity = totalUserCapacity,
            GeneratedAt = DateTime.UtcNow
        };

        _logger.LogInformation(
            "Platform stats: {Total} tenants, {Active} active, {Expiring} expiring soon",
            stats.TotalTenants, stats.ActiveTenants, stats.ExpiringSubscriptionCount);

        return Ok(stats);
    }
}

// ─── Response DTOs ────────────────────────────────────────────────────

/// <summary>
/// System overview data for the admin dashboard.
/// </summary>
public sealed class SystemOverviewDto
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int InactiveTenants { get; set; }
    public int TotalMaxUsers { get; set; }
    public int TotalMaxRooms { get; set; }
    public Dictionary<string, int> TenantsByTier { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Aggregated health status of all microservices.
/// </summary>
public sealed class SystemHealthDto
{
    public string OverallStatus { get; set; } = string.Empty;
    public int HealthyServices { get; set; }
    public int TotalServices { get; set; }
    public List<ServiceHealthDto> Services { get; set; } = [];
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// Health check result for an individual microservice.
/// </summary>
public sealed class ServiceHealthDto
{
    public string ServiceName { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public string? Error { get; set; }
    public DateTime CheckedAt { get; set; }
}

/// <summary>
/// Platform-wide statistics for admin reporting.
/// </summary>
public sealed class PlatformStatsDto
{
    public int TotalTenants { get; set; }
    public int ActiveTenants { get; set; }
    public int InactiveTenants { get; set; }
    public Dictionary<string, int> TierDistribution { get; set; } = new();
    public List<TenantSummaryDto> ExpiringSubscriptions { get; set; } = [];
    public int ExpiringSubscriptionCount { get; set; }
    public int RecentlyCreatedCount { get; set; }
    public long TotalRoomCapacity { get; set; }
    public long TotalUserCapacity { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>
/// Lightweight tenant summary for dashboard lists.
/// </summary>
public sealed class TenantSummaryDto
{
    public string TenantId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubscriptionTier { get; set; } = string.Empty;
    public DateTime? SubscriptionEndDate { get; set; }
    public bool IsActive { get; set; }
}
