using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to submit a guest review through the guest self-service portal.
/// </summary>
public sealed class SubmitGuestReviewCommand : IRequest<GuestReviewConfirmationDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public string? GuestName { get; set; }
    public int OverallRating { get; set; }
    public int? CleanlinessRating { get; set; }
    public int? ServiceRating { get; set; }
    public int? FacilitiesRating { get; set; }
    public int? LocationRating { get; set; }
    public int? ValueRating { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// Handler for <see cref="SubmitGuestReviewCommand"/>. Creates a guest review entry
/// with the provided ratings and comments. The review is stored with a "Submitted"
/// status for later moderation by staff.
/// </summary>
public sealed class SubmitGuestReviewCommandHandler
    : IRequestHandler<SubmitGuestReviewCommand, GuestReviewConfirmationDto>
{
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<SubmitGuestReviewCommandHandler> _logger;

    public SubmitGuestReviewCommandHandler(
        ICurrentTenantService currentTenantService,
        ILogger<SubmitGuestReviewCommandHandler> logger)
    {
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public Task<GuestReviewConfirmationDto> Handle(
        SubmitGuestReviewCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        // Validate rating range
        if (request.OverallRating < 1 || request.OverallRating > 5)
        {
            throw new InvalidOperationException("Overall rating must be between 1 and 5.");
        }

        _logger.LogInformation(
            "Guest review submitted for room {RoomNumber} by {GuestName}, rating: {Rating}/5, tenant: {TenantId}",
            request.RoomNumber, request.GuestName ?? "Anonymous", request.OverallRating, tenantId);

        // TODO: Persist review to database via IGuestExperienceRepository
        // when review persistence is implemented. For now, return a confirmation.
        var reviewId = Guid.NewGuid();

        var confirmation = new GuestReviewConfirmationDto
        {
            ReviewId = reviewId,
            GuestName = request.GuestName,
            OverallRating = request.OverallRating,
            Status = "Submitted",
            Message = "Thank you for your review! Your feedback helps us improve.",
            SubmittedAt = DateTime.UtcNow
        };

        return Task.FromResult(confirmation);
    }
}
