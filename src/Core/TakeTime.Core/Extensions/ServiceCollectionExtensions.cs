using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using TakeTime.Core.Application.Behaviors;

namespace TakeTime.Core.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// TakeTime.Core services into the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all Core-layer services including MediatR, pipeline behaviors,
    /// and FluentValidation validators discovered from the calling assembly.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        var coreAssembly = typeof(ServiceCollectionExtensions).Assembly;

        // Register MediatR from the Core assembly and any assembly that references it
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(coreAssembly);

            // Register pipeline behaviors in execution order
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantBehavior<,>));
        });

        // Register all FluentValidation validators from the Core assembly
        services.AddValidatorsFromAssembly(coreAssembly, includeInternalTypes: true);

        return services;
    }

    /// <summary>
    /// Registers Core services and also scans additional assemblies for
    /// MediatR handlers and FluentValidation validators.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">Additional assemblies to scan.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCore(
        this IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        var coreAssembly = typeof(ServiceCollectionExtensions).Assembly;
        var allAssemblies = new[] { coreAssembly }.Concat(assemblies).Distinct().ToArray();

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(allAssemblies);

            // Register pipeline behaviors in execution order
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(TenantBehavior<,>));
        });

        // Register all FluentValidation validators from all assemblies
        foreach (var assembly in allAssemblies)
        {
            services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        }

        return services;
    }
}
