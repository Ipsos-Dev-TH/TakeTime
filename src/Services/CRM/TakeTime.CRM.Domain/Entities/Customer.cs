using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.CRM.Domain.Enums;

namespace TakeTime.CRM.Domain.Entities;

/// <summary>
/// Aggregate root representing a customer in the CRM system.
/// Tracks customer profile, loyalty, preferences, and visit history.
/// </summary>
public class Customer : AggregateRoot
{
    private readonly List<string> _preferences = [];
    private readonly List<string> _tags = [];
    private readonly List<string> _notes = [];

    /// <summary>
    /// Unique customer code for identification.
    /// </summary>
    public string CustomerCode { get; private set; } = string.Empty;

    /// <summary>
    /// Customer's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Customer's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Full name for display purposes.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>
    /// Email address.
    /// </summary>
    public string? Email { get; private set; }

    /// <summary>
    /// Physical address.
    /// </summary>
    public string? Address { get; private set; }

    /// <summary>
    /// Current loyalty tier.
    /// </summary>
    public LoyaltyTier LoyaltyTier { get; private set; }

    /// <summary>
    /// Current loyalty points balance.
    /// </summary>
    public int LoyaltyPoints { get; private set; }

    /// <summary>
    /// Total amount the customer has spent at the property.
    /// </summary>
    public Money TotalSpent { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Customer preferences (e.g., "Non-smoking", "High floor", "Extra pillows").
    /// </summary>
    public IReadOnlyCollection<string> Preferences => _preferences.AsReadOnly();

    /// <summary>
    /// Tags for customer segmentation (e.g., "VIP", "Corporate", "Family").
    /// </summary>
    public IReadOnlyCollection<string> Tags => _tags.AsReadOnly();

    /// <summary>
    /// Internal notes about the customer.
    /// </summary>
    public IReadOnlyCollection<string> Notes => _notes.AsReadOnly();

    /// <summary>
    /// How the customer was first acquired.
    /// </summary>
    public CustomerSource Source { get; private set; }

    /// <summary>
    /// Date of the customer's first visit.
    /// </summary>
    public DateTime? FirstVisitDate { get; private set; }

    /// <summary>
    /// Date of the customer's most recent visit.
    /// </summary>
    public DateTime? LastVisitDate { get; private set; }

    /// <summary>
    /// Total number of visits/stays.
    /// </summary>
    public int TotalVisits { get; private set; }

    /// <summary>
    /// Customer's date of birth for birthday promotions.
    /// </summary>
    public DateTime? DateOfBirth { get; private set; }

    /// <summary>
    /// Customer's anniversary date for special promotions.
    /// </summary>
    public DateTime? Anniversary { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private Customer() { }

    /// <summary>
    /// Creates a new customer.
    /// </summary>
    public Customer(
        string customerCode,
        string firstName,
        string lastName,
        string phone,
        CustomerSource source,
        string? email = null,
        string? address = null,
        DateTime? dateOfBirth = null,
        DateTime? anniversary = null)
    {
        if (string.IsNullOrWhiteSpace(customerCode))
            throw new ArgumentException("Customer code is required.", nameof(customerCode));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Phone number is required.", nameof(phone));

        CustomerCode = customerCode;
        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
        Email = email;
        Address = address;
        Source = source;
        DateOfBirth = dateOfBirth?.Date;
        Anniversary = anniversary?.Date;
        LoyaltyTier = LoyaltyTier.Bronze;
        LoyaltyPoints = 0;
    }

    /// <summary>
    /// Updates the customer's personal information.
    /// </summary>
    public void UpdateProfile(
        string firstName,
        string lastName,
        string phone,
        string? email = null,
        string? address = null,
        DateTime? dateOfBirth = null,
        DateTime? anniversary = null)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        FirstName = firstName;
        LastName = lastName;
        Phone = phone;
        Email = email;
        Address = address;
        DateOfBirth = dateOfBirth?.Date;
        Anniversary = anniversary?.Date;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a visit, updating visit history and spending.
    /// </summary>
    public void RecordVisit(Money amountSpent, DateTime visitDate)
    {
        TotalVisits++;
        TotalSpent = TotalSpent + amountSpent;
        LastVisitDate = visitDate;

        if (!FirstVisitDate.HasValue)
            FirstVisitDate = visitDate;

        UpdateLoyaltyTier();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds loyalty points to the customer's balance.
    /// </summary>
    public void AddLoyaltyPoints(int points)
    {
        if (points <= 0)
            throw new ArgumentException("Points must be positive.", nameof(points));

        LoyaltyPoints += points;
        UpdateLoyaltyTier();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Redeems loyalty points from the customer's balance.
    /// </summary>
    public void RedeemLoyaltyPoints(int points)
    {
        if (points <= 0)
            throw new ArgumentException("Points must be positive.", nameof(points));

        if (points > LoyaltyPoints)
            throw new InvalidOperationException(
                $"Insufficient loyalty points. Available: {LoyaltyPoints}, Requested: {points}.");

        LoyaltyPoints -= points;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a preference.
    /// </summary>
    public void AddPreference(string preference)
    {
        if (string.IsNullOrWhiteSpace(preference))
            throw new ArgumentException("Preference is required.", nameof(preference));

        if (!_preferences.Contains(preference, StringComparer.OrdinalIgnoreCase))
        {
            _preferences.Add(preference);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes a preference.
    /// </summary>
    public void RemovePreference(string preference)
    {
        _preferences.Remove(preference);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds a tag for customer segmentation.
    /// </summary>
    public void AddTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag is required.", nameof(tag));

        if (!_tags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            _tags.Add(tag);
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Removes a tag.
    /// </summary>
    public void RemoveTag(string tag)
    {
        _tags.Remove(tag);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Adds an internal note.
    /// </summary>
    public void AddNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Note is required.", nameof(note));

        var timestamped = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm}] {note}";
        _notes.Add(timestamped);
        UpdatedAt = DateTime.UtcNow;
    }

    private void UpdateLoyaltyTier()
    {
        // Tier thresholds based on total spending
        LoyaltyTier = TotalSpent.Amount switch
        {
            >= 100000m => LoyaltyTier.Platinum,
            >= 50000m => LoyaltyTier.Gold,
            >= 20000m => LoyaltyTier.Silver,
            _ => LoyaltyTier.Bronze
        };
    }
}
