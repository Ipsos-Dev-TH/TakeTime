using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.HumanResources.Domain.Enums;

namespace TakeTime.HumanResources.Domain.Entities;

/// <summary>
/// Tracks overtime worked by an employee for payroll calculation.
/// </summary>
public class OvertimeRecord : Entity
{
    /// <summary>
    /// Identifier of the employee who worked overtime.
    /// </summary>
    public Guid EmployeeId { get; private set; }

    /// <summary>
    /// Date the overtime was worked.
    /// </summary>
    public DateTime Date { get; private set; }

    /// <summary>
    /// Start time of the overtime period.
    /// </summary>
    public TimeSpan StartTime { get; private set; }

    /// <summary>
    /// End time of the overtime period.
    /// </summary>
    public TimeSpan EndTime { get; private set; }

    /// <summary>
    /// Total hours of overtime worked.
    /// </summary>
    public decimal TotalHours { get; private set; }

    /// <summary>
    /// Overtime rate multiplier (e.g., 1.5 for time-and-a-half).
    /// </summary>
    public decimal OTRate { get; private set; }

    /// <summary>
    /// Calculated overtime amount.
    /// </summary>
    public Money Amount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Approval status of the overtime record.
    /// </summary>
    public OvertimeStatus Status { get; private set; }

    /// <summary>
    /// Identifier of the approver.
    /// </summary>
    public string? ApprovedBy { get; private set; }

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private OvertimeRecord() { }

    /// <summary>
    /// Creates a new overtime record.
    /// </summary>
    public OvertimeRecord(
        Guid employeeId,
        DateTime date,
        TimeSpan startTime,
        TimeSpan endTime,
        decimal otRate,
        Money hourlyRate)
    {
        if (endTime <= startTime)
            throw new ArgumentException("End time must be after start time.");

        if (otRate <= 0)
            throw new ArgumentException("OT rate must be positive.", nameof(otRate));

        EmployeeId = employeeId;
        Date = date.Date;
        StartTime = startTime;
        EndTime = endTime;
        OTRate = otRate;
        Status = OvertimeStatus.Pending;

        TotalHours = Math.Round((decimal)(endTime - startTime).TotalHours, 2);
        Amount = Money.InBaht(Math.Round(hourlyRate.Amount * TotalHours * otRate, 2));
    }

    /// <summary>
    /// Approves the overtime record.
    /// </summary>
    public void Approve(string approvedBy)
    {
        if (Status != OvertimeStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot approve overtime record in '{Status}' status.");

        if (string.IsNullOrWhiteSpace(approvedBy))
            throw new ArgumentException("Approved by user is required.", nameof(approvedBy));

        Status = OvertimeStatus.Approved;
        ApprovedBy = approvedBy;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Rejects the overtime record.
    /// </summary>
    public void Reject(string rejectedBy)
    {
        if (Status != OvertimeStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot reject overtime record in '{Status}' status.");

        Status = OvertimeStatus.Rejected;
        ApprovedBy = rejectedBy;
        UpdatedAt = DateTime.UtcNow;
    }
}
