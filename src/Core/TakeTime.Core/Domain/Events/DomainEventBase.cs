using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Core.Domain.Events;

/// <summary>
/// Abstract base record for domain events. Provides default implementations
/// for <see cref="IDomainEvent"/> members so that concrete events only
/// need to declare their own payload properties.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    /// <inheritdoc />
    public Guid Id { get; } = Guid.NewGuid();

    /// <inheritdoc />
    public DateTime OccurredOn { get; } = DateTime.UtcNow;

    /// <inheritdoc />
    public string EventType => GetType().AssemblyQualifiedName ?? GetType().FullName!;
}
