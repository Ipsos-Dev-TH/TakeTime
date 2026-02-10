using TakeTime.Core.Domain.Events;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Reservation.Domain.Events;

/// <summary>
/// Domain event raised when a guest checks out.
/// </summary>
public sealed record GuestCheckedOutEvent(
    Guid ReservationId,
    string ReservationNumber,
    string CustomerName,
    DateTime CheckedOutAt,
    Money TotalAmount,
    Money BalanceAmount) : DomainEventBase;
