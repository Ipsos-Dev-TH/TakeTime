using TakeTime.Core.Domain.Base;

namespace TakeTime.Payment.Domain.ValueObjects;

/// <summary>
/// Value object representing company information for tax invoices.
/// Contains details required by Thai Revenue Department for tax documentation.
/// </summary>
public class CompanyInfo : ValueObject
{
    /// <summary>
    /// Legal company name.
    /// </summary>
    public string CompanyName { get; }

    /// <summary>
    /// Tax identification number (Thai: เลขประจำตัวผู้เสียภาษี).
    /// </summary>
    public string TaxId { get; }

    /// <summary>
    /// Branch number (Thai: สาขาที่). "00000" for head office.
    /// </summary>
    public string BranchNumber { get; }

    /// <summary>
    /// Full registered address.
    /// </summary>
    public string Address { get; }

    /// <summary>
    /// Contact phone number.
    /// </summary>
    public string? Phone { get; }

    /// <summary>
    /// Contact email address.
    /// </summary>
    public string? Email { get; }

    private CompanyInfo(
        string companyName,
        string taxId,
        string branchNumber,
        string address,
        string? phone,
        string? email)
    {
        CompanyName = companyName;
        TaxId = taxId;
        BranchNumber = branchNumber;
        Address = address;
        Phone = phone;
        Email = email;
    }

    /// <summary>
    /// Creates a new CompanyInfo instance.
    /// </summary>
    public static CompanyInfo Create(
        string companyName,
        string taxId,
        string branchNumber,
        string address,
        string? phone = null,
        string? email = null)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            throw new ArgumentException("Company name is required.", nameof(companyName));

        if (string.IsNullOrWhiteSpace(taxId))
            throw new ArgumentException("Tax ID is required.", nameof(taxId));

        if (string.IsNullOrWhiteSpace(address))
            throw new ArgumentException("Address is required.", nameof(address));

        return new CompanyInfo(companyName, taxId, branchNumber ?? "00000", address, phone, email);
    }

    /// <summary>
    /// Creates a CompanyInfo for a head office.
    /// </summary>
    public static CompanyInfo HeadOffice(
        string companyName,
        string taxId,
        string address,
        string? phone = null,
        string? email = null)
    {
        return Create(companyName, taxId, "00000", address, phone, email);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CompanyName;
        yield return TaxId;
        yield return BranchNumber;
        yield return Address;
        yield return Phone;
        yield return Email;
    }
}
