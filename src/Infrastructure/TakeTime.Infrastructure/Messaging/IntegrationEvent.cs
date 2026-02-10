using System.Text.Json.Serialization;

namespace TakeTime.Infrastructure.Messaging;

/// <summary>
/// Base class for integration events that are exchanged between microservices.
/// Integration events represent cross-boundary notifications that other services
/// may subscribe to for eventual consistency.
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>
    /// Unique identifier for this event occurrence.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// UTC timestamp when the event was created.
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Fully qualified type name of the event for routing and deserialization.
    /// </summary>
    public string EventType { get; }

    /// <summary>
    /// The tenant context in which the event was produced.
    /// </summary>
    public string? TenantId { get; init; }

    /// <summary>
    /// Correlation ID for distributed tracing across services.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// The user who triggered the action that produced this event.
    /// </summary>
    public string? UserId { get; init; }

    /// <summary>
    /// The source service that produced this event.
    /// </summary>
    public string SourceService { get; init; } = string.Empty;

    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreatedAt = DateTime.UtcNow;
        EventType = GetType().FullName ?? GetType().Name;
    }

    [JsonConstructor]
    protected IntegrationEvent(Guid id, DateTime createdAt, string eventType)
    {
        Id = id;
        CreatedAt = createdAt;
        EventType = eventType;
    }

    public override string ToString()
    {
        return $"[{EventType}] Id={Id}, TenantId={TenantId}, CreatedAt={CreatedAt:O}";
    }
}

/// <summary>
/// Integration event published when a reservation is confirmed.
/// </summary>
public class ReservationConfirmedIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public string GuestName { get; init; } = string.Empty;
    public DateTime CheckInDate { get; init; }
    public DateTime CheckOutDate { get; init; }
    public decimal TotalAmount { get; init; }
    public string Currency { get; init; } = "THB";
}

/// <summary>
/// Integration event published when a reservation is cancelled.
/// </summary>
public class ReservationCancelledIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public string Reason { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public string Currency { get; init; } = "THB";
}

/// <summary>
/// Integration event published when a payment is completed.
/// </summary>
public class PaymentCompletedIntegrationEvent : IntegrationEvent
{
    public Guid PaymentId { get; init; }
    public Guid ReservationId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "THB";
    public string PaymentMethod { get; init; } = string.Empty;
    public string TransactionReference { get; init; } = string.Empty;
}

/// <summary>
/// Integration event published when a guest checks in.
/// </summary>
public class GuestCheckedInIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public string GuestName { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public DateTime CheckInTime { get; init; }
}

/// <summary>
/// Integration event published when a guest checks out.
/// </summary>
public class GuestCheckedOutIntegrationEvent : IntegrationEvent
{
    public Guid ReservationId { get; init; }
    public string GuestName { get; init; } = string.Empty;
    public string RoomNumber { get; init; } = string.Empty;
    public DateTime CheckOutTime { get; init; }
    public decimal FinalBillAmount { get; init; }
    public string Currency { get; init; } = "THB";
}
