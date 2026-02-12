using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to start work on a maintenance request.
/// </summary>
public sealed class StartMaintenanceWorkCommand : IRequest<MaintenanceRequestDto>
{
    public Guid RequestId { get; set; }
}

/// <summary>
/// Handler for <see cref="StartMaintenanceWorkCommand"/>. Validates the request is in
/// Assigned status, sets it to InProgress, and records the StartedAt timestamp.
/// </summary>
public sealed class StartMaintenanceWorkCommandHandler : IRequestHandler<StartMaintenanceWorkCommand, MaintenanceRequestDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<StartMaintenanceWorkCommandHandler> _logger;

    public StartMaintenanceWorkCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<StartMaintenanceWorkCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MaintenanceRequestDto> Handle(StartMaintenanceWorkCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting maintenance work on request {RequestId}", request.RequestId);

        var entry = await _repository.GetMaintenanceRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("MaintenanceRequest", request.RequestId);

        if (entry.Status != "Assigned")
        {
            throw new InvalidOperationException(
                $"Cannot start maintenance work in '{entry.Status}' status. Request must be in Assigned status.");
        }

        entry.Status = "InProgress";
        entry.StartedAt = DateTime.UtcNow;

        await _repository.UpdateMaintenanceRequestAsync(entry, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation(
            "Maintenance request {RequestId} work started at {StartedAt}",
            request.RequestId, entry.StartedAt);

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
