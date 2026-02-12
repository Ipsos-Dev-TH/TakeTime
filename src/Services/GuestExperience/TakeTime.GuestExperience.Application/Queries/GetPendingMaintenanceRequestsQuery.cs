using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve all pending (open and unassigned) maintenance requests.
/// Returns requests ordered by priority (urgent first) then creation time.
/// </summary>
public sealed class GetPendingMaintenanceRequestsQuery : IRequest<List<MaintenanceRequestDto>>
{
}

/// <summary>
/// Handler for <see cref="GetPendingMaintenanceRequestsQuery"/>. Retrieves all open
/// maintenance requests that have not been assigned to a technician.
/// </summary>
public sealed class GetPendingMaintenanceRequestsQueryHandler : IRequestHandler<GetPendingMaintenanceRequestsQuery, List<MaintenanceRequestDto>>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetPendingMaintenanceRequestsQueryHandler> _logger;

    public GetPendingMaintenanceRequestsQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetPendingMaintenanceRequestsQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<MaintenanceRequestDto>> Handle(GetPendingMaintenanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation("Retrieving pending maintenance requests for tenant {TenantId}", tenantId);

        // Retrieve open requests with a large page to get all pending
        var requests = await _repository.GetMaintenanceRequestsAsync(
            tenantId, "Open", null, 1, 1000, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var result = requests
            .Where(r => string.IsNullOrWhiteSpace(r.AssignedTo))
            .Select(r => new MaintenanceRequestDto
            {
                Id = r.Id,
                TenantId = r.TenantId,
                RequestNumber = r.RequestNumber,
                RoomNumber = r.RoomNumber,
                AccommodationId = r.AccommodationId,
                Category = r.Category,
                Priority = r.Priority,
                Status = r.Status,
                Description = r.Description,
                AssignedTo = r.AssignedTo,
                AssignedToName = r.AssignedToName,
                EstimatedCost = r.EstimatedCost,
                ActualCost = r.ActualCost,
                Currency = currency,
                ResolutionNotes = r.ResolutionNotes,
                StartedAt = r.StartedAt,
                CompletedAt = r.CompletedAt,
                CreatedAt = r.CreatedAt
            }).ToList();

        _logger.LogInformation("Retrieved {Count} pending maintenance requests", result.Count);

        return result;
    }
}
