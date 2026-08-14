using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Infrastructure.Persistence;

/// <summary>
/// Zentraler EF-Core-Kontext. Übernimmt Repository-/Unit-of-Work-Rolle direkt (§6 der Vorgaben) —
/// kein zusätzliches IRepository/IUnitOfWork. Phase 1 enthält bewusst noch keine Domain-Entitäten
/// (siehe docs/backend/ARCHITECTURE_PLAN.md, Phase-Tabelle); Entity-Konfigurationen werden ab Phase 2
/// über <see cref="ModelBuilder.ApplyConfigurationsFromAssembly"/> aus Infrastructure/Persistence/Configurations
/// eingesammelt.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
