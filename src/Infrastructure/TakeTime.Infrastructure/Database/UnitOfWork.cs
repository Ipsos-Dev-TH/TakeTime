using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Infrastructure.Database;

/// <summary>
/// Unit of Work implementation coordinating persistence of changes
/// across multiple repositories within a single transaction boundary.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly BaseDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;
    private bool _disposed;

    public UnitOfWork(BaseDbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Whether a transaction is currently active.
    /// </summary>
    public bool HasActiveTransaction => _currentTransaction is not null;

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug("Saved {Count} changes to the database", result);

            return result;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict detected while saving changes. " +
                "{EntryCount} entities involved",
                ex.Entries.Count);
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Error saving changes to the database");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
        {
            _logger.LogWarning("A transaction is already active. Returning existing transaction.");
            return _currentTransaction;
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        _logger.LogDebug(
            "Started database transaction {TransactionId}",
            _currentTransaction.TransactionId);

        return _currentTransaction;
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            throw new InvalidOperationException(
                "No active transaction to commit. Call BeginTransactionAsync first.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);

            _logger.LogDebug(
                "Committed transaction {TransactionId}",
                _currentTransaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error committing transaction {TransactionId}. Rolling back.",
                _currentTransaction.TransactionId);

            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
        {
            _logger.LogWarning("No active transaction to roll back.");
            return;
        }

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);

            _logger.LogDebug(
                "Rolled back transaction {TransactionId}",
                _currentTransaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error rolling back transaction {TransactionId}",
                _currentTransaction.TransactionId);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _currentTransaction?.Dispose();
            _context.Dispose();
        }

        _disposed = true;
    }
}
