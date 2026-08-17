using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Repositories.Interfaces;

/// <summary>TenantSettings/TenantProfile are 1:1 config rows keyed by TenantId, not BaseEntity —
/// they don't fit the generic IRepository&lt;T&gt; constraint, so they get their own minimal contract.</summary>
public interface ITenantSettingsRepository
{
    Task<TenantSettings?> GetAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantProfile?> GetProfileAsync(Guid tenantId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
