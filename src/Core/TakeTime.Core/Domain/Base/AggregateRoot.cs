namespace TakeTime.Core.Domain.Base;

/// <summary>
/// Base class for aggregate roots. Extends <see cref="Entity"/> with
/// a version property used for optimistic concurrency control.
/// </summary>
public abstract class AggregateRoot : Entity
{
    /// <summary>
    /// Concurrency version token. Incremented on each successful update
    /// to detect and reject stale writes.
    /// </summary>
    public int Version { get; private set; }

    protected AggregateRoot() : base()
    {
    }

    protected AggregateRoot(Guid id) : base(id)
    {
    }

    /// <summary>
    /// Increments the version number. Called by the persistence layer
    /// when saving changes to enforce optimistic concurrency.
    /// </summary>
    public void IncrementVersion()
    {
        Version++;
    }

    /// <summary>
    /// Validates that the provided version matches the current version.
    /// Throws if a concurrency conflict is detected.
    /// </summary>
    /// <param name="expectedVersion">The version the caller expects.</param>
    /// <exception cref="InvalidOperationException">Thrown when versions do not match.</exception>
    public void CheckVersion(int expectedVersion)
    {
        if (Version != expectedVersion)
        {
            throw new InvalidOperationException(
                $"Concurrency conflict on {GetType().Name} with Id '{Id}'. " +
                $"Expected version {expectedVersion} but current version is {Version}.");
        }
    }
}
