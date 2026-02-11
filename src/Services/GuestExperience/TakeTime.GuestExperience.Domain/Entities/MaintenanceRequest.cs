using TakeTime.Core.Domain.Base;

namespace TakeTime.GuestExperience.Domain.Entities;

public class MaintenanceRequest : Entity
{
    public string RequestNumber { get; set; } = string.Empty;
    public Guid AccommodationId { get; set; }
    public string RoomNumber { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty; // Plumbing, Electrical, HVAC, Furniture, Other
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Critical
    public string Status { get; set; } = "Reported"; // Reported, Assigned, InProgress, Completed, Closed
    public Guid? AssignedTo { get; set; }
    public string? AssignedToName { get; set; }
    public string? ReportedBy { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public string? ResolutionNotes { get; set; }
    public decimal? EstimatedCost { get; set; }
    public decimal? ActualCost { get; set; }
    public List<string> Photos { get; set; } = [];

    public void Assign(Guid employeeId, string name)
    {
        AssignedTo = employeeId;
        AssignedToName = name;
        Status = "Assigned";
    }

    public void Resolve(string resolvedBy, string? notes = null)
    {
        Status = "Completed";
        ResolvedAt = DateTime.UtcNow;
        ResolvedBy = resolvedBy;
        ResolutionNotes = notes;
    }
}
