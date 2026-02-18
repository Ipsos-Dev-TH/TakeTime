using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Application.Commands;
using TakeTime.Payment.Application.DTOs;
using TakeTime.Payment.Domain.Entities;
using TakeTime.Payment.Domain.ValueObjects;

namespace TakeTime.Payment.Infrastructure.Repositories;

/// <summary>
/// Infrastructure implementation of the application-layer <see cref="ITaxInvoiceRepository"/>.
/// Works with <see cref="TaxInvoiceDto"/> DTOs, mapping between them and the
/// <see cref="TaxInvoice"/> domain entity for persistence via <see cref="PaymentDbContext"/>.
/// </summary>
public sealed class TaxInvoiceDtoRepository : ITaxInvoiceRepository
{
    private readonly PaymentDbContext _db;

    public TaxInvoiceDtoRepository(PaymentDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<TaxInvoiceDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TaxInvoices
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task CreateAsync(TaxInvoiceDto invoice, CancellationToken cancellationToken = default)
    {
        var sellerInfo = CompanyInfo.Create(
            companyName: invoice.SellerName,
            taxId: invoice.SellerTaxId,
            branchNumber: invoice.SellerBranch ?? "00000",
            address: invoice.SellerAddress ?? string.Empty,
            phone: invoice.SellerPhone);

        var buyerInfo = CompanyInfo.Create(
            companyName: invoice.BuyerName,
            taxId: invoice.BuyerTaxId ?? string.Empty,
            branchNumber: invoice.BuyerBranch ?? "00000",
            address: invoice.BuyerAddress ?? string.Empty);

        var entity = new TaxInvoice(
            receiptId: invoice.ReservationId, // ReceiptId maps from the reservation context
            sellerInfo: sellerInfo,
            buyerInfo: buyerInfo,
            vatRate: invoice.VATRate);

        entity.TenantId = invoice.TenantId;

        // Add line items
        var currency = invoice.Currency ?? "THB";
        foreach (var itemDto in invoice.Items)
        {
            var itemCurrency = itemDto.Currency ?? currency;

            var item = new ReceiptItem(
                description: itemDto.Description,
                quantity: itemDto.Quantity > 0 ? itemDto.Quantity : 1,
                unitPrice: Money.Create(itemDto.UnitPrice, itemCurrency));

            entity.AddItem(item);
        }

        await _db.TaxInvoices.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        // Propagate generated values back to the DTO
        invoice.Id = entity.Id;
        invoice.InvoiceNumber = entity.InvoiceNumber;
    }

    /// <inheritdoc />
    public async Task<int> GetTodayInvoiceCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.TaxInvoices
            .CountAsync(t => t.CreatedAt >= today, cancellationToken);
    }

    /// <summary>
    /// Maps a <see cref="TaxInvoice"/> entity to a <see cref="TaxInvoiceDto"/>.
    /// </summary>
    private static TaxInvoiceDto MapToDto(TaxInvoice entity)
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

        return new TaxInvoiceDto
        {
            Id = entity.Id,
            InvoiceNumber = entity.InvoiceNumber,
            TenantId = entity.TenantId ?? string.Empty,

            // Seller information
            SellerName = entity.SellerInfo.CompanyName,
            SellerAddress = entity.SellerInfo.Address,
            SellerTaxId = entity.SellerInfo.TaxId,
            SellerPhone = entity.SellerInfo.Phone,
            SellerBranch = entity.SellerInfo.BranchNumber,

            // Buyer information
            BuyerName = entity.BuyerInfo.CompanyName,
            BuyerAddress = entity.BuyerInfo.Address,
            BuyerTaxId = entity.BuyerInfo.TaxId,
            BuyerBranch = entity.BuyerInfo.BranchNumber,

            // Line items
            Items = items,

            // Totals
            SubTotal = entity.SubTotal.Amount,
            VATRate = entity.VATRate,
            VATAmount = entity.VATAmount.Amount,
            TotalAmount = entity.TotalAmount.Amount,
            Currency = currency,

            // Metadata
            InvoiceDate = entity.IssuedDate,
            CreatedAt = entity.CreatedAt
        };
    }
}
