using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.Core.Exceptions;
using TakeTime.GuestExperience.Application.Commands;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Queries;

/// <summary>
/// Query to retrieve a specific maintenance request by its unique identifier.
/// </summary>
public sealed class GetMaintenanceRequestByIdQuery : IRequest<MaintenanceRequestDto>
{
    public Guid Id { get; set; }
}

/// <summary>
/// Handler for <see cref="GetMaintenanceRequestByIdQuery"/>. Retrieves a single
/// maintenance request from the repository and maps it to a DTO.
/// </summary>
public sealed class GetMaintenanceRequestByIdQueryHandler : IRequestHandler<GetMaintenanceRequestByIdQuery, MaintenanceRequestDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<GetMaintenanceRequestByIdQueryHandler> _logger;

    public GetMaintenanceRequestByIdQueryHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<GetMaintenanceRequestByIdQueryHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MaintenanceRequestDto> Handle(GetMaintenanceRequestByIdQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving maintenance request {RequestId}", request.Id);

        var entry = await _repository.GetMaintenanceRequestByIdAsync(request.Id, cancellationToken)
            ?? throw new NotFoundException("MaintenanceRequest", request.Id);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

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
