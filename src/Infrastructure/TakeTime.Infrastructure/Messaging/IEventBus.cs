namespace TakeTime.Infrastructure.Messaging;

/// <summary>
/// Interface for publishing integration events between microservices.
/// Implementations may use in-memory messaging, RabbitMQ, Azure Service Bus,
/// or any other transport mechanism.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes an integration event to all subscribed consumers.
    /// </summary>
    /// <typeparam name="TEvent">The type of integration event.</typeparam>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent;

    /// <summary>
    /// Publishes multiple integration events.
    /// </summary>
    /// <param name="events">The events to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishManyAsync(IEnumerable<IntegrationEvent> events, CancellationToken cancellationToken = default);

    /// <summary>
    /// Subscribes to integration events of a specific type.
    /// </summary>
    /// <typeparam name="TEvent">The type of integration event.</typeparam>
    /// <typeparam name="THandler">The handler type for the event.</typeparam>
    void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>;
}

/// <summary>
/// Handler interface for integration events.
/// </summary>
/// <typeparam name="TEvent">The type of integration event to handle.</typeparam>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IntegrationEvent
{
    /// <summary>
    /// Handles the integration event.
    /// </summary>
    /// <param name="event">The event to handle.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}
