using TakeTime.Core.Domain.Base;

namespace TakeTime.GuestExperience.Domain.Entities;

public class HousekeepingTask : Entity
{
    public Guid AccommodationId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string TaskType { get; set; } = "Cleaning"; // Cleaning, TurnDown, DeepClean, Inspection
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed, Verified
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public DateTime? ScheduledAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }
    public string? Notes { get; set; }

    public void Assign(Guid employeeId, string employeeName)
    {
        AssignedTo = employeeId;
        AssignedToName = employeeName;
        Status = "Pending";
    }

    public void Start()
    {
        Status = "InProgress";
        StartedAt = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status = "Completed";
        CompletedAt = DateTime.UtcNow;
    }

    public void Verify(string verifiedBy)
    {
        Status = "Verified";
        VerifiedAt = DateTime.UtcNow;
        VerifiedBy = verifiedBy;
    }
}
