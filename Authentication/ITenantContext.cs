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

    /// <summary>True for a request authenticated with an impersonation token (see
    /// IJwtTokenService.GenerateImpersonationToken) — a super admin actively browsing a tenant's
    /// admin UI as that tenant via an active SupportSession.</summary>
    bool IsImpersonation { get; }
    /// <summary>The real super admin's own user id while impersonating — UserId instead carries the
    /// same value (see GenerateImpersonationToken's `sub` choice), this is kept as an explicit,
    /// self-describing claim for anything (e.g. ImpersonationAuditMiddleware) that shouldn't have to
    /// know that detail.</summary>
    Guid? ImpersonatedBySuperAdminId { get; }
    /// <summary>The SupportSession backing the current impersonation token — TenantContextMiddleware
    /// re-validates this session is still active on every request.</summary>
    Guid? ImpersonationSessionId { get; }

    void Set(Guid? tenantId, Guid? userId, string? role, Guid? facilityId, bool isImpersonation = false, Guid? impersonatedBySuperAdminId = null, Guid? impersonationSessionId = null);
}

public class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string? Role { get; private set; }
    public Guid? FacilityId { get; private set; }
    public bool IsSuperAdmin => Role == "SUPER_ADMIN";
    public bool IsImpersonation { get; private set; }
    public Guid? ImpersonatedBySuperAdminId { get; private set; }
    public Guid? ImpersonationSessionId { get; private set; }

    public void Set(Guid? tenantId, Guid? userId, string? role, Guid? facilityId, bool isImpersonation = false, Guid? impersonatedBySuperAdminId = null, Guid? impersonationSessionId = null)
    {
        TenantId = tenantId;
        UserId = userId;
        Role = role;
        FacilityId = facilityId;
        IsImpersonation = isImpersonation;
        ImpersonatedBySuperAdminId = impersonatedBySuperAdminId;
        ImpersonationSessionId = impersonationSessionId;
    }
}
