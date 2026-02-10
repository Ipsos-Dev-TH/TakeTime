namespace TakeTime.Notification.Application.Interfaces;

/// <summary>
/// Interface for a notification delivery provider. Each implementation handles
/// a specific channel (Email, SMS, LINE, Telegram) and knows how to deliver
/// messages through that channel using tenant-specific configuration.
/// </summary>
public interface INotificationProvider
{
    /// <summary>
    /// The notification channel this provider handles (e.g., "Email", "SMS", "LINE", "Telegram").
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Sends a notification through the provider's channel.
    /// </summary>
    /// <param name="recipient">The recipient address or identifier.</param>
    /// <param name="subject">The notification subject (may be null for non-email channels).</param>
    /// <param name="body">The notification body content.</param>
    /// <param name="tenantSettings">Tenant-specific settings for the channel (SMTP config, API keys, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result indicating success or failure with optional error details.</returns>
    Task<NotificationSendResult> SendAsync(
        string recipient,
        string? subject,
        string body,
        IDictionary<string, string> tenantSettings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a notification send operation.
/// </summary>
public sealed class NotificationSendResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public static NotificationSendResult Succeeded(string? externalReference = null) =>
        new() { Success = true, ExternalReference = externalReference };

    public static NotificationSendResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

/// <summary>
/// Interface for the notification log repository.
/// </summary>
public interface INotificationLogRepository
{
    Task<Guid> LogAsync(NotificationLogEntry entry, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid logId, string status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<NotificationLogEntry> Items, int TotalCount)> SearchAsync(
        string tenantId, string? channel, string? status, string? recipient,
        DateTime? dateFrom, DateTime? dateTo,
        int page, int pageSize,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for the notification template repository.
/// </summary>
public interface INotificationTemplateRepository
{
    Task<NotificationTemplateEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NotificationTemplateEntry>> GetAllByTenantAsync(string tenantId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(NotificationTemplateEntry template, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationTemplateEntry template, CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal log entry model used by the application layer.
/// </summary>
public sealed class NotificationLogEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? ErrorMessage { get; set; }
    public Guid? TemplateId { get; set; }
    public int RetryCount { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Internal template entry model used by the application layer.
/// </summary>
public sealed class NotificationTemplateEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
