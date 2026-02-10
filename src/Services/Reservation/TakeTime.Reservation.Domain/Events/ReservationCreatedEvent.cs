using TakeTime.Core.Domain.Events;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Events;

/// <summary>
/// Domain event raised when a new reservation is created.
/// </summary>
public sealed record ReservationCreatedEvent(
    Guid ReservationId,
    string ReservationNumber,
    string CustomerName,
    DateTime CheckInDate,
    DateTime CheckOutDate,
    Money TotalAmount) : DomainEventBase;
