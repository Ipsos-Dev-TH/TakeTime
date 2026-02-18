using Microsoft.EntityFrameworkCore;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Inventory.Application.Commands;
using TakeTime.Inventory.Application.DTOs;
using TakeTime.Inventory.Domain.Entities;
using TakeTime.Inventory.Domain.Enums;

namespace TakeTime.Inventory.Infrastructure.Repositories;

public sealed class ProductDtoRepository : IProductRepository
{
    private readonly InventoryDbContext _db;

    public ProductDtoRepository(InventoryDbContext db)
    {
        _db = db;
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return entity is null ? null : MapToDto(entity);
    }

    public async Task CreateAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        var currency = product.Currency ?? "THB";

        if (!Enum.TryParse<ProductCategory>(product.Category, true, out var category))
            category = ProductCategory.General;

        var entity = new Product(
            barcode: product.Barcode ?? product.SKU,
            name: product.Name,
            category: category,
            costPrice: Money.Create(product.CostPrice, currency),
            sellingPrice: Money.Create(product.SellingPrice, currency),
            stockQuantity: product.CurrentStock,
            minStockLevel: product.MinimumStock,
            unit: product.UnitOfMeasure,
            description: product.Description,
            imageUrl: product.ImageUrl);

        entity.TenantId = product.TenantId;
        entity.CreatedBy = product.CreatedBy;

        await _db.Products.AddAsync(entity, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        product.Id = entity.Id;
    }

    public async Task UpdateAsync(ProductDto product, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == product.Id, cancellationToken);
        if (entity is null) return;

        if (!Enum.TryParse<ProductCategory>(product.Category, true, out var category))
            category = entity.Category;

        entity.Update(
            name: product.Name,
            description: product.Description,
            category: category,
            unit: product.UnitOfMeasure,
            minStockLevel: product.MinimumStock,
            chargeType: entity.ChargeType,
            imageUrl: product.ImageUrl);

        var currency = product.Currency ?? "THB";
        entity.SetPrice(Money.Create(product.SellingPrice, currency));
        entity.SetCostPrice(Money.Create(product.CostPrice, currency));

        if (product.IsActive && !entity.IsActive)
            entity.Activate();
        else if (!product.IsActive && entity.IsActive)
            entity.Deactivate();

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PaginatedResult<ProductDto>> SearchAsync(
        ProductSearchCriteria criteria, CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(criteria.SearchTerm))
        {
            var term = criteria.SearchTerm;
            query = query.Where(p =>
                p.Name.Contains(term) ||
                (p.Barcode != null && p.Barcode.Contains(term)) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(criteria.Category) &&
            Enum.TryParse<ProductCategory>(criteria.Category, true, out var cat))
        {
            query = query.Where(p => p.Category == cat);
        }

        if (criteria.IsActive.HasValue)
            query = query.Where(p => p.IsActive == criteria.IsActive.Value);

        if (criteria.IsLowStock == true)
            query = query.Where(p => p.StockQuantity <= p.MinStockLevel);

        if (criteria.MinPrice.HasValue)
            query = query.Where(p => p.SellingPrice.Amount >= criteria.MinPrice.Value);

        if (criteria.MaxPrice.HasValue)
            query = query.Where(p => p.SellingPrice.Amount <= criteria.MaxPrice.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        query = criteria.SortBy?.ToLower() switch
        {
            "price" or "sellingprice" => criteria.SortDescending
                ? query.OrderByDescending(p => p.SellingPrice.Amount)
                : query.OrderBy(p => p.SellingPrice.Amount),
            "stock" or "stockquantity" => criteria.SortDescending
                ? query.OrderByDescending(p => p.StockQuantity)
                : query.OrderBy(p => p.StockQuantity),
            "category" => criteria.SortDescending
                ? query.OrderByDescending(p => p.Category)
                : query.OrderBy(p => p.Category),
            "createdat" => criteria.SortDescending
                ? query.OrderByDescending(p => p.CreatedAt)
                : query.OrderBy(p => p.CreatedAt),
            _ => criteria.SortDescending
                ? query.OrderByDescending(p => p.Name)
                : query.OrderBy(p => p.Name)
        };

        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize < 1 ? 20 : criteria.PageSize;

        var entities = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PaginatedResult<ProductDto>
        {
            Items = entities.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task AdjustStockAsync(
        Guid productId, int quantityChange, string adjustmentType,
        string? reference, string? adjustedBy,
        CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (product is null) return;

        var previousQty = product.StockQuantity;
        product.AdjustStock(quantityChange, adjustmentType);

        var movementType = quantityChange > 0 ? MovementType.In : MovementType.Out;
        if (adjustmentType.Equals("Adjustment", StringComparison.OrdinalIgnoreCase))
            movementType = MovementType.Adjustment;

        var movement = new StockMovement(
            productId: productId,
            movementType: movementType,
            quantity: Math.Abs(quantityChange),
            reason: adjustmentType,
            performedBy: adjustedBy ?? "System",
            referenceNumber: reference);
        movement.TenantId = product.TenantId;

        await _db.StockMovements.AddAsync(movement, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCurrentStockAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        return product?.StockQuantity ?? 0;
    }

    public async Task<string> GenerateSKUAsync(string category, CancellationToken cancellationToken = default)
    {
        var prefix = category.Length >= 3 ? category[..3].ToUpper() : category.ToUpper();
        var count = await _db.Products.CountAsync(cancellationToken);
        return $"{prefix}-{(count + 1):D5}";
    }

    public async Task DeleteAsync(Guid productId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        if (entity is null) return;

        entity.Deactivate();
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<StockMovementDto>> GetStockHistoryAsync(
        Guid productId, DateTime? from, DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var query = _db.StockMovements.Where(m => m.ProductId == productId);

        if (from.HasValue)
            query = query.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)
            query = query.Where(m => m.CreatedAt <= to.Value);

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new StockMovementDto
            {
                Id = m.Id,
                ProductId = m.ProductId,
                ProductName = product != null ? product.Name : string.Empty,
                MovementType = m.MovementType.ToString(),
                Quantity = m.Quantity,
                Reason = m.Reason,
                ReferenceNumber = m.ReferenceNumber,
                PerformedBy = m.PerformedBy,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<CategorySummaryDto>> GetCategoriesAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .GroupBy(p => p.Category)
            .Select(g => new CategorySummaryDto
            {
                Category = g.Key.ToString(),
                ProductCount = g.Count(),
                ActiveCount = g.Count(p => p.IsActive),
                LowStockCount = g.Count(p => p.StockQuantity <= p.MinStockLevel)
            })
            .ToListAsync(cancellationToken);
    }

    private static ProductDto MapToDto(Product entity)
    {
        return new ProductDto
        {
            Id = entity.Id,
            TenantId = entity.TenantId ?? string.Empty,
            Name = entity.Name,
            SKU = entity.Barcode,
            Barcode = entity.Barcode,
            Description = entity.Description,
            Category = entity.Category.ToString(),
            CostPrice = entity.CostPrice.Amount,
            SellingPrice = entity.SellingPrice.Amount,
            Currency = entity.SellingPrice.Currency,
            CurrentStock = entity.StockQuantity,
            MinimumStock = entity.MinStockLevel,
            UnitOfMeasure = entity.Unit,
            IsActive = entity.IsActive,
            TrackInventory = true,
            IsLowStock = entity.IsLowStock,
            ImageUrl = entity.ImageUrl,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            CreatedBy = entity.CreatedBy
        };
    }
}
