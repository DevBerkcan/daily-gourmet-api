using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Tenant-facing read of the flag catalog resolved for the caller's own tenant — the
/// counterpart to SuperAdminHandler's admin-side CRUD, used by frontend code to gate real UI/
/// behavior via useFeatureFlag(key) rather than the flags being admin-editable but inert.</summary>
public class FeatureFlagHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<List<TenantFeatureFlagStatusDto>> ListForCurrentTenantAsync(CancellationToken ct = default)
    {
        var flags = await db.FeatureFlags.OrderBy(f => f.Key).ToListAsync(ct);
        if (tenantContext.TenantId is not { } tenantId)
            return flags.Select(f => new TenantFeatureFlagStatusDto { Key = f.Key, Enabled = f.DefaultEnabled }).ToList();

        var overrides = await db.TenantFeatureFlags.Where(x => x.TenantId == tenantId).ToDictionaryAsync(x => x.FeatureFlagId, x => x.Enabled, ct);
        return flags.Select(f => new TenantFeatureFlagStatusDto { Key = f.Key, Enabled = overrides.TryGetValue(f.Id, out var v) ? v : f.DefaultEnabled }).ToList();
    }
}
