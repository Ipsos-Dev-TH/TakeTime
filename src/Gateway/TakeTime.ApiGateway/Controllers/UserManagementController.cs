using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Identity.Domain;
using TakeTime.Identity.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// Admin API for managing users and their roles.
/// Provides CRUD operations, role assignment, activation/deactivation,
/// and administrative password resets.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/admin/users")]
public class UserManagementController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly AuthenticationService _authService;

    public UserManagementController(
        IUserRepository userRepository,
        AuthenticationService authService)
    {
        _userRepository = userRepository;
        _authService = authService;
    }

    /// <summary>
    /// Lists all users for a given tenant.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { error = "tenantId query parameter is required." });

        var users = await _userRepository.GetByTenantAsync(tenantId, ct);

        var response = users.Select(u => new UserSummaryResponse(
            Id: u.Id,
            Username: u.Username,
            Email: u.Email,
            FullName: u.FullName,
            Phone: u.Phone,
            Roles: u.Roles,
            IsActive: u.IsActive,
            LastLoginAt: u.LastLoginAt,
            CreatedAt: u.CreatedAt));

        return Ok(response);
    }

    /// <summary>
    /// Gets a single user by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        return Ok(new UserDetailResponse(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            AvatarUrl: user.AvatarUrl,
            Roles: user.Roles,
            Permissions: user.Permissions,
            IsActive: user.IsActive,
            LastLoginAt: user.LastLoginAt,
            IsLockedOut: user.IsLockedOut,
            LockoutEnd: user.LockoutEnd,
            TenantId: user.TenantId,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt));
    }

    /// <summary>
    /// Creates a new user with a hashed password.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateUserRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest(new { error = "Username is required." });

        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Password is required." });

        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { error = "TenantId is required." });

        // Check for duplicate username within the tenant
        var existing = await _userRepository.GetByUsernameAsync(request.Username, request.TenantId, ct);
        if (existing is not null)
            return Conflict(new { error = $"Username '{request.Username}' already exists for this tenant." });

        // Check for duplicate email within the tenant (if email provided)
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var emailExists = await _userRepository.GetByEmailAsync(request.Email, request.TenantId, ct);
            if (emailExists is not null)
                return Conflict(new { error = $"Email '{request.Email}' already exists for this tenant." });
        }

        var user = new User
        {
            Username = request.Username,
            PasswordHash = _authService.HashPassword(request.Password),
            Email = request.Email ?? string.Empty,
            FullName = request.FullName ?? string.Empty,
            Phone = request.Phone,
            Roles = request.Roles ?? [],
            Permissions = request.Permissions ?? [],
            IsActive = true,
            TenantId = request.TenantId,
            CreatedBy = request.CreatedBy
        };

        await _userRepository.SaveAsync(user, ct);

        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new UserDetailResponse(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            AvatarUrl: user.AvatarUrl,
            Roles: user.Roles,
            Permissions: user.Permissions,
            IsActive: user.IsActive,
            LastLoginAt: user.LastLoginAt,
            IsLockedOut: user.IsLockedOut,
            LockoutEnd: user.LockoutEnd,
            TenantId: user.TenantId,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt));
    }

    /// <summary>
    /// Updates user information (name, email, phone, roles).
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        if (request.FullName is not null)
            user.FullName = request.FullName;

        if (request.Email is not null)
            user.Email = request.Email;

        if (request.Phone is not null)
            user.Phone = request.Phone;

        if (request.Roles is not null)
            user.Roles = request.Roles;

        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = request.UpdatedBy;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new UserDetailResponse(
            Id: user.Id,
            Username: user.Username,
            Email: user.Email,
            FullName: user.FullName,
            Phone: user.Phone,
            AvatarUrl: user.AvatarUrl,
            Roles: user.Roles,
            Permissions: user.Permissions,
            IsActive: user.IsActive,
            LastLoginAt: user.LastLoginAt,
            IsLockedOut: user.IsLockedOut,
            LockoutEnd: user.LockoutEnd,
            TenantId: user.TenantId,
            CreatedAt: user.CreatedAt,
            UpdatedAt: user.UpdatedAt));
    }

    /// <summary>
    /// Updates roles for a specific user.
    /// </summary>
    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> UpdateRoles(
        Guid id, [FromBody] UpdateRolesRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        if (request.Roles is null)
            return BadRequest(new { error = "Roles list is required." });

        user.Roles = request.Roles;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = request.UpdatedBy;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new
        {
            userId = user.Id,
            roles = user.Roles,
            message = "Roles updated successfully."
        });
    }

    /// <summary>
    /// Activates a deactivated user account.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate(
        Guid id, [FromBody] UserActionRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        if (user.IsActive)
            return BadRequest(new { error = "User is already active." });

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = request.PerformedBy;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new { message = "User activated successfully." });
    }

    /// <summary>
    /// Deactivates a user account. Prevents further logins.
    /// </summary>
    [HttpPost("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(
        Guid id, [FromBody] UserActionRequest request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        if (!user.IsActive)
            return BadRequest(new { error = "User is already deactivated." });

        user.IsActive = false;
        user.RevokeRefreshToken();
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = request.PerformedBy;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new { message = "User deactivated successfully." });
    }

    /// <summary>
    /// Resets a user's password (admin operation).
    /// Does not require the current password.
    /// </summary>
    [HttpPost("{id:guid}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "NewPassword is required." });

        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutEnd = null;
        user.RevokeRefreshToken();
        user.UpdatedAt = DateTime.UtcNow;
        user.UpdatedBy = request.PerformedBy;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new { message = "Password has been reset successfully." });
    }

    /// <summary>
    /// Deletes a user by ID.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAsync(id, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        await _userRepository.DeleteAsync(id, ct);

        return Ok(new { message = "User deleted successfully." });
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────

public record CreateUserRequest(
    string Username,
    string Password,
    string TenantId,
    string? Email = null,
    string? FullName = null,
    string? Phone = null,
    List<string>? Roles = null,
    List<string>? Permissions = null,
    string? CreatedBy = null);

public record UpdateUserRequest(
    string? FullName = null,
    string? Email = null,
    string? Phone = null,
    List<string>? Roles = null,
    string? UpdatedBy = null);

public record UpdateRolesRequest(
    List<string> Roles,
    string? UpdatedBy = null);

public record UserActionRequest(
    string? PerformedBy = null);

public record ResetPasswordRequest(
    string NewPassword,
    string? PerformedBy = null);

public record UserSummaryResponse(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    List<string> Roles,
    bool IsActive,
    DateTime? LastLoginAt,
    DateTime CreatedAt);

public record UserDetailResponse(
    Guid Id,
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? AvatarUrl,
    List<string> Roles,
    List<string> Permissions,
    bool IsActive,
    DateTime? LastLoginAt,
    bool IsLockedOut,
    DateTime? LockoutEnd,
    string? TenantId,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
