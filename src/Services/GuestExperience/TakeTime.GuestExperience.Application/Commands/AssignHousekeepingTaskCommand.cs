using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to assign a housekeeping task to a staff member.
/// </summary>
public sealed class AssignHousekeepingTaskCommand : IRequest<HousekeepingTaskDto>
{
    public Guid TaskId { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }
}

/// <summary>
/// Handler for <see cref="AssignHousekeepingTaskCommand"/>. Validates the task exists,
/// sets the status to Assigned, and records the assignment details.
/// </summary>
public sealed class AssignHousekeepingTaskCommandHandler : IRequestHandler<AssignHousekeepingTaskCommand, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<AssignHousekeepingTaskCommandHandler> _logger;

    public AssignHousekeepingTaskCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<AssignHousekeepingTaskCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(AssignHousekeepingTaskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Assigning housekeeping task {TaskId} to staff {StaffId}",
            request.TaskId, request.AssignedTo);

        var task = await _repository.GetHousekeepingTaskByIdAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("HousekeepingTask", request.TaskId);

        task.AssignedTo = request.AssignedTo;
        task.AssignedToName = request.AssignedToName;
        task.Status = "Assigned";

        await _repository.UpdateHousekeepingTaskAsync(task, cancellationToken);

        _logger.LogInformation(
            "Housekeeping task {TaskId} assigned to {StaffName}",
            request.TaskId, request.AssignedToName ?? request.AssignedTo);

        return new HousekeepingTaskDto
        {
            Id = task.Id,
            TenantId = task.TenantId,
            RoomNumber = task.RoomNumber,
            AccommodationId = task.AccommodationId,
            TaskType = task.TaskType,
            Priority = task.Priority,
            Status = task.Status,
            AssignedTo = task.AssignedTo,
            AssignedToName = task.AssignedToName,
            Description = task.Description,
            Notes = task.Notes,
            ScheduledDate = task.ScheduledDate,
            StartedAt = task.StartedAt,
            CompletedAt = task.CompletedAt,
            CompletedBy = task.CompletedBy,
            InspectionRating = task.InspectionRating,
            InspectionNotes = task.InspectionNotes,
            CreatedAt = task.CreatedAt
        };
    }
}
