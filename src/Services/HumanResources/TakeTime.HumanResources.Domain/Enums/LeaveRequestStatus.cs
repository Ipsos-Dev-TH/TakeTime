namespace TakeTime.HumanResources.Domain.Enums;

/// <summary>
/// Status of a leave request.
/// </summary>
public enum LeaveRequestStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
