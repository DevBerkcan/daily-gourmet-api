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
}
