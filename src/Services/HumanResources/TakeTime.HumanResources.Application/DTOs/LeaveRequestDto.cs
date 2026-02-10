namespace TakeTime.HumanResources.Application.DTOs;

/// <summary>
/// Full leave request data transfer object.
/// </summary>
public sealed class LeaveRequestDto
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string RequestNumber { get; set; } = string.Empty;
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string Department { get; set; } = string.Empty;
    public string LeaveType { get; set; } = string.Empty; // Annual, Sick, Personal, Maternity, Unpaid, etc.
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal TotalDays { get; set; }
    public bool IsHalfDay { get; set; }
    public string? HalfDayPeriod { get; set; } // Morning, Afternoon
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = string.Empty; // Pending, Approved, Rejected, Cancelled
    public Guid? ApproverId { get; set; }
    public string? ApproverName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApproverNotes { get; set; }
    public string? RejectionReason { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

/// <summary>
/// DTO for submitting a new leave request.
/// </summary>
public sealed class CreateLeaveRequestDto
{
    public Guid EmployeeId { get; set; }
    public string LeaveType { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsHalfDay { get; set; }
    public string? HalfDayPeriod { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
}

/// <summary>
/// DTO for approving/rejecting a leave request.
/// </summary>
public sealed class ApproveLeaveRequestDto
{
    public Guid RequestId { get; set; }
    public bool IsApproved { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Leave balance summary for an employee.
/// </summary>
public sealed class LeaveBalanceDto
{
    public Guid EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public List<LeaveTypeBalanceDto> Balances { get; set; } = [];
}

/// <summary>
/// Balance for a specific leave type.
/// </summary>
public sealed class LeaveTypeBalanceDto
{
    public string LeaveType { get; set; } = string.Empty;
    public decimal Entitled { get; set; }
    public decimal Used { get; set; }
    public decimal Pending { get; set; }
    public decimal Remaining { get; set; }
}
