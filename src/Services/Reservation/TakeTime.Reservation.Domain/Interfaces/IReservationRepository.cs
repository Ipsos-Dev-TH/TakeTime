using TakeTime.Core.Domain.Interfaces;

namespace TakeTime.Reservation.Domain.Interfaces;

/// <summary>
/// Repository interface for Reservation aggregate root.
/// Extends the generic repository with reservation-specific query methods.
/// </summary>
public interface IReservationRepository : IRepository<Entities.Reservation>
{
    /// <summary>
    /// Retrieves a reservation by its unique reservation number.
    /// </summary>
    Task<Entities.Reservation?> GetByReservationNumberAsync(
        string reservationNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reservations within the specified date range
    /// (overlapping check-in/check-out dates).
    /// </summary>
    Task<IReadOnlyList<Entities.Reservation>> GetByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reservations for a specific customer.
    /// </summary>
    Task<IReadOnlyList<Entities.Reservation>> GetByCustomerIdAsync(
        Guid customerId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active reservations (Pending, Confirmed, or CheckedIn).
    /// </summary>
    Task<IReadOnlyList<Entities.Reservation>> GetActiveReservationsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reservations with check-in scheduled for today.
    /// </summary>
    Task<IReadOnlyList<Entities.Reservation>> GetTodayCheckInsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all reservations with check-out scheduled for today.
    /// </summary>
    Task<IReadOnlyList<Entities.Reservation>> GetTodayCheckOutsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all accommodation units that are available within the specified date range.
    /// </summary>
    Task<IReadOnlyList<Entities.Accommodation>> GetAvailableAccommodationsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether a specific accommodation is available for the given date range.
    /// </summary>
    Task<bool> CheckAvailabilityAsync(
        Guid accommodationId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}
