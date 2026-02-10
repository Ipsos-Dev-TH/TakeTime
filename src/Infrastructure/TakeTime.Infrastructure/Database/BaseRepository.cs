using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Infrastructure.Database;

/// <summary>
/// Generic repository implementation providing standard CRUD operations
/// with automatic tenant filtering, soft-delete awareness, and eager loading support.
/// </summary>
/// <typeparam name="T">The aggregate root type managed by this repository.</typeparam>
public class BaseRepository<T> : IRepository<T> where T : AggregateRoot
{
    protected readonly BaseDbContext Context;
    protected readonly DbSet<T> DbSet;
    protected readonly ICurrentTenantService CurrentTenantService;

    public BaseRepository(BaseDbContext context, ICurrentTenantService currentTenantService)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        CurrentTenantService = currentTenantService ?? throw new ArgumentNullException(nameof(currentTenantService));
        DbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <summary>
    /// Retrieves an entity by ID with eager loading of specified navigation properties.
    /// </summary>
    public virtual async Task<T?> GetByIdWithIncludesAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes)
    {
        var query = ApplyIncludes(DbSet.AsQueryable(), includes);
        return await query.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await DbSet.ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves entities with pagination support.
    /// </summary>
    /// <param name="pageNumber">The 1-based page number.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <param name="orderBy">Optional ordering expression.</param>
    /// <param name="ascending">Sort direction. True for ascending.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A tuple of items and total count.</returns>
    public virtual async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = DbSet.AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy != null)
        {
            query = ascending
                ? query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);
        }
        else
        {
            query = query.OrderByDescending(e => e.CreatedAt);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves paged entities with eager loading.
    /// </summary>
    public virtual async Task<(IReadOnlyList<T> Items, int TotalCount)> GetPagedWithIncludesAsync(
        int pageNumber,
        int pageSize,
        Expression<Func<T, object>>? orderBy = null,
        bool ascending = true,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = ApplyIncludes(DbSet.AsQueryable(), includes);

        var totalCount = await query.CountAsync(cancellationToken);

        if (orderBy != null)
        {
            query = ascending
                ? query.OrderBy(orderBy)
                : query.OrderByDescending(orderBy);
        }
        else
        {
            query = query.OrderByDescending(e => e.CreatedAt);
        }

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Finds entities matching a predicate with eager loading.
    /// </summary>
    public virtual async Task<IReadOnlyList<T>> FindWithIncludesAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default,
        params Expression<Func<T, object>>[] includes)
    {
        var query = ApplyIncludes(DbSet.AsQueryable(), includes);
        return await query.Where(predicate).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<T?> FindSingleAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.SingleOrDefaultAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<bool> ExistsAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await DbSet.AnyAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(
        Expression<Func<T, bool>>? predicate = null,
        CancellationToken cancellationToken = default)
    {
        return predicate == null
            ? await DbSet.CountAsync(cancellationToken)
            : await DbSet.CountAsync(predicate, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<T> AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        entity.TenantId ??= CurrentTenantService.TenantId;
        await DbSet.AddAsync(entity, cancellationToken);
        return entity;
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        var entityList = entities.ToList();
        foreach (var entity in entityList)
        {
            entity.TenantId ??= CurrentTenantService.TenantId;
        }
        await DbSet.AddRangeAsync(entityList, cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        Context.Entry(entity).State = EntityState.Modified;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public virtual async Task SoftDeleteAsync(
        Guid id,
        string? deletedBy = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await GetByIdAsync(id, cancellationToken);
        if (entity is null)
            return;

        entity.MarkAsDeleted(deletedBy);
        Context.Entry(entity).State = EntityState.Modified;
    }

    /// <summary>
    /// Returns a queryable for advanced query composition in derived repositories.
    /// The global filters (tenant + soft-delete) are already applied.
    /// </summary>
    protected IQueryable<T> Query()
    {
        return DbSet.AsQueryable();
    }

    /// <summary>
    /// Returns a no-tracking queryable for read-only scenarios.
    /// </summary>
    protected IQueryable<T> QueryNoTracking()
    {
        return DbSet.AsNoTracking();
    }

    /// <summary>
    /// Applies eager loading includes to a queryable.
    /// </summary>
    private static IQueryable<T> ApplyIncludes(
        IQueryable<T> query,
        IEnumerable<Expression<Func<T, object>>> includes)
    {
        return includes.Aggregate(query, (current, include) => current.Include(include));
    }
}
