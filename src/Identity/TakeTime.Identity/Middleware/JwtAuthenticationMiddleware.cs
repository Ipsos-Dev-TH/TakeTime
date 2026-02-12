using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TakeTime.Identity.Services;

namespace TakeTime.Identity.Middleware;

public class JwtAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<JwtAuthenticationMiddleware> _logger;

    public JwtAuthenticationMiddleware(
        RequestDelegate next,
        JwtTokenService jwtTokenService,
        ILogger<JwtAuthenticationMiddleware> logger)
    {
        _next = next;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var token = ExtractToken(context);

        if (!string.IsNullOrEmpty(token))
        {
            var principal = _jwtTokenService.ValidateToken(token);
            if (principal != null)
            {
                context.User = principal;

                var tenantId = principal.FindFirst("tenant_id")?.Value;
                if (!string.IsNullOrEmpty(tenantId))
                {
                    context.Items["TenantId"] = tenantId;
                }

                var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    context.Items["UserId"] = userId;
                }
            }
            else
            {
                _logger.LogDebug("Invalid JWT token received");
            }
        }

        await _next(context);
    }

    private static string? ExtractToken(HttpContext context)
    {
        var authorization = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return authorization["Bearer ".Length..].Trim();
        }

        return null;
    }
}
