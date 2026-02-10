using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TakeTime.MultiTenancy.Configuration;
using TakeTime.MultiTenancy.Core;
using TakeTime.MultiTenancy.Middleware;
using TakeTime.MultiTenancy.Resolution;
using TakeTime.MultiTenancy.Services;

namespace TakeTime.MultiTenancy;

/// <summary>
/// Extension methods for registering the TakeTime multi-tenancy system
/// in the ASP.NET Core dependency injection container and middleware pipeline.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all multi-tenancy services, resolution strategies, tenant store,
    /// and configuration in the DI container.
    ///
    /// <example>
    /// Usage in Program.cs:
    /// <code>
    /// builder.Services.AddMultiTenancy(builder.Configuration);
    ///
    /// var app = builder.Build();
    /// app.UseMultiTenancy();
    /// </code>
    /// </example>
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMultiTenancy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        var configSection = configuration.GetSection(TenantConfiguration.SectionName);
        services.Configure<TenantConfiguration>(configSection);

        var config = new TenantConfiguration();
        configSection.Bind(config);

        // Register HttpContextAccessor (idempotent -- safe to call multiple times)
        services.AddHttpContextAccessor();

        // Register memory cache for tenant caching
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = config.MaxCachedTenants > 0
                ? config.MaxCachedTenants * 3  // x3 because we cache by ID, code, and "all"
                : null;
        });

        // Register TenantDbContext for the central tenant management database
        if (!string.IsNullOrWhiteSpace(config.ConnectionString))
        {
            services.AddDbContext<TenantDbContext>(options =>
            {
                options.UseSqlServer(config.ConnectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsHistoryTable("__TenantMigrationsHistory", "multitenancy");
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
            });
        }

        // Register tenant store
        services.AddScoped<ITenantStore, DatabaseTenantStore>();

        // Register resolution strategies based on configuration
        if (config.EnableHeaderResolution)
        {
            services.AddSingleton<ITenantResolutionStrategy, HeaderTenantResolutionStrategy>();
        }

        if (config.EnableClaimsResolution)
        {
            services.AddSingleton<ITenantResolutionStrategy, ClaimsTenantResolutionStrategy>();
        }

        if (config.EnableSubdomainResolution)
        {
            services.AddSingleton<ITenantResolutionStrategy, SubdomainTenantResolutionStrategy>();
        }

        if (config.EnableQueryStringResolution)
        {
            services.AddSingleton<ITenantResolutionStrategy, QueryStringTenantResolutionStrategy>();
        }

        // Register tenant services
        services.AddScoped<ICurrentTenantService, CurrentTenantService>();
        services.AddScoped<TenantManagementService>();
        services.AddScoped<TenantSettingsService>();

        return services;
    }

    /// <summary>
    /// Overload that accepts an action for inline configuration without
    /// requiring an IConfiguration section.
    /// </summary>
    public static IServiceCollection AddMultiTenancy(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<TenantConfiguration> configureOptions)
    {
        services.AddMultiTenancy(configuration);
        services.PostConfigure(configureOptions);
        return services;
    }

    /// <summary>
    /// Adds the tenant resolution and security middleware to the ASP.NET Core pipeline.
    /// Must be called after <c>UseAuthentication()</c> and before <c>UseAuthorization()</c>.
    ///
    /// <example>
    /// <code>
    /// app.UseAuthentication();
    /// app.UseMultiTenancy();   // &lt;-- here
    /// app.UseAuthorization();
    /// </code>
    /// </example>
    /// </summary>
    public static IApplicationBuilder UseMultiTenancy(this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseMiddleware<TenantSecurityMiddleware>();
        return app;
    }

    /// <summary>
    /// Ensures the tenant management database schema is created / migrated.
    /// Call this during application startup if you want automatic migration.
    /// </summary>
    public static async Task MigrateTenantDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<TenantDbContext>();

        if (dbContext is not null)
        {
            await dbContext.Database.MigrateAsync();
        }
    }
}
