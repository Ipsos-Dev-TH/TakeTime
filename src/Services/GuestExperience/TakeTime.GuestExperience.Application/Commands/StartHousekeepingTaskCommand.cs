using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to mark a housekeeping task as started (in progress).
/// </summary>
public sealed class StartHousekeepingTaskCommand : IRequest<HousekeepingTaskDto>
{
    public Guid TaskId { get; set; }
}

/// <summary>
/// Handler for <see cref="StartHousekeepingTaskCommand"/>. Validates the task is in
/// Assigned status, sets it to InProgress, and records the StartedAt timestamp.
/// </summary>
public sealed class StartHousekeepingTaskCommandHandler : IRequestHandler<StartHousekeepingTaskCommand, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<StartHousekeepingTaskCommandHandler> _logger;

    public StartHousekeepingTaskCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<StartHousekeepingTaskCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(StartHousekeepingTaskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting housekeeping task {TaskId}", request.TaskId);

        var task = await _repository.GetHousekeepingTaskByIdAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("HousekeepingTask", request.TaskId);

        if (task.Status != "Assigned" && task.Status != "Pending")
        {
            throw new InvalidOperationException(
                $"Cannot start housekeeping task in '{task.Status}' status. Task must be in Assigned or Pending status.");
        }

        task.Status = "InProgress";
        task.StartedAt = DateTime.UtcNow;

        await _repository.UpdateHousekeepingTaskAsync(task, cancellationToken);

        _logger.LogInformation("Housekeeping task {TaskId} started at {StartedAt}", request.TaskId, task.StartedAt);

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
