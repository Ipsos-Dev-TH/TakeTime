using Microsoft.EntityFrameworkCore;
using TakeTime.Inventory.Domain.Entities;
using TakeTime.Inventory.Domain.Enums;

namespace TakeTime.Inventory.Infrastructure.Repositories;

public class ProductRepository
{
    private readonly InventoryDbContext _context;

    public ProductRepository(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Products.FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Product?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
        => await _context.Products.FirstOrDefaultAsync(p => p.Barcode == barcode, ct);

    public async Task<List<Product>> SearchAsync(string? keyword, string? category, int page, int pageSize, CancellationToken ct = default)
    {
        var query = _context.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(p => p.Name.Contains(keyword) || (p.Barcode != null && p.Barcode.Contains(keyword)));

        if (!string.IsNullOrWhiteSpace(category) && Enum.TryParse<ProductCategory>(category, true, out var categoryEnum))
            query = query.Where(p => p.Category == categoryEnum);

        return await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<List<Product>> GetLowStockAsync(CancellationToken ct = default)
        => await _context.Products
            .Where(p => p.IsActive && p.StockQuantity <= p.MinStockLevel)
            .OrderBy(p => p.StockQuantity)
            .ToListAsync(ct);

    public async Task AddAsync(Product product, CancellationToken ct = default)
    {
        await _context.Products.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Product product, CancellationToken ct = default)
    {
        _context.Products.Update(product);
        await _context.SaveChangesAsync(ct);
    }

    public async Task AddStockMovementAsync(StockMovement movement, CancellationToken ct = default)
    {
        await _context.StockMovements.AddAsync(movement, ct);
        await _context.SaveChangesAsync(ct);
    }
}
