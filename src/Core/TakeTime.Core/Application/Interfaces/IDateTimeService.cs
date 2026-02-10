namespace TakeTime.Core.Application.Interfaces;

/// <summary>
/// Abstraction over date/time operations to enable deterministic testing.
/// All application code should use this service instead of calling
/// <see cref="DateTime.UtcNow"/> or <see cref="DateTimeOffset.UtcNow"/> directly.
/// </summary>
public interface IDateTimeService
{
    /// <summary>
    /// Returns the current UTC date and time.
    /// </summary>
    DateTime UtcNow { get; }

    /// <summary>
    /// Returns the current UTC date and time as a <see cref="DateTimeOffset"/>.
    /// </summary>
    DateTimeOffset UtcNowOffset { get; }

    /// <summary>
    /// Returns the current date and time in the Bangkok timezone (UTC+7).
    /// Useful for Thai business logic and display.
    /// </summary>
    DateTime BangkokNow { get; }

    /// <summary>
    /// Returns today's date in UTC.
    /// </summary>
    DateTime Today => UtcNow.Date;

    /// <summary>
    /// Returns today's date in the Bangkok timezone.
    /// </summary>
    DateTime BangkokToday => BangkokNow.Date;

    /// <summary>
    /// Converts a UTC datetime to Bangkok local time (UTC+7).
    /// </summary>
    DateTime ToBangkokTime(DateTime utcDateTime);

    /// <summary>
    /// Converts a Bangkok local time to UTC.
    /// </summary>
    DateTime ToUtcFromBangkok(DateTime bangkokDateTime);
}
