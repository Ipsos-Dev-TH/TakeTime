using TakeTime.Core.Domain.Base;
using TakeTime.HumanResources.Domain.Enums;

namespace TakeTime.HumanResources.Domain.Entities;

/// <summary>
/// Aggregate root representing a leave request submitted by an employee.
/// Supports the approval workflow: Pending -> Approved/Rejected -> Cancelled.
/// </summary>
public class LeaveRequest : AggregateRoot
{
    /// <summary>
    /// Identifier of the employee requesting leave.
    /// </summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Type of leave being requested.
    /// </summary>
    public LeaveType LeaveType { get; private set; }

    /// <summary>
    /// Start date of the leave period.
    /// </summary>
    public DateTime StartDate { get; private set; }

    /// <summary>
    /// End date of the leave period (inclusive).
    /// </summary>
    public DateTime EndDate { get; private set; }

    /// <summary>
    /// Total number of leave days.
    /// </summary>
    public decimal TotalDays { get; private set; }

    /// <summary>
    /// Reason for the leave request.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>
    /// Current status of the leave request.
    /// </summary>
    public LeaveRequestStatus Status { get; private set; }

    /// <summary>
    /// Identifier of the person who approved or rejected the request.
    /// </summary>
    public string? ApprovedBy { get; private set; }

    /// <summary>
    /// Timestamp when the request was approved or rejected.
    /// </summary>
    public DateTime? ApprovedAt { get; private set; }

    /// <summary>
    /// Rejection reason, if the request was rejected.
    /// </summary>
    public string? RejectionReason { get; private set; }

    /// <summary>
    /// Identifier of the employee covering for the person on leave.
    /// </summary>
    public Guid? ReplacementEmployeeId { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private LeaveRequest() { }

    /// <summary>
    /// Creates a new leave request.
    /// </summary>
    public LeaveRequest(
        Guid employeeId,
        LeaveType leaveType,
        DateTime startDate,
        DateTime endDate,
        string? reason = null,
        Guid? replacementEmployeeId = null)
    {
        if (endDate < startDate)
            throw new ArgumentException("End date must be on or after start date.");

        EmployeeId = employeeId;
        LeaveType = leaveType;
        StartDate = startDate.Date;
        EndDate = endDate.Date;
        TotalDays = CalculateLeaveDays(startDate, endDate);
        Reason = reason;
        ReplacementEmployeeId = replacementEmployeeId;
        Status = LeaveRequestStatus.Pending;
    }

    /// <summary>
    /// Approves the leave request.
    /// </summary>
    public void Approve(string approvedBy)
    {
        if (Status != LeaveRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve leave request in '{Status}' status. Only Pending requests can be approved.");

        if (string.IsNullOrWhiteSpace(approvedBy))
            throw new ArgumentException("Approved by user is required.", nameof(approvedBy));

        Status = LeaveRequestStatus.Approved;
        ApprovedBy = approvedBy;
        ApprovedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rejects the leave request with a reason.
    /// </summary>
    public void Reject(string rejectedBy, string reason)
    {
        if (Status != LeaveRequestStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject leave request in '{Status}' status. Only Pending requests can be rejected.");

        if (string.IsNullOrWhiteSpace(rejectedBy))
            throw new ArgumentException("Rejected by user is required.", nameof(rejectedBy));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Rejection reason is required.", nameof(reason));

        Status = LeaveRequestStatus.Rejected;
        ApprovedBy = rejectedBy;
        ApprovedAt = DateTime.UtcNow;
        RejectionReason = reason;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancels the leave request.
    /// </summary>
    public void Cancel()
    {
        if (Status == LeaveRequestStatus.Cancelled)
            throw new InvalidOperationException("Leave request is already cancelled.");

        if (Status == LeaveRequestStatus.Rejected)
            throw new InvalidOperationException("Cannot cancel a rejected leave request.");

        Status = LeaveRequestStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
    }

    private static decimal CalculateLeaveDays(DateTime startDate, DateTime endDate)
    {
        // Simple calculation: count calendar days (can be enhanced for half-days, weekends, etc.)
        return (endDate.Date - startDate.Date).Days + 1;
    }
}
