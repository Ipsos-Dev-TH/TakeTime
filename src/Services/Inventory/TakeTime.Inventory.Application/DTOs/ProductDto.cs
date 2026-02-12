namespace TakeTime.Inventory.Application.DTOs;

/// <summary>
/// Full product data transfer object.
/// </summary>
public sealed class ProductDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public string Currency { get; set; } = "THB";
    public int CurrentStock { get; set; }
    public int MinimumStock { get; set; }
    public int MaximumStock { get; set; }
    public string UnitOfMeasure { get; set; } = "Piece";
    public bool IsActive { get; set; } = true;
    public bool TrackInventory { get; set; } = true;
    public bool IsLowStock { get; set; }
    public string? ImageUrl { get; set; }
    public string? Supplier { get; set; }
    public decimal? VATRate { get; set; }
    public bool IsVATExempt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for creating a new product.
/// </summary>
public sealed class CreateProductDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SKU { get; set; }
    public string? Barcode { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SubCategory { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SellingPrice { get; set; }
    public int InitialStock { get; set; }
    public int MinimumStock { get; set; }
    public int MaximumStock { get; set; } = 9999;
    public string UnitOfMeasure { get; set; } = "Piece";
    public bool TrackInventory { get; set; } = true;
    public string? ImageUrl { get; set; }
    public string? Supplier { get; set; }
    public bool IsVATExempt { get; set; }
}

/// <summary>
/// DTO for updating an existing product.
/// </summary>
public sealed class UpdateProductDto
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Barcode { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SellingPrice { get; set; }
    public int? MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public string? UnitOfMeasure { get; set; }
    public bool? IsActive { get; set; }
    public bool? TrackInventory { get; set; }
    public string? ImageUrl { get; set; }
    public string? Supplier { get; set; }
    public bool? IsVATExempt { get; set; }
}

/// <summary>
/// Search criteria for filtering products.
/// </summary>
public sealed class ProductSearchCriteria
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? SubCategory { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsLowStock { get; set; }
    public string? Supplier { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string SortBy { get; set; } = "Name";
    public bool SortDescending { get; set; }
}

/// <summary>
/// DTO for stock movement history entries.
/// </summary>
public sealed class StockMovementDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? SKU { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for product category summary with item counts.
/// </summary>
public sealed class CategorySummaryDto
{
    public string Category { get; set; } = string.Empty;
    public int ProductCount { get; set; }
    public int ActiveCount { get; set; }
    public int LowStockCount { get; set; }
}

/// <summary>
/// Paginated result wrapper for inventory queries.
/// </summary>
public sealed class PaginatedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}
