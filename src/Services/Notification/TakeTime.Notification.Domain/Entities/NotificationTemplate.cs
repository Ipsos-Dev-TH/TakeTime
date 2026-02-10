using TakeTime.Core.Domain.Base;
using TakeTime.Notification.Domain.Enums;

namespace TakeTime.Notification.Domain.Entities;

/// <summary>
/// Represents a reusable notification template for a specific channel.
/// Supports placeholder substitution in subject and body.
/// </summary>
public class NotificationTemplate : Entity
{
    /// <summary>
    /// Unique template name for identification (e.g., "reservation_confirmation").
    /// </summary>
    public string TemplateName { get; private set; } = string.Empty;

    /// <summary>
    /// Communication channel this template is for.
    /// </summary>
    public NotificationChannel Channel { get; private set; }

    /// <summary>
    /// Subject line (applicable for Email; optional for other channels).
    /// May contain placeholders like {{CustomerName}}.
    /// </summary>
    public string? Subject { get; private set; }

    /// <summary>
    /// Body content of the notification.
    /// Supports placeholders like {{ReservationNumber}}, {{CheckInDate}}, etc.
    /// </summary>
    public string Body { get; private set; } = string.Empty;

    /// <summary>
    /// Whether this template is currently active and available for use.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// Language code for the template (e.g., "th", "en").
    /// </summary>
    public string Language { get; private set; } = "th";

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private NotificationTemplate() { }

    /// <summary>
    /// Creates a new notification template.
    /// </summary>
    public NotificationTemplate(
        string templateName,
        NotificationChannel channel,
        string body,
        string? subject = null,
        string language = "th")
    {
        if (string.IsNullOrWhiteSpace(templateName))
            throw new ArgumentException("Template name is required.", nameof(templateName));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        TemplateName = templateName;
        Channel = channel;
        Body = body;
        Subject = subject;
        Language = language;
    }

    /// <summary>
    /// Updates the template content.
    /// </summary>
    public void UpdateContent(string body, string? subject = null)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body is required.", nameof(body));

        Body = body;
        Subject = subject;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Renders the template body by replacing placeholders with actual values.
    /// </summary>
    public string RenderBody(Dictionary<string, string> placeholders)
    {
        var rendered = Body;
        foreach (var placeholder in placeholders)
        {
            rendered = rendered.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
        }
        return rendered;
    }

    /// <summary>
    /// Renders the template subject by replacing placeholders with actual values.
    /// </summary>
    public string? RenderSubject(Dictionary<string, string> placeholders)
    {
        if (Subject is null) return null;

        var rendered = Subject;
        foreach (var placeholder in placeholders)
        {
            rendered = rendered.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value);
        }
        return rendered;
    }

    /// <summary>
    /// Activates the template.
    /// </summary>
    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the template.
    /// </summary>
    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
