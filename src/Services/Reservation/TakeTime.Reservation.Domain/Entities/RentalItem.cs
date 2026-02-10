using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Represents an item available for rental to guests
/// (e.g., kayak, bicycle, barbecue set, camping equipment).
/// </summary>
public class RentalItem : Entity
{
    /// <summary>
    /// Name of the rental item.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Detailed description of the rental item.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Category for organizing rental items.
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Rental price per day.
    /// </summary>
    public Money PricePerDay { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Number of units currently available for rent.
    /// </summary>
    public int AvailableQuantity { get; private set; }

    /// <summary>
    /// Whether the rental item is currently active and rentable.
    /// </summary>
    public string Status { get; private set; } = "Active";

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private RentalItem() { }

    /// <summary>
    /// Creates a new rental item.
    /// </summary>
    public RentalItem(
        string name,
        string category,
        Money pricePerDay,
        int availableQuantity,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.", nameof(category));

        if (availableQuantity < 0)
            throw new ArgumentException("Available quantity cannot be negative.", nameof(availableQuantity));

        Name = name;
        Category = category;
        PricePerDay = pricePerDay;
        AvailableQuantity = availableQuantity;
        Description = description;
    }

    /// <summary>
    /// Updates the rental item details.
    /// </summary>
    public void Update(string name, string? description, string category, Money pricePerDay)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Name = name;
        Description = description;
        Category = category;
        PricePerDay = pricePerDay;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adjusts the available quantity.
    /// </summary>
    public void AdjustQuantity(int adjustment)
    {
        var newQuantity = AvailableQuantity + adjustment;
        if (newQuantity < 0)
            throw new InvalidOperationException(
                $"Cannot reduce quantity below zero. Current: {AvailableQuantity}, Adjustment: {adjustment}.");

        AvailableQuantity = newQuantity;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the rental item.
    /// </summary>
    public void Deactivate()
    {
        Status = "Inactive";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the rental item.
    /// </summary>
    public void Activate()
    {
        Status = "Active";
        UpdatedAt = DateTime.UtcNow;
    }
}
