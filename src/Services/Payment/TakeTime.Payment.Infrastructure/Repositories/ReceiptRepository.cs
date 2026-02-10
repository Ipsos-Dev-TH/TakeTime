using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Payment.Domain.Entities;
using TakeTime.Payment.Domain.Enums;
using TakeTime.Payment.Domain.Interfaces;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Implementation of IReceiptRepository providing data access for the Receipt aggregate.
/// </summary>
public class ReceiptRepository : BaseRepository<Receipt>, IReceiptRepository
{
    private readonly PaymentDbContext _dbContext;

    public ReceiptRepository(
        PaymentDbContext context,
        ICurrentTenantService currentTenantService)
        : base(context, currentTenantService)
    {
        _dbContext = context;
    }

    /// <inheritdoc />
    public override async Task<Receipt?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Receipt?> GetByReceiptNumberAsync(
        string receiptNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.ReceiptNumber == receiptNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Receipt>> GetByReservationIdAsync(
        Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .Where(r => r.ReservationId == reservationId)
            .OrderByDescending(r => r.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Receipt?> GetByPaymentIdAsync(
        Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Receipt>> GetByTypeAsync(
        ReceiptType receiptType, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .Where(r => r.ReceiptType == receiptType)
            .OrderByDescending(r => r.IssuedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Receipt>> GetByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Receipts
            .Include(r => r.Items)
            .Where(r => r.IssuedAt >= startDate && r.IssuedAt <= endDate)
            .OrderByDescending(r => r.IssuedAt)
            .ToListAsync(cancellationToken);
    }
}
