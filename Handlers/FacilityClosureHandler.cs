using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Facilities;
using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Schließtage/Abwesenheit — a facility's year-ahead closure calendar. Portal users manage
/// only their own facility (tenantContext.FacilityId); admin/Verwaltung can manage any facility's,
/// for entries that arrive late by email (see FacilityClosure.CreatedByUserId).</summary>
public class FacilityClosureHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<List<FacilityClosureDto>> ListAsync(Guid facilityId, CancellationToken ct = default)
    {
        EnsureFacilityAccess(facilityId);
        var closures = await db.FacilityClosures.Where(c => c.FacilityId == facilityId).OrderBy(c => c.StartDate).ToListAsync(ct);
        return closures.Select(ToDto).ToList();
    }

    public async Task<FacilityClosureDto> CreateAsync(Guid facilityId, SaveFacilityClosureDto dto, CancellationToken ct = default)
    {
        EnsureFacilityAccess(facilityId);
        if (dto.EndDate < dto.StartDate) throw new ValidationException("Enddatum darf nicht vor dem Startdatum liegen.");
        var exists = await db.Facilities.AnyAsync(f => f.Id == facilityId, ct);
        if (!exists) throw new NotFoundException(nameof(Facility), facilityId);

        var closure = new FacilityClosure
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, FacilityId = facilityId,
            StartDate = dto.StartDate, EndDate = dto.EndDate, Note = dto.Note,
            CreatedByUserId = tenantContext.FacilityId is null ? tenantContext.UserId : null,
            CreatedAt = DateTime.UtcNow,
        };
        db.FacilityClosures.Add(closure);
        await db.SaveChangesAsync(ct);
        return ToDto(closure);
    }

    public async Task DeleteAsync(Guid facilityId, Guid closureId, CancellationToken ct = default)
    {
        EnsureFacilityAccess(facilityId);
        var closure = await db.FacilityClosures.FirstOrDefaultAsync(c => c.Id == closureId && c.FacilityId == facilityId, ct)
            ?? throw new NotFoundException(nameof(FacilityClosure), closureId);
        db.FacilityClosures.Remove(closure);
        await db.SaveChangesAsync(ct);
    }

    private void EnsureFacilityAccess(Guid facilityId)
    {
        if (tenantContext.FacilityId is { } own && own != facilityId)
            throw new ForbiddenException("Kein Zugriff auf die Schließtage einer anderen Einrichtung.");
    }

    private static FacilityClosureDto ToDto(FacilityClosure c) => new()
    {
        Id = c.Id, FacilityId = c.FacilityId, StartDate = c.StartDate, EndDate = c.EndDate, Note = c.Note,
        AddedByAdmin = c.CreatedByUserId != null,
    };
}
