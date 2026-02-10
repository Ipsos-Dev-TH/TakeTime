namespace TakeTime.Accounting.Domain.Enums;

/// <summary>
/// Types of financial transactions.
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Revenue or incoming payment.
    /// </summary>
    Income = 0,

    /// <summary>
    /// Cost or outgoing payment.
    /// </summary>
    Expense = 1
}
