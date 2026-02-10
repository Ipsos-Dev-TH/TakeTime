using MassTransit;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TakeTime.Core.Domain.Interfaces;
using TakeTime.Infrastructure.Caching;
using TakeTime.Infrastructure.ExternalServices;
using TakeTime.Infrastructure.Messaging;

namespace TakeTime.Infrastructure;

/// <summary>
/// Extension methods for registering shared infrastructure services in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all shared infrastructure services including caching, messaging,
    /// email, LINE Notify, Telegram, and logging.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCachingServices(configuration);
        services.AddMessagingServices(configuration);
        services.AddExternalServices(configuration);

        return services;
    }

    /// <summary>
    /// Registers caching services. Uses Redis if configured, otherwise falls back to in-memory.
    /// </summary>
    public static IServiceCollection AddCachingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("Redis");

        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "TakeTime:";
            });
            services.AddSingleton<ICacheService, RedisCacheService>();
        }
        else
        {
            services.AddMemoryCache();
            services.AddSingleton<ICacheService, MemoryCacheService>();
        }

        return services;
    }

    /// <summary>
    /// Registers messaging services. Uses MassTransit with RabbitMQ if configured,
    /// otherwise falls back to in-memory event bus.
    /// </summary>
    public static IServiceCollection AddMessagingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitMqHost = configuration.GetValue<string>("RabbitMQ:Host");

        if (!string.IsNullOrEmpty(rabbitMqHost))
        {
            services.AddMassTransit(busConfig =>
            {
                busConfig.SetKebabCaseEndpointNameFormatter();

                // Scan for consumers in the calling assembly
                busConfig.AddConsumers(AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name?.StartsWith("TakeTime") ?? false)
                    .ToArray());

                busConfig.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Host(rabbitMqHost, configuration.GetValue<string>("RabbitMQ:VirtualHost") ?? "/", h =>
                    {
                        h.Username(configuration.GetValue<string>("RabbitMQ:Username") ?? "guest");
                        h.Password(configuration.GetValue<string>("RabbitMQ:Password") ?? "guest");
                    });

                    cfg.ConfigureEndpoints(context);

                    // Retry policy
                    cfg.UseMessageRetry(retryConfig =>
                    {
                        retryConfig.Exponential(
                            retryLimit: 3,
                            minInterval: TimeSpan.FromSeconds(1),
                            maxInterval: TimeSpan.FromSeconds(30),
                            intervalDelta: TimeSpan.FromSeconds(2));
                    });
                });
            });

            services.AddScoped<IEventBus, MassTransitEventBus>();
        }
        else
        {
            services.AddSingleton<IEventBus, InMemoryEventBus>();
        }

        return services;
    }

    /// <summary>
    /// Registers external communication services (email, LINE Notify, Telegram).
    /// </summary>
    public static IServiceCollection AddExternalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Email
        services.Configure<EmailSettings>(configuration.GetSection(EmailSettings.SectionName));
        services.AddScoped<IEmailService, EmailService>();

        // LINE Notify
        services.Configure<LineNotifySettings>(configuration.GetSection(LineNotifySettings.SectionName));
        services.AddHttpClient<ILineNotifyService, LineNotifyService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Telegram
        services.Configure<TelegramSettings>(configuration.GetSection(TelegramSettings.SectionName));
        services.AddHttpClient<ITelegramService, TelegramService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        return services;
    }

    /// <summary>
    /// Registers a generic repository and Unit of Work for a specific DbContext.
    /// Call this in service-specific DI registration methods.
    /// </summary>
    public static IServiceCollection AddRepositoryServices<TContext>(
        this IServiceCollection services)
        where TContext : Database.BaseDbContext
    {
        services.AddScoped<IUnitOfWork>(sp =>
        {
            var context = sp.GetRequiredService<TContext>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Database.UnitOfWork>>();
            return new Database.UnitOfWork(context, logger);
        });

        return services;
    }
}
