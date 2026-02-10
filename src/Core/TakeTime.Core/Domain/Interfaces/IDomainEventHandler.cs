using MediatR;

namespace TakeTime.Core.Domain.Interfaces;

/// <summary>
/// Handler interface for domain events. Wraps MediatR's <see cref="INotificationHandler{TNotification}"/>
/// to provide a domain-specific abstraction for event handling.
/// </summary>
/// <typeparam name="TEvent">The type of domain event to handle.</typeparam>
public interface IDomainEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IDomainEvent
{
}
