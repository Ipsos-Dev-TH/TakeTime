using TakeTime.Core.Domain.Events;
using TakeTime.Core.Domain.ValueObjects;

namespace TakeTime.Payment.Domain.Events;

/// <summary>
/// Domain event raised when a payment is verified by staff.
/// </summary>
public sealed record PaymentVerifiedEvent(
    Guid PaymentId,
    string PaymentNumber,
    Guid ReservationId,
    Money Amount,
    string VerifiedBy) : DomainEventBase;
