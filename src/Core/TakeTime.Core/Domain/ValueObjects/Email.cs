using System.Text.RegularExpressions;
using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated email address.
/// Stores the email in a normalized (lowercase) form for consistent comparison.
/// </summary>
public partial class Email : ValueObject
{
    /// <summary>
    /// The normalized (lowercase, trimmed) email address.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// The local part of the email (before the @).
    /// </summary>
    public string LocalPart => Value.Split('@')[0];

    /// <summary>
    /// The domain part of the email (after the @).
    /// </summary>
    public string Domain => Value.Split('@')[1];

    private Email(string value)
    {
        Value = value;
    }

    /// <summary>
    /// Creates an Email value object after validating the format.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the email address is invalid.</exception>
    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email address is required.", nameof(email));

        var normalized = email.Trim().ToLowerInvariant();

        if (normalized.Length > 254)
            throw new ArgumentException("Email address must not exceed 254 characters.", nameof(email));

        if (!EmailRegex().IsMatch(normalized))
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(email));

        // Additional structural validation
        var parts = normalized.Split('@');
        if (parts.Length != 2)
            throw new ArgumentException($"'{email}' is not a valid email address.", nameof(email));

        var localPart = parts[0];
        var domainPart = parts[1];

        if (localPart.Length > 64)
            throw new ArgumentException("Email local part must not exceed 64 characters.", nameof(email));

        if (domainPart.Length > 253)
            throw new ArgumentException("Email domain must not exceed 253 characters.", nameof(email));

        // Domain must have at least one dot
        if (!domainPart.Contains('.'))
            throw new ArgumentException($"'{email}' has an invalid domain.", nameof(email));

        return new Email(normalized);
    }

    /// <summary>
    /// Attempts to create an Email. Returns null if validation fails.
    /// </summary>
    public static Email? TryCreate(string email)
    {
        try
        {
            return Create(email);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Checks whether the email domain matches the specified domain (case-insensitive).
    /// </summary>
    public bool HasDomain(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain))
            return false;

        return Domain.Equals(domain.Trim().ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Implicit conversion to string.
    /// </summary>
    public static implicit operator string(Email email) => email.Value;

    public override string ToString() => Value;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    [GeneratedRegex(
        @"^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)*$",
        RegexOptions.Compiled)]
    private static partial Regex EmailRegex();
}
