using TakeTime.Core.Domain.Base;
using TakeTime.Notification.Domain.Enums;

namespace TakeTime.Notification.Domain.Entities;

/// <summary>
/// Tracks all notification delivery attempts for audit and retry purposes.
/// </summary>
public class NotificationLog : Entity
{
    /// <summary>
    /// Communication channel used for delivery.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Recipient address (email, phone number, LINE user ID, etc.).
    /// </summary>
    public string Recipient { get; private set; } = string.Empty;

    /// <summary>
    /// Subject line of the notification (if applicable).
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>
    /// Body content of the notification.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Delivery status.
    /// </summary>
    public string Status { get; private set; } = "Queued";

    /// <summary>
    /// Timestamp when the notification was sent.
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Error message if delivery failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Number of delivery retry attempts.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    /// Type of the related entity (e.g., "Reservation", "Payment").
    /// </summary>
    public string? RelatedEntityType { get; private set; }

    /// <summary>
    /// Identifier of the related entity.
    /// </summary>
    public Guid? RelatedEntityId { get; private set; }

    /// <summary>
    /// Maximum retry attempts before marking as permanently failed.
    /// </summary>
    public const int MaxRetries = 3;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private NotificationLog() { }

    /// <summary>
    /// Creates a new notification log entry.
    /// </summary>
    public NotificationLog(
        NotificationChannel channel,
        string recipient,
        string body,
        string? subject = null,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null)
    {
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Recipient is required.", nameof(recipient));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Channel = channel;
        Recipient = recipient;
        Body = body;
        Subject = subject;
        RelatedEntityType = relatedEntityType;
        RelatedEntityId = relatedEntityId;
        Status = "Queued";
    }

    /// <summary>
    /// Marks the notification as successfully sent.
    /// </summary>
    public void MarkAsSent()
    {
        Status = "Sent";
        SentAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the notification as failed with an error message.
    /// </summary>
    public void MarkAsFailed(string errorMessage)
    {
        RetryCount++;
        ErrorMessage = errorMessage;

        Status = RetryCount >= MaxRetries ? "Failed" : "Queued";
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Whether the notification can be retried.
    /// </summary>
    public bool CanRetry => RetryCount < MaxRetries && Status == "Queued";
}
