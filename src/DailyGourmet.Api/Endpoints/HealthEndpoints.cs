using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace DailyGourmet.Api.Endpoints;

/// <summary>
/// /health/live prüft nur, dass der Prozess läuft (keine registrierten Checks — immer Healthy).
/// /health/ready prüft Kernabhängigkeiten (aktuell: Datenbank, siehe DatabaseHealthCheck, Tag "ready").
/// Keine Secrets in den Responses (§42).
/// </summary>
public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
        });

        return app;
    }
}
