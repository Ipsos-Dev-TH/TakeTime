using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.DTOs;
using TakeTime.Notification.Application.Interfaces;
using TakeTime.Notification.Application.Services;

namespace TakeTime.Notification.Application.Commands;

/// <summary>
/// Command to send a single notification through the appropriate channel.
/// Resolves tenant notification settings, selects the correct provider,
/// delivers the message, and logs the result.
/// </summary>
public sealed class SendNotificationCommand : IRequest<NotificationLogDto>
{
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public Dictionary<string, string> Placeholders { get; set; } = new();
}

/// <summary>
/// Handler for <see cref="SendNotificationCommand"/>. Resolves tenant notification
/// settings, picks the right channel provider via <see cref="NotificationDispatcher"/>,
/// sends the notification, and logs the result.
/// </summary>
public sealed class SendNotificationCommandHandler : IRequestHandler<SendNotificationCommand, NotificationLogDto>
{
    private readonly NotificationDispatcher _dispatcher;
    private readonly INotificationLogRepository _logRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<SendNotificationCommandHandler> _logger;

    public SendNotificationCommandHandler(
        NotificationDispatcher dispatcher,
        INotificationLogRepository logRepository,
        INotificationTemplateRepository templateRepository,
        ICurrentTenantService currentTenantService,
        ILogger<SendNotificationCommandHandler> logger)
    {
        _dispatcher = dispatcher;
        _logRepository = logRepository;
        _templateRepository = templateRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<NotificationLogDto> Handle(SendNotificationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required to send notifications.");

        var tenantSettings = _currentTenantService.TenantSettings;

        _logger.LogInformation(
            "Sending {Channel} notification to {Recipient} for tenant {TenantId}",
            request.Channel, request.Recipient, tenantId);

        // Resolve template if provided
        var subject = request.Subject;
        var body = request.Body;

        if (request.TemplateId.HasValue)
        {
            var template = await _templateRepository.GetByIdAsync(request.TemplateId.Value, cancellationToken);
            if (template is not null && template.IsActive)
            {
                subject = template.Subject;
                body = template.Body;

                // Apply placeholder substitutions
                foreach (var placeholder in request.Placeholders)
                {
                    body = body.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value, StringComparison.OrdinalIgnoreCase);
                    if (subject is not null)
                    {
                        subject = subject.Replace($"{{{{{placeholder.Key}}}}}", placeholder.Value, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Template {TemplateId} not found or inactive, falling back to inline body",
                    request.TemplateId.Value);
            }
        }

        // Create the log entry
        var logEntry = new NotificationLogEntry
        {
            TenantId = tenantId,
            Channel = request.Channel,
            Recipient = request.Recipient,
            Subject = subject,
            Body = body,
            TemplateId = request.TemplateId,
            Status = "Sending"
        };

        var logId = await _logRepository.LogAsync(logEntry, cancellationToken);

        // Dispatch the notification through the appropriate provider
        var result = await _dispatcher.DispatchAsync(
            request.Channel, request.Recipient, subject, body, tenantSettings, cancellationToken);

        // Update the log with the result
        var finalStatus = result.Success ? "Sent" : "Failed";
        await _logRepository.UpdateStatusAsync(logId, finalStatus, result.ErrorMessage, cancellationToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Notification sent successfully via {Channel} to {Recipient}",
                request.Channel, request.Recipient);
        }
        else
        {
            _logger.LogWarning(
                "Notification failed via {Channel} to {Recipient}: {Error}",
                request.Channel, request.Recipient, result.ErrorMessage);
        }

        return new NotificationLogDto
        {
            Id = logId,
            TenantId = tenantId,
            Channel = request.Channel,
            Recipient = request.Recipient,
            Subject = subject,
            Body = body,
            Status = finalStatus,
            ErrorMessage = result.ErrorMessage,
            TemplateId = request.TemplateId,
            RetryCount = 0,
            SentAt = result.SentAt,
            CreatedAt = logEntry.CreatedAt
        };
    }
}
