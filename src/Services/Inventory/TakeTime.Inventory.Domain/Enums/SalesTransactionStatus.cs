namespace TakeTime.Inventory.Domain.Enums;

/// <summary>
/// Status of a POS sales transaction.
/// </summary>
public enum SalesTransactionStatus
{
    /// <summary>
    /// Transaction is in progress.
    /// </summary>
    InProgress = 0,

    /// <summary>
    /// Transaction has been completed.
    /// </summary>
    Completed = 1,

    /// <summary>
    /// Transaction has been voided.
    /// </summary>
    Voided = 2,

    /// <summary>
    /// Transaction has been refunded.
    /// </summary>
    Refunded = 3
}
