namespace TakeTime.Core.Domain.Interfaces;

/// <summary>
/// Unit of work abstraction that coordinates persistence of changes
/// across multiple repositories within a single transaction boundary.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Persists all pending changes to the underlying data store.
    /// Returns the number of state entries written.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <returns>A disposable transaction handle.</returns>
    Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction. All changes made since
    /// <see cref="BeginTransactionAsync"/> are finalized.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction. All changes made since
    /// <see cref="BeginTransactionAsync"/> are discarded.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
