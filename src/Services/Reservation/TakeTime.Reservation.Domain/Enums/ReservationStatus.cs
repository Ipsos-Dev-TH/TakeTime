namespace TakeTime.Reservation.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of a reservation.
/// </summary>
public enum ReservationStatus
{
    /// <summary>
    /// Reservation has been created but not yet confirmed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Reservation has been confirmed (e.g., deposit received).
    /// </summary>
    Confirmed = 1,

    /// <summary>
    /// Guest has checked in.
    /// </summary>
    CheckedIn = 2,

    /// <summary>
    /// Guest has checked out.
    /// </summary>
    CheckedOut = 3,

    /// <summary>
    /// Reservation has been cancelled.
    /// </summary>
    Cancelled = 4,

    /// <summary>
    /// Check-in date has been postponed to a later date.
    /// </summary>
    PostponedCheckIn = 5,

    /// <summary>
    /// Guest did not show up on the check-in date.
    /// </summary>
    NoShow = 6
}
