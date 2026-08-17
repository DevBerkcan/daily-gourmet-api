using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Repositories.Implementations;

public class TenantSettingsRepository(DailyGourmetDbContext db) : ITenantSettingsRepository
{
    public Task<TenantSettings?> GetAsync(Guid tenantId, CancellationToken ct = default) =>
        db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

    public Task<TenantProfile?> GetProfileAsync(Guid tenantId, CancellationToken ct = default) =>
        db.TenantProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
