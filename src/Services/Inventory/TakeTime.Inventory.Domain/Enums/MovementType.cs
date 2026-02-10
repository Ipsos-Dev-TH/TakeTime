namespace TakeTime.Inventory.Domain.Enums;

/// <summary>
/// Types of stock movements for inventory tracking.
/// </summary>
public enum MovementType
{
    /// <summary>
    /// Stock received (purchase, return, etc.).
    /// </summary>
    In = 0,

    /// <summary>
    /// Stock issued (sale, usage, etc.).
    /// </summary>
    Out = 1,

    /// <summary>
    /// Manual stock adjustment (correction, write-off, etc.).
    /// </summary>
    Adjustment = 2
}
