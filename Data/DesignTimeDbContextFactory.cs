using DailyGourmet.Api.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace DailyGourmet.Api.Data;

/// <summary>Lets `dotnet ef migrations add/database update` construct the DbContext without the
/// full DI container. Reads the same appsettings.json/appsettings.{Environment}.json as the app
/// (falling back to a LocalDB default) so `dotnet ef database update` targets the same database
/// the app would use locally.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DailyGourmetDbContext>
{
    public DailyGourmetDbContext CreateDbContext(string[] args)
    {
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{env}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            connectionString = "Server=(localdb)\\MSSQLLocalDB;Database=DailyGourmetDev;Trusted_Connection=True;TrustServerCertificate=True";

        var optionsBuilder = new DbContextOptionsBuilder<DailyGourmetDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new DailyGourmetDbContext(optionsBuilder.Options, new TenantContext());
    }
}
