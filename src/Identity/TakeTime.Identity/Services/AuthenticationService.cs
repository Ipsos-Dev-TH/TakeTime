using Microsoft.Extensions.Logging;
using TakeTime.Identity.Domain;

namespace TakeTime.Identity.Services;

public record AuthResult(bool Success, string? AccessToken, string? RefreshToken, string? Error, User? User);

public class AuthenticationService
{
    private readonly PasswordService _passwordService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        PasswordService passwordService,
        JwtTokenService jwtTokenService,
        ILogger<AuthenticationService> logger)
    {
        _passwordService = passwordService;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public AuthResult Authenticate(User? user, string password)
    {
        if (user == null)
        {
            return new AuthResult(false, null, null, "Invalid username or password.", null);
        }

        if (!user.IsActive)
        {
            return new AuthResult(false, null, null, "Account is deactivated.", null);
        }

        if (user.IsLockedOut)
        {
            return new AuthResult(false, null, null, $"Account is locked until {user.LockoutEnd:yyyy-MM-dd HH:mm} UTC.", null);
        }

        if (!_passwordService.VerifyPassword(password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            _logger.LogWarning("Failed login attempt for user {Username}. Attempts: {Attempts}",
                user.Username, user.FailedLoginAttempts);
            return new AuthResult(false, null, null, "Invalid username or password.", null);
        }

        user.RecordLogin();

        var accessToken = _jwtTokenService.GenerateAccessToken(user);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();
        user.SetRefreshToken(refreshToken);

        _logger.LogInformation("User {Username} authenticated successfully for tenant {TenantId}",
            user.Username, user.TenantId);

        return new AuthResult(true, accessToken, refreshToken, null, user);
    }

    public AuthResult RefreshAccessToken(User? user, string refreshToken)
    {
        if (user == null)
        {
            return new AuthResult(false, null, null, "User not found.", null);
        }

        if (user.RefreshToken != refreshToken)
        {
            return new AuthResult(false, null, null, "Invalid refresh token.", null);
        }

        if (user.RefreshTokenExpiryTime < DateTime.UtcNow)
        {
            return new AuthResult(false, null, null, "Refresh token expired.", null);
        }

        var newAccessToken = _jwtTokenService.GenerateAccessToken(user);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();
        user.SetRefreshToken(newRefreshToken);

        return new AuthResult(true, newAccessToken, newRefreshToken, null, user);
    }

    public string HashPassword(string password)
    {
        return _passwordService.HashPassword(password);
    }
}
