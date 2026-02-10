using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.DTOs;
using TakeTime.Notification.Application.Interfaces;
using TakeTime.Notification.Application.Services;

namespace TakeTime.Notification.Application.Commands;

/// <summary>
/// Command to send a notification to multiple recipients in bulk.
/// Each recipient is processed individually and the results are aggregated.
/// </summary>
public sealed class SendBulkNotificationCommand : IRequest<BulkNotificationResultDto>
{
    public string Channel { get; set; } = string.Empty;
    public List<string> Recipients { get; set; } = [];
    public string? Subject { get; set; }
    public string Body { get; set; } = string.Empty;
    public Guid? TemplateId { get; set; }
    public Dictionary<string, string> Placeholders { get; set; } = new();
}

/// <summary>
/// Handler for <see cref="SendBulkNotificationCommand"/>. Iterates over all recipients,
/// resolves the template if provided, dispatches each notification through the correct
/// channel provider, logs each result, and returns an aggregated summary.
/// </summary>
public sealed class SendBulkNotificationCommandHandler : IRequestHandler<SendBulkNotificationCommand, BulkNotificationResultDto>
{
    private readonly NotificationDispatcher _dispatcher;
    private readonly INotificationLogRepository _logRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<SendBulkNotificationCommandHandler> _logger;

    public SendBulkNotificationCommandHandler(
        NotificationDispatcher dispatcher,
        INotificationLogRepository logRepository,
        INotificationTemplateRepository templateRepository,
        ICurrentTenantService currentTenantService,
        ILogger<SendBulkNotificationCommandHandler> logger)
    {
        _dispatcher = dispatcher;
        _logRepository = logRepository;
        _templateRepository = templateRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<BulkNotificationResultDto> Handle(SendBulkNotificationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required to send notifications.");

        var tenantSettings = _currentTenantService.TenantSettings;

        _logger.LogInformation(
            "Starting bulk {Channel} notification to {Count} recipients for tenant {TenantId}",
            request.Channel, request.Recipients.Count, tenantId);

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
                    "Template {TemplateId} not found or inactive for bulk send, using inline body",
                    request.TemplateId.Value);
            }
        }

        var result = new BulkNotificationResultDto
        {
            TotalRecipients = request.Recipients.Count
        };

        foreach (var recipient in request.Recipients)
        {
            var logEntry = new NotificationLogEntry
            {
                TenantId = tenantId,
                Channel = request.Channel,
                Recipient = recipient,
                Subject = subject,
                Body = body,
                TemplateId = request.TemplateId,
                Status = "Sending"
            };

            var logId = await _logRepository.LogAsync(logEntry, cancellationToken);

            try
            {
                var sendResult = await _dispatcher.DispatchAsync(
                    request.Channel, recipient, subject, body, tenantSettings, cancellationToken);

                var finalStatus = sendResult.Success ? "Sent" : "Failed";
                await _logRepository.UpdateStatusAsync(logId, finalStatus, sendResult.ErrorMessage, cancellationToken);

                if (sendResult.Success)
                {
                    result.SuccessCount++;
                }
                else
                {
                    result.FailureCount++;
                }

                result.Results.Add(new NotificationLogDto
                {
                    Id = logId,
                    TenantId = tenantId,
                    Channel = request.Channel,
                    Recipient = recipient,
                    Subject = subject,
                    Body = body,
                    Status = finalStatus,
                    ErrorMessage = sendResult.ErrorMessage,
                    TemplateId = request.TemplateId,
                    RetryCount = 0,
                    SentAt = sendResult.SentAt,
                    CreatedAt = logEntry.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to {Recipient} via {Channel}", recipient, request.Channel);

                await _logRepository.UpdateStatusAsync(logId, "Failed", ex.Message, cancellationToken);
                result.FailureCount++;

                result.Results.Add(new NotificationLogDto
                {
                    Id = logId,
                    TenantId = tenantId,
                    Channel = request.Channel,
                    Recipient = recipient,
                    Subject = subject,
                    Body = body,
                    Status = "Failed",
                    ErrorMessage = ex.Message,
                    TemplateId = request.TemplateId,
                    RetryCount = 0,
                    SentAt = DateTime.UtcNow,
                    CreatedAt = logEntry.CreatedAt
                });
            }
        }

        _logger.LogInformation(
            "Bulk notification complete: {Success}/{Total} succeeded for tenant {TenantId}",
            result.SuccessCount, result.TotalRecipients, tenantId);

        return result;
    }
}
