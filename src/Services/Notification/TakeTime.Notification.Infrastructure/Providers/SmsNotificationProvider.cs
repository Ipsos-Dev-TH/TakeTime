using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Infrastructure.Providers;

/// <summary>
/// Notification provider for SMS delivery. This is a placeholder implementation
/// that supports a generic HTTP-based SMS gateway API. Tenant-specific SMS settings:
///   - Notification:Sms:ApiUrl
///   - Notification:Sms:ApiKey
///   - Notification:Sms:SenderId
///   - Notification:Sms:Provider (for future multi-provider support)
///
/// The recipient should be a phone number in E.164 format (e.g., +66812345678).
///
/// To integrate with a specific SMS provider (e.g., Twilio, ThaiBulkSMS, AWS SNS),
/// replace the HTTP call in SendAsync with the provider-specific SDK or API call.
/// </summary>
public sealed class SmsNotificationProvider : INotificationProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsNotificationProvider> _logger;

    public string Channel => "SMS";

    public SmsNotificationProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<SmsNotificationProvider> logger)
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
            var apiUrl = GetSettingOrDefault(tenantSettings, "Notification:Sms:ApiUrl", string.Empty);
            var apiKey = GetSettingOrDefault(tenantSettings, "Notification:Sms:ApiKey", string.Empty);
            var senderId = GetSettingOrDefault(tenantSettings, "Notification:Sms:SenderId", "TakeTime");

            // Validate that SMS is properly configured
            if (string.IsNullOrEmpty(apiUrl) || string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning(
                    "SMS provider is not fully configured for tenant. ApiUrl and ApiKey are required.");
                return NotificationSendResult.Failed(
                    "SMS provider is not configured. Please set Notification:Sms:ApiUrl and Notification:Sms:ApiKey in tenant settings.");
            }

            // Truncate SMS body to 160 characters for standard SMS, or 1600 for long SMS
            var smsBody = body.Length > 1600 ? body[..1597] + "..." : body;

            var payload = new
            {
                to = recipient,
                from = senderId,
                message = smsBody,
                api_key = apiKey
            };

            var client = _httpClientFactory.CreateClient("SMS");

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            var response = await client.PostAsync(apiUrl, jsonContent, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("SMS sent to {Recipient}, response: {Response}", recipient, responseBody);
                return NotificationSendResult.Succeeded(responseBody);
            }

            _logger.LogWarning(
                "SMS API returned {StatusCode} for {Recipient}: {Error}",
                response.StatusCode, recipient, responseBody);

            return NotificationSendResult.Failed($"SMS API error ({response.StatusCode}): {responseBody}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send SMS to {Recipient}", recipient);
            return NotificationSendResult.Failed($"SMS error: {ex.Message}");
        }
    }

    private static string GetSettingOrDefault(IDictionary<string, string> settings, string key, string defaultValue)
    {
        return settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
    }
}
