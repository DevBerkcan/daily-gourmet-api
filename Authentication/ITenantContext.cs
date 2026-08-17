namespace DailyGourmet.Api.Authentication;

/// <summary>Scoped per-request tenant context, populated by <see cref="TenantContextMiddleware"/>
/// from the caller's JWT claims. EF Core's global query filters read <see cref="TenantId"/> to
/// enforce hard multi-tenant isolation on every tenant-scoped entity.</summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    Guid? UserId { get; }
    string? Role { get; }
    Guid? FacilityId { get; }

    /// <summary>True once SUPER_ADMIN requests have explicitly opted out of the tenant filter
    /// for a cross-tenant query (via repository methods that call IgnoreQueryFilters()).</summary>
    bool IsSuperAdmin { get; }

    void Set(Guid? tenantId, Guid? userId, string? role, Guid? facilityId);
}

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public Guid? FacilityId { get; private set; }
    public bool IsSuperAdmin => Role == "SUPER_ADMIN";

    public void Set(Guid? tenantId, Guid? userId, string? role, Guid? facilityId)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        FacilityId = facilityId;
    }
}
