using TakeTime.Core.Domain.Base;

namespace TakeTime.Reservation.Domain.Entities;

/// <summary>
/// Aggregate root representing a seasonal rate configuration that applies
/// a multiplier to base rates during specific date ranges (e.g., high season,
/// holidays, low season).
/// </summary>
public class SeasonalRateEntity : AggregateRoot
{
    /// <summary>
    /// Display name of the season (e.g., "High Season", "Songkran Holiday").
    /// </summary>
    public string SeasonName { get; private set; } = string.Empty;

    /// <summary>
    /// Start date of the seasonal rate period.
    /// </summary>
    public DateTime StartDate { get; private set; }

    /// <summary>
    /// End date of the seasonal rate period.
    /// </summary>
    public DateTime EndDate { get; private set; }

    /// <summary>
    /// Multiplier applied to base rates during this season.
    /// Values greater than 1.0 increase rates, less than 1.0 decrease them.
    /// </summary>
    public decimal RateMultiplier { get; private set; } = 1.0m;

    /// <summary>
    /// Optional description of the seasonal rate.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Whether this seasonal rate is currently active.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private SeasonalRateEntity() { }

    /// <summary>
    /// Creates a new seasonal rate configuration.
    /// </summary>
    public SeasonalRateEntity(
        string seasonName,
        DateTime startDate,
        DateTime endDate,
        decimal rateMultiplier,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(seasonName))
            throw new ArgumentException("Season name is required.", nameof(seasonName));

        if (endDate <= startDate)
            throw new ArgumentException("End date must be after start date.");

        if (rateMultiplier <= 0)
            throw new ArgumentException("Rate multiplier must be greater than zero.", nameof(rateMultiplier));

        SeasonName = seasonName;
        StartDate = startDate;
        EndDate = endDate;
        RateMultiplier = rateMultiplier;
        Description = description;
        IsActive = true;
    }

    /// <summary>
    /// Updates the seasonal rate details.
    /// </summary>
    public void Update(
        string seasonName,
        DateTime startDate,
        DateTime endDate,
        decimal rateMultiplier,
        string? description)
    {
        if (string.IsNullOrWhiteSpace(seasonName))
            throw new ArgumentException("Season name is required.", nameof(seasonName));

        if (endDate <= startDate)
            throw new ArgumentException("End date must be after start date.");

        if (rateMultiplier <= 0)
            throw new ArgumentException("Rate multiplier must be greater than zero.", nameof(rateMultiplier));

        SeasonName = seasonName;
        StartDate = startDate;
        EndDate = endDate;
        RateMultiplier = rateMultiplier;
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Activates the seasonal rate.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the seasonal rate.
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

    /// <summary>
    /// Checks whether a given date falls within this seasonal rate period.
    /// </summary>
    public bool AppliesTo(DateTime date)
    {
        return IsActive && date.Date >= StartDate.Date && date.Date <= EndDate.Date;
    }
}
