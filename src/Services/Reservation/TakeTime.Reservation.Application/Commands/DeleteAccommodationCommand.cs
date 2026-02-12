using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to soft-delete an accommodation. Checks for active reservations
/// before allowing deletion.
/// </summary>
public sealed class DeleteAccommodationCommand : IRequest<Unit>
{
    public Guid AccommodationId { get; set; }
    public string? DeletedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="DeleteAccommodationCommand"/>. Validates that the accommodation
/// exists and has no active reservations, then performs a soft-delete by setting IsDeleted = true.
/// </summary>
public sealed class DeleteAccommodationCommandHandler : IRequestHandler<DeleteAccommodationCommand, Unit>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<DeleteAccommodationCommandHandler> _logger;

    public DeleteAccommodationCommandHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<DeleteAccommodationCommandHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(DeleteAccommodationCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Deleting accommodation {AccommodationId} for tenant {TenantId}",
            request.AccommodationId, tenantSettings.TenantId);

        // Verify the accommodation exists
        var accommodation = await _accommodationRepository.GetEntityByIdAsync(request.AccommodationId, cancellationToken)
            ?? throw new InvalidOperationException($"Accommodation with ID '{request.AccommodationId}' not found.");

        // Check for active reservations
        var hasActiveReservations = await _accommodationRepository.HasActiveReservationsAsync(
            request.AccommodationId, cancellationToken);

        if (hasActiveReservations)
        {
            throw new InvalidOperationException(
                $"Cannot delete accommodation '{accommodation.Name}'. " +
                "There are active reservations associated with this accommodation.");
        }

        // Perform soft-delete
        accommodation.MarkAsDeleted(request.DeletedBy);
        await _accommodationRepository.UpdateAsync(accommodation, cancellationToken);

        _logger.LogInformation(
            "Accommodation {AccommodationId} ('{AccommodationName}') soft-deleted by {DeletedBy} for tenant {TenantId}",
            request.AccommodationId, accommodation.Name, request.DeletedBy, tenantSettings.TenantId);

        return Unit.Value;
    }
}
