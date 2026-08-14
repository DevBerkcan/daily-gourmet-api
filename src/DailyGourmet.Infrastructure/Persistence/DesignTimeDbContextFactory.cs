using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DailyGourmet.Infrastructure.Persistence;

/// <summary>
/// Ermöglicht "dotnet ef migrations add" ohne Start des vollständigen Api-Composition-Roots.
/// Nur für Design-Time-Tooling relevant — die Laufzeit-Registrierung erfolgt über
/// <see cref="DependencyInjection.AddInfrastructure"/> mit der echten Konfiguration aus der Api.
/// Der lokale Fallback-Connection-String ist ein reines Dev-Passwort (siehe docker-compose.yml)
/// und niemals für Produktion gedacht — Produktion setzt CONNECTIONSTRINGS__DEFAULT als Env-Variable.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDevConnectionString =
        "Server=localhost,1433;Database=DailyGourmet;User Id=sa;Password=Dev_Passw0rd!;TrustServerCertificate=True";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__DEFAULT")
            ?? LocalDevConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
