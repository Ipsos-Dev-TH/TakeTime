using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace TakeTime.Infrastructure.Logging;

/// <summary>
/// Configures Serilog with structured logging, tenant context enrichment,
/// and multiple sinks for the TakeTime platform.
/// </summary>
public static class SerilogConfiguration
{
    /// <summary>
    /// Configures Serilog on the host builder with structured logging,
    /// environment enrichment, and tenant-aware properties.
    /// </summary>
    public static IHostBuilder UseTakeTimeSerilog(this IHostBuilder hostBuilder)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            ConfigureLogger(loggerConfiguration, context.Configuration, context.HostingEnvironment);
        });
    }

    /// <summary>
    /// Configures the Serilog logger with the standard TakeTime pipeline.
    /// </summary>
    public static void ConfigureLogger(
        LoggerConfiguration loggerConfiguration,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration.GetValue<string>("ServiceName") ?? environment.ApplicationName;

        loggerConfiguration
            .ReadFrom.Configuration(configuration)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithProperty("ServiceName", serviceName)
            .Enrich.WithProperty("Environment", environment.EnvironmentName);

        // Console sink - always enabled
        loggerConfiguration.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] [{TenantId}] {Message:lj}{NewLine}{Exception}");

        // File sink - rolling daily
        var logPath = configuration.GetValue<string>("Logging:FilePath") ?? "logs";
        loggerConfiguration.WriteTo.File(
            path: $"{logPath}/{serviceName}-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{ServiceName}] [{TenantId}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");

        // Seq sink - optional, for centralized structured log viewing
        var seqUrl = configuration.GetValue<string>("Logging:SeqUrl");
        if (!string.IsNullOrEmpty(seqUrl))
        {
            loggerConfiguration.WriteTo.Seq(seqUrl);
        }

        // Set minimum level based on environment
        if (environment.IsDevelopment())
        {
            loggerConfiguration.MinimumLevel.Debug();
        }
        else
        {
            loggerConfiguration.MinimumLevel.Information();
        }
    }

    /// <summary>
    /// Creates a bootstrap logger for use during application startup
    /// before the full DI container is built.
    /// </summary>
    public static void CreateBootstrapLogger()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
    }
}

/// <summary>
/// Serilog enricher that adds the current tenant context to all log entries.
/// </summary>
public class TenantLogEnricher : Serilog.Core.ILogEventEnricher
{
    private readonly string? _tenantId;

    public TenantLogEnricher(string? tenantId)
    {
        _tenantId = tenantId;
    }

    public void Enrich(LogEvent logEvent, Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        var tenantProperty = propertyFactory.CreateProperty("TenantId", _tenantId ?? "system");
        logEvent.AddPropertyIfAbsent(tenantProperty);
    }
}
