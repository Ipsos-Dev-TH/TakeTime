namespace TakeTime.GuestExperience.Application.DTOs;

/// <summary>
/// DTO representing a housekeeping task.
/// </summary>
public sealed class HousekeepingTaskDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RoomNumber { get; set; } = string.Empty;
    public Guid? AccommodationId { get; set; }
    public string TaskType { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = string.Empty;
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
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// DTO for creating a new housekeeping task.
/// </summary>
public sealed class CreateHousekeepingTaskDto
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
/// DTO for housekeeping dashboard summary.
/// </summary>
public sealed class HousekeepingDashboardDto
{
    public DateTime Date { get; set; }
    public int TotalRooms { get; set; }
    public int CleanRooms { get; set; }
    public int DirtyRooms { get; set; }
    public int InProgressRooms { get; set; }
    public int InspectionPendingRooms { get; set; }
    public int OutOfOrderRooms { get; set; }
    public List<HousekeepingTaskDto> PendingTasks { get; set; } = [];
}
