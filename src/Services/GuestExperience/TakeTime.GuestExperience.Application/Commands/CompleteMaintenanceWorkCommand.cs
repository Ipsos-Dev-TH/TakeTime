using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to complete work on a maintenance request.
/// </summary>
public sealed class CompleteMaintenanceWorkCommand : IRequest<MaintenanceRequestDto>
{
    public Guid RequestId { get; set; }
    public string? ResolutionNotes { get; set; }
    public decimal? ActualCost { get; set; }
}

/// <summary>
/// Handler for <see cref="CompleteMaintenanceWorkCommand"/>. Validates the request is in
/// InProgress status, sets it to Completed, and records the resolution details.
/// </summary>
public sealed class CompleteMaintenanceWorkCommandHandler : IRequestHandler<CompleteMaintenanceWorkCommand, MaintenanceRequestDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CompleteMaintenanceWorkCommandHandler> _logger;

    public CompleteMaintenanceWorkCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CompleteMaintenanceWorkCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MaintenanceRequestDto> Handle(CompleteMaintenanceWorkCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing maintenance request {RequestId}", request.RequestId);

        var entry = await _repository.GetMaintenanceRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("MaintenanceRequest", request.RequestId);

        if (entry.Status != "InProgress")
        {
            throw new InvalidOperationException(
                $"Cannot complete maintenance work in '{entry.Status}' status. Request must be in InProgress status.");
        }

        entry.Status = "Completed";
        entry.CompletedAt = DateTime.UtcNow;
        entry.ResolutionNotes = request.ResolutionNotes;
        entry.ActualCost = request.ActualCost;

        await _repository.UpdateMaintenanceRequestAsync(entry, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation(
            "Maintenance request {RequestId} completed at {CompletedAt}",
            request.RequestId, entry.CompletedAt);

        return new MaintenanceRequestDto
        {
            Id = entry.Id,
            TenantId = entry.TenantId,
            RequestNumber = entry.RequestNumber,
            RoomNumber = entry.RoomNumber,
            AccommodationId = entry.AccommodationId,
            Category = entry.Category,
            Priority = entry.Priority,
            Status = entry.Status,
            Description = entry.Description,
            AssignedTo = entry.AssignedTo,
            AssignedToName = entry.AssignedToName,
            EstimatedCost = entry.EstimatedCost,
            ActualCost = entry.ActualCost,
            Currency = currency,
            ResolutionNotes = entry.ResolutionNotes,
            StartedAt = entry.StartedAt,
            CompletedAt = entry.CompletedAt,
            CreatedAt = entry.CreatedAt
        };
    }
}
