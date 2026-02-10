using System.Text.RegularExpressions;
using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a Thai phone number.
/// Validates against Thai mobile format (0x-xxxx-xxxx) where x is the first
/// digit of the operator prefix (e.g., 06, 08, 09 for mobile).
/// Also supports Thai landline formats.
/// </summary>
public partial class PhoneNumber : ValueObject
{
    /// <summary>
    /// The normalized phone number stored without formatting (digits only).
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The original, unmodified input value.
    /// </summary>
    public string OriginalValue { get; }

    private PhoneNumber(string value, string originalValue)
    {
        Value = value;
        OriginalValue = originalValue;
    }

    /// <summary>
    /// Creates a PhoneNumber after validating it against Thai phone formats.
    /// Accepts formats: 0812345678, 08-1234-5678, 081-234-5678, +66812345678, etc.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the phone number is invalid.</exception>
    public static PhoneNumber Create(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));

        var original = phoneNumber.Trim();

        // Remove all non-digit characters except leading +
        var normalized = NormalizePhoneNumber(original);

        if (!IsValidThaiPhoneNumber(normalized))
        {
            throw new ArgumentException(
                $"'{original}' is not a valid Thai phone number. " +
                "Expected formats: 0x-xxxx-xxxx (mobile) or 0x-xxx-xxxx (landline).",
                nameof(phoneNumber));
        }

        return new PhoneNumber(normalized, original);
    }

    /// <summary>
    /// Attempts to create a PhoneNumber. Returns null if validation fails.
    /// </summary>
    public static PhoneNumber? TryCreate(string phoneNumber)
    {
        try
        {
            return Create(phoneNumber);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Formats the phone number in standard Thai mobile format: 0xx-xxx-xxxx.
    /// </summary>
    public string ToFormattedString()
    {
        var digits = Value;

        // Convert international format to local
        if (digits.StartsWith("66") && digits.Length == 11)
        {
            digits = "0" + digits[2..];
        }

        if (digits.Length == 10)
        {
            // Mobile format: 0xx-xxx-xxxx
            return $"{digits[..3]}-{digits[3..6]}-{digits[6..]}";
        }

        if (digits.Length == 9)
        {
            // Landline format: 0x-xxx-xxxx
            return $"{digits[..2]}-{digits[2..5]}-{digits[5..]}";
        }

        return digits;
    }

    /// <summary>
    /// Formats the number in international Thai format: +66xxxxxxxxx.
    /// </summary>
    public string ToInternationalFormat()
    {
        var digits = Value;

        if (digits.StartsWith('0'))
        {
            digits = "66" + digits[1..];
        }

        return $"+{digits}";
    }

    public override string ToString() => ToFormattedString();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Normalize to local format for comparison
        var normalized = Value;
        if (normalized.StartsWith("66") && normalized.Length >= 11)
        {
            normalized = "0" + normalized[2..];
        }

        yield return normalized;
    }

    private static string NormalizePhoneNumber(string input)
    {
        // Remove leading + sign for processing
        var cleaned = input.StartsWith('+') ? input[1..] : input;

        // Remove all non-digit characters
        cleaned = DigitsOnlyRegex().Replace(cleaned, "");

        return cleaned;
    }

    private static bool IsValidThaiPhoneNumber(string digits)
    {
        // Thai mobile: 10 digits starting with 06, 08, or 09
        if (ThaiMobileLocalRegex().IsMatch(digits))
            return true;

        // Thai mobile international: 11 digits starting with 666, 668, or 669
        if (ThaiMobileInternationalRegex().IsMatch(digits))
            return true;

        // Thai landline: 9 digits starting with 02-05
        if (ThaiLandlineLocalRegex().IsMatch(digits))
            return true;

        // Thai landline international: 10 digits starting with 662-665
        if (ThaiLandlineInternationalRegex().IsMatch(digits))
            return true;

        return false;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex DigitsOnlyRegex();

    [GeneratedRegex(@"^0[689]\d{8}$")]
    private static partial Regex ThaiMobileLocalRegex();

    [GeneratedRegex(@"^66[689]\d{8}$")]
    private static partial Regex ThaiMobileInternationalRegex();

    [GeneratedRegex(@"^0[2-5]\d{7}$")]
    private static partial Regex ThaiLandlineLocalRegex();

    [GeneratedRegex(@"^66[2-5]\d{7}$")]
    private static partial Regex ThaiLandlineInternationalRegex();
}
