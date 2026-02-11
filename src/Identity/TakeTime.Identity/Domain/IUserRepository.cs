namespace TakeTime.Identity.Domain;

/// <summary>
/// Repository interface for managing User entities.
/// Provides tenant-scoped queries and persistence operations.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Finds a user by username within a specific tenant.
    /// </summary>
    Task<User?> GetByUsernameAsync(string username, string tenantId, CancellationToken ct);

    /// <summary>
    /// Finds a user by their unique identifier (cross-tenant).
    /// </summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Finds a user by email within a specific tenant.
    /// </summary>
    Task<User?> GetByEmailAsync(string email, string tenantId, CancellationToken ct);

    /// <summary>
    /// Gets all users belonging to a specific tenant.
    /// </summary>
    Task<IReadOnlyList<User>> GetByTenantAsync(string tenantId, CancellationToken ct);

    /// <summary>
    /// Persists a new or updated user entity.
    /// </summary>
    Task SaveAsync(User user, CancellationToken ct);

    /// <summary>
    /// Deletes a user by their unique identifier.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
