using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;

namespace TakeTime.Core.Application.Behaviors;

/// <summary>
/// Marker interface for commands that require a tenant context.
/// All commands implementing this interface will have their TenantId
/// validated and populated by <see cref="TenantBehavior{TRequest,TResponse}"/>.
/// </summary>
public interface ITenantCommand
{
    /// <summary>
    /// The tenant identifier for this command.
    /// Populated automatically by the TenantBehavior pipeline.
    /// </summary>
    string? TenantId { get; set; }
}

/// <summary>
/// Marker interface for requests that should bypass tenant validation.
/// Used for cross-tenant administrative operations and system-level queries.
/// </summary>
public interface ITenantAgnostic
{
}

/// <summary>
/// MediatR pipeline behavior that ensures a valid tenant context is established
/// for all commands that implement <see cref="ITenantCommand"/>.
/// Automatically stamps the current tenant's identifier onto the command.
/// Requests implementing <see cref="ITenantAgnostic"/> bypass this check.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class TenantBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<TenantBehavior<TRequest, TResponse>> _logger;

    public TenantBehavior(
        ICurrentTenantService currentTenantService,
        ILogger<TenantBehavior<TRequest, TResponse>> logger)
    {
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip tenant validation for tenant-agnostic requests
        if (request is ITenantAgnostic)
        {
            _logger.LogDebug(
                "Request {RequestName} is tenant-agnostic, skipping tenant validation.",
                typeof(TRequest).Name);

            return await next();
        }

        // Enforce tenant context for tenant commands
        if (request is ITenantCommand tenantCommand)
        {
            if (!_currentTenantService.HasTenant)
            {
                throw new TenantException(
                    $"A valid tenant context is required for {typeof(TRequest).Name}. " +
                    "Ensure the tenant has been resolved before executing this command.");
            }

            // Stamp the tenant Id onto the command
            tenantCommand.TenantId = _currentTenantService.TenantId;

            _logger.LogDebug(
                "Tenant context set for {RequestName}: TenantId={TenantId}",
                typeof(TRequest).Name, _currentTenantService.TenantId);
        }

        return await next();
    }
}
