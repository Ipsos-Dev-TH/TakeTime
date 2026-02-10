using System.Reflection;

namespace TakeTime.Core.Domain.Base;

/// <summary>
/// Smart enum base class that provides type-safe, behaviour-rich alternatives
/// to plain C# enums. Each enumeration value is a singleton instance with
/// an integer id and a human-readable name.
/// </summary>
public abstract class Enumeration : IComparable<Enumeration>, IEquatable<Enumeration>
{
    /// <summary>
    /// Numeric identifier for the enumeration value.
    /// </summary>
    public int Id { get; }

    /// <summary>
    /// Display name for the enumeration value.
    /// </summary>
    public string Name { get; }

    protected Enumeration(int id, string name)
    {
        Id = id;
        Name = name;
    }

    public override string ToString() => Name;

    /// <summary>
    /// Returns all declared public static fields of type <typeparamref name="T"/>.
    /// </summary>
    public static IEnumerable<T> GetAll<T>() where T : Enumeration
    {
        return typeof(T)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(f => f.GetValue(null))
            .OfType<T>();
    }

    /// <summary>
    /// Returns the enumeration value whose <see cref="Id"/> matches
    /// the supplied <paramref name="id"/>, or throws if not found.
    /// </summary>
    public static T FromId<T>(int id) where T : Enumeration
    {
        var matchingItem = Parse<T, int>(id, "id", item => item.Id == id);
        return matchingItem;
    }

    /// <summary>
    /// Returns the enumeration value whose <see cref="Name"/> matches
    /// the supplied <paramref name="name"/> (case-insensitive), or throws if not found.
    /// </summary>
    public static T FromName<T>(string name) where T : Enumeration
    {
        var matchingItem = Parse<T, string>(name, "name",
            item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return matchingItem;
    }

    /// <summary>
    /// Attempts to find an enumeration value by its <see cref="Id"/>.
    /// </summary>
    public static bool TryFromId<T>(int id, out T? result) where T : Enumeration
    {
        result = GetAll<T>().FirstOrDefault(item => item.Id == id);
        return result is not null;
    }

    /// <summary>
    /// Attempts to find an enumeration value by its <see cref="Name"/> (case-insensitive).
    /// </summary>
    public static bool TryFromName<T>(string name, out T? result) where T : Enumeration
    {
        result = GetAll<T>().FirstOrDefault(item =>
            item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return result is not null;
    }

    private static T Parse<T, TValue>(TValue value, string description, Func<T, bool> predicate)
        where T : Enumeration
    {
        var matchingItem = GetAll<T>().FirstOrDefault(predicate);

        if (matchingItem is null)
        {
            throw new InvalidOperationException(
                $"'{value}' is not a valid {description} in {typeof(T).Name}. " +
                $"Valid values are: {string.Join(", ", GetAll<T>().Select(e => $"{e.Name} ({e.Id})"))}.");
        }

        return matchingItem;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not Enumeration other)
            return false;

        var typeMatches = GetType() == other.GetType();
        var valueMatches = Id == other.Id;

        return typeMatches && valueMatches;
    }

    public bool Equals(Enumeration? other)
    {
        return Equals((object?)other);
    }

    public override int GetHashCode()
    {
        return Id.GetHashCode();
    }

    public int CompareTo(Enumeration? other)
    {
        if (other is null) return 1;
        return Id.CompareTo(other.Id);
    }

    public static bool operator ==(Enumeration? left, Enumeration? right)
    {
        if (left is null && right is null) return true;
        if (left is null || right is null) return false;
        return left.Equals(right);
    }

    public static bool operator !=(Enumeration? left, Enumeration? right)
    {
        return !(left == right);
    }

    public static bool operator <(Enumeration left, Enumeration right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator >(Enumeration left, Enumeration right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator <=(Enumeration left, Enumeration right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >=(Enumeration left, Enumeration right)
    {
        return left.CompareTo(right) >= 0;
    }
}
