using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Inventory.Domain.Entities;

/// <summary>
/// Represents a single line item in a POS sales transaction.
/// </summary>
public class SalesTransactionItem : Entity
{
    /// <summary>
    /// Identifier of the parent sales transaction.
    /// </summary>
    public Guid SalesTransactionId { get; private set; }

    /// <summary>
    /// Identifier of the product sold.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Product name at the time of sale (denormalized for historical accuracy).
    /// </summary>
    public string ProductName { get; private set; } = string.Empty;

    /// <summary>
    /// Quantity sold.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Unit price at the time of sale.
    /// </summary>
    public Money UnitPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Discount applied to this line item.
    /// </summary>
    public Money DiscountAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total price for this line item after discount.
    /// </summary>
    public Money TotalPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SalesTransactionItem() { }

    /// <summary>
    /// Creates a new sales transaction item.
    /// </summary>
    public SalesTransactionItem(
        Guid productId,
        string productName,
        int quantity,
        Money unitPrice,
        Money? discountAmount = null)
    {
        if (string.IsNullOrWhiteSpace(productName))
            throw new ArgumentException("Product name is required.", nameof(productName));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));

        ProductId = productId;
        ProductName = productName;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount ?? Money.ZeroBaht();
        TotalPrice = (unitPrice * quantity) - DiscountAmount;
    }

    /// <summary>
    /// Sets the parent transaction identifier.
    /// </summary>
    internal void SetSalesTransactionId(Guid transactionId)
    {
        SalesTransactionId = transactionId;
    }
}
