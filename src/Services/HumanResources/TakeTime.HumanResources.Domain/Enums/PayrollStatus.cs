namespace TakeTime.HumanResources.Domain.Enums;

/// <summary>
/// Status of a payroll record through its lifecycle.
/// </summary>
public enum PayrollStatus
{
    Draft = 0,
    Calculated = 1,
    Approved = 2,
    Paid = 3
}
