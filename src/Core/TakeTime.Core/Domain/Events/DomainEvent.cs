using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Core.Domain.Events;

/// <summary>
/// Base implementation of <see cref="IDomainEvent"/>.
/// All concrete domain events should inherit from this class.
/// Automatically assigns a unique Id, captures the occurrence timestamp,
/// and derives the event type from the runtime class name.
/// </summary>
public abstract class DomainEvent : IDomainEvent
{
    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public DateTime OccurredOn { get; }

    /// <inheritdoc />
    public string EventType { get; }

    protected DomainEvent()
    {
        Id = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
        EventType = GetType().FullName ?? GetType().Name;
    }

    protected DomainEvent(DateTime occurredOn) : this()
    {
        OccurredOn = occurredOn;
    }

    public override string ToString()
    {
        return $"[{EventType}] Id={Id}, OccurredOn={OccurredOn:O}";
    }
}
