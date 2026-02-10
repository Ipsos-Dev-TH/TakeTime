using TakeTime.Core.Domain.Events;

namespace TakeTime.Reservation.Domain.Events;

/// <summary>
/// Domain event raised when a reservation is cancelled.
/// </summary>
public sealed record ReservationCancelledEvent(
    Guid ReservationId,
    string ReservationNumber,
    string CustomerName,
    string? CancellationReason) : DomainEventBase;
