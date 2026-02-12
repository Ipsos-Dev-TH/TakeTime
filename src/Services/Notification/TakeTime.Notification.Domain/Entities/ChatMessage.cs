using TakeTime.Core.Domain.Base;

namespace TakeTime.Notification.Domain.Entities;

/// <summary>
/// Represents a chat message in a guest-staff conversation.
/// Messages are grouped by room number to form conversations.
/// </summary>
public class ChatMessage : Entity
{
    /// <summary>
    /// The room number this conversation is associated with.
    /// </summary>
    public string RoomNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The sender type: "Guest" or "Staff".
    /// </summary>
    public string SenderType { get; private set; } = string.Empty;

    /// <summary>
    /// The display name of the sender.
    /// </summary>
    public string SenderName { get; private set; } = string.Empty;

    /// <summary>
    /// The message content.
    /// </summary>
    public string Message { get; private set; } = string.Empty;

    /// <summary>
    /// Whether the message has been read by the recipient.
    /// </summary>
    public bool IsRead { get; private set; }

    /// <summary>
    /// Timestamp when the message was read.
    /// </summary>
    public DateTime? ReadAt { get; private set; }

    /// <summary>
    /// Whether this message is part of an active (open) conversation.
    /// </summary>
    public bool IsConversationActive { get; private set; } = true;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private ChatMessage() { }

    /// <summary>
    /// Creates a new chat message.
    /// </summary>
    public ChatMessage(
        string roomNumber,
        string senderType,
        string senderName,
        string message)
    {
        if (string.IsNullOrWhiteSpace(roomNumber))
            throw new ArgumentException("Room number is required.", nameof(roomNumber));

        if (string.IsNullOrWhiteSpace(senderType))
            throw new ArgumentException("Sender type is required.", nameof(senderType));

        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Message content is required.", nameof(message));

        RoomNumber = roomNumber;
        SenderType = senderType;
        SenderName = senderName;
        Message = message;
    }

    /// <summary>
    /// Marks the message as read.
    /// </summary>
    public void MarkAsRead()
    {
        IsRead = true;
        ReadAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Marks the conversation containing this message as closed.
    /// </summary>
    public void CloseConversation()
    {
        IsConversationActive = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
