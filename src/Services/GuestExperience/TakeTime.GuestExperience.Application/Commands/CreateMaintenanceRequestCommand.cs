using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to create a new maintenance request for a room or facility.
/// </summary>
public sealed class CreateMaintenanceRequestCommand : IRequest<MaintenanceRequestDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Normal";
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public decimal? EstimatedCost { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateMaintenanceRequestCommand"/>. Creates a maintenance
/// request with a unique request number, assigns it to maintenance staff if specified,
/// and returns the created request.
/// </summary>
public sealed class CreateMaintenanceRequestCommandHandler : IRequestHandler<CreateMaintenanceRequestCommand, MaintenanceRequestDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CreateMaintenanceRequestCommandHandler> _logger;

    public CreateMaintenanceRequestCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CreateMaintenanceRequestCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<MaintenanceRequestDto> Handle(CreateMaintenanceRequestCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Creating maintenance request for room {RoomNumber}, category {Category}, priority {Priority}",
            request.RoomNumber, request.Category, request.Priority);

        // Generate request number
        var todayCount = await _repository.GetTodayMaintenanceCountAsync(tenantId, cancellationToken);
        var requestNumber = $"MNT-{DateTime.UtcNow:yyyyMMdd}-{(todayCount + 1):D4}";

        var entry = new MaintenanceRequestEntry
        {
            TenantId = tenantId,
            RequestNumber = requestNumber,
            RoomNumber = request.RoomNumber,
            AccommodationId = request.AccommodationId,
            Category = request.Category,
            Priority = request.Priority,
            Status = request.AssignedTo != null ? "Assigned" : "Open",
            Description = request.Description,
            AssignedTo = request.AssignedTo,
            EstimatedCost = request.EstimatedCost
        };

        var id = await _repository.CreateMaintenanceRequestAsync(entry, cancellationToken);

        var currency = _currentTenantService.GetSetting("Currency") ?? "THB";

        _logger.LogInformation(
            "Maintenance request {RequestNumber} created for room {RoomNumber}",
            requestNumber, request.RoomNumber);

        return new MaintenanceRequestDto
        {
            Id = id,
            TenantId = tenantId,
            RequestNumber = requestNumber,
            RoomNumber = request.RoomNumber,
            AccommodationId = request.AccommodationId,
            Category = request.Category,
            Priority = request.Priority,
            Status = entry.Status,
            Description = request.Description,
            AssignedTo = request.AssignedTo,
            EstimatedCost = request.EstimatedCost,
            Currency = currency,
            CreatedAt = entry.CreatedAt
        };
    }
}
