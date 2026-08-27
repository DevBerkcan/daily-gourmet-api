namespace DailyGourmet.Api.Services;

/// <summary>Resolves whether a named feature flag is enabled for a tenant — a TenantFeatureFlag
/// override wins when one exists, otherwise the flag's own DefaultEnabled applies. Any handler that
/// needs to gate real behavior behind a flag (see DbSeeder's seeded flag catalog) injects this
/// rather than querying FeatureFlags/TenantFeatureFlags directly.</summary>
public interface IFeatureFlagService
{
    Task<bool> IsEnabledAsync(Guid tenantId, string flagKey, CancellationToken ct = default);
}
