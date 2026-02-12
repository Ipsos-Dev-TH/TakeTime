using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to mark a housekeeping task as completed.
/// </summary>
public sealed class CompleteHousekeepingTaskCommand : IRequest<HousekeepingTaskDto>
{
    public Guid TaskId { get; set; }
    public string? Notes { get; set; }
    public string? CompletedBy { get; set; }
}

/// <summary>
/// Handler for <see cref="CompleteHousekeepingTaskCommand"/>. Validates the task is in
/// InProgress status, sets it to Completed, and records the completion details.
/// </summary>
public sealed class CompleteHousekeepingTaskCommandHandler : IRequestHandler<CompleteHousekeepingTaskCommand, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<CompleteHousekeepingTaskCommandHandler> _logger;

    public CompleteHousekeepingTaskCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<CompleteHousekeepingTaskCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(CompleteHousekeepingTaskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Completing housekeeping task {TaskId}", request.TaskId);

        var task = await _repository.GetHousekeepingTaskByIdAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("HousekeepingTask", request.TaskId);

        if (task.Status != "InProgress")
        {
            throw new InvalidOperationException(
                $"Cannot complete housekeeping task in '{task.Status}' status. Task must be in InProgress status.");
        }

        task.Status = "Completed";
        task.CompletedAt = DateTime.UtcNow;
        task.CompletedBy = request.CompletedBy;
        task.Notes = request.Notes ?? task.Notes;

        await _repository.UpdateHousekeepingTaskAsync(task, cancellationToken);

        _logger.LogInformation("Housekeeping task {TaskId} completed at {CompletedAt}", request.TaskId, task.CompletedAt);

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
