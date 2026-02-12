using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Notification.Application.Commands;
using TakeTime.Notification.Application.DTOs;
using TakeTime.Notification.Application.Queries;

namespace TakeTime.Notification.API.Controllers;

/// <summary>
/// API controller for managing guest-staff chat conversations.
/// Provides real-time messaging between guests and hotel staff,
/// conversation history, and conversation lifecycle management.
/// </summary>
[ApiController]
[Route("api/v1/chat")]
[Authorize]
[Produces("application/json")]
public sealed class ChatController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<ChatController> _logger;

    public ChatController(IMediator mediator, ILogger<ChatController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Send a chat message in a guest-staff conversation.
    /// Creates a new message associated with the specified room number.
    /// </summary>
    /// <param name="dto">Chat message details including room number, sender, and message content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created chat message.</returns>
    /// <response code="201">Message sent successfully.</response>
    /// <response code="400">Invalid message data.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("messages")]
    [ProducesResponseType(typeof(ChatMessageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatMessageDto>> SendMessage(
        [FromBody] SendChatMessageDto dto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Sending chat message for room {RoomNumber} from {SenderType} ({SenderName})",
            dto.RoomNumber, dto.SenderType, dto.SenderName);

        var command = new CreateChatMessageCommand
        {
            RoomNumber = dto.RoomNumber,
            SenderType = dto.SenderType,
            SenderName = dto.SenderName,
            Message = dto.Message
        };

        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(
            nameof(GetConversationHistory),
            new { roomNumber = dto.RoomNumber },
            result);
    }

    /// <summary>
    /// Get the chat conversation history for a specific room number.
    /// Returns all messages ordered by creation time.
    /// </summary>
    /// <param name="roomNumber">The room number to get conversation history for.</param>
    /// <param name="page">Page number (default: 1).</param>
    /// <param name="pageSize">Page size (default: 50).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The chat history for the specified room.</returns>
    /// <response code="200">Chat history retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("conversations/{roomNumber}")]
    [ProducesResponseType(typeof(ChatHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ChatHistoryDto>> GetConversationHistory(
        string roomNumber,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching chat history for room {RoomNumber}", roomNumber);

        var query = new GetChatHistoryQuery
        {
            RoomNumber = roomNumber,
            Page = page,
            PageSize = pageSize
        };

        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// List all active (open) chat conversations.
    /// Returns a summary of each conversation including unread message counts
    /// and the most recent message preview.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of active chat conversations.</returns>
    /// <response code="200">Active conversations retrieved successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpGet("conversations")]
    [ProducesResponseType(typeof(List<ChatConversationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<List<ChatConversationDto>>> GetActiveConversations(
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Fetching active chat conversations");

        var query = new GetActiveConversationsQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Close a chat conversation for a specific room number.
    /// Marks all messages in the conversation as belonging to a closed conversation.
    /// </summary>
    /// <param name="roomNumber">The room number of the conversation to close.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success confirmation.</returns>
    /// <response code="200">Conversation closed successfully.</response>
    /// <response code="401">Authentication required.</response>
    [HttpPost("conversations/{roomNumber}/close")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CloseConversation(
        string roomNumber,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Closing chat conversation for room {RoomNumber}", roomNumber);

        var command = new CloseChatConversationCommand
        {
            RoomNumber = roomNumber
        };

        await _mediator.Send(command, cancellationToken);

        _logger.LogInformation("Chat conversation for room {RoomNumber} closed", roomNumber);

        return Ok(new { message = $"Conversation for room '{roomNumber}' closed successfully." });
    }
}
