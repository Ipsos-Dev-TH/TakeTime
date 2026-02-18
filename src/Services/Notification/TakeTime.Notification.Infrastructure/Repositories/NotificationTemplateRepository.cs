using Microsoft.EntityFrameworkCore;
using TakeTime.Notification.Application.Interfaces;
using TakeTime.Notification.Domain.Entities;
using TakeTime.Notification.Domain.Enums;

namespace TakeTime.Notification.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for notification template persistence.
/// Maps between the application-layer NotificationTemplateEntry DTOs and the
/// NotificationTemplate domain entities, using NotificationDbContext for data access.
/// </summary>
public class NotificationTemplateRepository : INotificationTemplateRepository
{
    private readonly NotificationDbContext _dbContext;

    public NotificationTemplateRepository(NotificationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<NotificationTemplateEntry?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var template = await _dbContext.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return template is null ? null : MapToEntry(template);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NotificationTemplateEntry>> GetAllByTenantAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        var templates = await _dbContext.NotificationTemplates
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.TemplateName)
            .ToListAsync(cancellationToken);

        return templates.Select(MapToEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<Guid> CreateAsync(
        NotificationTemplateEntry template, CancellationToken cancellationToken = default)
    {
        var channel = ParseChannel(template.Channel);

        var entity = new NotificationTemplate(
            templateName: template.Name,
            channel: channel,
            body: template.Body,
            subject: template.Subject);

        entity.TenantId = template.TenantId;

        if (!template.IsActive)
        {
            entity.Deactivate();
        }

        await _dbContext.NotificationTemplates.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        NotificationTemplateEntry template, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == template.Id, cancellationToken);

        if (entity is null)
            return;

        entity.UpdateContent(template.Body, template.Subject);

        if (template.IsActive)
        {
            entity.Activate();
        }
        else
        {
            entity.Deactivate();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Maps a NotificationTemplate domain entity to a NotificationTemplateEntry DTO.
    /// </summary>
    private static NotificationTemplateEntry MapToEntry(NotificationTemplate template)
    {
        return new NotificationTemplateEntry
        {
            Id = template.Id,
            TenantId = template.TenantId ?? string.Empty,
            Name = template.TemplateName,
            Channel = template.Channel.ToString(),
            Subject = template.Subject,
            Body = template.Body,
            IsActive = template.IsActive,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }

    /// <summary>
    /// Parses a channel string into a NotificationChannel enum value.
    /// Defaults to Email if the string cannot be parsed.
    /// </summary>
    private static NotificationChannel ParseChannel(string channel)
    {
        return Enum.TryParse<NotificationChannel>(channel, true, out var parsed)
            ? parsed
            : NotificationChannel.Email;
    }
}
