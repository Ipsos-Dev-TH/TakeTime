using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Accounting.Domain.Enums;

namespace TakeTime.Accounting.Domain.Entities;

/// <summary>
/// Aggregate root representing a financial transaction for accounting purposes.
/// Tracks all income and expenses with references to source entities.
/// </summary>
public class FinancialTransaction : AggregateRoot
{
    /// <summary>
    /// Auto-generated unique transaction number.
    /// </summary>
    public string TransactionNumber { get; private set; } = string.Empty;

    /// <summary>
    /// Type of financial transaction (Income or Expense).
    /// </summary>
    public TransactionType Type { get; private set; }

    /// <summary>
    /// Accounting category (e.g., "Room Revenue", "F&B Revenue", "Utilities", "Wages").
    /// </summary>
    public string Category { get; private set; } = string.Empty;

    /// <summary>
    /// Transaction amount.
    /// </summary>
    public Money Amount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// VAT amount included in the transaction.
    /// </summary>
    public Money VATAmount { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Description of the transaction.
    /// </summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Type of the source entity (e.g., "Payment", "SalesTransaction", "Payroll").
    /// </summary>
    public string? ReferenceType { get; private set; }

    /// <summary>
    /// Identifier of the source entity.
    /// </summary>
    public Guid? ReferenceId { get; private set; }

    /// <summary>
    /// Date of the transaction.
    /// </summary>
    public DateTime TransactionDate { get; private set; }

    /// <summary>
    /// Identifier of the user who posted the transaction.
    /// </summary>
    public string PostedBy { get; private set; } = string.Empty;

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private FinancialTransaction() { }

    /// <summary>
    /// Creates a new financial transaction.
    /// </summary>
    public FinancialTransaction(
        TransactionType type,
        string category,
        Money amount,
        string postedBy,
        DateTime transactionDate,
        Money? vatAmount = null,
        string? description = null,
        string? referenceType = null,
        Guid? referenceId = null)
    {
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.", nameof(category));

        if (string.IsNullOrWhiteSpace(postedBy))
            throw new ArgumentException("Posted by user is required.", nameof(postedBy));

        Type = type;
        Category = category;
        Amount = amount;
        VATAmount = vatAmount ?? Money.ZeroBaht();
        Description = description;
        ReferenceType = referenceType;
        ReferenceId = referenceId;
        TransactionDate = transactionDate.Date;
        PostedBy = postedBy;

        TransactionNumber = GenerateTransactionNumber(type);
    }

    /// <summary>
    /// Creates an income transaction.
    /// </summary>
    public static FinancialTransaction RecordIncome(
        string category, Money amount, string postedBy, DateTime transactionDate,
        Money? vatAmount = null, string? description = null,
        string? referenceType = null, Guid? referenceId = null)
    {
        return new FinancialTransaction(
            TransactionType.Income, category, amount, postedBy, transactionDate,
            vatAmount, description, referenceType, referenceId);
    }

    /// <summary>
    /// Creates an expense transaction.
    /// </summary>
    public static FinancialTransaction RecordExpense(
        string category, Money amount, string postedBy, DateTime transactionDate,
        Money? vatAmount = null, string? description = null,
        string? referenceType = null, Guid? referenceId = null)
    {
        return new FinancialTransaction(
            TransactionType.Expense, category, amount, postedBy, transactionDate,
            vatAmount, description, referenceType, referenceId);
    }

    private static string GenerateTransactionNumber(TransactionType type)
    {
        var prefix = type == TransactionType.Income ? "INC" : "EXP";
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(1000, 9999);
        return $"{prefix}-{timestamp}-{random}";
    }
}
