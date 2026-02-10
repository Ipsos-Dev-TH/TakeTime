using MassTransit;
using Microsoft.Extensions.Logging;

namespace TakeTime.Infrastructure.Messaging;

/// <summary>
/// MassTransit-based implementation of IEventBus for distributed microservice deployment.
/// Supports RabbitMQ, Azure Service Bus, Amazon SQS, and other transports
/// through MassTransit's transport abstraction.
/// </summary>
public class MassTransitEventBus : IEventBus
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IBus _bus;
    private readonly ILogger<MassTransitEventBus> _logger;

    public MassTransitEventBus(
        IPublishEndpoint publishEndpoint,
        IBus bus,
        ILogger<MassTransitEventBus> logger)
    {
        _publishEndpoint = publishEndpoint ?? throw new ArgumentNullException(nameof(publishEndpoint));
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        _logger.LogInformation(
            "Publishing integration event {EventType} with Id {EventId} via MassTransit for tenant {TenantId}",
            typeof(TEvent).Name,
            @event.Id,
            @event.TenantId);

        try
        {
            await _publishEndpoint.Publish(@event, context =>
            {
                context.MessageId = @event.Id;
                context.CorrelationId = @event.CorrelationId is not null
                    ? Guid.Parse(@event.CorrelationId)
                    : Guid.NewGuid();

                // Set tenant context as a header so consumers can restore it
                if (!string.IsNullOrEmpty(@event.TenantId))
                {
                    context.Headers.Set("X-Tenant-Id", @event.TenantId);
                }

                if (!string.IsNullOrEmpty(@event.UserId))
                {
                    context.Headers.Set("X-User-Id", @event.UserId);
                }
            }, cancellationToken);

            _logger.LogDebug(
                "Successfully published {EventType} with Id {EventId}",
                typeof(TEvent).Name,
                @event.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish integration event {EventType} with Id {EventId}",
                typeof(TEvent).Name,
                @event.Id);
            throw;
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

            // Use dynamic dispatch to invoke the generic PublishAsync with the concrete event type
            await PublishDynamic(@event, cancellationToken);
        }
    }

    /// <inheritdoc />
    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        // MassTransit handles subscriptions through consumer registration in the DI container.
        // This method is a no-op since consumers are registered during startup via
        // AddMassTransit(x => x.AddConsumer<THandler>()).
        _logger.LogInformation(
            "MassTransit subscription for {EventType} -> {HandlerType} is managed via DI consumer registration",
            typeof(TEvent).Name,
            typeof(THandler).Name);
    }

    private async Task PublishDynamic(IntegrationEvent @event, CancellationToken cancellationToken)
    {
        var eventType = @event.GetType();

        await _publishEndpoint.Publish(@event, eventType, context =>
        {
            context.MessageId = @event.Id;

            if (!string.IsNullOrEmpty(@event.TenantId))
            {
                context.Headers.Set("X-Tenant-Id", @event.TenantId);
            }

            if (!string.IsNullOrEmpty(@event.CorrelationId))
            {
                context.CorrelationId = Guid.Parse(@event.CorrelationId);
            }
        }, cancellationToken);
    }
}
