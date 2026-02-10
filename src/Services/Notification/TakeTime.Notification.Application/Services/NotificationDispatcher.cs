using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Application.Services;

/// <summary>
/// Routes notification requests to the correct <see cref="INotificationProvider"/>
/// based on the requested channel. Validates that the channel is enabled for the
/// tenant before dispatching.
/// </summary>
public sealed class NotificationDispatcher
{
    private readonly IEnumerable<INotificationProvider> _providers;
    private readonly ILogger<NotificationDispatcher> _logger;

    /// <summary>
    /// Mapping of tenant setting keys that control whether a channel is enabled.
    /// </summary>
    private static readonly Dictionary<string, string> ChannelEnabledSettingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Email"] = "Notification:EmailEnabled",
        ["SMS"] = "Notification:SmsEnabled",
        ["LINE"] = "Notification:LineEnabled",
        ["Telegram"] = "Notification:TelegramEnabled"
    };

    public NotificationDispatcher(
        IEnumerable<INotificationProvider> providers,
        ILogger<NotificationDispatcher> logger)
    {
        _providers = providers;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a notification to the appropriate provider based on the channel.
    /// </summary>
    /// <param name="channel">The notification channel (Email, SMS, LINE, Telegram).</param>
    /// <param name="recipient">The recipient address or identifier.</param>
    /// <param name="subject">The notification subject (nullable).</param>
    /// <param name="body">The notification body content.</param>
    /// <param name="tenantSettings">Tenant-specific settings for channel configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the send operation.</returns>
    public async Task<NotificationSendResult> DispatchAsync(
        string channel,
        string recipient,
        string? subject,
        string body,
        IDictionary<string, string> tenantSettings,
        CancellationToken cancellationToken = default)
    {
        // Check if channel is enabled for this tenant
        if (!IsChannelEnabled(channel, tenantSettings))
        {
            _logger.LogWarning(
                "Channel {Channel} is disabled for the current tenant. Notification not sent to {Recipient}",
                channel, recipient);

            return NotificationSendResult.Failed($"Channel '{channel}' is not enabled for this tenant.");
        }

        // Find the appropriate provider
        var provider = _providers.FirstOrDefault(p =>
            p.Channel.Equals(channel, StringComparison.OrdinalIgnoreCase));

        if (provider is null)
        {
            _logger.LogError("No notification provider registered for channel {Channel}", channel);
            return NotificationSendResult.Failed($"No provider available for channel '{channel}'.");
        }

        _logger.LogDebug(
            "Dispatching {Channel} notification to {Recipient} via {ProviderType}",
            channel, recipient, provider.GetType().Name);

        try
        {
            return await provider.SendAsync(recipient, subject, body, tenantSettings, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception from {Channel} provider sending to {Recipient}",
                channel, recipient);

            return NotificationSendResult.Failed($"Provider error: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks whether the specified channel is enabled in the tenant settings.
    /// Defaults to true if no explicit setting is found (opt-out model).
    /// </summary>
    private static bool IsChannelEnabled(string channel, IDictionary<string, string> tenantSettings)
    {
        if (!ChannelEnabledSettingKeys.TryGetValue(channel, out var settingKey))
        {
            // Unknown channel - allow by default so custom providers work
            return true;
        }

        if (tenantSettings.TryGetValue(settingKey, out var value))
        {
            return bool.TryParse(value, out var enabled) && enabled;
        }

        // Default: channel is enabled if no explicit setting exists
        return true;
    }
}
