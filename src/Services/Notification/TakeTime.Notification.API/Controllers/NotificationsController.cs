using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Application.DTOs;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.API.Controllers;

/// <summary>
/// API controller for managing notifications, including sending single and bulk
/// notifications, querying notification logs, and managing notification templates.
/// </summary>
[ApiController]
[Route("api/v1/notifications")]
[Authorize]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly INotificationLogRepository _logRepository;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly ICurrentTenantService _currentTenantService;

    public NotificationsController(
        IMediator mediator,
        INotificationLogRepository logRepository,
        INotificationTemplateRepository templateRepository,
        ICurrentTenantService currentTenantService)
    {
        _mediator = mediator;
        _logRepository = logRepository;
        _templateRepository = templateRepository;
        _currentTenantService = currentTenantService;
    }

    /// <summary>
    /// Sends a single notification through the specified channel.
    /// </summary>
    /// <param name="dto">The notification details including channel, recipient, subject, and body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification log entry for the sent notification.</returns>
    /// <response code="200">Notification processed successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("send")]
    [ProducesResponseType(typeof(NotificationLogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationLogDto>> Send(
        [FromBody] SendNotificationDto dto,
        CancellationToken cancellationToken)
    {
        var command = new SendNotificationCommand
        {
            Channel = dto.Channel,
            Recipient = dto.Recipient,
            Subject = dto.Subject,
            Body = dto.Body,
            TemplateId = dto.TemplateId,
            Placeholders = dto.Placeholders
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Sends a notification to multiple recipients in bulk.
    /// </summary>
    /// <param name="dto">The bulk notification details including channel, recipients list, and content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An aggregated result showing success and failure counts.</returns>
    /// <response code="200">Bulk notification processed successfully.</response>
    /// <response code="400">Invalid request parameters.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(BulkNotificationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BulkNotificationResultDto>> SendBulk(
        [FromBody] SendBulkNotificationDto dto,
        CancellationToken cancellationToken)
    {
        var command = new SendBulkNotificationCommand
        {
            Channel = dto.Channel,
            Recipients = dto.Recipients,
            Subject = dto.Subject,
            Body = dto.Body,
            TemplateId = dto.TemplateId,
            Placeholders = dto.Placeholders
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Retrieves notification delivery logs with optional filtering.
    /// </summary>
    /// <param name="channel">Filter by notification channel.</param>
    /// <param name="status">Filter by delivery status (Sent, Failed, Pending).</param>
    /// <param name="recipient">Filter by recipient address.</param>
    /// <param name="dateFrom">Filter by start date.</param>
    /// <param name="dateTo">Filter by end date.</param>
    /// <param name="page">Page number (default 1).</param>
    /// <param name="pageSize">Page size (default 20).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated list of notification log entries.</returns>
    /// <response code="200">Logs retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("logs")]
    [ProducesResponseType(typeof(NotificationLogSearchResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationLogSearchResult>> GetLogs(
        [FromQuery] string? channel,
        [FromQuery] string? status,
        [FromQuery] string? recipient,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var (items, totalCount) = await _logRepository.SearchAsync(
            tenantId, channel, status, recipient, dateFrom, dateTo, page, pageSize, cancellationToken);

        var logDtos = items.Select(l => new NotificationLogDto
        {
            Id = l.Id,
            TenantId = l.TenantId,
            Channel = l.Channel,
            Recipient = l.Recipient,
            Subject = l.Subject,
            Body = l.Body,
            Status = l.Status,
            ErrorMessage = l.ErrorMessage,
            TemplateId = l.TemplateId,
            RetryCount = l.RetryCount,
            SentAt = l.SentAt ?? DateTime.MinValue,
            CreatedAt = l.CreatedAt
        }).ToList();

        return Ok(new NotificationLogSearchResult
        {
            Items = logDtos,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Retrieves all notification templates for the current tenant.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of notification templates.</returns>
    /// <response code="200">Templates retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(List<NotificationTemplateDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<NotificationTemplateDto>>> GetTemplates(
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var templates = await _templateRepository.GetAllByTenantAsync(tenantId, cancellationToken);

        var dtos = templates.Select(t => new NotificationTemplateDto
        {
            Id = t.Id,
            TenantId = t.TenantId,
            Name = t.Name,
            Channel = t.Channel,
            Subject = t.Subject,
            Body = t.Body,
            Description = t.Description,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Creates a new notification template for the current tenant.
    /// </summary>
    /// <param name="dto">The template details.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created template.</returns>
    /// <response code="201">Template created successfully.</response>
    /// <response code="400">Invalid template data.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("templates")]
    [ProducesResponseType(typeof(NotificationTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<NotificationTemplateDto>> CreateTemplate(
        [FromBody] CreateNotificationTemplateDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var entry = new NotificationTemplateEntry
        {
            TenantId = tenantId,
            Name = dto.Name,
            Channel = dto.Channel,
            Subject = dto.Subject,
            Body = dto.Body,
            Description = dto.Description,
            IsActive = dto.IsActive
        };

        var id = await _templateRepository.CreateAsync(entry, cancellationToken);

        var result = new NotificationTemplateDto
        {
            Id = id,
            TenantId = tenantId,
            Name = dto.Name,
            Channel = dto.Channel,
            Subject = dto.Subject,
            Body = dto.Body,
            Description = dto.Description,
            IsActive = dto.IsActive,
            CreatedAt = entry.CreatedAt
        };

        return CreatedAtAction(nameof(GetTemplates), null, result);
    }
}

/// <summary>
/// Paginated search result for notification logs.
/// </summary>
public sealed class NotificationLogSearchResult
{
    public List<NotificationLogDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
