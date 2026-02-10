namespace TakeTime.Core.Exceptions;

/// <summary>
/// Exception thrown when a requested entity cannot be found in the data store.
/// Carries the entity name and the key that was used in the lookup to
/// produce clear, actionable error messages.
/// </summary>
public class NotFoundException : DomainException
{
    /// <summary>
    /// Name of the entity type that was not found.
    /// </summary>
    public string EntityName { get; }

    /// <summary>
    /// The key value used in the failed lookup.
    /// </summary>
    public object Key { get; }

    public NotFoundException(string entityName, object key)
        : base($"Entity '{entityName}' with key '{key}' was not found.")
    {
        EntityName = entityName;
        Key = key;
    }

    public NotFoundException(string entityName, object key, string message)
        : base(message)
    {
        EntityName = entityName;
        Key = key;
    }

    public NotFoundException(string entityName, object key, Exception innerException)
        : base($"Entity '{entityName}' with key '{key}' was not found.", innerException)
    {
        EntityName = entityName;
        Key = key;
    }

    /// <summary>
    /// Creates a NotFoundException using the generic type parameter as the entity name.
    /// </summary>
    public static NotFoundException For<TEntity>(object key)
    {
        return new NotFoundException(typeof(TEntity).Name, key);
    }
}
