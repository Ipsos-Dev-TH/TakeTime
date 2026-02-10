using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Infrastructure.Providers;

/// <summary>
/// Notification provider that delivers messages via LINE Messaging API.
/// Tenant-specific LINE settings are read from the tenant settings dictionary:
///   - Notification:Line:ChannelAccessToken
///   - Notification:Line:ApiBaseUrl (defaults to https://api.line.me/v2/bot)
///
/// The recipient should be a LINE user ID for push messages.
/// </summary>
public sealed class LineNotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LineNotificationProvider> _logger;

    private const string DefaultApiBaseUrl = "https://api.line.me/v2/bot";

    public string Channel => "LINE";

    public LineNotificationProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<LineNotificationProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<NotificationSendResult> SendAsync(
        string recipient,
        string? subject,
        string body,
        IDictionary<string, string> tenantSettings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = GetRequiredSetting(tenantSettings, "Notification:Line:ChannelAccessToken");
            var apiBaseUrl = GetSettingOrDefault(tenantSettings, "Notification:Line:ApiBaseUrl", DefaultApiBaseUrl);

            // Combine subject and body for LINE messages
            var messageText = string.IsNullOrEmpty(subject) ? body : $"{subject}\n\n{body}";

            // Truncate to LINE's 5000 character limit
            if (messageText.Length > 5000)
            {
                messageText = messageText[..4997] + "...";
            }

            var payload = new
            {
                to = recipient,
                messages = new[]
                {
                    new
                    {
                        type = "text",
                        text = messageText
                    }
                }
            };

            var client = _httpClientFactory.CreateClient("LINE");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                $"{apiBaseUrl}/message/push",
                jsonContent,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("LINE message sent to {Recipient}, response: {Response}", recipient, responseBody);
                return NotificationSendResult.Succeeded(responseBody);
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "LINE API returned {StatusCode} for recipient {Recipient}: {Error}",
                response.StatusCode, recipient, errorBody);

            return NotificationSendResult.Failed($"LINE API error ({response.StatusCode}): {errorBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send LINE notification to {Recipient}", recipient);
            return NotificationSendResult.Failed($"LINE error: {ex.Message}");
        }
    }

    private static string GetRequiredSetting(IDictionary<string, string> settings, string key)
    {
        if (settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        throw new InvalidOperationException($"Required notification setting '{key}' is not configured for this tenant.");
    }

    private static string GetSettingOrDefault(IDictionary<string, string> settings, string key, string defaultValue)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }
}
