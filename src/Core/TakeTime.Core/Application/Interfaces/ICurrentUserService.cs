namespace TakeTime.Core.Application.Interfaces;

/// <summary>
/// Provides access to the current authenticated user's context.
/// Implementations typically extract user claims from the HTTP context
/// or security principal.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Unique identifier of the current user.
    /// Returns null if no user is authenticated.
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Display name or username of the current user.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Email address of the current user.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Collection of roles assigned to the current user.
    /// </summary>
    IReadOnlyCollection<string> Roles { get; }

    /// <summary>
    /// Collection of specific permissions granted to the current user.
    /// </summary>
    IReadOnlyCollection<string> Permissions { get; }

    /// <summary>
    /// Whether the current user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// Checks whether the current user has the specified role.
    /// </summary>
    bool IsInRole(string role);

    /// <summary>
    /// Checks whether the current user has the specified permission.
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// Checks whether the current user has any of the specified permissions.
    /// </summary>
    bool HasAnyPermission(params string[] permissions);

    /// <summary>
    /// Checks whether the current user has all of the specified permissions.
    /// </summary>
    bool HasAllPermissions(params string[] permissions);
}
