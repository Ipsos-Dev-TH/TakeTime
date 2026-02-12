using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Reservation.Application.Interfaces;
using TakeTime.Reservation.Domain.Enums;

namespace TakeTime.Reservation.Application.Commands;

/// <summary>
/// Command to update the operational status of an accommodation
/// (e.g., Available, Maintenance, Cleaning, Occupied, OutOfOrder).
/// </summary>
public sealed class UpdateAccommodationStatusCommand : IRequest<Unit>
{
    public Guid AccommodationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="UpdateAccommodationStatusCommand"/>. Validates the status
/// transition and updates the accommodation's operational status.
/// </summary>
public sealed class UpdateAccommodationStatusCommandHandler : IRequestHandler<UpdateAccommodationStatusCommand, Unit>
{
    private readonly IAccommodationRepository _accommodationRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<UpdateAccommodationStatusCommandHandler> _logger;

    public UpdateAccommodationStatusCommandHandler(
        IAccommodationRepository accommodationRepository,
        ITenantContext tenantContext,
        ILogger<UpdateAccommodationStatusCommandHandler> logger)
    {
        _accommodationRepository = accommodationRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(UpdateAccommodationStatusCommand request, CancellationToken cancellationToken)
    {
        var tenantSettings = await _tenantContext.GetTenantSettingsAsync(cancellationToken);

        _logger.LogInformation(
            "Updating accommodation {AccommodationId} status to '{Status}' for tenant {TenantId}",
            request.AccommodationId, request.Status, tenantSettings.TenantId);

        // Parse the target status
        if (!Enum.TryParse<AccommodationStatus>(request.Status, true, out var newStatus))
        {
            throw new InvalidOperationException(
                $"Invalid accommodation status '{request.Status}'. " +
                $"Valid statuses are: {string.Join(", ", Enum.GetNames<AccommodationStatus>())}.");
        }

        // Retrieve the accommodation
        var accommodation = await _accommodationRepository.GetEntityByIdAsync(request.AccommodationId, cancellationToken)
            ?? throw new InvalidOperationException($"Accommodation with ID '{request.AccommodationId}' not found.");

        var previousStatus = accommodation.Status;

        // Apply status change using appropriate domain methods
        switch (newStatus)
        {
            case AccommodationStatus.Available:
                accommodation.MarkAsAvailable();
                break;
            case AccommodationStatus.Occupied:
                accommodation.MarkAsOccupied();
                break;
            case AccommodationStatus.Maintenance:
                accommodation.PutUnderMaintenance();
                break;
            case AccommodationStatus.Cleaning:
                accommodation.MarkForCleaning();
                break;
            case AccommodationStatus.OutOfOrder:
                accommodation.SetStatus(AccommodationStatus.OutOfOrder);
                break;
            default:
                accommodation.SetStatus(newStatus);
                break;
        }

        accommodation.UpdatedBy = request.UpdatedBy;

        // Persist the change
        await _accommodationRepository.UpdateAsync(accommodation, cancellationToken);

        _logger.LogInformation(
            "Accommodation {AccommodationId} status changed from {PreviousStatus} to {NewStatus} for tenant {TenantId}",
            request.AccommodationId, previousStatus, newStatus, tenantSettings.TenantId);

        return Unit.Value;
    }
}
