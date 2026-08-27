using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Middleware;

/// <summary>Writes an AuditLog row for every mutating (non-GET/HEAD) request made while a super
/// admin is impersonating a tenant (see TenantContextMiddleware/IJwtTokenService.
/// GenerateImpersonationToken) — the first automatic AuditLog writer in the codebase; every other
/// write today is a manual call from within a handler (e.g.
/// SuperAdminHandler.ChangeTenantStatusAsync). Runs after authorization so it only fires for
/// requests that were actually allowed to execute.</summary>
public class ImpersonationAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, DailyGourmetDbContext db)
    {
        var shouldAudit = tenantContext.IsImpersonation
            && !HttpMethods.IsGet(context.Request.Method)
            && !HttpMethods.IsHead(context.Request.Method);

        await next(context);

        if (shouldAudit && context.Response.StatusCode < 400)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                TenantId = tenantContext.TenantId,
                UserId = tenantContext.ImpersonatedBySuperAdminId,
                Action = $"{context.Request.Method} {context.Request.Path}",
                Entity = "Impersonation",
                EntityId = tenantContext.ImpersonationSessionId?.ToString() ?? string.Empty,
                Reason = "Impersonation",
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }
    }
}
