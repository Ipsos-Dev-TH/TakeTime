using TakeTime.Core.Domain.Base;
using TakeTime.Inventory.Domain.Enums;

namespace TakeTime.Inventory.Domain.Entities;

/// <summary>
/// Tracks individual stock movements for audit and inventory management.
/// </summary>
public class StockMovement : Entity
{
    /// <summary>
    /// Identifier of the product affected by this movement.
    /// </summary>
    public Guid ProductId { get; private set; }

    /// <summary>
    /// Type of stock movement.
    /// </summary>
    public MovementType MovementType { get; private set; }

    /// <summary>
    /// Quantity moved (positive for In, negative for Out, signed for Adjustment).
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Reason for the stock movement.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Reference number (e.g., purchase order, sales transaction, etc.).
    /// </summary>
    public string? ReferenceNumber { get; private set; }

    /// <summary>
    /// Identifier of the user who performed the movement.
    /// </summary>
    public string PerformedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private StockMovement() { }

    /// <summary>
    /// Creates a new stock movement record.
    /// </summary>
    public StockMovement(
        Guid productId,
        MovementType movementType,
        int quantity,
        string reason,
        string performedBy,
        string? referenceNumber = null)
    {
        if (quantity == 0)
            throw new ArgumentException("Quantity cannot be zero.", nameof(quantity));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.", nameof(reason));

        if (string.IsNullOrWhiteSpace(performedBy))
            throw new ArgumentException("Performed by user is required.", nameof(performedBy));

        ProductId = productId;
        MovementType = movementType;
        Quantity = movementType == MovementType.Out ? -Math.Abs(quantity) : quantity;
        Reason = reason;
        PerformedBy = performedBy;
        ReferenceNumber = referenceNumber;
    }
}
