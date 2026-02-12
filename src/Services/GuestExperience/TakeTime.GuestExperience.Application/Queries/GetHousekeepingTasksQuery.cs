using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve housekeeping tasks with optional filtering by status, date, and priority.
/// </summary>
public sealed class GetHousekeepingTasksQuery : IRequest<List<HousekeepingTaskDto>>
{
    public string? Status { get; set; }
    public DateTime? Date { get; set; }
    public string? Priority { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Handler for <see cref="GetHousekeepingTasksQuery"/>. Retrieves housekeeping tasks
/// from the repository with filtering and pagination support.
/// </summary>
public sealed class GetHousekeepingTasksQueryHandler : IRequestHandler<GetHousekeepingTasksQuery, List<HousekeepingTaskDto>>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetHousekeepingTasksQueryHandler> _logger;

    public GetHousekeepingTasksQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetHousekeepingTasksQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<List<HousekeepingTaskDto>> Handle(GetHousekeepingTasksQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Retrieving housekeeping tasks for tenant {TenantId}. Status: {Status}, Date: {Date}, Priority: {Priority}",
            tenantId, request.Status, request.Date, request.Priority);

        var tasks = await _repository.GetHousekeepingTasksAsync(
            tenantId, request.Date, request.Status, request.Page, request.PageSize, cancellationToken);

        var result = tasks.Select(t => new HousekeepingTaskDto
        {
            Id = t.Id,
            TenantId = t.TenantId,
            RoomNumber = t.RoomNumber,
            AccommodationId = t.AccommodationId,
            TaskType = t.TaskType,
            Priority = t.Priority,
            Status = t.Status,
            AssignedTo = t.AssignedTo,
            AssignedToName = t.AssignedToName,
            Description = t.Description,
            Notes = t.Notes,
            ScheduledDate = t.ScheduledDate,
            StartedAt = t.StartedAt,
            CompletedAt = t.CompletedAt,
            CompletedBy = t.CompletedBy,
            InspectionRating = t.InspectionRating,
            InspectionNotes = t.InspectionNotes,
            CreatedAt = t.CreatedAt
        }).ToList();

        // Apply priority filter in-memory if specified (repository may not support it)
        if (!string.IsNullOrWhiteSpace(request.Priority))
        {
            result = result.Where(t => t.Priority.Equals(request.Priority, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        _logger.LogInformation("Retrieved {Count} housekeeping tasks", result.Count);

        return result;
    }
}
