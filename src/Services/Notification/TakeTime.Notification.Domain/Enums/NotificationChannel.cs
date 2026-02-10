namespace TakeTime.Notification.Domain.Enums;

/// <summary>
/// Communication channels available for sending notifications.
/// </summary>
public enum NotificationChannel
{
    Email = 0,
    SMS = 1,
    LINE = 2,
    Telegram = 3,
    Push = 4,
    InApp = 5
}
