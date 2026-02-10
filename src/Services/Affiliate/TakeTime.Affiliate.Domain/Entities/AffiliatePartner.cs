using TakeTime.Core.Domain.Base;
using TakeTime.Core.Domain.ValueObjects;
using TakeTime.Affiliate.Domain.Enums;

namespace TakeTime.Affiliate.Domain.Entities;

/// <summary>
/// Aggregate root representing an affiliate partner who earns commissions
/// by referring guests to the property.
/// </summary>
public class AffiliatePartner : AggregateRoot
{
    /// <summary>
    /// Unique affiliate code for tracking referrals.
    /// </summary>
    public string AffiliateCode { get; private set; } = string.Empty;

    /// <summary>
    /// Affiliate's first name.
    /// </summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>
    /// Affiliate's last name.
    /// </summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>
    /// Full name for display purposes.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}";

    /// <summary>
    /// Contact email address.
    /// </summary>
    public string Email { get; private set; } = string.Empty;

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string Phone { get; private set; } = string.Empty;

    /// <summary>
    /// Bank account number for commission payments.
    /// </summary>
    public string? BankAccount { get; private set; }

    /// <summary>
    /// Commission rate as a percentage (e.g., 10 for 10%).
    /// </summary>
    public decimal CommissionRate { get; private set; }

    /// <summary>
    /// Current status of the affiliate.
    /// </summary>
    public AffiliateStatus Status { get; private set; }

    /// <summary>
    /// Total earnings accumulated over the lifetime.
    /// </summary>
    public Money TotalEarnings { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Total amount paid out to the affiliate.
    /// </summary>
    public Money TotalPaid { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Current outstanding balance (TotalEarnings - TotalPaid).
    /// </summary>
    public Money Balance { get; private set; } = Money.ZeroBaht();

    /// <summary>
    /// Required by EF Core.
    /// </summary>
    private AffiliatePartner() { }

    /// <summary>
    /// Creates a new affiliate partner.
    /// </summary>
    public AffiliatePartner(
        string affiliateCode,
        string firstName,
        string lastName,
        string email,
        string phone,
        decimal commissionRate,
        string? bankAccount = null)
    {
        if (string.IsNullOrWhiteSpace(affiliateCode))
            throw new ArgumentException("Affiliate code is required.", nameof(affiliateCode));

        if (string.IsNullOrWhiteSpace(firstName))
            throw new ArgumentException("First name is required.", nameof(firstName));

        if (string.IsNullOrWhiteSpace(lastName))
            throw new ArgumentException("Last name is required.", nameof(lastName));

        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required.", nameof(email));

        if (commissionRate <= 0 || commissionRate > 100)
            throw new ArgumentException("Commission rate must be between 0 and 100.", nameof(commissionRate));

        AffiliateCode = affiliateCode;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        CommissionRate = commissionRate;
        BankAccount = bankAccount;
        Status = AffiliateStatus.Active;
    }

    /// <summary>
    /// Updates the affiliate's profile information.
    /// </summary>
    public void UpdateProfile(
        string firstName,
        string lastName,
        string email,
        string phone,
        string? bankAccount = null)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        Phone = phone;
        BankAccount = bankAccount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the commission rate.
    /// </summary>
    public void SetCommissionRate(decimal rate)
    {
        if (rate <= 0 || rate > 100)
            throw new ArgumentException("Commission rate must be between 0 and 100.", nameof(rate));

        CommissionRate = rate;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a commission earning.
    /// </summary>
    public void RecordEarning(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Earning amount must be positive.", nameof(amount));

        TotalEarnings = TotalEarnings + amount;
        Balance = TotalEarnings - TotalPaid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a payment to the affiliate.
    /// </summary>
    public void RecordPayment(Money amount)
    {
        if (amount.Amount <= 0)
            throw new ArgumentException("Payment amount must be positive.", nameof(amount));

        if (amount > Balance)
            throw new InvalidOperationException(
                $"Payment amount ({amount}) exceeds outstanding balance ({Balance}).");

        TotalPaid = TotalPaid + amount;
        Balance = TotalEarnings - TotalPaid;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Suspends the affiliate.
    /// </summary>
    public void Suspend()
    {
        Status = AffiliateStatus.Suspended;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Deactivates the affiliate.
    /// </summary>
    public void Deactivate()
    {
        Status = AffiliateStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reactivates the affiliate.
    /// </summary>
    public void Activate()
    {
        Status = AffiliateStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}
