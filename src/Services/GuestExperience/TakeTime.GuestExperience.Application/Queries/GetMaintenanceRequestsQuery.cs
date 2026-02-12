using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve maintenance requests with optional filtering by status, priority, and category.
/// </summary>
public sealed class GetMaintenanceRequestsQuery : IRequest<List<MaintenanceRequestDto>>
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Category { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetMaintenanceRequestsQuery"/>. Retrieves maintenance requests
/// from the repository with filtering and pagination support.
/// </summary>
public sealed class GetMaintenanceRequestsQueryHandler : IRequestHandler<GetMaintenanceRequestsQuery, List<MaintenanceRequestDto>>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetMaintenanceRequestsQueryHandler> _logger;

    public GetMaintenanceRequestsQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetMaintenanceRequestsQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<MaintenanceRequestDto>> Handle(GetMaintenanceRequestsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving maintenance requests for tenant {TenantId}. Status: {Status}, Priority: {Priority}",
            tenantId, request.Status, request.Priority);

        var requests = await _repository.GetMaintenanceRequestsAsync(
            tenantId, request.Status, request.Priority, request.Page, request.PageSize, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        var result = requests.Select(r => new MaintenanceRequestDto
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

        // Apply category filter in-memory if specified
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            result = result.Where(r => r.Category.Equals(request.Category, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _logger.LogInformation("Retrieved {Count} maintenance requests", result.Count);

        return result;
    }
}
