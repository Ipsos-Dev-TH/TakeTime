namespace TakeTime.Notification.Application.DTOs;

/// <summary>
/// DTO representing a chat message in a guest-staff conversation.
/// </summary>
public sealed class ChatMessageDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public string SenderType { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for sending a chat message.
/// </summary>
public sealed class SendChatMessageDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string SenderType { get; set; } = "Staff";
    public string SenderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// DTO representing a chat conversation summary.
/// </summary>
public sealed class ChatConversationDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public int TotalMessages { get; set; }
    public int UnreadMessages { get; set; }
    public ChatMessageDto? LastMessage { get; set; }
    public bool IsActive { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
}

/// <summary>
/// DTO for the chat history of a specific room/conversation.
/// </summary>
public sealed class ChatHistoryDto
{
    public string RoomNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int TotalMessages { get; set; }
    public List<ChatMessageDto> Messages { get; set; } = [];
}
