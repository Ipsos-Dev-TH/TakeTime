using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Aggregate root representing a rate plan that defines base pricing
/// for accommodations. Each tenant can have multiple rate plans
/// (e.g., Standard, Weekend, Holiday).
/// </summary>
public class RatePlan : AggregateRoot
{
    /// <summary>
    /// Display name of the rate plan.
    /// </summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>
    /// Optional description of the rate plan.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Base rate amount for this plan.
    /// </summary>
    public Money BaseRate { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Whether this is the default rate plan for the tenant.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// Whether this rate plan is currently active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private RatePlan() { }

    /// <summary>
    /// Creates a new rate plan.
    /// </summary>
    public RatePlan(
        string name,
        Money baseRate,
        string? description = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rate plan name is required.", nameof(name));

        Name = name;
        BaseRate = baseRate;
        Description = description;
        IsDefault = isDefault;
        IsActive = true;
    }

    /// <summary>
    /// Updates the rate plan details.
    /// </summary>
    public void Update(string name, string? description, Money baseRate)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Rate plan name is required.", nameof(name));

        Name = name;
        Description = description;
        BaseRate = baseRate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets whether this rate plan is the default.
    /// </summary>
    public void SetDefault(bool isDefault)
    {
        IsDefault = isDefault;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the rate plan.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the rate plan.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Sets the active status.
    /// </summary>
    public void SetActive(bool isActive)
    {
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
    }
}
