using System.Reflection;

namespace DailyGourmet.Api.Endpoints;

public static class VersionEndpoint
{
    public static WebApplication MapVersionEndpoint(this WebApplication app)
    {
        app.MapGet("/version", () =>
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            return Results.Ok(new { version });
        });

        return app;
    }
}
