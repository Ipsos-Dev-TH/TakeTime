using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace TakeTime.Reservation.API.Middleware;

/// <summary>
/// Global exception handling middleware that catches unhandled exceptions and
/// returns standardized ProblemDetails responses compliant with RFC 7807.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, problemDetails) = exception switch
        {
            ArgumentException argEx => (
                HttpStatusCode.BadRequest,
                CreateProblemDetails(
                    "Bad Request",
                    argEx.Message,
                    (int)HttpStatusCode.BadRequest,
                    context.Request.Path)),

            InvalidOperationException invalidOpEx => (
                HttpStatusCode.Conflict,
                CreateProblemDetails(
                    "Business Rule Violation",
                    invalidOpEx.Message,
                    (int)HttpStatusCode.Conflict,
                    context.Request.Path)),

            KeyNotFoundException notFoundEx => (
                HttpStatusCode.NotFound,
                CreateProblemDetails(
                    "Not Found",
                    notFoundEx.Message,
                    (int)HttpStatusCode.NotFound,
                    context.Request.Path)),

            UnauthorizedAccessException unauthorizedEx => (
                HttpStatusCode.Forbidden,
                CreateProblemDetails(
                    "Forbidden",
                    unauthorizedEx.Message,
                    (int)HttpStatusCode.Forbidden,
                    context.Request.Path)),

            OperationCanceledException => (
                HttpStatusCode.BadRequest,
                CreateProblemDetails(
                    "Request Cancelled",
                    "The request was cancelled.",
                    (int)HttpStatusCode.BadRequest,
                    context.Request.Path)),

            _ => (
                HttpStatusCode.InternalServerError,
                CreateProblemDetails(
                    "Internal Server Error",
                    "An unexpected error occurred. Please try again later.",
                    (int)HttpStatusCode.InternalServerError,
                    context.Request.Path))
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception,
                "Handled exception ({ExceptionType}) while processing {Method} {Path}: {Message}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                exception.Message);
        }

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problemDetails, JsonOptions));
    }

    private static ProblemDetails CreateProblemDetails(
        string title, string detail, int status, string instance)
    {
        return new ProblemDetails
        {
            Title = title,
            Detail = detail,
            Status = status,
            Instance = instance,
            Type = $"https://httpstatuses.com/{status}"
        };
    }
}

/// <summary>
/// Extension method for registering the exception handling middleware.
/// </summary>
public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}
