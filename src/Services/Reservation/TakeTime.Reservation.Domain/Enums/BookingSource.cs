namespace TakeTime.Reservation.Domain.Enums;

/// <summary>
/// Represents the channel through which a reservation was made.
/// </summary>
public enum BookingSource
{
    /// <summary>
    /// Direct booking at the property.
    /// </summary>
    Direct = 0,

    /// <summary>
    /// Booked through the property website.
    /// </summary>
    Website = 1,

    /// <summary>
    /// Booked through Agoda OTA.
    /// </summary>
    Agoda = 2,

    /// <summary>
    /// Booked through Booking.com OTA.
    /// </summary>
    BookingDotCom = 3,

    /// <summary>
    /// Booked through Expedia OTA.
    /// </summary>
    Expedia = 4,

    /// <summary>
    /// Booked through LINE messaging application.
    /// </summary>
    LINE = 5,

    /// <summary>
    /// Booked over the phone.
    /// </summary>
    Phone = 6,

    /// <summary>
    /// Walk-in guest without prior reservation.
    /// </summary>
    WalkIn = 7,

    /// <summary>
    /// Booked through an affiliate partner.
    /// </summary>
    Affiliate = 8
}
