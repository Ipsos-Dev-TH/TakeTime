using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Represents an additional item (rental equipment, service, amenity)
/// included in a reservation.
/// </summary>
public class ReservationItem : Entity
{
    /// <summary>
    /// Identifier of the parent reservation.
    /// </summary>
    public Guid ReservationId { get; private set; }

    /// <summary>
    /// Identifier of the rental item or service.
    /// </summary>
    public Guid ItemId { get; private set; }

    /// <summary>
    /// Display name of the item.
    /// </summary>
    public string ItemName { get; private set; } = string.Empty;

    /// <summary>
    /// Quantity ordered.
    /// </summary>
    public int Quantity { get; private set; }

    /// <summary>
    /// Price per single unit.
    /// </summary>
    public Money PricePerUnit { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total price for this line item (PricePerUnit x Quantity).
    /// </summary>
    public Money TotalPrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private ReservationItem() { }

    /// <summary>
    /// Creates a new reservation item.
    /// </summary>
    public ReservationItem(
        Guid itemId,
        string itemName,
        int quantity,
        Money pricePerUnit)
    {
        if (string.IsNullOrWhiteSpace(itemName))
            throw new ArgumentException("Item name is required.", nameof(itemName));

        if (quantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.", nameof(quantity));

        ItemId = itemId;
        ItemName = itemName;
        Quantity = quantity;
        PricePerUnit = pricePerUnit;
        TotalPrice = pricePerUnit * quantity;
    }

    /// <summary>
    /// Sets the parent reservation identifier.
    /// </summary>
    internal void SetReservationId(Guid reservationId)
    {
        ReservationId = reservationId;
    }

    /// <summary>
    /// Updates the quantity and recalculates the total.
    /// </summary>
    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be at least 1.", nameof(newQuantity));

        Quantity = newQuantity;
        TotalPrice = PricePerUnit * Quantity;
        UpdatedAt = DateTime.UtcNow;
    }
}
