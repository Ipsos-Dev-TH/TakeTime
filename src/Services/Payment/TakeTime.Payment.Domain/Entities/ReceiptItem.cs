using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Payment.Domain.Entities;

/// <summary>
/// Represents a single line item on a receipt.
/// </summary>
public class ReceiptItem : Entity
{
    /// <summary>
    /// Identifier of the parent receipt.
    /// </summary>
    public Guid ReceiptId { get; private set; }

    /// <summary>
    /// Description of the item or service.
    /// </summary>
    public string Description { get; private set; } = string.Empty;

    /// <summary>
    /// Quantity of the item.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Price per unit.
    /// </summary>
    public Money UnitPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total amount for this line item (UnitPrice x Quantity - DiscountAmount).
    /// </summary>
    public Money Amount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Discount applied to this line item.
    /// </summary>
    public Money DiscountAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private ReceiptItem() { }

    /// <summary>
    /// Creates a new receipt item.
    /// </summary>
    public ReceiptItem(
        string description,
        int quantity,
        Money unitPrice,
        Money? discountAmount = null)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));

        Description = description;
        Quantity = quantity;
        UnitPrice = unitPrice;
        DiscountAmount = discountAmount ?? Money.ZeroBaht();
        Amount = (unitPrice * quantity) - DiscountAmount;
    }

    /// <summary>
    /// Sets the parent receipt identifier.
    /// </summary>
    internal void SetReceiptId(Guid receiptId)
    {
        ReceiptId = receiptId;
    }
}
