using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.MsSql;

namespace DailyGourmet.Api.IntegrationTests;

/// <summary>
/// Startet die Api gegen eine echte, containerisierte SQL-Server-Instanz (§52: "echte PostgreSQL/SQL-Server
/// Testinstanz, bevorzugt containerisiert"). Kein In-Memory-Provider — Migrations/Constraints/Concurrency
/// verhalten sich sonst anders als produktiv.
/// </summary>
public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting statt ConfigureAppConfiguration: die Werte müssen bereits verfügbar sein, bevor
        // Program.cs (Minimal Hosting, Top-Level-Statements) builder.Configuration synchron ausliest.
        builder.UseSetting("ConnectionStrings:Default", _sqlContainer.GetConnectionString());
        builder.UseSetting("Cors:FrontendOrigin", "http://localhost:3000");
    }

    public Task InitializeAsync() => _sqlContainer.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
        await base.DisposeAsync();
    }
}
