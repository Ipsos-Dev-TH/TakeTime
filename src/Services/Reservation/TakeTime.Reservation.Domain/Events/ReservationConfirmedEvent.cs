using TakeTime.Core.Domain.Events;

namespace TakeTime.Reservation.Domain.Events;

/// <summary>
/// Domain event raised when a reservation is confirmed.
/// </summary>
public sealed record ReservationConfirmedEvent(
    Guid ReservationId,
    string ReservationNumber,
    string CustomerName,
    DateTime CheckInDate) : DomainEventBase;
