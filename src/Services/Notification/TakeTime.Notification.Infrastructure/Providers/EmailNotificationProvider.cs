using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Infrastructure.Providers;

/// <summary>
/// Notification provider that delivers messages via SMTP email using MailKit.
/// Tenant-specific SMTP settings are read from the tenant settings dictionary:
///   - Notification:Smtp:Host
///   - Notification:Smtp:Port
///   - Notification:Smtp:Username
///   - Notification:Smtp:Password
///   - Notification:Smtp:FromEmail
///   - Notification:Smtp:FromName
///   - Notification:Smtp:UseSsl
/// </summary>
public sealed class EmailNotificationProvider : INotificationProvider
{
    private readonly ILogger<EmailNotificationProvider> _logger;

    public string Channel => "Email";

    public EmailNotificationProvider(ILogger<EmailNotificationProvider> logger)
    {
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
            var host = GetRequiredSetting(tenantSettings, "Notification:Smtp:Host");
            var port = int.Parse(GetSettingOrDefault(tenantSettings, "Notification:Smtp:Port", "587"));
            var username = GetSettingOrDefault(tenantSettings, "Notification:Smtp:Username", string.Empty);
            var password = GetSettingOrDefault(tenantSettings, "Notification:Smtp:Password", string.Empty);
            var fromEmail = GetRequiredSetting(tenantSettings, "Notification:Smtp:FromEmail");
            var fromName = GetSettingOrDefault(tenantSettings, "Notification:Smtp:FromName", "TakeTime Notifications");
            var useSsl = bool.Parse(GetSettingOrDefault(tenantSettings, "Notification:Smtp:UseSsl", "true"));

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(fromName, fromEmail));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject ?? "Notification";

            var bodyBuilder = new BodyBuilder();

            // Detect HTML content
            if (body.Contains('<') && body.Contains('>'))
            {
                bodyBuilder.HtmlBody = body;
            }
            else
            {
                bodyBuilder.TextBody = body;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            var secureSocketOptions = useSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(host, port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }

            var response = await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogDebug("Email sent to {Recipient} via {Host}:{Port}, response: {Response}",
                recipient, host, port, response);

            return NotificationSendResult.Succeeded(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
            return NotificationSendResult.Failed($"SMTP error: {ex.Message}");
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
