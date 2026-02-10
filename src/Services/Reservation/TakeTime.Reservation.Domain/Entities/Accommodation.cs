using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Aggregate root representing an accommodation unit at the property
/// (room, suite, villa, tent, etc.).
/// </summary>
public class Accommodation : AggregateRoot
{
    private readonly List<string> _amenities = [];
    private readonly List<string> _images = [];

    /// <summary>
    /// Display name of the accommodation (e.g., "Deluxe Sea View Room").
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Type of accommodation.
    /// </summary>
    public AccommodationType Type { get; private set; }

    /// <summary>
    /// Detailed description of the accommodation.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Maximum number of guests the accommodation can hold.
    /// </summary>
    public int MaxGuests { get; private set; }

    /// <summary>
    /// Standard base price per night before any discounts or surcharges.
    /// </summary>
    public Money BasePrice { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Current operational status of the accommodation.
    /// </summary>
    public AccommodationStatus Status { get; private set; }

    /// <summary>
    /// Floor number where the accommodation is located.
    /// </summary>
    public string? Floor { get; private set; }

    /// <summary>
    /// Building or zone within the property.
    /// </summary>
    public string? Building { get; private set; }

    /// <summary>
    /// List of amenities available in this accommodation.
    /// </summary>
    public IReadOnlyCollection<string> Amenities => _amenities.AsReadOnly();

    /// <summary>
    /// List of image URLs for the accommodation.
    /// </summary>
    public IReadOnlyCollection<string> Images => _images.AsReadOnly();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Accommodation() { }

    /// <summary>
    /// Creates a new accommodation.
    /// </summary>
    public Accommodation(
        string name,
        AccommodationType type,
        int maxGuests,
        Money basePrice,
        string? description = null,
        string? floor = null,
        string? building = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Accommodation name is required.", nameof(name));

        if (maxGuests <= 0)
            throw new ArgumentException("Maximum guests must be at least 1.", nameof(maxGuests));

        Name = name;
        Type = type;
        MaxGuests = maxGuests;
        BasePrice = basePrice;
        Description = description;
        Floor = floor;
        Building = building;
        Status = AccommodationStatus.Available;
    }

    /// <summary>
    /// Updates the accommodation details.
    /// </summary>
    public void Update(
        string name,
        string? description,
        int maxGuests,
        Money basePrice,
        string? floor = null,
        string? building = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Accommodation name is required.", nameof(name));

        if (maxGuests <= 0)
            throw new ArgumentException("Maximum guests must be at least 1.", nameof(maxGuests));

        Name = name;
        Description = description;
        MaxGuests = maxGuests;
        BasePrice = basePrice;
        Floor = floor;
        Building = building;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Changes the status of the accommodation.
    /// </summary>
    public void SetStatus(AccommodationStatus newStatus)
    {
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the accommodation as occupied.
    /// </summary>
    public void MarkAsOccupied()
    {
        if (Status != AccommodationStatus.Available)
            throw new InvalidOperationException(
                $"Accommodation must be Available to be marked as Occupied. Current status: {Status}.");

        Status = AccommodationStatus.Occupied;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the accommodation for cleaning after guest checkout.
    /// </summary>
    public void MarkForCleaning()
    {
        Status = AccommodationStatus.Cleaning;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the accommodation as available after cleaning or maintenance.
    /// </summary>
    public void MarkAsAvailable()
    {
        Status = AccommodationStatus.Available;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Puts the accommodation under maintenance.
    /// </summary>
    public void PutUnderMaintenance()
    {
        Status = AccommodationStatus.Maintenance;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds an amenity to the accommodation.
    /// </summary>
    public void AddAmenity(string amenity)
    {
        if (string.IsNullOrWhiteSpace(amenity))
            throw new ArgumentException("Amenity name is required.", nameof(amenity));

        if (!_amenities.Contains(amenity, StringComparer.OrdinalIgnoreCase))
        {
            _amenities.Add(amenity);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes an amenity from the accommodation.
    /// </summary>
    public void RemoveAmenity(string amenity)
    {
        _amenities.Remove(amenity);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds an image URL to the accommodation.
    /// </summary>
    public void AddImage(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            throw new ArgumentException("Image URL is required.", nameof(imageUrl));

        _images.Add(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes an image URL from the accommodation.
    /// </summary>
    public void RemoveImage(string imageUrl)
    {
        _images.Remove(imageUrl);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the base price.
    /// </summary>
    public void SetBasePrice(Money newPrice)
    {
        BasePrice = newPrice;
        UpdatedAt = DateTime.UtcNow;
    }
}
