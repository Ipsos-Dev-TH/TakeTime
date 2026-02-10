using TakeTime.Core.Domain.Events;

namespace TakeTime.Reservation.Domain.Events;

/// <summary>
/// Domain event raised when a guest checks in.
/// </summary>
public sealed record GuestCheckedInEvent(
    Guid ReservationId,
    string ReservationNumber,
    string CustomerName,
    DateTime CheckedInAt) : DomainEventBase;
