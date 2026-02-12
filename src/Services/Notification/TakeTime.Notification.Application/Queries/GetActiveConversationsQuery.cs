using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Application.DTOs;

namespace TakeTime.Notification.Application.Queries;

/// <summary>
/// Query to retrieve all active (open) chat conversations for the current tenant.
/// Returns a summary of each conversation including unread message counts.
/// </summary>
public sealed class GetActiveConversationsQuery : IRequest<List<ChatConversationDto>>
{
}

/// <summary>
/// Handler for <see cref="GetActiveConversationsQuery"/>. Retrieves all active
/// chat conversations across rooms for the current tenant, including unread
/// message counts and the last message preview.
/// </summary>
public sealed class GetActiveConversationsQueryHandler
    : IRequestHandler<GetActiveConversationsQuery, List<ChatConversationDto>>
{
    private readonly IChatRepository _chatRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetActiveConversationsQueryHandler> _logger;

    public GetActiveConversationsQueryHandler(
        IChatRepository chatRepository,
        ICurrentTenantService currentTenantService,
        ILogger<GetActiveConversationsQueryHandler> logger)
    {
        _chatRepository = chatRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<ChatConversationDto>> Handle(
        GetActiveConversationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogDebug("Fetching active chat conversations for tenant {TenantId}", tenantId);

        var conversations = await _chatRepository.GetActiveConversationsAsync(tenantId, cancellationToken);

        var result = conversations.Select(c => new ChatConversationDto
        {
            RoomNumber = c.RoomNumber,
            GuestName = c.GuestName,
            TotalMessages = c.TotalMessages,
            UnreadMessages = c.UnreadMessages,
            LastMessage = c.LastMessage is not null ? new ChatMessageDto
            {
                Id = c.LastMessage.Id,
                TenantId = c.LastMessage.TenantId,
                RoomNumber = c.LastMessage.RoomNumber,
                SenderType = c.LastMessage.SenderType,
                SenderName = c.LastMessage.SenderName,
                Message = c.LastMessage.Message,
                IsRead = c.LastMessage.IsRead,
                ReadAt = c.LastMessage.ReadAt,
                CreatedAt = c.LastMessage.CreatedAt
            } : null,
            IsActive = c.IsActive,
            StartedAt = c.StartedAt,
            LastMessageAt = c.LastMessageAt
        }).ToList();

        _logger.LogInformation(
            "Found {Count} active chat conversations for tenant {TenantId}",
            result.Count, tenantId);

        return result;
    }
}
