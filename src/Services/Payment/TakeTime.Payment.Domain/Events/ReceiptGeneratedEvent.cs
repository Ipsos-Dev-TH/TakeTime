using TakeTime.Core.Domain.Events;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Payment.Domain.Enums;

namespace TakeTime.Payment.Domain.Events;

/// <summary>
/// Domain event raised when a receipt is generated.
/// </summary>
public sealed record ReceiptGeneratedEvent(
    Guid ReceiptId,
    string ReceiptNumber,
    Guid ReservationId,
    Money TotalAmount,
    ReceiptType ReceiptType) : DomainEventBase;
