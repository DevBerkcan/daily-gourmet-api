using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class AuditLogHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<PagedResult<AuditLogDto>> ListAsync(Guid? userId, string? action, string? entity, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.AuditLogs.Include(a => a.User).Where(a => a.TenantId == tenantContext.TenantId);
        if (userId is { } uid) query = query.Where(a => a.UserId == uid);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action.Contains(action));
        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (from is { } f) query = query.Where(a => a.CreatedAtUtc >= f);
        if (to is { } t) query = query.Where(a => a.CreatedAtUtc <= t);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<AuditLogDto>
        {
            Items = items.Select(a => new AuditLogDto { Id = a.Id, Action = a.Action, Entity = a.Entity, EntityId = a.EntityId, Reason = a.Reason, UserName = a.User?.Name ?? "System", CreatedAtUtc = a.CreatedAtUtc }).ToList(),
            Total = total, Page = page, PageSize = pageSize,
        };
    }

    /// <summary>Cross-tenant audit trail for the Super Admin — unlike <see cref="ListAsync"/>, not scoped to the caller's tenant.</summary>
    public async Task<PagedResult<GlobalAuditLogDto>> ListForSuperAdminAsync(Guid? tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.AuditLogs.IgnoreQueryFilters().Include(a => a.User).Include(a => a.Tenant).AsQueryable();
        if (tenantId is { } tid) query = query.Where(a => a.TenantId == tid);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<GlobalAuditLogDto>
        {
            Items = items.Select(a => new GlobalAuditLogDto
            {
                Id = a.Id, Action = a.Action, Entity = a.Entity, EntityId = a.EntityId, Reason = a.Reason,
                UserName = a.User?.Name ?? "System", TenantName = a.Tenant?.Name, CreatedAtUtc = a.CreatedAtUtc,
            }).ToList(),
            Total = total, Page = page, PageSize = pageSize,
        };
    }
}
