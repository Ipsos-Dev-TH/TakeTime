using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TakeTime.Infrastructure.Messaging;

/// <summary>
/// In-memory implementation of IEventBus for single-process deployment scenarios.
/// Suitable for development, testing, and monolithic deployments.
/// Replace with MassTransitEventBus for true distributed microservice deployment.
/// </summary>
public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    private readonly ConcurrentDictionary<Type, List<Type>> _subscriptions = new();

    public InMemoryEventBus(
        IServiceProvider serviceProvider,
        ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var eventType = typeof(TEvent);

        _logger.LogInformation(
            "Publishing integration event {EventType} with Id {EventId} for tenant {TenantId}",
            eventType.Name,
            @event.Id,
            @event.TenantId);

        if (!_subscriptions.TryGetValue(eventType, out var handlerTypes))
        {
            _logger.LogDebug("No handlers registered for event type {EventType}", eventType.Name);
            return;
        }

        using var scope = _serviceProvider.CreateScope();

        foreach (var handlerType in handlerTypes)
        {
            try
            {
                var handler = scope.ServiceProvider.GetService(handlerType);
                if (handler is null)
                {
                    _logger.LogWarning(
                        "Handler {HandlerType} is registered but could not be resolved from DI",
                        handlerType.Name);
                    continue;
                }

                var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                var method = concreteType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync));

                if (method is not null)
                {
                    await (Task)method.Invoke(handler, [@event, cancellationToken])!;
                }

                _logger.LogDebug(
                    "Handler {HandlerType} processed event {EventType} with Id {EventId}",
                    handlerType.Name,
                    eventType.Name,
                    @event.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error in handler {HandlerType} processing event {EventType} with Id {EventId}",
                    handlerType.Name,
                    eventType.Name,
                    @event.Id);
                // In-memory bus does not retry; errors are logged and processing continues
            }
        }
    }

    /// <inheritdoc />
    public async Task PublishManyAsync(
        IEnumerable<IntegrationEvent> events,
        CancellationToken cancellationToken = default)
    {
        foreach (var @event in events)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var method = typeof(InMemoryEventBus)
                .GetMethod(nameof(PublishAsync))!
                .MakeGenericMethod(@event.GetType());

            await (Task)method.Invoke(this, [@event, cancellationToken])!;
        }
    }

    /// <inheritdoc />
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventType = typeof(TEvent);
        var handlerType = typeof(THandler);

        var handlers = _subscriptions.GetOrAdd(eventType, _ => []);

        if (handlers.Contains(handlerType))
        {
            _logger.LogWarning(
                "Handler {HandlerType} is already subscribed to {EventType}",
                handlerType.Name,
                eventType.Name);
            return;
        }

        handlers.Add(handlerType);

        _logger.LogInformation(
            "Subscribed {HandlerType} to {EventType}",
            handlerType.Name,
            eventType.Name);
    }
}
