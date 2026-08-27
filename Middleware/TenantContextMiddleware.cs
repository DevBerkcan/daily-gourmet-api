using System.Security.Claims;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Middleware;

/// <summary>Runs after JWT authentication has populated HttpContext.User and fills the scoped
/// ITenantContext from its claims, so EF Core's global query filters (DailyGourmetDbContext) see
/// the caller's tenant/role for the rest of the request pipeline.</summary>
public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, DailyGourmetDbContext db)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            Guid? tenantId = Guid.TryParse(user.FindFirstValue(DgClaimTypes.TenantId), out var t) ? t : null;
            Guid? facilityId = Guid.TryParse(user.FindFirstValue(DgClaimTypes.FacilityId), out var f) ? f : null;
            Guid? userId = Guid.TryParse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub), out var u) ? u : null;
            var role = user.FindFirstValue(ClaimTypes.Role);
            var isImpersonation = user.FindFirstValue(DgClaimTypes.IsImpersonation) == "true";
            Guid? impersonatedBy = Guid.TryParse(user.FindFirstValue(DgClaimTypes.ImpersonatedBySuperAdminId), out var ib) ? ib : null;
            Guid? impersonationSessionId = Guid.TryParse(user.FindFirstValue(DgClaimTypes.SupportSessionId), out var sid) ? sid : null;

            tenantContext.Set(tenantId, userId, role, facilityId, isImpersonation, impersonatedBy, impersonationSessionId);

            if (isImpersonation)
            {
                // The JWT's own `exp` isn't enough — if the tenant or super admin ends the support
                // session early (see SupportSessionHandler.EndAsync/EndCurrentForCallerTenantAsync),
                // the still-technically-unexpired token must stop working immediately. One indexed
                // lookup by SupportSession.Id, only ever hit for the rare impersonation case.
                var sessionStillActive = impersonationSessionId is { } sid2 && await db.SupportSessions.IgnoreQueryFilters()
                    .AnyAsync(s => s.Id == sid2 && s.EndedAtUtc == null && s.ExpiresAtUtc > DateTime.UtcNow);
                if (!sessionStillActive)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }
            }
        }

        await next(context);
    }
}
