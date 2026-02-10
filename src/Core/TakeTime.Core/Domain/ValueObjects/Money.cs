using System.Globalization;
using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing a monetary amount with its associated currency.
/// Provides arithmetic operations, comparison, and formatting with
/// first-class support for Thai Baht (THB).
/// </summary>
public class Money : ValueObject, IComparable<Money>
{
    /// <summary>
    /// The monetary amount.
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// ISO 4217 currency code (e.g., "THB", "USD").
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Thai Baht currency code constant.
    /// </summary>
    public const string ThaiCurrencyCode = "THB";

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a new Money instance.
    /// </summary>
    /// <param name="amount">The monetary amount.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    public static Money Create(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency code is required.", nameof(currency));

        if (currency.Length != 3)
            throw new ArgumentException("Currency code must be a 3-letter ISO 4217 code.", nameof(currency));

        return new Money(amount, currency.ToUpperInvariant());
    }

    /// <summary>
    /// Creates a Money instance in Thai Baht.
    /// </summary>
    public static Money InBaht(decimal amount) => Create(amount, ThaiCurrencyCode);

    /// <summary>
    /// Returns a zero-amount Money in the specified currency.
    /// </summary>
    public static Money Zero(string currency) => Create(0, currency);

    /// <summary>
    /// Returns a zero-amount Money in Thai Baht.
    /// </summary>
    public static Money ZeroBaht() => Zero(ThaiCurrencyCode);

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Create(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return Create(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, decimal multiplier)
    {
        return Create(money.Amount * multiplier, money.Currency);
    }

    public static Money operator *(decimal multiplier, Money money)
    {
        return money * multiplier;
    }

    public static Money operator /(Money money, decimal divisor)
    {
        if (divisor == 0)
            throw new DivideByZeroException("Cannot divide money by zero.");

        return Create(money.Amount / divisor, money.Currency);
    }

    public static Money operator -(Money money)
    {
        return Create(-money.Amount, money.Currency);
    }

    public static bool operator >(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount > right.Amount;
    }

    public static bool operator <(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount < right.Amount;
    }

    public static bool operator >=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount >= right.Amount;
    }

    public static bool operator <=(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return left.Amount <= right.Amount;
    }

    /// <summary>
    /// Whether this amount is zero.
    /// </summary>
    public bool IsZero => Amount == 0;

    /// <summary>
    /// Whether this amount is positive (greater than zero).
    /// </summary>
    public bool IsPositive => Amount > 0;

    /// <summary>
    /// Whether this amount is negative (less than zero).
    /// </summary>
    public bool IsNegative => Amount < 0;

    /// <summary>
    /// Returns a new Money rounded to the specified number of decimal places.
    /// </summary>
    public Money Round(int decimals = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
    {
        return Create(Math.Round(Amount, decimals, mode), Currency);
    }

    /// <summary>
    /// Applies a percentage to this amount and returns the result.
    /// </summary>
    /// <param name="percentage">Percentage as a whole number (e.g., 7 for 7%).</param>
    public Money Percentage(decimal percentage)
    {
        return Create(Amount * percentage / 100m, Currency);
    }

    /// <summary>
    /// Formats the amount for Thai Baht display (e.g., "฿1,234.50").
    /// For non-THB currencies, uses the standard format with the currency code.
    /// </summary>
    public string ToFormattedString()
    {
        if (Currency == ThaiCurrencyCode)
        {
            return $"\u0E3F{Amount:N2}";
        }

        return $"{Currency} {Amount:N2}";
    }

    /// <summary>
    /// Formats with a specific culture.
    /// </summary>
    public string ToFormattedString(CultureInfo culture)
    {
        return Amount.ToString("N2", culture) + $" {Currency}";
    }

    public int CompareTo(Money? other)
    {
        if (other is null) return 1;
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public override string ToString() => ToFormattedString();

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Math.Round(Amount, 4);
        yield return Currency;
    }

    private static void EnsureSameCurrency(Money left, Money right)
    {
        if (left.Currency != right.Currency)
        {
            throw new InvalidOperationException(
                $"Cannot perform operations on Money with different currencies: {left.Currency} and {right.Currency}.");
        }
    }
}
