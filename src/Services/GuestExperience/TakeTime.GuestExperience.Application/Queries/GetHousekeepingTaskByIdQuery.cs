using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve a specific housekeeping task by its unique identifier.
/// </summary>
public sealed class GetHousekeepingTaskByIdQuery : IRequest<HousekeepingTaskDto>
{
    public Guid Id { get; set; }
}

/// <summary>
/// Handler for <see cref="GetHousekeepingTaskByIdQuery"/>. Retrieves a single
/// housekeeping task from the repository and maps it to a DTO.
/// </summary>
public sealed class GetHousekeepingTaskByIdQueryHandler : IRequestHandler<GetHousekeepingTaskByIdQuery, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ILogger<GetHousekeepingTaskByIdQueryHandler> _logger;

    public GetHousekeepingTaskByIdQueryHandler(
        IGuestExperienceRepository repository,
        ILogger<GetHousekeepingTaskByIdQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(GetHousekeepingTaskByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving housekeeping task {TaskId}", request.Id);

        var task = await _repository.GetHousekeepingTaskByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("HousekeepingTask", request.Id);

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
