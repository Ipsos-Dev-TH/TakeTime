namespace TakeTime.HumanResources.Application.DTOs;

/// <summary>
/// Full overtime record data transfer object.
/// </summary>
public sealed class OvertimeDto
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string Department { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public decimal TotalHours { get; set; }
    public decimal OTRate { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "THB";
    public string Status { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
