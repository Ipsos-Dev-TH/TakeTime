using TakeTime.Core.Domain.Base;

namespace TakeTime.Core.Domain.ValueObjects;

/// <summary>
/// Value object representing an inclusive date range with start and end dates.
/// Provides overlap detection, containment checks, and duration calculations.
/// </summary>
public class DateRange : ValueObject, IComparable<DateRange>
{
    /// <summary>
    /// Inclusive start date of the range.
    /// </summary>
    public DateTime Start { get; }

    /// <summary>
    /// Inclusive end date of the range.
    /// </summary>
    public DateTime End { get; }

    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a new DateRange. The start must be on or before the end.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when start is after end.</exception>
    public static DateRange Create(DateTime start, DateTime end)
    {
        if (start > end)
        {
            throw new ArgumentException(
                $"Start date ({start:yyyy-MM-dd}) must be on or before end date ({end:yyyy-MM-dd}).");
        }

        return new DateRange(start.Date, end.Date);
    }

    /// <summary>
    /// Creates a single-day DateRange.
    /// </summary>
    public static DateRange SingleDay(DateTime date)
    {
        return new DateRange(date.Date, date.Date);
    }

    /// <summary>
    /// Creates a DateRange from a start date and a duration in days.
    /// </summary>
    /// <param name="start">Start date.</param>
    /// <param name="days">Number of days (inclusive of start).</param>
    public static DateRange FromDays(DateTime start, int days)
    {
        if (days < 1)
            throw new ArgumentException("Duration must be at least 1 day.", nameof(days));

        return new DateRange(start.Date, start.Date.AddDays(days - 1));
    }

    /// <summary>
    /// Returns the total number of days in the range (inclusive).
    /// </summary>
    public int NumberOfDays => (End.Date - Start.Date).Days + 1;

    /// <summary>
    /// Returns the duration as a <see cref="TimeSpan"/>.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Checks whether this range overlaps with another range.
    /// Two ranges overlap if one starts before the other ends.
    /// </summary>
    public bool Overlaps(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start <= other.End && other.Start <= End;
    }

    /// <summary>
    /// Checks whether a specific date falls within this range (inclusive).
    /// </summary>
    public bool Contains(DateTime date)
    {
        return date.Date >= Start && date.Date <= End;
    }

    /// <summary>
    /// Checks whether this range fully contains another range.
    /// </summary>
    public bool Contains(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Start <= other.Start && End >= other.End;
    }

    /// <summary>
    /// Returns the intersection of two overlapping ranges, or null if they do not overlap.
    /// </summary>
    public DateRange? Intersection(DateRange other)
    {
        if (!Overlaps(other))
            return null;

        var start = Start > other.Start ? Start : other.Start;
        var end = End < other.End ? End : other.End;

        return Create(start, end);
    }

    /// <summary>
    /// Returns a new range that is the union (smallest range covering both),
    /// or null if the two ranges are disjoint with a gap.
    /// </summary>
    public DateRange? Union(DateRange other)
    {
        if (!Overlaps(other) && Start.AddDays(-1) > other.End && other.Start.AddDays(-1) > End)
            return null;

        var start = Start < other.Start ? Start : other.Start;
        var end = End > other.End ? End : other.End;

        return Create(start, end);
    }

    /// <summary>
    /// Returns the number of overlapping days with another range.
    /// Returns 0 if the ranges do not overlap.
    /// </summary>
    public int OverlappingDays(DateRange other)
    {
        var intersection = Intersection(other);
        return intersection?.NumberOfDays ?? 0;
    }

    /// <summary>
    /// Enumerates all dates in the range.
    /// </summary>
    public IEnumerable<DateTime> EnumerateDates()
    {
        for (var date = Start; date <= End; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    /// <summary>
    /// Returns a new DateRange extended by the specified number of days on each side.
    /// </summary>
    public DateRange Extend(int daysBefore, int daysAfter)
    {
        return Create(Start.AddDays(-daysBefore), End.AddDays(daysAfter));
    }

    public int CompareTo(DateRange? other)
    {
        if (other is null) return 1;

        var startComparison = Start.CompareTo(other.Start);
        return startComparison != 0 ? startComparison : End.CompareTo(other.End);
    }

    public override string ToString() => $"{Start:yyyy-MM-dd} - {End:yyyy-MM-dd} ({NumberOfDays} days)";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
