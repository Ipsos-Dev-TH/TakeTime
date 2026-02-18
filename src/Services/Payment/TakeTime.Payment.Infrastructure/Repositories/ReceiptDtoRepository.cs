using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Domain.Entities;
using TakeTime.Payment.Domain.Enums;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of the application-layer <see cref="IReceiptRepository"/>.
/// Works with <see cref="ReceiptDto"/> DTOs, mapping between them and the
/// <see cref="Receipt"/> / <see cref="ReceiptItem"/> domain entities for
/// persistence via <see cref="PaymentDbContext"/>.
/// </summary>
public sealed class ReceiptDtoRepository : IReceiptRepository
{
    private readonly PaymentDbContext _db;

    public ReceiptDtoRepository(PaymentDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<ReceiptDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task CreateAsync(ReceiptDto receipt, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<ReceiptType>(receipt.ReceiptType, true, out var receiptType))
            receiptType = ReceiptType.Regular;

        var entity = new Receipt(
            paymentId: receipt.PaymentId,
            reservationId: receipt.ReservationId,
            customerId: Guid.Empty, // Customer ID is not on ReceiptDto; handled via reservation context
            receiptType: receiptType,
            issuedBy: receipt.IssuedBy ?? "System",
            isVATIncluded: receipt.IsVATInclusive,
            vatRate: receipt.VATRate);

        entity.TenantId = receipt.TenantId;

        // Add line items from the DTO
        foreach (var itemDto in receipt.Items)
        {
            var currency = itemDto.Currency ?? receipt.Currency ?? "THB";

            var receiptItem = new ReceiptItem(
                description: itemDto.Description,
                quantity: itemDto.Quantity > 0 ? itemDto.Quantity : 1,
                unitPrice: Money.Create(itemDto.UnitPrice, currency));

            entity.AddItem(receiptItem);
        }

        // Recalculate VAT based on tenant configuration
        entity.CalculateVAT(receipt.IsVATRegistered, receipt.VATRate);

        await _db.Receipts.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Propagate generated values back to the DTO
        receipt.Id = entity.Id;
        receipt.ReceiptNumber = entity.ReceiptNumber;
    }

    /// <inheritdoc />
    public async Task<int> GetTodayReceiptCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.Receipts
            .CountAsync(r => r.CreatedAt >= today, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ReceiptDto?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Receipts
            .Include(r => r.Items)
            .FirstOrDefaultAsync(r => r.PaymentId == paymentId, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    /// <summary>
    /// Maps a <see cref="Receipt"/> entity (with its <see cref="ReceiptItem"/> children)
    /// to a <see cref="ReceiptDto"/>.
    /// </summary>
    private static ReceiptDto MapToDto(Receipt entity)
    {
        var currency = entity.TotalAmount.Currency;

        var items = entity.Items.Select((item, index) => new ReceiptItemDto
        {
            LineNumber = index + 1,
            Description = item.Description,
            Quantity = item.Quantity,
            UnitPrice = item.UnitPrice.Amount,
            TotalPrice = item.Amount.Amount,
            Currency = item.Amount.Currency
        }).ToList();

        return new ReceiptDto
        {
            Id = entity.Id,
            ReceiptNumber = entity.ReceiptNumber,
            TenantId = entity.TenantId ?? string.Empty,
            ReservationId = entity.ReservationId,
            PaymentId = entity.PaymentId,

            Items = items,

            SubTotal = entity.SubTotal.Amount,
            VATRate = entity.VATRate,
            VATAmount = entity.VATAmount.Amount,
            TotalAmount = entity.TotalAmount.Amount,
            Currency = currency,
            IsVATInclusive = entity.IsVATIncluded,

            ReceiptType = entity.ReceiptType.ToString(),
            IssuedDate = entity.IssuedAt,
            IssuedBy = entity.IssuedBy,
            CreatedAt = entity.CreatedAt,

            FormattedSubTotal = entity.SubTotal.ToFormattedString(),
            FormattedVATAmount = entity.VATAmount.ToFormattedString(),
            FormattedTotal = entity.TotalAmount.ToFormattedString()
        };
    }
}
