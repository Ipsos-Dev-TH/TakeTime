using TakeTime.Core.Domain.Base;

namespace TakeTime.Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing the details of a bank transfer payment.
/// Used when a guest pays via bank transfer and submits a transfer slip.
/// </summary>
public class BankTransferDetails : ValueObject
{
    /// <summary>
    /// Name of the bank used for the transfer.
    /// </summary>
    public string BankName { get; }

    /// <summary>
    /// Bank account number the transfer was made from.
    /// </summary>
    public string AccountNumber { get; }

    /// <summary>
    /// Date of the transfer.
    /// </summary>
    public DateTime TransferDate { get; }

    /// <summary>
    /// Time of the transfer.
    /// </summary>
    public TimeSpan TransferTime { get; }

    /// <summary>
    /// Amount transferred.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Bank reference or transaction number.
    /// </summary>
    public string? ReferenceNumber { get; }

    private BankTransferDetails(
        string bankName,
        string accountNumber,
        DateTime transferDate,
        TimeSpan transferTime,
        decimal amount,
        string? referenceNumber)
    {
        BankName = bankName;
        AccountNumber = accountNumber;
        TransferDate = transferDate;
        TransferTime = transferTime;
        Amount = amount;
        ReferenceNumber = referenceNumber;
    }

    /// <summary>
    /// Creates a new BankTransferDetails instance.
    /// </summary>
    public static BankTransferDetails Create(
        string bankName,
        string accountNumber,
        DateTime transferDate,
        TimeSpan transferTime,
        decimal amount,
        string? referenceNumber = null)
    {
        if (string.IsNullOrWhiteSpace(bankName))
            throw new ArgumentException("Bank name is required.", nameof(bankName));

        if (string.IsNullOrWhiteSpace(accountNumber))
            throw new ArgumentException("Account number is required.", nameof(accountNumber));

        if (amount <= 0)
            throw new ArgumentException("Transfer amount must be positive.", nameof(amount));

        return new BankTransferDetails(bankName, accountNumber, transferDate, transferTime, amount, referenceNumber);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BankName;
        yield return AccountNumber;
        yield return TransferDate;
        yield return TransferTime;
        yield return Amount;
        yield return ReferenceNumber;
    }
}
