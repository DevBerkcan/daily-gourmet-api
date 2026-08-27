using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Authentication;

public interface IJwtTokenService
{
    string GenerateToken(User user);

    /// <summary>Issues a short-lived token for a super admin actively impersonating a tenant (see
    /// SupportSessionHandler.ImpersonateAsync). `sub` stays the super admin's own id, so anything
    /// written while impersonating is correctly attributed — but the `role`/`tenantId` claims are the
    /// target tenant's, so every existing tenant-scoped [Authorize] check and EF query filter "just
    /// works" unmodified. Expires exactly at the backing SupportSession's expiry, not the normal
    /// token lifetime.</summary>
    string GenerateImpersonationToken(User superAdmin, Guid targetTenantId, Guid supportSessionId, DateTime expiresAtUtc);
}
