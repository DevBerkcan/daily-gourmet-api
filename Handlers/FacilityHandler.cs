using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Facilities;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using DailyGourmet.Api.Repositories.Interfaces;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Handlers;

public class FacilityHandler(
    IRepository<Facility> facilities,
    IRepository<Location> locations,
    ITenantSettingsRepository tenantSettings,
    ITenantContext tenantContext,
    DailyGourmetDbContext db,
    IEmailService email,
    IOptions<AppOptions> appOptions,
    IFeatureFlagService featureFlags)
{
    public async Task<PagedResult<FacilityDto>> ListAsync(string? search, Guid? locationId, Guid? tenantId, int page, int pageSize, CancellationToken ct = default)
    {
        // tenantId is only ever passed by the super-admin route (SuperAdminController) — a normal
        // tenant caller already gets this for free via the EF tenant query filter and passes null.
        var query = tenantId is { } tid ? facilities.Query().IgnoreQueryFilters().Where(f => f.TenantId == tid) : facilities.Query();
        query = query.Include(f => f.Location);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.Name.Contains(search) || f.ContactPerson.Contains(search));
        if (locationId is { } lid)
            query = query.Where(f => f.LocationId == lid);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => ToDto(f))
            .ToListAsync(ct);

        return new PagedResult<FacilityDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<FacilityDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var facility = await facilities.Query().Include(f => f.Location).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException(nameof(Facility), id);
        if (tenantContext.FacilityId is { } own && own != id)
            throw new ForbiddenException("Kein Zugriff auf eine andere Einrichtung.");
        return ToDto(facility);
    }

    public async Task<FacilityDto> CreateAsync(Guid tenantId, CreateFacilityDto dto, CancellationToken ct = default)
    {
        if (await locations.GetByIdAsync(dto.LocationId, ct) is null)
            throw new ValidationException("Der ausgewählte Standort existiert nicht.");

        var email = dto.Email.Trim();
        var autoInviteEnabled = await featureFlags.IsEnabledAsync(tenantId, "facility-auto-invite", ct);
        // Global uniqueness check (User.Email has a global unique index) — IgnoreQueryFilters()
        // because the normal User query filter only shows this tenant's own users, and we must
        // catch a collision with any tenant's account before it becomes a raw DB constraint error.
        if (autoInviteEnabled && await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Diese E-Mail-Adresse wird bereits für ein anderes Konto verwendet.");

        var settings = await tenantSettings.GetAsync(tenantId, ct);
        var prefix = settings?.FacilityNumberPrefix ?? "DG-1";
        // Explicit TenantId filter (not just facilities.Query(), which is only implicitly
        // tenant-scoped for a normal caller) — a super-admin call bypasses the EF query filter
        // entirely, so without this it would count facilities across every tenant.
        var nextSequence = await facilities.Query().IgnoreQueryFilters().CountAsync(f => f.TenantId == tenantId, ct) + 1;
        var customerNumber = $"{prefix}{nextSequence:000}";

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LocationId = dto.LocationId,
            Name = dto.Name.Trim(),
            CustomerNumber = customerNumber,
            Address = dto.Address.Trim(),
            ContactPerson = dto.ContactPerson.Trim(),
            Email = email,
            Phone = dto.Phone.Trim(),
            ActiveWeekdays = dto.ActiveWeekdays,
            PortionPrice = dto.PortionPrice,
            Status = FacilityStatus.AKTIV,
            Notes = dto.Notes,
            RouteNumber = dto.RouteNumber,
        };
        await facilities.AddAsync(facility, ct);
        await facilities.SaveChangesAsync(ct);

        facility.Location = (await locations.GetByIdAsync(dto.LocationId, ct))!;
        var result = ToDto(facility);

        // The facility's Email doubles as the login for its FACILITY_ADMIN — auto-invite one now,
        // the same way UserManagementHandler.InviteAsync invites any other user. Gated by the
        // facility-auto-invite flag so a tenant can opt out without a code change.
        if (autoInviteEnabled)
        {
            var invitedAdmin = UserInvitationHelper.BuildInvitedUser(tenantId, facility.Id, dto.ContactPerson.Trim(), email, Role.FACILITY_ADMIN);
            db.Users.Add(invitedAdmin);
            await db.SaveChangesAsync(ct);
            await SendFacilityInviteEmailAsync(invitedAdmin);
            result.AdminInvited = true;
        }

        return result;
    }

    private async Task SendFacilityInviteEmailAsync(User user)
    {
        var baseUrl = appOptions.Value.PublicBaseUrl.TrimEnd('/');
        var acceptUrl = $"{baseUrl}/accept-invite/{user.InvitationToken}";
        var html = $"""
            <p>Für Ihre Einrichtung wurde ein Zugang zu Daily Gourmet angelegt. Klicken Sie auf den folgenden Link, um Ihr Passwort festzulegen und sich anzumelden:</p>
            <p><a href="{acceptUrl}">Konto aktivieren</a></p>
            <p>Der Link ist 72 Stunden gültig.</p>
            """;
        var text = $"Für Ihre Einrichtung wurde ein Zugang zu Daily Gourmet angelegt. Passwort festlegen: {acceptUrl}\nDer Link ist 72 Stunden gültig.";
        await email.SendAsync(user.Email, user.Name, "Zugang zu Daily Gourmet", html, text);
    }

    public async Task<FacilityDto> UpdateAsync(Guid id, UpdateFacilityDto dto, CancellationToken ct = default)
    {
        var facility = await facilities.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Facility), id);
        if (await locations.GetByIdAsync(dto.LocationId, ct) is null)
            throw new ValidationException("Der ausgewählte Standort existiert nicht.");
        if (!Enum.TryParse<FacilityStatus>(dto.Status, out var status))
            throw new ValidationException("Ungültiger Status.");

        facility.LocationId = dto.LocationId;
        facility.Name = dto.Name.Trim();
        facility.Address = dto.Address.Trim();
        facility.ContactPerson = dto.ContactPerson.Trim();
        facility.Email = dto.Email.Trim();
        facility.Phone = dto.Phone.Trim();
        facility.ActiveWeekdays = dto.ActiveWeekdays;
        facility.PortionPrice = dto.PortionPrice;
        facility.Status = status;
        facility.Notes = dto.Notes;
        facility.RouteNumber = dto.RouteNumber;

        facilities.Update(facility);
        await facilities.SaveChangesAsync(ct);

        facility.Location = (await locations.GetByIdAsync(dto.LocationId, ct))!;
        return ToDto(facility);
    }

    /// <summary>Portal-Selbstbedienung: Einrichtung pflegt ihre eigenen Kontaktdaten — Preise, Tour,
    /// Standort und Status bleiben unangetastet (siehe <see cref="UpdatePortalFacilityDto"/>).</summary>
    public async Task<FacilityDto> UpdateOwnContactAsync(Guid id, UpdatePortalFacilityDto dto, CancellationToken ct = default)
    {
        var facility = await facilities.Query().Include(f => f.Location).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException(nameof(Facility), id);

        facility.Address = dto.Address.Trim();
        facility.ContactPerson = dto.ContactPerson.Trim();
        facility.Email = dto.Email.Trim();
        facility.Phone = dto.Phone.Trim();

        facilities.Update(facility);
        await facilities.SaveChangesAsync(ct);

        return ToDto(facility);
    }

    public async Task<FacilityDeleteImpactDto> GetDeleteImpactAsync(Guid id, CancellationToken ct = default)
    {
        if (await facilities.GetByIdAsync(id, ct) is null) throw new NotFoundException(nameof(Facility), id);
        return new FacilityDeleteImpactDto
        {
            OrderCount = await db.Orders.CountAsync(o => o.FacilityId == id, ct),
            ClosureCount = await db.FacilityClosures.CountAsync(c => c.FacilityId == id, ct),
            UserCount = await db.Users.CountAsync(u => u.FacilityId == id, ct),
            RouteStopCount = await db.RouteStops.CountAsync(s => s.FacilityId == id, ct),
        };
    }

    /// <summary>Hard delete, per product decision: cascades through everything referencing this
    /// facility rather than leaving it soft-archived. FK config on Order/RouteStop/MealPlanFacility/
    /// User is deliberately Restrict (see Data/Configurations), so the deletes below are ordered by
    /// hand instead of relying on DB cascade — RouteStops (and their Items) must go before Orders
    /// (and their Items) since RouteStopItem.OrderId/OrderItemId are themselves Restrict.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var facility = await facilities.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Facility), id);

        var stops = await db.RouteStops.Where(s => s.FacilityId == id).ToListAsync(ct);
        db.RouteStops.RemoveRange(stops);

        var orders = await db.Orders.Where(o => o.FacilityId == id).ToListAsync(ct);
        db.Orders.RemoveRange(orders);

        db.MealPlanFacilities.RemoveRange(db.MealPlanFacilities.Where(m => m.FacilityId == id));
        db.FacilityClosures.RemoveRange(db.FacilityClosures.Where(c => c.FacilityId == id));

        // Deactivate rather than hard-delete: User rows are Restrict-referenced elsewhere
        // (SupportTicket.CreatedByUserId, AuditLog.UserId, etc.) that this facility delete doesn't
        // touch, so hard-deleting could throw on an unrelated FK. Deactivating preserves history.
        var facilityUsers = await db.Users.Where(u => u.FacilityId == id).ToListAsync(ct);
        foreach (var user in facilityUsers)
        {
            user.Status = UserStatus.DEAKTIVIERT;
            user.FacilityId = null;
            user.UpdatedAt = DateTime.UtcNow;
        }

        facilities.Remove(facility);
        await facilities.SaveChangesAsync(ct);
    }

    private static FacilityDto ToDto(Facility f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        CustomerNumber = f.CustomerNumber,
        Address = f.Address,
        ContactPerson = f.ContactPerson,
        Email = f.Email,
        Phone = f.Phone,
        LocationId = f.LocationId,
        LocationName = f.Location?.Name ?? string.Empty,
        ActiveWeekdays = f.ActiveWeekdays,
        PortionPrice = f.PortionPrice,
        Status = f.Status.ToString(),
        Notes = f.Notes,
        RouteNumber = f.RouteNumber,
    };
}
