using System.Security.Claims;
using DailyGourmet.Api.Authentication;

namespace DailyGourmet.Api.Middleware;

/// <summary>Runs after JWT authentication has populated HttpContext.User and fills the scoped
/// ITenantContext from its claims, so EF Core's global query filters (DailyGourmetDbContext) see
/// the caller's tenant/role for the rest of the request pipeline.</summary>
public class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            Guid? tenantId = Guid.TryParse(user.FindFirstValue(DgClaimTypes.TenantId), out var t) ? t : null;
            Guid? facilityId = Guid.TryParse(user.FindFirstValue(DgClaimTypes.FacilityId), out var f) ? f : null;
            Guid? userId = Guid.TryParse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub), out var u) ? u : null;
            var role = user.FindFirstValue(ClaimTypes.Role);

            tenantContext.Set(tenantId, userId, role, facilityId);
        }

        await next(context);
    }
}
