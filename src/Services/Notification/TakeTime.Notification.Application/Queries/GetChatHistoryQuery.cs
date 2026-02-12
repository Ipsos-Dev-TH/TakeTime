using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Application.DTOs;

namespace TakeTime.Notification.Application.Queries;

/// <summary>
/// Query to retrieve the chat history for a specific room number.
/// Returns all messages in the conversation ordered by creation time.
/// </summary>
public sealed class GetChatHistoryQuery : IRequest<ChatHistoryDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

/// <summary>
/// Handler for <see cref="GetChatHistoryQuery"/>. Retrieves the full chat
/// conversation history for the specified room number within the current tenant.
/// </summary>
public sealed class GetChatHistoryQueryHandler
    : IRequestHandler<GetChatHistoryQuery, ChatHistoryDto>
{
    private readonly IChatRepository _chatRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetChatHistoryQueryHandler> _logger;

    public GetChatHistoryQueryHandler(
        IChatRepository chatRepository,
        ICurrentTenantService currentTenantService,
        ILogger<GetChatHistoryQueryHandler> logger)
    {
        _chatRepository = chatRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<ChatHistoryDto> Handle(
        GetChatHistoryQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogDebug(
            "Fetching chat history for room {RoomNumber} in tenant {TenantId}",
            request.RoomNumber, tenantId);

        var messages = await _chatRepository.GetMessagesByRoomAsync(
            tenantId, request.RoomNumber, request.Page, request.PageSize, cancellationToken);

        var totalMessages = await _chatRepository.GetMessageCountByRoomAsync(
            tenantId, request.RoomNumber, cancellationToken);

        var isActive = messages.Any(m => m.IsConversationActive);

        var messageDtos = messages.Select(m => new ChatMessageDto
        {
            Id = m.Id,
            TenantId = m.TenantId,
            RoomNumber = m.RoomNumber,
            SenderType = m.SenderType,
            SenderName = m.SenderName,
            Message = m.Message,
            IsRead = m.IsRead,
            ReadAt = m.ReadAt,
            CreatedAt = m.CreatedAt
        }).ToList();

        _logger.LogDebug(
            "Returning {Count} chat messages for room {RoomNumber} in tenant {TenantId}",
            messageDtos.Count, request.RoomNumber, tenantId);

        return new ChatHistoryDto
        {
            RoomNumber = request.RoomNumber,
            IsActive = isActive,
            TotalMessages = totalMessages,
            Messages = messageDtos
        };
    }
}
