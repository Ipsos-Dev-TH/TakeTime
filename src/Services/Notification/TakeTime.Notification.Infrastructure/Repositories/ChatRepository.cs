using Microsoft.EntityFrameworkCore;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Domain.Entities;

namespace TakeTime.Notification.Infrastructure.Repositories;

public sealed class ChatRepository : IChatRepository
{
    private readonly NotificationDbContext _db;

    public ChatRepository(NotificationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid> CreateMessageAsync(ChatMessageEntry message, CancellationToken cancellationToken = default)
    {
        var entity = new ChatMessage(
            message.RoomNumber,
            message.SenderType,
            message.SenderName,
            message.Message);

        entity.TenantId = message.TenantId;

        _db.ChatMessages.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        message.Id = entity.Id;
        message.CreatedAt = entity.CreatedAt;

        return entity.Id;
    }

    public async Task<IReadOnlyList<ChatMessageEntry>> GetMessagesByRoomAsync(
        string tenantId, string roomNumber, int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var messages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.RoomNumber == roomNumber)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ChatMessageEntry
            {
                Id = m.Id,
                TenantId = m.TenantId!,
                RoomNumber = m.RoomNumber,
                SenderType = m.SenderType,
                SenderName = m.SenderName,
                Message = m.Message,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                IsConversationActive = m.IsConversationActive,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return messages;
    }

    public async Task<IReadOnlyList<ChatConversationSummary>> GetActiveConversationsAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        var activeMessages = await _db.ChatMessages
            .AsNoTracking()
            .Where(m => m.TenantId == tenantId && m.IsConversationActive)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        var summaries = activeMessages
            .GroupBy(m => m.RoomNumber)
            .Select(group =>
            {
                var lastMessage = group.First();
                var guestMessage = group.FirstOrDefault(m =>
                    m.SenderType.Equals("Guest", StringComparison.OrdinalIgnoreCase));

                return new ChatConversationSummary
                {
                    RoomNumber = group.Key,
                    GuestName = guestMessage?.SenderName,
                    TotalMessages = group.Count(),
                    UnreadMessages = group.Count(m => !m.IsRead),
                    LastMessage = new ChatMessageEntry
                    {
                        Id = lastMessage.Id,
                        TenantId = lastMessage.TenantId!,
                        RoomNumber = lastMessage.RoomNumber,
                        SenderType = lastMessage.SenderType,
                        SenderName = lastMessage.SenderName,
                        Message = lastMessage.Message,
                        IsRead = lastMessage.IsRead,
                        ReadAt = lastMessage.ReadAt,
                        IsConversationActive = lastMessage.IsConversationActive,
                        CreatedAt = lastMessage.CreatedAt
                    },
                    IsActive = true,
                    StartedAt = group.Min(m => m.CreatedAt),
                    LastMessageAt = lastMessage.CreatedAt
                };
            })
            .OrderByDescending(s => s.LastMessageAt)
            .ToList();

        return summaries;
    }

    public async Task CloseConversationAsync(
        string tenantId, string roomNumber, CancellationToken cancellationToken = default)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.TenantId == tenantId
                && m.RoomNumber == roomNumber
                && m.IsConversationActive)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            message.CloseConversation();
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetMessageCountByRoomAsync(
        string tenantId, string roomNumber, CancellationToken cancellationToken = default)
    {
        return await _db.ChatMessages
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId && m.RoomNumber == roomNumber,
                cancellationToken);
    }

    public async Task<int> GetUnreadCountByRoomAsync(
        string tenantId, string roomNumber, CancellationToken cancellationToken = default)
    {
        return await _db.ChatMessages
            .AsNoTracking()
            .CountAsync(m => m.TenantId == tenantId
                && m.RoomNumber == roomNumber
                && !m.IsRead,
                cancellationToken);
    }
}
