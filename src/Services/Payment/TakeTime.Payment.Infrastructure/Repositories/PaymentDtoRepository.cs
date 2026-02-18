using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of the application-layer <see cref="IPaymentRepository"/>.
/// Works with <see cref="PaymentDto"/> and <see cref="PaymentSummaryDto"/> DTOs,
/// mapping between them and the <see cref="Domain.Entities.Payment"/> domain entity
/// for persistence via <see cref="PaymentDbContext"/>.
/// </summary>
public sealed class PaymentDtoRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;

    public PaymentDtoRepository(PaymentDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<PaymentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Payments.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task CreateAsync(PaymentDto payment, CancellationToken cancellationToken = default)
    {
        var currency = payment.Currency ?? "THB";

        if (!Enum.TryParse<Domain.Enums.PaymentMethod>(payment.PaymentMethod, true, out var method))
            method = Domain.Enums.PaymentMethod.Cash;

        var entity = new Domain.Entities.Payment(
            payment.ReservationId,
            payment.CustomerId ?? Guid.Empty,
            Money.Create(payment.Amount, currency),
            method,
            bankTransferDetails: null,
            slipImageUrl: null,
            notes: payment.Notes);

        // Synchronize audit fields from the DTO where applicable.
        // CreatedBy and TenantId have public setters on the Entity base class.
        entity.CreatedBy = payment.CreatedBy;
        entity.TenantId = payment.TenantId;

        await _db.Payments.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Propagate generated values back to the DTO so the caller can use them.
        payment.Id = entity.Id;
        payment.PaymentNumber = entity.PaymentNumber;
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(Guid paymentId, string status, string? updatedBy, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (entity is null) return;

        switch (status)
        {
            case "Verified":
            case "Completed":
                entity.Verify(updatedBy ?? "System");
                break;
            case "Rejected":
            case "Failed":
                entity.Reject(updatedBy ?? "System", "Status update");
                break;
            case "Refunded":
                entity.Refund(updatedBy ?? "System", "Full refund");
                break;
            case "Voided":
                // Voided status is handled through VoidPaymentCommand / domain method
                // but we support it here as a fallback via the change tracker.
                _db.Entry(entity).Property("Status").CurrentValue = Domain.Enums.PaymentStatus.Voided;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = updatedBy;
                break;
            default:
                // For any unrecognised status strings, update audit fields only.
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = updatedBy;
                break;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> GetTodayPaymentCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Payments
            .CountAsync(p => p.CreatedAt >= today, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<PaymentSummaryDto>> GetPaymentsByReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .Where(p => p.ReservationId == reservationId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentSummaryDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                ReservationId = p.ReservationId,
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString(),
                PaymentDate = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalPaidForReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .Where(p => p.ReservationId == reservationId && p.Status == Domain.Enums.PaymentStatus.Verified)
            .SumAsync(p => p.Amount.Amount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<decimal> GetTotalRefundedForReservationAsync(Guid reservationId, CancellationToken cancellationToken = default)
    {
        return await _db.Payments
            .Where(p => p.ReservationId == reservationId)
            .SumAsync(p => p.RefundedAmount != null ? p.RefundedAmount.Amount : 0m, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PaginatedPaymentResult> SearchAsync(PaymentSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _db.Payments.AsQueryable();

        if (!string.IsNullOrEmpty(criteria.Status))
            query = query.Where(p => p.Status.ToString() == criteria.Status);

        if (!string.IsNullOrEmpty(criteria.PaymentMethod))
            query = query.Where(p => p.PaymentMethod.ToString() == criteria.PaymentMethod);

        if (criteria.FromDate.HasValue)
            query = query.Where(p => p.CreatedAt >= criteria.FromDate.Value);

        if (criteria.ToDate.HasValue)
            query = query.Where(p => p.CreatedAt <= criteria.ToDate.Value);

        if (criteria.ReservationId.HasValue)
            query = query.Where(p => p.ReservationId == criteria.ReservationId.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 20 : criteria.PageSize;

        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new PaymentSummaryDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                ReservationId = p.ReservationId,
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString(),
                PaymentDate = p.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PaginatedPaymentResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    /// <inheritdoc />
    public async Task<List<PaymentSummaryDto>> GetPaymentsByDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await _db.Payments
            .Where(p => p.CreatedAt >= startOfDay && p.CreatedAt < endOfDay)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PaymentSummaryDto
            {
                Id = p.Id,
                PaymentNumber = p.PaymentNumber,
                ReservationId = p.ReservationId,
                Amount = p.Amount.Amount,
                Currency = p.Amount.Currency,
                PaymentMethod = p.PaymentMethod.ToString(),
                Status = p.Status.ToString(),
                PaymentDate = p.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task UpdateAsync(PaymentDto payment, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Payments.FirstOrDefaultAsync(p => p.Id == payment.Id, cancellationToken);
        if (entity is null) return;

        // Apply mutable audit fields from the DTO.
        entity.UpdatedAt = payment.UpdatedAt ?? DateTime.UtcNow;
        entity.UpdatedBy = payment.CreatedBy;

        // Notes can be updated directly (public setter is not available, but
        // the entity provides domain methods for status transitions which also
        // update notes). For generic note updates that don't go through a domain
        // method, update via the change tracker.
        if (payment.Notes is not null)
        {
            _db.Entry(entity).Property("Notes").CurrentValue = payment.Notes;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps a <see cref="Domain.Entities.Payment"/> entity to a <see cref="PaymentDto"/>.
    /// </summary>
    private static PaymentDto MapToDto(Domain.Entities.Payment entity)
    {
        return new PaymentDto
        {
            Id = entity.Id,
            PaymentNumber = entity.PaymentNumber,
            TenantId = entity.TenantId ?? string.Empty,
            ReservationId = entity.ReservationId,
            CustomerId = entity.CustomerId,
            Amount = entity.Amount.Amount,
            Currency = entity.Amount.Currency,
            PaymentMethod = entity.PaymentMethod.ToString(),
            Status = entity.Status.ToString(),
            Notes = entity.Notes,
            PaymentDate = entity.CreatedAt,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy,
            VerifiedBy = entity.VerifiedBy,
            VerifiedAt = entity.VerifiedAt
        };
    }
}
