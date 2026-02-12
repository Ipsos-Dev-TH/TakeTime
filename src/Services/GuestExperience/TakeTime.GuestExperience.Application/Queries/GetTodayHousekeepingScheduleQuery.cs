using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve today's housekeeping schedule with summary statistics.
/// Returns tasks grouped by status with room status counts.
/// </summary>
public sealed class GetTodayHousekeepingScheduleQuery : IRequest<HousekeepingDashboardDto>
{
}

/// <summary>
/// Handler for <see cref="GetTodayHousekeepingScheduleQuery"/>. Retrieves today's
/// housekeeping tasks and calculates room status summary statistics.
/// </summary>
public sealed class GetTodayHousekeepingScheduleQueryHandler : IRequestHandler<GetTodayHousekeepingScheduleQuery, HousekeepingDashboardDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetTodayHousekeepingScheduleQueryHandler> _logger;

    public GetTodayHousekeepingScheduleQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetTodayHousekeepingScheduleQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<HousekeepingDashboardDto> Handle(GetTodayHousekeepingScheduleQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        var today = DateTime.UtcNow.Date;

        _logger.LogInformation(
            "Retrieving today's housekeeping schedule for tenant {TenantId} on {Date}",
            tenantId, today.ToString("yyyy-MM-dd"));

        // Retrieve all tasks for today with a large page size to get all
        var tasks = await _repository.GetHousekeepingTasksAsync(
            tenantId, today, null, 1, 1000, cancellationToken);

        // Calculate room status counts
        var pendingTasks = tasks.Where(t => t.Status == "Pending").ToList();
        var assignedTasks = tasks.Where(t => t.Status == "Assigned").ToList();
        var inProgressTasks = tasks.Where(t => t.Status == "InProgress").ToList();
        var completedTasks = tasks.Where(t => t.Status == "Completed").ToList();
        var verifiedTasks = tasks.Where(t => t.Status == "Verified").ToList();

        // Dirty = Pending + Assigned (not started)
        var dirtyRooms = pendingTasks.Count + assignedTasks.Count;
        // In progress = currently being cleaned
        var inProgressRooms = inProgressTasks.Count;
        // Inspection pending = completed but not yet verified
        var inspectionPendingRooms = completedTasks.Count;
        // Clean = verified
        var cleanRooms = verifiedTasks.Count;

        var totalRooms = tasks.Count;

        var pendingTaskDtos = tasks
            .Where(t => t.Status != "Verified")
            .Select(t => new HousekeepingTaskDto
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

        _logger.LogInformation(
            "Today's schedule: {Total} total, {Dirty} dirty, {InProgress} in-progress, {Inspection} inspection pending, {Clean} clean",
            totalRooms, dirtyRooms, inProgressRooms, inspectionPendingRooms, cleanRooms);

        return new HousekeepingDashboardDto
        {
            Date = today,
            TotalRooms = totalRooms,
            CleanRooms = cleanRooms,
            DirtyRooms = dirtyRooms,
            InProgressRooms = inProgressRooms,
            InspectionPendingRooms = inspectionPendingRooms,
            OutOfOrderRooms = 0,
            PendingTasks = pendingTaskDtos
        };
    }
}
