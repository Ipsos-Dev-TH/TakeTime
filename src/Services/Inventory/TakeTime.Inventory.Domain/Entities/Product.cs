using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Inventory.Domain.Enums;

namespace TakeTime.Inventory.Domain.Entities;

/// <summary>
/// Aggregate root representing a product in the inventory system.
/// Tracks stock levels, pricing, and supports both POS and room-charge sales.
/// </summary>
public class Product : AggregateRoot
{
    /// <summary>
    /// Barcode or SKU for the product.
    /// </summary>
    public string Barcode { get; private set; } = string.Empty;

    /// <summary>
    /// Product name.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Product category for organization and reporting.
    /// </summary>
    public ProductCategory Category { get; private set; }

    /// <summary>
    /// Cost price (purchase price) for profit calculation.
    /// </summary>
    public Money CostPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Selling price to the customer.
    /// </summary>
    public Money SellingPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Current stock quantity on hand.
    /// </summary>
    public int StockQuantity { get; private set; }

    /// <summary>
    /// Minimum stock level that triggers a reorder alert.
    /// </summary>
    public int MinStockLevel { get; private set; }

    /// <summary>
    /// Unit of measurement (e.g., "piece", "bottle", "kg").
    /// </summary>
    public string Unit { get; private set; } = "piece";

    /// <summary>
    /// Whether the product is currently active for sale.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// URL of the product image.
    /// </summary>
    public string? ImageUrl { get; private set; }

    /// <summary>
    /// How the product can be charged (Normal POS, Room Charge, or Both).
    /// </summary>
    public ChargeType ChargeType { get; private set; }

    /// <summary>
    /// Whether the current stock is at or below the minimum stock level.
    /// </summary>
    public bool IsLowStock => StockQuantity <= MinStockLevel;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Product() { }

    /// <summary>
    /// Creates a new product.
    /// </summary>
    public Product(
        string barcode,
        string name,
        ProductCategory category,
        Money costPrice,
        Money sellingPrice,
        int stockQuantity,
        int minStockLevel,
        string unit = "piece",
        ChargeType chargeType = ChargeType.Normal,
        string? description = null,
        string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException("Barcode is required.", nameof(barcode));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        if (stockQuantity < 0)
            throw new ArgumentException("Stock quantity cannot be negative.", nameof(stockQuantity));

        if (minStockLevel < 0)
            throw new ArgumentException("Minimum stock level cannot be negative.", nameof(minStockLevel));

        Barcode = barcode;
        Name = name;
        Category = category;
        CostPrice = costPrice;
        SellingPrice = sellingPrice;
        StockQuantity = stockQuantity;
        MinStockLevel = minStockLevel;
        Unit = unit;
        ChargeType = chargeType;
        Description = description;
        ImageUrl = imageUrl;
    }

    /// <summary>
    /// Adjusts the stock quantity with a reason for audit trail.
    /// </summary>
    public void AdjustStock(int quantity, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required for stock adjustment.", nameof(reason));

        var newQuantity = StockQuantity + quantity;
        if (newQuantity < 0)
            throw new InvalidOperationException(
                $"Stock adjustment would result in negative stock. Current: {StockQuantity}, Adjustment: {quantity}.");

        StockQuantity = newQuantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the selling price.
    /// </summary>
    public void SetPrice(Money price)
    {
        SellingPrice = price;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the cost price.
    /// </summary>
    public void SetCostPrice(Money price)
    {
        CostPrice = price;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the product details.
    /// </summary>
    public void Update(
        string name,
        string? description,
        ProductCategory category,
        string unit,
        int minStockLevel,
        ChargeType chargeType,
        string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.", nameof(name));

        Name = name;
        Description = description;
        Category = category;
        Unit = unit;
        MinStockLevel = minStockLevel;
        ChargeType = chargeType;
        ImageUrl = imageUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the product so it can no longer be sold.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates the product for sale.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
