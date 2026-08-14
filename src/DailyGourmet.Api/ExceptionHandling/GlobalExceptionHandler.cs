using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace DailyGourmet.Api.ExceptionHandling;

/// <summary>
/// Übersetzt unbehandelte Exceptions in ein ProblemDetails-Response (§24) — nie Stacktraces oder
/// interne Exception-Details an Clients. Details werden nur geloggt.
/// </summary>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogUnhandledException(logger, httpContext.TraceIdentifier, httpContext.Request.Path, exception);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Title = "Ein unerwarteter Fehler ist aufgetreten.",
                Status = StatusCodes.Status500InternalServerError,
                Extensions =
                {
                    ["code"] = "INTERNAL_SERVER_ERROR",
                    ["requestId"] = httpContext.TraceIdentifier,
                },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unbehandelte Exception bei {RequestId} für {Path}")]
    private static partial void LogUnhandledException(ILogger logger, string requestId, PathString path, Exception exception);
}
