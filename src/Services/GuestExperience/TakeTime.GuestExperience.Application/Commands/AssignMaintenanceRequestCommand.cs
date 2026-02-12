using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to assign a maintenance request to a technician.
/// </summary>
public sealed class AssignMaintenanceRequestCommand : IRequest<MaintenanceRequestDto>
{
    public Guid RequestId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
}

/// <summary>
/// Handler for <see cref="AssignMaintenanceRequestCommand"/>. Validates the request exists,
/// sets the status to Assigned, and records the technician assignment.
/// </summary>
public sealed class AssignMaintenanceRequestCommandHandler : IRequestHandler<AssignMaintenanceRequestCommand, MaintenanceRequestDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<AssignMaintenanceRequestCommandHandler> _logger;

    public AssignMaintenanceRequestCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<AssignMaintenanceRequestCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MaintenanceRequestDto> Handle(AssignMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Assigning maintenance request {RequestId} to technician {TechnicianId}",
            request.RequestId, request.AssignedTo);

        var entry = await _repository.GetMaintenanceRequestByIdAsync(request.RequestId, cancellationToken)
            ?? throw new NotFoundException("MaintenanceRequest", request.RequestId);

        entry.AssignedTo = request.AssignedTo;
        entry.AssignedToName = request.AssignedToName;
        entry.Status = "Assigned";

        await _repository.UpdateMaintenanceRequestAsync(entry, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation(
            "Maintenance request {RequestId} assigned to {TechnicianName}",
            request.RequestId, request.AssignedToName ?? request.AssignedTo);

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
