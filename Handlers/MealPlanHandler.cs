using System.Globalization;
using System.Text.Json;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.MealPlans;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Handlers;

public class MealPlanHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IFeatureFlagService featureFlags, IEmailService email, IOptions<AppOptions> appOptions)
{
    private static readonly string[] Weekdays = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag"];

    private static IQueryable<MealPlan> FullQuery(DailyGourmetDbContext db) => db.MealPlans
        .Include(m => m.Locations)
        .Include(m => m.Facilities)
        .Include(m => m.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<MealPlanDto>> ListAsync(int? year, int? calendarWeek, string? status, bool? isTemplate, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (year is { } y) query = query.Where(m => m.Year == y);
        if (calendarWeek is { } w) query = query.Where(m => m.CalendarWeek == w);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MealPlanStatus>(status, out var s)) query = query.Where(m => m.Status == s);
        if (isTemplate is { } template) query = query.Where(m => m.IsTemplate == template);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CalendarWeek)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<MealPlanDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<MealPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id));

    public async Task<MealPlanDto> CreateAsync(CreateMealPlanDto dto, CancellationToken ct = default)
    {
        if (!dto.IsTemplate && dto.FacilityIds.Length == 0)
            throw new ValidationException("Für einen Wochenplan ist mindestens eine Einrichtung erforderlich.");

        var tenantId = tenantContext.TenantId!.Value;
        var plan = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = tenantId, CalendarWeek = dto.CalendarWeek, Year = dto.Year, Status = MealPlanStatus.DRAFT,
            IsTemplate = dto.IsTemplate, TemplateSlot = dto.IsTemplate ? dto.TemplateSlot : null,
        };
        db.MealPlans.Add(plan);
        AddLocations(plan.Id, dto.LocationIds);
        if (!dto.IsTemplate) AddFacilities(plan.Id, dto.FacilityIds, tenantId, dto.Year, dto.CalendarWeek);
        AddDays(plan.Id, dto.Year, dto.CalendarWeek);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlanFacilities_TenantId_FacilityId_Year_CalendarWeek") == true)
        {
            throw new ConflictException("Für mindestens eine der ausgewählten Einrichtungen existiert in dieser Kalenderwoche bereits ein Speiseplan.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlans_TenantId_TemplateSlot") == true)
        {
            throw new ConflictException("Dieser Vorlagenplatz (1-8) ist bereits belegt.");
        }
        return await GetByIdAsync(plan.Id, ct);
    }

    public async Task<MealPlanDto> UpdateAsync(Guid id, UpdateMealPlanDto dto, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.Include(m => m.Locations).Include(m => m.Facilities)
            .Include(m => m.Days).ThenInclude(d => d.Items)
            .FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status is not (MealPlanStatus.DRAFT or MealPlanStatus.REVIEW))
            throw new ConflictException("Speiseplan kann in diesem Status nicht mehr bearbeitet werden.");
        if (!plan.IsTemplate && dto.FacilityIds.Length == 0)
            throw new ValidationException("Für einen Wochenplan ist mindestens eine Einrichtung erforderlich.");

        db.MealPlanLocations.RemoveRange(plan.Locations);
        AddLocations(plan.Id, dto.LocationIds);
        if (!plan.IsTemplate)
        {
            db.MealPlanFacilities.RemoveRange(plan.Facilities);
            AddFacilities(plan.Id, dto.FacilityIds, plan.TenantId, plan.Year, plan.CalendarWeek);
        }

        foreach (var dayDto in dto.Days)
        {
            var day = plan.Days.FirstOrDefault(d => d.Id == dayDto.DayId) ?? throw new ValidationException("Unbekannter Tag in diesem Speiseplan.");
            day.Note = dayDto.Note;
            db.MealPlanItems.RemoveRange(day.Items);
            foreach (var itemDto in dayDto.Items)
            {
                var dietLine = Enum.TryParse<DietLine>(itemDto.DietLine, out var dl) ? dl : DietLine.NORMALKOST;
                db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = day.Id, RecipeId = itemDto.RecipeId, DietLine = dietLine, CreatedAt = DateTime.UtcNow });
            }
        }

        plan.UpdatedAt = DateTime.UtcNow;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlanFacilities_TenantId_FacilityId_Year_CalendarWeek") == true)
        {
            throw new ConflictException("Für mindestens eine der ausgewählten Einrichtungen existiert in dieser Kalenderwoche bereits ein Speiseplan.");
        }
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Duplicates a plan — a template or an arbitrary past/other week — into a target
    /// calendar week. Serves both "create this week from Vorlage 3" and "duplicate KW36 into KW37"
    /// with one operation; when no explicit target is given, defaults to the next ISO week after
    /// the source (the original behavior of the plain "Duplizieren" action).</summary>
    public async Task<MealPlanDto> DuplicateAsync(Guid id, Guid[]? targetFacilityIds = null, int? targetYear = null, int? targetCalendarWeek = null, CancellationToken ct = default)
    {
        var source = await FullQuery(db).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        var resolvedFacilityIds = targetFacilityIds is { Length: > 0 } ? targetFacilityIds : source.Facilities.Select(f => f.FacilityId).ToArray();
        if (resolvedFacilityIds.Length == 0)
            throw new ValidationException("Für den neuen Wochenplan ist mindestens eine Einrichtung erforderlich.");

        int targetWeek, targetYearResolved;
        if (targetYear is { } ty && targetCalendarWeek is { } tw)
        {
            targetYearResolved = ty;
            targetWeek = tw;
        }
        else
        {
            var weeksInYear = ISOWeek.GetWeeksInYear(source.Year);
            if (source.CalendarWeek + 1 > weeksInYear) { targetWeek = 1; targetYearResolved = source.Year + 1; }
            else { targetWeek = source.CalendarWeek + 1; targetYearResolved = source.Year; }
        }

        var copy = new MealPlan { Id = Guid.NewGuid(), TenantId = source.TenantId, CalendarWeek = targetWeek, Year = targetYearResolved, Status = MealPlanStatus.DRAFT };
        db.MealPlans.Add(copy);
        foreach (var loc in source.Locations) db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = copy.Id, LocationId = loc.LocationId });
        AddFacilities(copy.Id, resolvedFacilityIds, source.TenantId, targetYearResolved, targetWeek);

        var monday = ISOWeek.ToDateTime(targetYearResolved, targetWeek, DayOfWeek.Monday);
        var sourceDays = source.Days.OrderBy(d => d.Date).ToList();
        for (var i = 0; i < 5; i++)
        {
            var newDay = new MealPlanDay { Id = Guid.NewGuid(), MealPlanId = copy.Id, Weekday = Weekdays[i], Date = DateOnly.FromDateTime(monday.AddDays(i)), CreatedAt = DateTime.UtcNow };
            db.MealPlanDays.Add(newDay);
            if (i < sourceDays.Count)
            {
                newDay.Note = sourceDays[i].Note;
                foreach (var item in sourceDays[i].Items)
                    db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = newDay.Id, RecipeId = item.RecipeId, DietLine = item.DietLine, CreatedAt = DateTime.UtcNow });
            }
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlanFacilities_TenantId_FacilityId_Year_CalendarWeek") == true)
        {
            throw new ConflictException("Für mindestens eine der ausgewählten Einrichtungen existiert in dieser Kalenderwoche bereits ein Speiseplan.");
        }
        return await GetByIdAsync(copy.Id, ct);
    }

    /// <summary>Turns an existing plan into a new, independent template (slot 1-8) — a deep copy, not
    /// a conversion, so the source plan (its facilities, status, live orders) is completely untouched.
    /// The template is facility-neutral (no MealPlanFacility rows at all — see MealPlan.Facilities doc
    /// comment) and keeps the source's day/dish structure verbatim, including the original dates —
    /// those are cosmetic for a template (it's never scheduled itself), so no ISO-week recompute is
    /// needed here unlike DuplicateAsync.</summary>
    public async Task<MealPlanDto> MarkAsTemplateAsync(Guid id, int templateSlot, CancellationToken ct = default)
    {
        var source = await FullQuery(db).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);

        var template = new MealPlan
        {
            Id = Guid.NewGuid(), TenantId = source.TenantId, CalendarWeek = source.CalendarWeek, Year = source.Year,
            Status = MealPlanStatus.DRAFT, IsTemplate = true, TemplateSlot = templateSlot,
        };
        db.MealPlans.Add(template);
        foreach (var loc in source.Locations) db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = template.Id, LocationId = loc.LocationId });

        foreach (var sourceDay in source.Days)
        {
            var newDay = new MealPlanDay { Id = Guid.NewGuid(), MealPlanId = template.Id, Weekday = sourceDay.Weekday, Date = sourceDay.Date, Note = sourceDay.Note, CreatedAt = DateTime.UtcNow };
            db.MealPlanDays.Add(newDay);
            foreach (var item in sourceDay.Items)
                db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = newDay.Id, RecipeId = item.RecipeId, DietLine = item.DietLine, CreatedAt = DateTime.UtcNow });
        }

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlans_TenantId_TemplateSlot") == true)
        {
            throw new ConflictException("Dieser Vorlagenplatz (1-8) ist bereits belegt.");
        }
        return await GetByIdAsync(template.Id, ct);
    }

    /// <summary>Drafts and plans still in review can both be removed — review included, since an
    /// admin who spots a mistake mid-review shouldn't have to reject-then-delete. Deleting a
    /// submitted plan notifies the tenant's other admins (excluding whoever just deleted it), since
    /// it disappears from their queue without them having acted on it.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status is not (MealPlanStatus.DRAFT or MealPlanStatus.REVIEW))
            throw new ConflictException("Nur Entwürfe oder Pläne in Prüfung können gelöscht werden.");

        var wasInReview = plan.Status == MealPlanStatus.REVIEW;
        var tenantId = plan.TenantId;
        var week = plan.CalendarWeek;
        var year = plan.Year;
        db.MealPlans.Remove(plan);
        await db.SaveChangesAsync(ct);

        if (wasInReview)
        {
            await NotifyOtherAdminsAsync(tenantId,
                $"Wochenplan KW {week}/{year} gelöscht",
                $"Wochenplan KW {week}/{year} wurde gelöscht.",
                $"<p>Der Wochenplan für KW {week}/{year}, der sich in Prüfung befand, wurde gelöscht.</p>",
                $"Der Wochenplan für KW {week}/{year}, der sich in Prüfung befand, wurde gelöscht.",
                ct);
        }
    }

    public async Task<MealPlanDto> SubmitReviewAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != MealPlanStatus.DRAFT) throw new ConflictException($"Übergang von {plan.Status} nach REVIEW ist nicht erlaubt.");
        plan.Status = MealPlanStatus.REVIEW;
        plan.RejectionReason = null;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>REVIEW → DRAFT with a required reason, rather than a separate terminal status — a
    /// rejection is feedback for revision, not a dead end, and reusing DRAFT keeps it editable
    /// through the existing UpdateAsync path without new status-transition logic. The reason is
    /// cleared again by SubmitReviewAsync so it never lingers past the next resubmission.</summary>
    public async Task<MealPlanDto> RejectAsync(Guid id, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ValidationException("Für eine Ablehnung ist ein Grund erforderlich.");
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != MealPlanStatus.REVIEW) throw new ConflictException("Nur Pläne in Prüfung können abgelehnt werden.");

        plan.Status = MealPlanStatus.DRAFT;
        plan.RejectionReason = reason.Trim();
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var reasonEncoded = System.Net.WebUtility.HtmlEncode(plan.RejectionReason);
        await NotifyOtherAdminsAsync(plan.TenantId,
            $"Wochenplan KW {plan.CalendarWeek}/{plan.Year} abgelehnt",
            $"Wochenplan KW {plan.CalendarWeek}/{plan.Year} wurde abgelehnt.",
            $"<p>Der Wochenplan für KW {plan.CalendarWeek}/{plan.Year} wurde zur Überarbeitung zurückgeschickt.</p><p><strong>Grund:</strong> {reasonEncoded}</p>",
            $"Der Wochenplan für KW {plan.CalendarWeek}/{plan.Year} wurde zur Überarbeitung zurückgeschickt.\nGrund: {plan.RejectionReason}",
            ct);

        return await GetByIdAsync(id, ct);
    }

    public async Task<MealPlanDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.Include(m => m.Facilities)
            .Include(m => m.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != MealPlanStatus.REVIEW) throw new ConflictException("Nur Pläne in Prüfung können veröffentlicht werden.");

        foreach (var day in plan.Days)
        foreach (var item in day.Items)
        {
            var snapshot = new { item.Recipe.Id, item.Recipe.Name, item.Recipe.StandardPortions, item.Recipe.Nutrition, DietLine = item.DietLine.ToString() };
            item.RecipeSnapshotJson = JsonSerializer.Serialize(snapshot);
        }
        plan.Status = MealPlanStatus.PUBLISHED;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        await NotifyFacilitiesOfPublishAsync(plan, ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Tells every active facility user of a just-published plan that they can now view it
    /// in the Kundenportal. Silently skipped when the tenant doesn't have kundenportal enabled — the
    /// portal link in the email would otherwise 403 for every recipient (see PortalListAsync's own
    /// gate on the same flag).</summary>
    private async Task NotifyFacilitiesOfPublishAsync(MealPlan plan, CancellationToken ct)
    {
        if (plan.Facilities.Count == 0) return;
        if (!await featureFlags.IsEnabledAsync(plan.TenantId, "kundenportal", ct)) return;

        var facilityIds = plan.Facilities.Select(f => f.FacilityId).ToArray();
        var empfaenger = await db.Users
            .Where(u => u.FacilityId != null && facilityIds.Contains(u.FacilityId.Value) &&
                        (u.Role == Role.FACILITY_ADMIN || u.Role == Role.FACILITY_USER) && u.Status == UserStatus.AKTIV)
            .ToListAsync(ct);
        if (empfaenger.Count == 0) return;

        var baseUrl = appOptions.Value.PublicBaseUrl.TrimEnd('/');
        var portalUrl = $"{baseUrl}/portal/meal-plans";
        var subject = $"Speiseplan KW {plan.CalendarWeek}/{plan.Year} veröffentlicht";
        var bodyHtml = $"<p>Der Speiseplan für KW {plan.CalendarWeek}/{plan.Year} wurde veröffentlicht und steht ab sofort im Kundenportal zur Einsicht bereit.</p>";
        var html = EmailTemplate.Render($"Speiseplan KW {plan.CalendarWeek}/{plan.Year} ist jetzt verfügbar.", bodyHtml, "Speiseplan ansehen", portalUrl);
        var text = $"Der Speiseplan für KW {plan.CalendarWeek}/{plan.Year} wurde veröffentlicht.\nJetzt ansehen: {portalUrl}";

        foreach (var user in empfaenger)
            await email.SendAsync(user.Email, user.Name, subject, html, text);
    }

    public async Task<MealPlanDto> UnpublishAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != MealPlanStatus.PUBLISHED) throw new ConflictException("Nur veröffentlichte Pläne können zurückgezogen werden.");

        var settings = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == plan.TenantId, ct);
        var requireNoOrders = settings?.UnpublishRequiresNoOrders ?? true;
        if (requireNoOrders && await db.Orders.AnyAsync(o => o.MealPlanId == id, ct))
            throw new ConflictException("Veröffentlichung kann nicht zurückgezogen werden — es liegen bereits Bestellungen vor.");

        plan.Status = MealPlanStatus.REVIEW;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<MealPlanDto> ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        plan.Status = MealPlanStatus.ARCHIVED;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public Task<MealPlanDto> PreviewAsync(Guid id, Guid? facilityId, CancellationToken ct = default) => GetByIdAsync(id, ct);

    public async Task<List<MealPlanDto>> PortalListAsync(CancellationToken ct = default)
    {
        var facilityId = tenantContext.FacilityId ?? throw new ForbiddenException("Kein Einrichtungskontext vorhanden.");
        if (!await featureFlags.IsEnabledAsync(tenantContext.TenantId!.Value, "kundenportal", ct))
            throw new ForbiddenException("Das Kundenportal ist für Ihren Mandanten nicht aktiviert.");
        var plans = await FullQuery(db)
            .Where(m => m.Facilities.Any(f => f.FacilityId == facilityId) &&
                        (m.Status == MealPlanStatus.PUBLISHED || m.Status == MealPlanStatus.CLOSED || m.Status == MealPlanStatus.ARCHIVED))
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.CalendarWeek)
            .ToListAsync(ct);
        return plans.Select(ToDto).ToList();
    }

    /// <summary>Detaches one facility from a shared plan without touching the rest — the facility
    /// keeps whatever it had before (nothing), and an admin can separately give it its own version
    /// via MarkAsTemplateAsync/DuplicateAsync if needed. Always leaves at least one facility behind;
    /// a plan with zero facilities is a state nothing else in the app expects (mirrors the
    /// "at least one facility" requirement on Create/Update).</summary>
    public async Task<MealPlanDto> RemoveFacilityAsync(Guid id, Guid facilityId, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.Include(m => m.Facilities).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        var link = plan.Facilities.FirstOrDefault(f => f.FacilityId == facilityId) ?? throw new NotFoundException(nameof(MealPlanFacility), facilityId);
        if (plan.Facilities.Count <= 1)
            throw new ConflictException("Die letzte Einrichtung kann nicht entfernt werden — dafür den ganzen Plan löschen.");

        db.MealPlanFacilities.Remove(link);
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Mirrors OrderHandler.NotifyTenantAdminsOfNewOrderAsync's recipient pattern (fresh
    /// role query, no hardcoded addresses) — excludes the acting admin themselves so they don't get
    /// emailed about their own reject/delete.</summary>
    private async Task NotifyOtherAdminsAsync(Guid tenantId, string subject, string preheader, string bodyHtml, string plainText, CancellationToken ct)
    {
        var empfaenger = await db.Users
            .Where(u => u.TenantId == tenantId && (u.Role == Role.TENANT_OWNER || u.Role == Role.TENANT_ADMIN) && u.Status == UserStatus.AKTIV && u.Id != tenantContext.UserId)
            .ToListAsync(ct);
        if (empfaenger.Count == 0) return;

        var html = EmailTemplate.Render(preheader, bodyHtml);
        foreach (var user in empfaenger)
            await email.SendAsync(user.Email, user.Name, subject, html, plainText);
    }

    private void AddLocations(Guid planId, Guid[] locationIds)
    {
        foreach (var locationId in locationIds.Distinct()) db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = planId, LocationId = locationId });
    }

    /// <summary>Year/CalendarWeek are denormalized onto each row here (copied from the plan they'll
    /// belong to) so the DB-level unique index on MealPlanFacilities can enforce "one plan per
    /// facility per week" without a join — see MealPlanFacility's doc comment.</summary>
    private void AddFacilities(Guid planId, Guid[] facilityIds, Guid tenantId, int year, int calendarWeek)
    {
        foreach (var facilityId in facilityIds.Distinct())
            db.MealPlanFacilities.Add(new MealPlanFacility { MealPlanId = planId, FacilityId = facilityId, TenantId = tenantId, Year = year, CalendarWeek = calendarWeek });
    }

    private void AddDays(Guid planId, int year, int calendarWeek)
    {
        var monday = ISOWeek.ToDateTime(year, calendarWeek, DayOfWeek.Monday);
        for (var i = 0; i < 5; i++)
            db.MealPlanDays.Add(new MealPlanDay { Id = Guid.NewGuid(), MealPlanId = planId, Weekday = Weekdays[i], Date = DateOnly.FromDateTime(monday.AddDays(i)), CreatedAt = DateTime.UtcNow });
    }

    private static MealPlanDto ToDto(MealPlan m) => new()
    {
        Id = m.Id,
        CalendarWeek = m.CalendarWeek,
        Year = m.Year,
        Status = m.Status.ToString(),
        IsTemplate = m.IsTemplate,
        TemplateSlot = m.TemplateSlot,
        LocationIds = m.Locations.Select(l => l.LocationId).ToArray(),
        FacilityIds = m.Facilities.Select(f => f.FacilityId).ToArray(),
        RejectionReason = m.RejectionReason,
        Days = m.Days.OrderBy(d => d.Date).Select(d => new MealPlanDayDto
        {
            Id = d.Id, Weekday = d.Weekday, Date = d.Date, Note = d.Note,
            Items = d.Items.Select(i => new MealPlanItemDto { Id = i.Id, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty, DietLine = i.DietLine.ToString() }).ToList(),
        }).ToList(),
    };
}
