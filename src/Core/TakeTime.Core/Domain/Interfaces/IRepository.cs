using System.Linq.Expressions;
using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.Interfaces;

/// <summary>
/// Generic repository interface providing standard CRUD operations
/// for aggregate roots. Implementations should apply tenant-scoping
/// and soft-delete filtering automatically.
/// </summary>
/// <typeparam name="T">The aggregate root type managed by this repository.</typeparam>
public interface IRepository<T> where T : AggregateRoot
{
    /// <summary>
    /// Retrieves an entity by its unique identifier.
    /// Returns null if no matching entity is found or the entity is soft-deleted.
    /// </summary>
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all non-deleted entities for the current tenant.
    /// </summary>
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all non-deleted entities matching the given predicate.
    /// </summary>
    Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single entity matching the predicate, or null if none found.
    /// Throws if more than one entity matches.
    /// </summary>
    Task<T?> FindSingleAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether any entity matching the predicate exists.
    /// </summary>
    Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the count of entities matching the predicate.
    /// </summary>
    Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a collection of entities to the repository.
    /// </summary>
    Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently removes an entity from the data store.
    /// Prefer <see cref="SoftDeleteAsync"/> for most use cases.
    /// </summary>
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an entity as soft-deleted without physically removing it.
    /// </summary>
    Task SoftDeleteAsync(Guid id, string? deletedBy = null, CancellationToken cancellationToken = default);
}
