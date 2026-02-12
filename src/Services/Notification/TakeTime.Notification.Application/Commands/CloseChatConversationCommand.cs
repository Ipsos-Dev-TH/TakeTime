using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;

namespace TakeTime.Notification.Application.Commands;

/// <summary>
/// Command to close a chat conversation for a specific room number.
/// Marks all messages in the conversation as belonging to a closed conversation.
/// </summary>
public sealed class CloseChatConversationCommand : IRequest<Unit>
{
    public string RoomNumber { get; set; } = string.Empty;
}

/// <summary>
/// Handler for <see cref="CloseChatConversationCommand"/>. Closes the active
/// chat conversation for the specified room number, marking all messages
/// as part of a closed conversation.
/// </summary>
public sealed class CloseChatConversationCommandHandler
    : IRequestHandler<CloseChatConversationCommand, Unit>
{
    private readonly IChatRepository _chatRepository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CloseChatConversationCommandHandler> _logger;

    public CloseChatConversationCommandHandler(
        IChatRepository chatRepository,
        ICurrentTenantService currentTenantService,
        ILogger<CloseChatConversationCommandHandler> logger)
    {
        _chatRepository = chatRepository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<Unit> Handle(
        CloseChatConversationCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Closing chat conversation for room {RoomNumber} in tenant {TenantId}",
            request.RoomNumber, tenantId);

        await _chatRepository.CloseConversationAsync(tenantId, request.RoomNumber, cancellationToken);

        _logger.LogInformation(
            "Chat conversation for room {RoomNumber} closed in tenant {TenantId}",
            request.RoomNumber, tenantId);

        return Unit.Value;
    }
}
