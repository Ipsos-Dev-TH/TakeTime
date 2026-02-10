using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Infrastructure.Providers;

/// <summary>
/// Notification provider that delivers messages via the Telegram Bot API.
/// Tenant-specific Telegram settings are read from the tenant settings dictionary:
///   - Notification:Telegram:BotToken
///   - Notification:Telegram:ParseMode (defaults to "HTML")
///
/// The recipient should be a Telegram chat ID.
/// </summary>
public sealed class TelegramNotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TelegramNotificationProvider> _logger;

    private const string TelegramApiBase = "https://api.telegram.org";

    public string Channel => "Telegram";

    public TelegramNotificationProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<TelegramNotificationProvider> logger)
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
            var botToken = GetRequiredSetting(tenantSettings, "Notification:Telegram:BotToken");
            var parseMode = GetSettingOrDefault(tenantSettings, "Notification:Telegram:ParseMode", "HTML");

            // Combine subject and body for Telegram messages
            var messageText = string.IsNullOrEmpty(subject)
                ? body
                : parseMode.Equals("HTML", StringComparison.OrdinalIgnoreCase)
                    ? $"<b>{EscapeHtml(subject)}</b>\n\n{body}"
                    : $"*{EscapeMarkdown(subject)}*\n\n{body}";

            // Truncate to Telegram's 4096 character limit
            if (messageText.Length > 4096)
            {
                messageText = messageText[..4093] + "...";
            }

            var payload = new
            {
                chat_id = recipient,
                text = messageText,
                parse_mode = parseMode
            };

            var client = _httpClientFactory.CreateClient("Telegram");

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(
                $"{TelegramApiBase}/bot{botToken}/sendMessage",
                jsonContent,
                cancellationToken);

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Telegram message sent to chat {ChatId}, response: {Response}", recipient, responseBody);

                // Extract message_id from response for reference
                using var doc = JsonDocument.Parse(responseBody);
                var messageId = doc.RootElement
                    .GetProperty("result")
                    .GetProperty("message_id")
                    .GetInt64()
                    .ToString();

                return NotificationSendResult.Succeeded(messageId);
            }

            _logger.LogWarning(
                "Telegram API returned {StatusCode} for chat {ChatId}: {Error}",
                response.StatusCode, recipient, responseBody);

            return NotificationSendResult.Failed($"Telegram API error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram notification to {Recipient}", recipient);
            return NotificationSendResult.Failed($"Telegram error: {ex.Message}");
        }
    }

    private static string EscapeHtml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }

    private static string EscapeMarkdown(string text)
    {
        var specialChars = new[] { '_', '*', '[', ']', '(', ')', '~', '`', '>', '#', '+', '-', '=', '|', '{', '}', '.', '!' };
        var sb = new StringBuilder(text.Length * 2);
        foreach (var c in text)
        {
            if (Array.IndexOf(specialChars, c) >= 0)
                sb.Append('\\');
            sb.Append(c);
        }
        return sb.ToString();
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
