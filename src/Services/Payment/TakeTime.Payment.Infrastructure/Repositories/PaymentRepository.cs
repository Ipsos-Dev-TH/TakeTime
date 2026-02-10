using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Infrastructure.Database;
using TakeTime.Payment.Domain.Enums;
using TakeTime.Payment.Domain.Interfaces;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Implementation of IPaymentRepository providing data access for the Payment aggregate.
/// </summary>
public class PaymentRepository : BaseRepository<Domain.Entities.Payment>, IPaymentRepository
{
    private readonly PaymentDbContext _dbContext;

    public PaymentRepository(
        PaymentDbContext context,
        ICurrentTenantService currentTenantService)
        : base(context, currentTenantService)
    {
        _dbContext = context;
    }

    /// <inheritdoc />
    public async Task<Domain.Entities.Payment?> GetByPaymentNumberAsync(
        string paymentNumber, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.PaymentNumber == paymentNumber, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Payment>> GetByReservationIdAsync(
        Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .Where(p => p.ReservationId == reservationId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Payment>> GetByCustomerIdAsync(
        Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Payment>> GetByStatusAsync(
        PaymentStatus status, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Payment>> GetPendingPaymentsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .Where(p => p.Status == PaymentStatus.Pending)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Domain.Entities.Payment>> GetByDateRangeAsync(
        DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Payments
            .Where(p => p.CreatedAt >= startDate && p.CreatedAt <= endDate)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
