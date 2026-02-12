using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Notification.Application.DTOs;
using TakeTime.Notification.Application.Interfaces;

namespace TakeTime.Notification.Application.Commands;

/// <summary>
/// Command to send a chat message in a guest-staff conversation.
/// </summary>
public sealed class CreateChatMessageCommand : IRequest<ChatMessageDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public string SenderType { get; set; } = "Staff";
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Handler for <see cref="CreateChatMessageCommand"/>. Creates and persists a new
/// chat message in the conversation for the specified room number.
/// </summary>
public sealed class CreateChatMessageCommandHandler
    : IRequestHandler<CreateChatMessageCommand, ChatMessageDto>
{
    private readonly IChatRepository _chatRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CreateChatMessageCommandHandler> _logger;

    public CreateChatMessageCommandHandler(
        IChatRepository chatRepository,
        ICurrentTenantService currentTenantService,
        ILogger<CreateChatMessageCommandHandler> logger)
    {
        _chatRepository = chatRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<ChatMessageDto> Handle(
        CreateChatMessageCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new InvalidOperationException("Message content is required.");
        }

        _logger.LogInformation(
            "Creating chat message for room {RoomNumber} from {SenderType} ({SenderName}) in tenant {TenantId}",
            request.RoomNumber, request.SenderType, request.SenderName, tenantId);

        var entry = new ChatMessageEntry
        {
            TenantId = tenantId,
            RoomNumber = request.RoomNumber,
            SenderType = request.SenderType,
            SenderName = request.SenderName,
            Message = request.Message
        };

        var id = await _chatRepository.CreateMessageAsync(entry, cancellationToken);

        _logger.LogInformation(
            "Chat message {MessageId} created for room {RoomNumber} in tenant {TenantId}",
            id, request.RoomNumber, tenantId);

        return new ChatMessageDto
        {
            Id = id,
            TenantId = tenantId,
            RoomNumber = request.RoomNumber,
            SenderType = request.SenderType,
            SenderName = request.SenderName,
            Message = request.Message,
            IsRead = false,
            CreatedAt = entry.CreatedAt
        };
    }
}

/// <summary>
/// Repository interface for chat operations.
/// </summary>
public interface IChatRepository
{
    Task<Guid> CreateMessageAsync(ChatMessageEntry message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatMessageEntry>> GetMessagesByRoomAsync(string tenantId, string roomNumber, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ChatConversationSummary>> GetActiveConversationsAsync(string tenantId, CancellationToken cancellationToken = default);
    Task CloseConversationAsync(string tenantId, string roomNumber, CancellationToken cancellationToken = default);
    Task<int> GetMessageCountByRoomAsync(string tenantId, string roomNumber, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountByRoomAsync(string tenantId, string roomNumber, CancellationToken cancellationToken = default);
}

/// <summary>
/// Internal chat message entry model used by the application layer.
/// </summary>
public sealed class ChatMessageEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsConversationActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Internal conversation summary model used by the application layer.
/// </summary>
public sealed class ChatConversationSummary
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public int TotalMessages { get; set; }
    public int UnreadMessages { get; set; }
    public ChatMessageEntry? LastMessage { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}
