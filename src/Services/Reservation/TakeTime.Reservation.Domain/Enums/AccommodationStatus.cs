namespace TakeTime.Reservation.Domain.Enums;

/// <summary>
/// Current operational status of an accommodation unit.
/// </summary>
public enum AccommodationStatus
{
    /// <summary>
    /// Unit is available for booking.
    /// </summary>
    Available = 0,

    /// <summary>
    /// Unit is currently occupied by a guest.
    /// </summary>
    Occupied = 1,

    /// <summary>
    /// Unit is undergoing scheduled maintenance.
    /// </summary>
    Maintenance = 2,

    /// <summary>
    /// Unit is out of order and cannot be booked.
    /// </summary>
    OutOfOrder = 3,

    /// <summary>
    /// Unit is being cleaned after a guest checkout.
    /// </summary>
    Cleaning = 4
}
