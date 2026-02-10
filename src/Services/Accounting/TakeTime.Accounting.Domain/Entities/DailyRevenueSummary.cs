using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Accounting.Domain.Entities;

/// <summary>
/// Daily revenue summary for management reporting and performance tracking.
/// Aggregates key financial and occupancy metrics for a single day.
/// </summary>
public class DailyRevenueSummary : Entity
{
    /// <summary>
    /// The date this summary represents.
    /// </summary>
    public DateTime Date { get; private set; }

    /// <summary>
    /// Total revenue from accommodation bookings.
    /// </summary>
    public Money TotalReservationRevenue { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total revenue from point-of-sale transactions.
    /// </summary>
    public Money TotalPOSRevenue { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total revenue from other sources (services, activities, etc.).
    /// </summary>
    public Money TotalOtherRevenue { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total expenses for the day.
    /// </summary>
    public Money TotalExpenses { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Net revenue (total revenue minus total expenses).
    /// </summary>
    public Money NetRevenue { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Number of accommodation units sold (occupied).
    /// </summary>
    public int RoomsSold { get; private set; }

    /// <summary>
    /// Occupancy rate as a percentage (0-100).
    /// </summary>
    public decimal OccupancyRate { get; private set; }

    /// <summary>
    /// Average Daily Rate (ADR) = Total Room Revenue / Rooms Sold.
    /// </summary>
    public Money ADR { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Revenue Per Available Room (RevPAR) = Total Room Revenue / Total Available Rooms.
    /// </summary>
    public Money RevPAR { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private DailyRevenueSummary() { }

    /// <summary>
    /// Creates a new daily revenue summary.
    /// </summary>
    public DailyRevenueSummary(DateTime date)
    {
        Date = date.Date;
    }

    /// <summary>
    /// Updates the revenue figures for the day.
    /// </summary>
    public void UpdateRevenue(
        Money reservationRevenue,
        Money posRevenue,
        Money otherRevenue,
        Money expenses)
    {
        TotalReservationRevenue = reservationRevenue;
        TotalPOSRevenue = posRevenue;
        TotalOtherRevenue = otherRevenue;
        TotalExpenses = expenses;

        var totalRevenue = reservationRevenue + posRevenue + otherRevenue;
        NetRevenue = totalRevenue - expenses;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the occupancy metrics.
    /// </summary>
    /// <param name="roomsSold">Number of rooms occupied.</param>
    /// <param name="totalAvailableRooms">Total rooms available in the property.</param>
    public void UpdateOccupancy(int roomsSold, int totalAvailableRooms)
    {
        if (totalAvailableRooms <= 0)
            throw new ArgumentException("Total available rooms must be positive.", nameof(totalAvailableRooms));

        RoomsSold = roomsSold;
        OccupancyRate = Math.Round((decimal)roomsSold / totalAvailableRooms * 100m, 2);

        // ADR = Total Room Revenue / Rooms Sold
        ADR = roomsSold > 0
            ? Money.InBaht(Math.Round(TotalReservationRevenue.Amount / roomsSold, 2))
            : Money.ZeroBaht();

        // RevPAR = Total Room Revenue / Total Available Rooms
        RevPAR = Money.InBaht(Math.Round(TotalReservationRevenue.Amount / totalAvailableRooms, 2));

        UpdatedAt = DateTime.UtcNow;
    }
}
