using System.Globalization;
using System.Text.Json;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.MealPlans;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class MealPlanHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly string[] Weekdays = ["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag"];

    private static IQueryable<MealPlan> FullQuery(DailyGourmetDbContext db) => db.MealPlans
        .Include(m => m.Locations)
        .Include(m => m.Facilities)
        .Include(m => m.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<MealPlanDto>> ListAsync(int? year, int? calendarWeek, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (year is { } y) query = query.Where(m => m.Year == y);
        if (calendarWeek is { } w) query = query.Where(m => m.CalendarWeek == w);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MealPlanStatus>(status, out var s)) query = query.Where(m => m.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(m => m.Year).ThenByDescending(m => m.CalendarWeek)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<MealPlanDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<MealPlanDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id));

    public async Task<MealPlanDto> CreateAsync(CreateMealPlanDto dto, CancellationToken ct = default)
    {
        var plan = new MealPlan { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, CalendarWeek = dto.CalendarWeek, Year = dto.Year, Status = MealPlanStatus.DRAFT };
        db.MealPlans.Add(plan);
        AddLocationsAndFacilities(plan.Id, dto.LocationIds, dto.FacilityIds);
        AddDays(plan.Id, dto.Year, dto.CalendarWeek);

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_MealPlans_TenantId_Year_CalendarWeek") == true)
        {
            throw new ConflictException("Für diese Kalenderwoche existiert bereits ein Speiseplan.");
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

        db.MealPlanLocations.RemoveRange(plan.Locations);
        db.MealPlanFacilities.RemoveRange(plan.Facilities);
        AddLocationsAndFacilities(plan.Id, dto.LocationIds, dto.FacilityIds);

        foreach (var dayDto in dto.Days)
        {
            var day = plan.Days.FirstOrDefault(d => d.Id == dayDto.DayId) ?? throw new ValidationException("Unbekannter Tag in diesem Speiseplan.");
            day.Note = dayDto.Note;
            db.MealPlanItems.RemoveRange(day.Items);
            foreach (var recipeId in dayDto.RecipeIds)
                db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = day.Id, RecipeId = recipeId, CreatedAt = DateTime.UtcNow });
        }

        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<MealPlanDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await FullQuery(db).FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);

        var weeksInYear = ISOWeek.GetWeeksInYear(source.Year);
        int targetWeek, targetYear;
        if (source.CalendarWeek + 1 > weeksInYear) { targetWeek = 1; targetYear = source.Year + 1; }
        else { targetWeek = source.CalendarWeek + 1; targetYear = source.Year; }

        var copy = new MealPlan { Id = Guid.NewGuid(), TenantId = source.TenantId, CalendarWeek = targetWeek, Year = targetYear, Status = MealPlanStatus.DRAFT };
        db.MealPlans.Add(copy);
        foreach (var loc in source.Locations) db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = copy.Id, LocationId = loc.LocationId });
        foreach (var fac in source.Facilities) db.MealPlanFacilities.Add(new MealPlanFacility { MealPlanId = copy.Id, FacilityId = fac.FacilityId });

        var monday = ISOWeek.ToDateTime(targetYear, targetWeek, DayOfWeek.Monday);
        var sourceDays = source.Days.OrderBy(d => d.Date).ToList();
        for (var i = 0; i < 5; i++)
        {
            var newDay = new MealPlanDay { Id = Guid.NewGuid(), MealPlanId = copy.Id, Weekday = Weekdays[i], Date = DateOnly.FromDateTime(monday.AddDays(i)), CreatedAt = DateTime.UtcNow };
            db.MealPlanDays.Add(newDay);
            if (i < sourceDays.Count)
            {
                newDay.Note = sourceDays[i].Note;
                foreach (var item in sourceDays[i].Items)
                    db.MealPlanItems.Add(new MealPlanItem { Id = Guid.NewGuid(), MealPlanDayId = newDay.Id, RecipeId = item.RecipeId, CreatedAt = DateTime.UtcNow });
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(copy.Id, ct);
    }

    public async Task<MealPlanDto> SubmitReviewAsync(Guid id, CancellationToken ct = default) => await TransitionAsync(id, MealPlanStatus.DRAFT, MealPlanStatus.REVIEW, ct);

    public async Task<MealPlanDto> PublishAsync(Guid id, CancellationToken ct = default)
    {
        var plan = await db.MealPlans.Include(m => m.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Nutrition)
            .FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != MealPlanStatus.REVIEW) throw new ConflictException("Nur Pläne in Prüfung können veröffentlicht werden.");

        foreach (var day in plan.Days)
        foreach (var item in day.Items)
        {
            var snapshot = new { item.Recipe.Id, item.Recipe.Name, item.Recipe.StandardPortions, item.Recipe.Nutrition };
            item.RecipeSnapshotJson = JsonSerializer.Serialize(snapshot);
        }
        plan.Status = MealPlanStatus.PUBLISHED;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
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
        var plans = await FullQuery(db)
            .Where(m => m.Facilities.Any(f => f.FacilityId == facilityId) &&
                        (m.Status == MealPlanStatus.PUBLISHED || m.Status == MealPlanStatus.CLOSED || m.Status == MealPlanStatus.ARCHIVED))
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.CalendarWeek)
            .ToListAsync(ct);
        return plans.Select(ToDto).ToList();
    }

    private async Task<MealPlanDto> TransitionAsync(Guid id, MealPlanStatus from, MealPlanStatus to, CancellationToken ct)
    {
        var plan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == id, ct) ?? throw new NotFoundException(nameof(MealPlan), id);
        if (plan.Status != from) throw new ConflictException($"Übergang von {plan.Status} nach {to} ist nicht erlaubt.");
        plan.Status = to;
        plan.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private void AddLocationsAndFacilities(Guid planId, Guid[] locationIds, Guid[] facilityIds)
    {
        foreach (var locationId in locationIds.Distinct()) db.MealPlanLocations.Add(new MealPlanLocation { MealPlanId = planId, LocationId = locationId });
        foreach (var facilityId in facilityIds.Distinct()) db.MealPlanFacilities.Add(new MealPlanFacility { MealPlanId = planId, FacilityId = facilityId });
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
        LocationIds = m.Locations.Select(l => l.LocationId).ToArray(),
        FacilityIds = m.Facilities.Select(f => f.FacilityId).ToArray(),
        Days = m.Days.OrderBy(d => d.Date).Select(d => new MealPlanDayDto
        {
            Id = d.Id, Weekday = d.Weekday, Date = d.Date, Note = d.Note,
            Items = d.Items.Select(i => new MealPlanItemDto { Id = i.Id, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty }).ToList(),
        }).ToList(),
    };
}
