using MediatR;

namespace TakeTime.Core.Domain.Interfaces;

/// <summary>
/// Marker interface for domain events. All domain events are MediatR notifications
/// so they can be dispatched through the mediator pipeline.
/// </summary>
public interface IDomainEvent : INotification
{
    /// <summary>
    /// Unique identifier for this specific event occurrence.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// UTC timestamp when the event occurred.
    /// </summary>
    DateTime OccurredOn { get; }

    /// <summary>
    /// Fully qualified type name of the event, used for serialization and routing.
    /// </summary>
    string EventType { get; }
}
