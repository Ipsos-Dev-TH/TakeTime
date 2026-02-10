using System.Text.RegularExpressions;
using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a Thai address following the standard Thai
/// administrative address structure: Address Line, Subdistrict (Tambon/Khwaeng),
/// District (Amphoe/Khet), Province (Changwat), Postal Code.
/// Defaults to Thailand as the country.
/// </summary>
public partial class Address : ValueObject
{
    /// <summary>
    /// Street address, house number, building, floor, etc.
    /// </summary>
    public string AddressLine { get; }

    /// <summary>
    /// Subdistrict (Tambon / Khwaeng).
    /// </summary>
    public string Subdistrict { get; }

    /// <summary>
    /// District (Amphoe / Khet).
    /// </summary>
    public string District { get; }

    /// <summary>
    /// Province (Changwat), or Bangkok for the capital.
    /// </summary>
    public string Province { get; }

    /// <summary>
    /// Thai postal code (5 digits).
    /// </summary>
    public string PostalCode { get; }

    /// <summary>
    /// Country name. Defaults to "Thailand".
    /// </summary>
    public string Country { get; }

    private Address(
        string addressLine,
        string subdistrict,
        string district,
        string province,
        string postalCode,
        string country)
    {
        AddressLine = addressLine;
        Subdistrict = subdistrict;
        District = district;
        Province = province;
        PostalCode = postalCode;
        Country = country;
    }

    /// <summary>
    /// Creates a new Address with validation.
    /// </summary>
    /// <param name="addressLine">Street address, house number, building, etc.</param>
    /// <param name="subdistrict">Tambon or Khwaeng.</param>
    /// <param name="district">Amphoe or Khet.</param>
    /// <param name="province">Changwat.</param>
    /// <param name="postalCode">5-digit Thai postal code.</param>
    /// <param name="country">Country name (defaults to "Thailand").</param>
    public static Address Create(
        string addressLine,
        string subdistrict,
        string district,
        string province,
        string postalCode,
        string country = "Thailand")
    {
        if (string.IsNullOrWhiteSpace(addressLine))
            throw new ArgumentException("Address line is required.", nameof(addressLine));

        if (string.IsNullOrWhiteSpace(subdistrict))
            throw new ArgumentException("Subdistrict is required.", nameof(subdistrict));

        if (string.IsNullOrWhiteSpace(district))
            throw new ArgumentException("District is required.", nameof(district));

        if (string.IsNullOrWhiteSpace(province))
            throw new ArgumentException("Province is required.", nameof(province));

        if (string.IsNullOrWhiteSpace(postalCode))
            throw new ArgumentException("Postal code is required.", nameof(postalCode));

        postalCode = postalCode.Trim();
        if (!ThaiPostalCodeRegex().IsMatch(postalCode))
        {
            throw new ArgumentException(
                $"'{postalCode}' is not a valid Thai postal code. Expected 5 digits (e.g., 10110).",
                nameof(postalCode));
        }

        return new Address(
            addressLine.Trim(),
            subdistrict.Trim(),
            district.Trim(),
            province.Trim(),
            postalCode,
            string.IsNullOrWhiteSpace(country) ? "Thailand" : country.Trim());
    }

    /// <summary>
    /// Returns a single-line formatted address.
    /// </summary>
    public string ToSingleLine()
    {
        return $"{AddressLine}, {Subdistrict}, {District}, {Province} {PostalCode}, {Country}";
    }

    /// <summary>
    /// Returns a multi-line formatted address suitable for display or printing.
    /// </summary>
    public string ToMultiLine()
    {
        return string.Join(Environment.NewLine,
            AddressLine,
            $"{Subdistrict}, {District}",
            $"{Province} {PostalCode}",
            Country);
    }

    /// <summary>
    /// Returns the address formatted in Thai convention.
    /// </summary>
    public string ToThaiFormat()
    {
        var prefix = Province == "Bangkok" || Province == "กรุงเทพมหานคร"
            ? new { Sub = "แขวง", Dist = "เขต" }
            : new { Sub = "ตำบล", Dist = "อำเภอ" };

        return $"{AddressLine} {prefix.Sub}{Subdistrict} {prefix.Dist}{District} {Province} {PostalCode}";
    }

    public override string ToString() => ToSingleLine();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return AddressLine.ToUpperInvariant();
        yield return Subdistrict.ToUpperInvariant();
        yield return District.ToUpperInvariant();
        yield return Province.ToUpperInvariant();
        yield return PostalCode;
        yield return Country.ToUpperInvariant();
    }

    [GeneratedRegex(@"^\d{5}$")]
    private static partial Regex ThaiPostalCodeRegex();
}
