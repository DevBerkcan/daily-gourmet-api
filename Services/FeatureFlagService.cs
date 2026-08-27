using DailyGourmet.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Services;

public class FeatureFlagService(DailyGourmetDbContext db) : IFeatureFlagService
{
    public async Task<bool> IsEnabledAsync(Guid tenantId, string flagKey, CancellationToken ct = default)
    {
        var flag = await db.FeatureFlags.IgnoreQueryFilters().FirstOrDefaultAsync(f => f.Key == flagKey, ct);
        if (flag is null) return false;
        var overrideRow = await db.TenantFeatureFlags.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FeatureFlagId == flag.Id, ct);
        return overrideRow?.Enabled ?? flag.DefaultEnabled;
    }
}
