using Microsoft.EntityFrameworkCore;
using TakeTime.Notification.Application.Interfaces;
using TakeTime.Notification.Domain.Entities;
using TakeTime.Notification.Domain.Enums;

namespace TakeTime.Notification.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for notification log persistence.
/// Maps between the application-layer NotificationLogEntry DTOs and the
/// NotificationLog domain entities, using NotificationDbContext for data access.
/// </summary>
public class NotificationLogRepository : INotificationLogRepository
{
    private readonly NotificationDbContext _dbContext;

    public NotificationLogRepository(NotificationDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    /// <inheritdoc />
    public async Task<Guid> LogAsync(NotificationLogEntry entry, CancellationToken cancellationToken = default)
    {
        var channel = ParseChannel(entry.Channel);

        var log = new NotificationLog(
            channel: channel,
            recipient: entry.Recipient,
            body: entry.Body,
            subject: entry.Subject);

        log.TenantId = entry.TenantId;

        await _dbContext.NotificationLogs.AddAsync(log, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return log.Id;
    }

    /// <inheritdoc />
    public async Task UpdateStatusAsync(
        Guid logId,
        string status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var log = await _dbContext.NotificationLogs
            .FirstOrDefaultAsync(l => l.Id == logId, cancellationToken);

        if (log is null)
            return;

        if (string.Equals(status, "Sent", StringComparison.OrdinalIgnoreCase))
        {
            log.MarkAsSent();
        }
        else if (string.Equals(status, "Failed", StringComparison.OrdinalIgnoreCase)
              || string.Equals(status, "Queued", StringComparison.OrdinalIgnoreCase))
        {
            log.MarkAsFailed(errorMessage ?? "Unknown error");
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<NotificationLogEntry> Items, int TotalCount)> SearchAsync(
        string tenantId,
        string? channel,
        string? status,
        string? recipient,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 100) pageSize = 100;

        var query = _dbContext.NotificationLogs
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(channel) && Enum.TryParse<NotificationChannel>(channel, true, out var parsedChannel))
        {
            query = query.Where(l => l.Channel == parsedChannel);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(recipient))
        {
            query = query.Where(l => l.Recipient.Contains(recipient));
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(l => l.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(l => l.CreatedAt <= dateTo.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = logs.Select(MapToEntry).ToList();

        return (items, totalCount);
    }

    /// <summary>
    /// Maps a NotificationLog domain entity to a NotificationLogEntry DTO.
    /// </summary>
    private static NotificationLogEntry MapToEntry(NotificationLog log)
    {
        return new NotificationLogEntry
        {
            Id = log.Id,
            TenantId = log.TenantId ?? string.Empty,
            Channel = log.Channel.ToString(),
            Recipient = log.Recipient,
            Subject = log.Subject,
            Body = log.Body,
            Status = log.Status,
            ErrorMessage = log.ErrorMessage,
            RetryCount = log.RetryCount,
            SentAt = log.SentAt,
            CreatedAt = log.CreatedAt
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
