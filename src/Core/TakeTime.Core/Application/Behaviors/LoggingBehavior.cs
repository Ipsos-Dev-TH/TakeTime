using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Core.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs request/response details including
/// execution time, user context, and tenant context. Logs warnings for
/// requests that exceed the configured performance threshold.
/// </summary>
/// <typeparam name="TRequest">The MediatR request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICurrentTenantService _currentTenantService;

    /// <summary>
    /// Requests exceeding this threshold (in milliseconds) will be logged as warnings.
    /// </summary>
    private const long PerformanceThresholdMs = 500;

    public LoggingBehavior(
        ILogger<LoggingBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUserService,
        ICurrentTenantService currentTenantService)
    {
        _logger = logger;
        _currentUserService = currentUserService;
        _currentTenantService = currentTenantService;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = _currentUserService.UserId ?? "Anonymous";
        var userName = _currentUserService.UserName ?? "Unknown";
        var tenantId = _currentTenantService.TenantId ?? "No-Tenant";

        _logger.LogInformation(
            "Handling {RequestName} | User: {UserId} ({UserName}) | Tenant: {TenantId}",
            requestName, userId, userName, tenantId);

        _logger.LogDebug(
            "Request {RequestName} details: {@Request}",
            requestName, request);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next();

            stopwatch.Stop();
            var elapsedMs = stopwatch.ElapsedMilliseconds;

            if (elapsedMs > PerformanceThresholdMs)
            {
                _logger.LogWarning(
                    "Long-running request {RequestName} completed in {ElapsedMs}ms | " +
                    "User: {UserId} | Tenant: {TenantId}",
                    requestName, elapsedMs, userId, tenantId);
            }
            else
            {
                _logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMs}ms",
                    requestName, elapsedMs);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Request {RequestName} failed after {ElapsedMs}ms | " +
                "User: {UserId} | Tenant: {TenantId} | Error: {ErrorMessage}",
                requestName, stopwatch.ElapsedMilliseconds, userId, tenantId, ex.Message);

            throw;
        }
    }
}
