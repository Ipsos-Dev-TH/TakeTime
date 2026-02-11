using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TakeTime.Identity.Domain;
using TakeTime.Identity.Services;

namespace TakeTime.ApiGateway.Controllers;

/// <summary>
/// Authentication API controller.
/// Handles login, token refresh, logout, and password changes.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly AuthenticationService _authService;

    public AuthController(
        IUserRepository userRepository,
        AuthenticationService authService)
    {
        _userRepository = userRepository;
        _authService = authService;
    }

    /// <summary>
    /// Authenticates a user with username and password.
    /// Returns JWT access token and refresh token on success.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Username and password are required." });

        if (string.IsNullOrWhiteSpace(request.TenantId))
            return BadRequest(new { error = "TenantId is required." });

        var user = await _userRepository.GetByUsernameAsync(request.Username, request.TenantId, ct);

        var result = _authService.Authenticate(user, request.Password);

        if (!result.Success)
        {
            // Persist failed login attempt tracking (lockout counter)
            if (user is not null)
                await _userRepository.SaveAsync(user, ct);

            return Unauthorized(new { error = result.Error });
        }

        // Persist updated login info and refresh token
        await _userRepository.SaveAsync(result.User!, ct);

        return Ok(new LoginResponse(
            AccessToken: result.AccessToken!,
            RefreshToken: result.RefreshToken!,
            Username: result.User!.Username,
            FullName: result.User.FullName,
            Email: result.User.Email,
            Roles: result.User.Roles,
            TenantId: result.User.TenantId));
    }

    /// <summary>
    /// Refreshes an access token using a valid refresh token.
    /// Returns a new access token and a rotated refresh token.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        if (request.UserId == Guid.Empty)
            return BadRequest(new { error = "UserId is required." });

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return BadRequest(new { error = "RefreshToken is required." });

        var user = await _userRepository.GetByIdAsync(request.UserId, ct);

        var result = _authService.RefreshAccessToken(user, request.RefreshToken);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        // Persist the rotated refresh token
        await _userRepository.SaveAsync(result.User!, ct);

        return Ok(new RefreshTokenResponse(
            AccessToken: result.AccessToken!,
            RefreshToken: result.RefreshToken!));
    }

    /// <summary>
    /// Logs out the current user by revoking their refresh token.
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request, CancellationToken ct)
    {
        var userId = request.UserId != Guid.Empty
            ? request.UserId
            : GetCurrentUserId();

        if (userId == Guid.Empty)
            return BadRequest(new { error = "UserId is required." });

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        user.RevokeRefreshToken();
        await _userRepository.SaveAsync(user, ct);

        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>
    /// Changes the password for the currently authenticated user.
    /// Requires the current password for verification.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId == Guid.Empty)
            return Unauthorized(new { error = "User identity could not be determined." });

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
            return BadRequest(new { error = "Current password and new password are required." });

        if (request.CurrentPassword == request.NewPassword)
            return BadRequest(new { error = "New password must be different from the current password." });

        var user = await _userRepository.GetByIdAsync(userId, ct);
        if (user is null)
            return NotFound(new { error = "User not found." });

        // Verify current password using the authentication service
        var verifyResult = _authService.Authenticate(user, request.CurrentPassword);
        if (!verifyResult.Success)
        {
            // Persist failed attempt tracking
            await _userRepository.SaveAsync(user, ct);
            return BadRequest(new { error = "Current password is incorrect." });
        }

        // Hash and set the new password
        user.PasswordHash = _authService.HashPassword(request.NewPassword);
        user.RevokeRefreshToken();
        user.UpdatedAt = DateTime.UtcNow;

        await _userRepository.SaveAsync(user, ct);

        return Ok(new { message = "Password changed successfully. Please log in again." });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : Guid.Empty;
    }
}

// ─── Request / Response DTOs ─────────────────────────────────────────

public record LoginRequest(
    string Username,
    string Password,
    string TenantId);

public record LoginResponse(
    string AccessToken,
    string RefreshToken,
    string Username,
    string FullName,
    string Email,
    List<string> Roles,
    string? TenantId);

public record RefreshTokenRequest(
    Guid UserId,
    string RefreshToken);

public record RefreshTokenResponse(
    string AccessToken,
    string RefreshToken);

public record LogoutRequest(
    Guid UserId = default);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
