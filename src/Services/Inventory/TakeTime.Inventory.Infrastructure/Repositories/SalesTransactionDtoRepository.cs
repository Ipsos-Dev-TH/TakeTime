using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;
using TakeTime.Inventory.Domain.Entities;

namespace TakeTime.Inventory.Infrastructure.Repositories;

public sealed class SalesTransactionDtoRepository : ISalesTransactionRepository
{
    private readonly InventoryDbContext _db;

    public SalesTransactionDtoRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task CreateAsync(SalesTransactionDto transaction, CancellationToken cancellationToken = default)
    {
        var currency = transaction.Currency ?? "THB";

        var entity = new SalesTransaction(
            cashierUserId: transaction.CreatedBy ?? "System",
            paymentMethod: transaction.PaymentMethod,
            roomChargeReservationId: transaction.ReservationId);

        entity.TenantId = transaction.TenantId;
        entity.CreatedBy = transaction.CreatedBy;

        foreach (var item in transaction.Items)
        {
            var txItem = new SalesTransactionItem(
                productId: item.ProductId,
                productName: item.ProductName,
                quantity: item.Quantity,
                unitPrice: Money.Create(item.UnitPrice, currency),
                discountAmount: item.DiscountAmount > 0 ? Money.Create(item.DiscountAmount, currency) : null);

            entity.AddItem(txItem);
        }

        if (transaction.DiscountAmount > 0)
        {
            entity.ApplyDiscount(Money.Create(transaction.DiscountAmount, currency));
        }

        entity.Complete();

        await _db.SalesTransactions.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        transaction.Id = entity.Id;
        transaction.TransactionNumber = entity.TransactionNumber;
    }

    public async Task<int> GetTodayTransactionCountAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        return await _db.SalesTransactions
            .CountAsync(t => t.CreatedAt >= today, cancellationToken);
    }

    public async Task<SalesReportDto> GetSalesReportAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var transactions = await _db.SalesTransactions
            .Include(t => t.Items)
            .Where(t => t.CreatedAt >= fromDate && t.CreatedAt <= toDate &&
                        t.Status == Domain.Enums.SalesTransactionStatus.Completed)
            .ToListAsync(cancellationToken);

        var totalSales = transactions.Sum(t => t.TotalAmount.Amount);
        var totalVAT = transactions.Sum(t => t.VATAmount.Amount);
        var totalDiscount = transactions.Sum(t => t.DiscountAmount.Amount);
        var netSales = totalSales - totalVAT;
        var transactionCount = transactions.Count;

        // Sales by category from transaction items
        var allItems = transactions.SelectMany(t => t.Items).ToList();
        var productIds = allItems.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, cancellationToken);

        var salesByCategory = allItems
            .GroupBy(i => products.TryGetValue(i.ProductId, out var p) ? p.Category.ToString() : "Unknown")
            .Select(g => new SalesByCategoryDto
            {
                Category = g.Key,
                TotalSales = g.Sum(i => i.TotalPrice.Amount),
                ItemsSold = g.Sum(i => i.Quantity),
                Percentage = totalSales > 0 ? Math.Round(g.Sum(i => i.TotalPrice.Amount) / totalSales * 100, 2) : 0
            })
            .ToList();

        var topProducts = allItems
            .GroupBy(i => new { i.ProductId, i.ProductName })
            .Select(g => new TopSellingProductDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                QuantitySold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.TotalPrice.Amount)
            })
            .OrderByDescending(p => p.Revenue)
            .Take(10)
            .ToList();

        var dailySales = transactions
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new DailySalesDto
            {
                Date = g.Key,
                TotalSales = g.Sum(t => t.TotalAmount.Amount),
                TransactionCount = g.Count()
            })
            .OrderBy(d => d.Date)
            .ToList();

        return new SalesReportDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            TotalSales = totalSales,
            TotalVAT = totalVAT,
            TotalDiscount = totalDiscount,
            NetSales = netSales,
            TransactionCount = transactionCount,
            AverageTransactionValue = transactionCount > 0 ? Math.Round(totalSales / transactionCount, 2) : 0,
            Currency = "THB",
            SalesByCategory = salesByCategory,
            TopSellingProducts = topProducts,
            DailySales = dailySales
        };
    }

    public async Task<List<SalesTransactionDto>> GetTransactionsAsync(
        DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var transactions = await _db.SalesTransactions
            .Include(t => t.Items)
            .Where(t => t.CreatedAt >= fromDate && t.CreatedAt <= toDate)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        return transactions.Select(MapToDto).ToList();
    }

    private static SalesTransactionDto MapToDto(SalesTransaction entity)
    {
        return new SalesTransactionDto
        {
            Id = entity.Id,
            TransactionNumber = entity.TransactionNumber,
            TenantId = entity.TenantId ?? string.Empty,
            ReservationId = entity.RoomChargeReservationId,
            SubTotal = entity.SubTotal.Amount,
            DiscountAmount = entity.DiscountAmount.Amount,
            VATAmount = entity.VATAmount.Amount,
            TotalAmount = entity.TotalAmount.Amount,
            Currency = entity.TotalAmount.Currency,
            PaymentMethod = entity.PaymentMethod,
            Status = entity.Status.ToString(),
            TransactionDate = entity.CreatedAt,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            Items = entity.Items.Select(i => new SalesTransactionItemDto
            {
                Id = i.Id,
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice.Amount,
                DiscountAmount = i.DiscountAmount.Amount,
                TotalPrice = i.TotalPrice.Amount,
                Currency = i.TotalPrice.Currency
            }).ToList()
        };
    }
}
