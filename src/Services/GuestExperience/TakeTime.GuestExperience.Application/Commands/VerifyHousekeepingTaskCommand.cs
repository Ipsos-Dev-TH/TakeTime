using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to verify (inspect) a completed housekeeping task by a supervisor.
/// </summary>
public sealed class VerifyHousekeepingTaskCommand : IRequest<HousekeepingTaskDto>
{
    public Guid TaskId { get; set; }
    public int InspectionRating { get; set; }
    public string? InspectionNotes { get; set; }
}

/// <summary>
/// Handler for <see cref="VerifyHousekeepingTaskCommand"/>. Validates the task is in
/// Completed status, sets it to Verified, and records the inspection details.
/// </summary>
public sealed class VerifyHousekeepingTaskCommandHandler : IRequestHandler<VerifyHousekeepingTaskCommand, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<VerifyHousekeepingTaskCommandHandler> _logger;

    public VerifyHousekeepingTaskCommandHandler(
        IGuestExperienceRepository repository,
        ILogger<VerifyHousekeepingTaskCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(VerifyHousekeepingTaskCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Verifying housekeeping task {TaskId}, rating: {Rating}",
            request.TaskId, request.InspectionRating);

        var task = await _repository.GetHousekeepingTaskByIdAsync(request.TaskId, cancellationToken)
            ?? throw new NotFoundException("HousekeepingTask", request.TaskId);

        if (task.Status != "Completed")
        {
            throw new InvalidOperationException(
                $"Cannot verify housekeeping task in '{task.Status}' status. Task must be in Completed status.");
        }

        task.Status = "Verified";
        task.InspectionRating = request.InspectionRating;
        task.InspectionNotes = request.InspectionNotes;

        await _repository.UpdateHousekeepingTaskAsync(task, cancellationToken);

        _logger.LogInformation(
            "Housekeeping task {TaskId} verified with rating {Rating}",
            request.TaskId, request.InspectionRating);

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
