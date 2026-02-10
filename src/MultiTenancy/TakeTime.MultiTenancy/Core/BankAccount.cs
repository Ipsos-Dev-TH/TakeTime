using TakeTime.Core.Domain.Base;

namespace TakeTime.MultiTenancy.Core;

/// <summary>
/// Represents a bank account associated with a tenant for payment processing.
/// Modeled as a value object because it has no independent identity.
/// </summary>
public class BankAccount : ValueObject
{
    /// <summary>
    /// Name of the bank (e.g., "Bangkok Bank", "Kasikorn Bank").
    /// </summary>
    public string BankName { get; private set; } = string.Empty;

    /// <summary>
    /// The bank account number.
    /// </summary>
    public string AccountNumber { get; private set; } = string.Empty;

    /// <summary>
    /// The name on the bank account.
    /// </summary>
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>
    /// The branch name or code where the account was opened.
    /// </summary>
    public string? BranchName { get; private set; }

    /// <summary>
    /// Whether this is the default/primary bank account for the tenant.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>
    /// SWIFT/BIC code for international transfers.
    /// </summary>
    public string? SwiftCode { get; private set; }

    /// <summary>
    /// Type of account (savings, current, etc.).
    /// </summary>
    public string? AccountType { get; private set; }

    private BankAccount() { }

    public BankAccount(
        string bankName,
        string accountNumber,
        string accountName,
        string? branchName = null,
        bool isDefault = false,
        string? swiftCode = null,
        string? accountType = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);

        BankName = bankName;
        AccountNumber = accountNumber;
        AccountName = accountName;
        BranchName = branchName;
        IsDefault = isDefault;
        SwiftCode = swiftCode;
        AccountType = accountType;
    }

    public BankAccount WithDefault(bool isDefault)
    {
        return new BankAccount(BankName, AccountNumber, AccountName, BranchName, isDefault, SwiftCode, AccountType);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return BankName;
        yield return AccountNumber;
        yield return AccountName;
        yield return BranchName;
        yield return IsDefault;
        yield return SwiftCode;
        yield return AccountType;
    }
}
