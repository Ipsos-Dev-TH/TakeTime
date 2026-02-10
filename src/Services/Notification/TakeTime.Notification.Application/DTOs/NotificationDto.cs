namespace TakeTime.Notification.Application.DTOs;

/// <summary>
/// Data transfer object for sending a notification through a specific channel.
/// </summary>
public sealed class SendNotificationDto
{
    /// <summary>
    /// The delivery channel: Email, SMS, LINE, Telegram.
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>
    /// Recipient address (email, phone number, LINE user ID, Telegram chat ID).
    /// </summary>
    public string Recipient { get; set; } = string.Empty;

    /// <summary>
    /// Notification subject (primarily for email; optional for other channels).
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Notification body content. If a TemplateId is provided, this is ignored
    /// in favor of the rendered template.
    /// </summary>
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Optional template identifier for rendering the notification body.
    /// </summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Key-value placeholders to substitute into the template body.
    /// </summary>
    public Dictionary<string, string> Placeholders { get; set; } = new();
}

/// <summary>
/// Data transfer object representing a logged notification delivery attempt.
/// </summary>
public sealed class NotificationLogDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Guid? TemplateId { get; set; }
    public int RetryCount { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Data transfer object for a reusable notification template.
/// </summary>
public sealed class NotificationTemplateDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// DTO for creating or updating a notification template.
/// </summary>
public sealed class CreateNotificationTemplateDto
{
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// DTO for bulk notification sending.
/// </summary>
public sealed class SendBulkNotificationDto
{
    public string Channel { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public Dictionary<string, string> Placeholders { get; set; } = new();
}

/// <summary>
/// Result of a bulk notification operation.
/// </summary>
public sealed class BulkNotificationResultDto
{
    public int TotalRecipients { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public List<NotificationLogDto> Results { get; set; } = [];
}

/// <summary>
/// Search criteria for querying notification logs.
/// </summary>
public sealed class NotificationLogSearchCriteria
{
    public string? Channel { get; set; }
    public string? Status { get; set; }
    public string? Recipient { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
