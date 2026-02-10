using MediatR;
using Microsoft.Extensions.Logging;
using TakeTime.Core.Application.Interfaces;
using TakeTime.GuestExperience.Application.DTOs;

namespace TakeTime.GuestExperience.Application.Commands;

/// <summary>
/// Command to create a new housekeeping task for a room.
/// </summary>
public sealed class CreateHousekeepingTaskCommand : IRequest<HousekeepingTaskDto>
{
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string TaskType { get; set; } = "CleanAndPrep";
    public string Priority { get; set; } = "Normal";
    public string? AssignedTo { get; set; }
    public string? Description { get; set; }
    public DateTime? ScheduledDate { get; set; }
}

/// <summary>
/// Handler for <see cref="CreateHousekeepingTaskCommand"/>. Creates a new housekeeping
/// task, assigns it to staff if specified, and returns the created task.
/// </summary>
public sealed class CreateHousekeepingTaskCommandHandler : IRequestHandler<CreateHousekeepingTaskCommand, HousekeepingTaskDto>
{
    private readonly IGuestExperienceRepository _repository;
    private readonly ICurrentTenantService _currentTenantService;
    private readonly ILogger<CreateHousekeepingTaskCommandHandler> _logger;

    public CreateHousekeepingTaskCommandHandler(
        IGuestExperienceRepository repository,
        ICurrentTenantService currentTenantService,
        ILogger<CreateHousekeepingTaskCommandHandler> logger)
    {
        _repository = repository;
        _currentTenantService = currentTenantService;
        _logger = logger;
    }

    public async Task<HousekeepingTaskDto> Handle(CreateHousekeepingTaskCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentTenantService.TenantId
            ?? throw new InvalidOperationException("Tenant context is required.");

        _logger.LogInformation(
            "Creating housekeeping task for room {RoomNumber}, type {TaskType}",
            request.RoomNumber, request.TaskType);

        var task = new HousekeepingTaskEntry
        {
            TenantId = tenantId,
            RoomNumber = request.RoomNumber,
            AccommodationId = request.AccommodationId,
            TaskType = request.TaskType,
            Priority = request.Priority,
            Status = request.AssignedTo != null ? "Assigned" : "Pending",
            AssignedTo = request.AssignedTo,
            Description = request.Description,
            ScheduledDate = request.ScheduledDate ?? DateTime.UtcNow.Date
        };

        var id = await _repository.CreateHousekeepingTaskAsync(task, cancellationToken);

        _logger.LogInformation(
            "Housekeeping task created for room {RoomNumber} with status {Status}",
            request.RoomNumber, task.Status);

        return new HousekeepingTaskDto
        {
            Id = id,
            TenantId = tenantId,
            RoomNumber = request.RoomNumber,
            AccommodationId = request.AccommodationId,
            TaskType = request.TaskType,
            Priority = request.Priority,
            Status = task.Status,
            AssignedTo = request.AssignedTo,
            Description = request.Description,
            ScheduledDate = task.ScheduledDate,
            CreatedAt = task.CreatedAt
        };
    }
}

/// <summary>
/// Repository interface for guest experience operations.
/// </summary>
public interface IGuestExperienceRepository
{
    // Housekeeping
    Task<Guid> CreateHousekeepingTaskAsync(HousekeepingTaskEntry task, CancellationToken cancellationToken = default);
    Task UpdateHousekeepingTaskAsync(HousekeepingTaskEntry task, CancellationToken cancellationToken = default);
    Task<HousekeepingTaskEntry?> GetHousekeepingTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<HousekeepingTaskEntry>> GetHousekeepingTasksAsync(string tenantId, DateTime? date, string? status, int page, int pageSize, CancellationToken cancellationToken = default);

    // Room Service
    Task<Guid> CreateRoomServiceOrderAsync(RoomServiceOrderEntry order, CancellationToken cancellationToken = default);
    Task UpdateRoomServiceOrderAsync(RoomServiceOrderEntry order, CancellationToken cancellationToken = default);
    Task<RoomServiceOrderEntry?> GetRoomServiceOrderByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoomServiceOrderEntry>> GetRoomServiceOrdersAsync(string tenantId, string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTodayOrderCountAsync(string tenantId, CancellationToken cancellationToken = default);

    // Maintenance
    Task<Guid> CreateMaintenanceRequestAsync(MaintenanceRequestEntry request, CancellationToken cancellationToken = default);
    Task UpdateMaintenanceRequestAsync(MaintenanceRequestEntry request, CancellationToken cancellationToken = default);
    Task<MaintenanceRequestEntry?> GetMaintenanceRequestByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MaintenanceRequestEntry>> GetMaintenanceRequestsAsync(string tenantId, string? status, string? priority, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> GetTodayMaintenanceCountAsync(string tenantId, CancellationToken cancellationToken = default);
}

public sealed class HousekeepingTaskEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Pending";
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public int? InspectionRating { get; set; }
    public string? InspectionNotes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class RoomServiceOrderEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? ReservationId { get; set; }
    public string? GuestName { get; set; }
    public string Status { get; set; } = "Pending";
    public decimal SubTotal { get; set; }
    public decimal ServiceCharge { get; set; }
    public decimal VATAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "THB";
    public bool ChargeToRoom { get; set; }
    public string? SpecialInstructions { get; set; }
    public DateTime? EstimatedDeliveryTime { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<RoomServiceItemEntry> Items { get; set; } = [];
}

public sealed class RoomServiceItemEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public string? SpecialRequest { get; set; }
}

public sealed class MaintenanceRequestEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string TenantId { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Open";
    public string Description { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public string? ResolutionNotes { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
