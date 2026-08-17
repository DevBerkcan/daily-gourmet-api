using System.Text.Json;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;

namespace DailyGourmet.Api.Middleware;

/// <summary>Centralized exception handling. Never leaks stack traces, SQL errors, or connection
/// strings to the client — only a safe message is returned, full details go to the log.</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = ex switch
            {
                NotFoundException => (StatusCodes.Status404NotFound, ex.Message),
                ValidationException => (StatusCodes.Status400BadRequest, ex.Message),
                UnauthorizedException => (StatusCodes.Status401Unauthorized, ex.Message),
                ForbiddenException => (StatusCodes.Status403Forbidden, ex.Message),
                ConflictException => (StatusCodes.Status409Conflict, ex.Message),
                _ => (StatusCodes.Status500InternalServerError, "Ein unerwarteter Fehler ist aufgetreten."),
            };

            if (statusCode == StatusCodes.Status500InternalServerError)
                logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogWarning("{ExceptionType} on {Method} {Path}: {Message}", ex.GetType().Name, context.Request.Method, context.Request.Path, ex.Message);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;
            var response = ApiResponse.Fail(message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
