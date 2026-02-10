using TakeTime.Core.Domain.Events;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Domain.Enums;

namespace TakeTime.Payment.Domain.Events;

/// <summary>
/// Domain event raised when a new payment is received.
/// </summary>
public sealed record PaymentReceivedEvent(
    Guid PaymentId,
    string PaymentNumber,
    Guid ReservationId,
    Guid CustomerId,
    Money Amount,
    PaymentMethod PaymentMethod) : DomainEventBase;
