using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Support;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class SupportSessionHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<SupportSessionDto> StartAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new NotFoundException(nameof(Tenant), tenantId);

        var active = await db.SupportSessions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.EndedAtUtc == null && s.ExpiresAtUtc > DateTime.UtcNow, ct);
        if (active is not null)
        {
            active.EndedAtUtc = DateTime.UtcNow;
            active.EndedReason = SupportSessionEndReason.MANUAL;
        }

        var session = new SupportSession
        {
            Id = Guid.NewGuid(), TenantId = tenantId, StartedByUserId = tenantContext.UserId!.Value,
            StartedAtUtc = DateTime.UtcNow, ExpiresAtUtc = DateTime.UtcNow.AddMinutes(60), CreatedAt = DateTime.UtcNow,
        };
        db.SupportSessions.Add(session);
        await db.SaveChangesAsync(ct);

        return ToDto(session, tenant.Name, (await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == session.StartedByUserId, ct))?.Name ?? string.Empty);
    }

    public async Task EndAsync(Guid id, CancellationToken ct = default)
    {
        var session = await db.SupportSessions.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Id == id, ct) ?? throw new NotFoundException(nameof(SupportSession), id);
        if (session.EndedAtUtc is not null) throw new ConflictException("Supportzugriff wurde bereits beendet.");
        session.EndedAtUtc = DateTime.UtcNow;
        session.EndedReason = SupportSessionEndReason.MANUAL;
        await db.SaveChangesAsync(ct);
    }

    private static SupportSessionDto ToDto(SupportSession s, string tenantName, string startedByName) => new()
    {
        Id = s.Id, TenantId = s.TenantId, TenantName = tenantName, StartedByUserName = startedByName,
        StartedAtUtc = s.StartedAtUtc, ExpiresAtUtc = s.ExpiresAtUtc, EndedAtUtc = s.EndedAtUtc, EndedReason = s.EndedReason?.ToString(),
    };
}
