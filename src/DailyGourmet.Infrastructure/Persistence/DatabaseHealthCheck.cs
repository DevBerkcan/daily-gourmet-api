using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DailyGourmet.Infrastructure.Persistence;

/// <summary>Prüft für /health/ready, ob die Datenbank erreichbar ist — ohne Zusatzpaket, nur CanConnectAsync.</summary>
public sealed class DatabaseHealthCheck(AppDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("Datenbank ist nicht erreichbar.");
    }
}
