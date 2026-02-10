using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace TakeTime.Infrastructure.ExternalServices;

/// <summary>
/// Configuration options for the SMTP email service.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "TakeTime";
    public bool EnableSsl { get; set; } = true;
    public int TimeoutMs { get; set; } = 30000;
}

/// <summary>
/// Interface for email sending operations.
/// </summary>
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default);
    Task SendEmailAsync(string to, string subject, string htmlBody, string? textBody = null, IEnumerable<EmailAttachment>? attachments = null, CancellationToken cancellationToken = default);
    Task SendTemplatedEmailAsync(string to, string subject, string templateName, IDictionary<string, string> templateData, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an email attachment.
/// </summary>
public class EmailAttachment
{
    public string FileName { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = "application/octet-stream";
}

/// <summary>
/// SMTP-based email service implementation using MailKit.
/// Supports HTML templates, plain text fallback, and attachments.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Built-in email templates for common notifications.
    /// In production, these would typically be loaded from a template store or file system.
    /// </summary>
    private static readonly Dictionary<string, string> Templates = new()
    {
        ["reservation-confirmation"] = """
            <html>
            <body style="font-family: Arial, sans-serif; color: #333;">
                <h2>Reservation Confirmation</h2>
                <p>Dear {{GuestName}},</p>
                <p>Your reservation has been confirmed.</p>
                <table style="border-collapse: collapse; width: 100%; max-width: 500px;">
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Confirmation #</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{ConfirmationNumber}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Check-in</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{CheckInDate}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Check-out</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{CheckOutDate}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Room Type</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{RoomType}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Total</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{TotalAmount}}</td></tr>
                </table>
                <p>Thank you for choosing {{PropertyName}}!</p>
            </body>
            </html>
            """,

        ["payment-receipt"] = """
            <html>
            <body style="font-family: Arial, sans-serif; color: #333;">
                <h2>Payment Receipt</h2>
                <p>Dear {{GuestName}},</p>
                <p>We have received your payment.</p>
                <table style="border-collapse: collapse; width: 100%; max-width: 500px;">
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Receipt #</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{ReceiptNumber}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Amount</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{Amount}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Payment Method</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{PaymentMethod}}</td></tr>
                    <tr><td style="padding: 8px; border: 1px solid #ddd;"><strong>Date</strong></td><td style="padding: 8px; border: 1px solid #ddd;">{{PaymentDate}}</td></tr>
                </table>
                <p>Thank you!</p>
            </body>
            </html>
            """,

        ["cancellation"] = """
            <html>
            <body style="font-family: Arial, sans-serif; color: #333;">
                <h2>Reservation Cancellation</h2>
                <p>Dear {{GuestName}},</p>
                <p>Your reservation ({{ConfirmationNumber}}) has been cancelled.</p>
                <p>Cancellation reason: {{Reason}}</p>
                <p>Refund amount: {{RefundAmount}}</p>
                <p>If you have any questions, please contact us.</p>
            </body>
            </html>
            """
    };

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        await SendEmailAsync(to, subject, htmlBody, null, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendEmailAsync(
        string to,
        string subject,
        string htmlBody,
        string? textBody = null,
        IEnumerable<EmailAttachment>? attachments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(to);
        ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(htmlBody);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;

        var builder = new BodyBuilder
        {
            HtmlBody = htmlBody,
            TextBody = textBody ?? StripHtml(htmlBody)
        };

        if (attachments is not null)
        {
            foreach (var attachment in attachments)
            {
                builder.Attachments.Add(attachment.FileName, attachment.Content,
                    ContentType.Parse(attachment.ContentType));
            }
        }

        message.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            client.Timeout = _settings.TimeoutMs;

            var secureSocketOptions = _settings.EnableSsl
                ? SecureSocketOptions.StartTls
                : SecureSocketOptions.None;

            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_settings.SmtpUsername))
            {
                await client.AuthenticateAsync(_settings.SmtpUsername, _settings.SmtpPassword, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email sent successfully to {Recipient} with subject '{Subject}'", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Recipient} with subject '{Subject}'", to, subject);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SendTemplatedEmailAsync(
        string to,
        string subject,
        string templateName,
        IDictionary<string, string> templateData,
        CancellationToken cancellationToken = default)
    {
        if (!Templates.TryGetValue(templateName, out var template))
        {
            throw new ArgumentException($"Email template '{templateName}' not found.", nameof(templateName));
        }

        var htmlBody = ReplaceTemplatePlaceholders(template, templateData);
        await SendEmailAsync(to, subject, htmlBody, cancellationToken: cancellationToken);
    }

    private static string ReplaceTemplatePlaceholders(string template, IDictionary<string, string> data)
    {
        var result = template;
        foreach (var (key, value) in data)
        {
            result = result.Replace($"{{{{{key}}}}}", value);
        }
        return result;
    }

    private static string StripHtml(string html)
    {
        // Simple HTML stripping for plain-text fallback
        var result = System.Text.RegularExpressions.Regex.Replace(html, "<[^>]*>", " ");
        result = System.Text.RegularExpressions.Regex.Replace(result, @"\s+", " ");
        return result.Trim();
    }
}
