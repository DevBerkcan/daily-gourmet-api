using System.Globalization;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.SuperAdmin;
using DailyGourmet.Api.Models.DTOs.Tenants;
using DailyGourmet.Api.Models.DTOs.Users;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Handlers;

public class SuperAdminHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IEmailService email, IOptions<AppOptions> appOptions)
{
    public async Task<SuperAdminDashboardDto> DashboardAsync(CancellationToken ct = default)
    {
        var tenantCounts = await db.Tenants.IgnoreQueryFilters().GroupBy(t => t.Status).Select(g => new { Status = g.Key.ToString(), Count = g.Count() }).ToListAsync(ct);
        var totalUsers = await db.Users.IgnoreQueryFilters().CountAsync(ct);
        var activeLast7Days = await db.Users.IgnoreQueryFilters().CountAsync(u => u.LastLoginAt != null && u.LastLoginAt >= DateTime.UtcNow.AddDays(-7), ct);
        var totalFacilities = await db.Facilities.IgnoreQueryFilters().CountAsync(ct);

        var week = ISOWeek.GetWeekOfYear(DateTime.UtcNow);
        var year = ISOWeek.GetYear(DateTime.UtcNow);
        var thisWeekOrders = await db.Orders.IgnoreQueryFilters().CountAsync(o => o.MealPlan.CalendarWeek == week && o.MealPlan.Year == year, ct);

        var failedLogins = await db.Users.IgnoreQueryFilters().CountAsync(u => u.FailedLoginCount > 0 && u.LastLoginAt != null && u.LastLoginAt >= DateTime.UtcNow.AddHours(-24), ct);

        var topTenants = await db.Orders.IgnoreQueryFilters()
            .Where(o => o.MealPlan.CalendarWeek == week && o.MealPlan.Year == year)
            .GroupBy(o => o.Tenant.Name)
            .Select(g => new TenantOrderCountDto { TenantName = g.Key, OrderCount = g.Count() })
            .OrderByDescending(x => x.OrderCount)
            .Take(5)
            .ToListAsync(ct);

        var lockedOutUsers = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant)
            .Where(u => u.LockedUntil != null && u.LockedUntil > DateTime.UtcNow)
            .OrderByDescending(u => u.LockedUntil)
            .Select(u => new LockedUserDto { Name = u.Name, Email = u.Email, TenantName = u.Tenant != null ? u.Tenant.Name : null, LockedUntil = u.LockedUntil!.Value })
            .ToListAsync(ct);

        var ticketsWithSuperAdminReply = await db.SupportTickets.IgnoreQueryFilters().Include(t => t.Replies)
            .Where(t => t.Replies.Any(r => r.Role == SupportReplyRole.SUPER_ADMIN))
            .ToListAsync(ct);
        double? avgFirstResponseMinutes = ticketsWithSuperAdminReply.Count == 0 ? null : ticketsWithSuperAdminReply
            .Select(t => (t.Replies.Where(r => r.Role == SupportReplyRole.SUPER_ADMIN).OrderBy(r => r.CreatedAt).First().CreatedAt - t.CreatedAt).TotalMinutes)
            .Average();

        return new SuperAdminDashboardDto
        {
            TenantCountsByStatus = tenantCounts.ToDictionary(x => x.Status, x => x.Count),
            TotalUsers = totalUsers, ActiveUsersLast7Days = activeLast7Days, TotalFacilities = totalFacilities,
            ThisWeekOrderCount = thisWeekOrders, FailedLoginsLast24h = failedLogins,
            TopTenantsByOrdersThisWeek = topTenants, CurrentlyLockedOutUsers = lockedOutUsers,
            AverageFirstResponseMinutes = avgFirstResponseMinutes,
        };
    }

    public async Task<SystemStatusDto> SystemStatusAsync(CancellationToken ct = default) => new()
    {
        DatabaseConnected = await db.Database.CanConnectAsync(ct),
        Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown",
    };

    public async Task<PagedResult<TenantDto>> ListTenantsAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Tenants.IgnoreQueryFilters().AsQueryable();
        var total = await query.CountAsync(ct);
        var tenants = await query.OrderBy(t => t.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var dtos = new List<TenantDto>();
        foreach (var t in tenants) dtos.Add(await ToTenantDtoAsync(t, ct));
        return new PagedResult<TenantDto> { Items = dtos, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<TenantDto> GetTenantByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(Tenant), id);
        return await ToTenantDtoAsync(tenant, ct);
    }

    public async Task<TenantDto> CreateTenantAsync(CreateTenantDto dto, CancellationToken ct = default)
    {
        var tenant = new Tenant { Id = Guid.NewGuid(), Name = dto.Name.Trim(), Status = TenantStatus.AKTIV, MainContactName = dto.MainContactName, MainContactEmail = dto.MainContactEmail, CreatedAt = DateTime.UtcNow };
        db.Tenants.Add(tenant);
        db.TenantProfiles.Add(new TenantProfile { TenantId = tenant.Id });
        db.TenantSettings.Add(new TenantSettings { TenantId = tenant.Id });

        var token = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenant.Id, Name = dto.MainContactName, Email = dto.MainContactEmail, PasswordHash = string.Empty,
            Role = Role.TENANT_OWNER, Status = UserStatus.EINGELADEN, InvitationToken = token, InvitationExpiresAt = DateTime.UtcNow.AddHours(72), CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await SendInvitationEmailAsync(user, token);

        return await ToTenantDtoAsync(tenant, ct);
    }

    public async Task<TenantDto> UpdateTenantAsync(Guid id, UpdateTenantDto dto, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(Tenant), id);
        tenant.Name = dto.Name.Trim();
        tenant.MainContactName = dto.MainContactName;
        tenant.MainContactEmail = dto.MainContactEmail;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToTenantDtoAsync(tenant, ct);
    }

    public async Task<TenantDto> ChangeTenantStatusAsync(Guid id, TenantStatus status, string action, LockTenantDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new ValidationException("Eine Begründung ist erforderlich.");
        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct) ?? throw new NotFoundException(nameof(Tenant), id);
        tenant.Status = status;
        tenant.UpdatedAt = DateTime.UtcNow;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = null, UserId = tenantContext.UserId, Action = action, Entity = "Tenant", EntityId = tenant.Id.ToString(),
            Reason = dto.Reason, CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return await ToTenantDtoAsync(tenant, ct);
    }

    public async Task<List<UserDto>> TenantUsersAsync(Guid tenantId, CancellationToken ct = default)
    {
        var users = await db.Users.IgnoreQueryFilters().Include(u => u.Facility).Where(u => u.TenantId == tenantId).ToListAsync(ct);
        return users.Select(u => ToUserDto(u, null, u.Facility?.Name)).ToList();
    }

    public async Task<PagedResult<UserDto>> GlobalUsersAsync(Guid? tenantId, string? role, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Users.IgnoreQueryFilters().Include(u => u.Tenant).Include(u => u.Facility).AsQueryable();
        if (tenantId is { } tid) query = query.Where(u => u.TenantId == tid);
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<Role>(role, out var r)) query = query.Where(u => u.Role == r);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, out var s)) query = query.Where(u => u.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items.Select(u => ToUserDto(u, u.Tenant?.Name, u.Facility?.Name)).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    /// <summary>Super admin adds a login-capable user directly and picks their role/tenant — unlike
    /// UserManagementHandler.InviteAsync, which a tenant admin uses and which is scoped to their own
    /// tenant. Sends the same "set your password" invitation email; AuthHandler.AcceptInvitationAsync
    /// is the single place that later turns the invite into an active account.</summary>
    public async Task<UserDto> CreateUserAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Role>(dto.Role, out var role)) throw new ValidationException("Ungültige Rolle.");

        Guid? tenantId = dto.TenantId;
        Tenant? tenant = null;
        if (role == Role.SUPER_ADMIN)
        {
            tenantId = null;
        }
        else
        {
            if (tenantId is null) throw new ValidationException("Für diese Rolle ist ein Mandant erforderlich.");
            tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct)
                ?? throw new NotFoundException(nameof(Tenant), tenantId);
        }

        Guid? facilityId = null;
        if (role is Role.FACILITY_ADMIN or Role.FACILITY_USER && dto.FacilityId is { } fid)
        {
            var facilityBelongsToTenant = await db.Facilities.IgnoreQueryFilters().AnyAsync(f => f.Id == fid && f.TenantId == tenantId, ct);
            if (!facilityBelongsToTenant) throw new ValidationException("Die Einrichtung gehört nicht zum ausgewählten Mandanten.");
            facilityId = fid;
        }

        var emailNormalized = dto.Email.Trim();
        var emailExists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == emailNormalized, ct);
        if (emailExists) throw new ConflictException("Diese E-Mail-Adresse wird bereits verwendet.");

        var token = Guid.NewGuid().ToString("N");
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, FacilityId = facilityId, Name = dto.Name.Trim(), Email = emailNormalized,
            PasswordHash = string.Empty, Role = role, Status = UserStatus.EINGELADEN,
            InvitationToken = token, InvitationExpiresAt = DateTime.UtcNow.AddHours(72), CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await SendInvitationEmailAsync(user, token);

        string? facilityName = facilityId is { } fidResult
            ? await db.Facilities.IgnoreQueryFilters().Where(f => f.Id == fidResult).Select(f => f.Name).FirstOrDefaultAsync(ct)
            : null;
        return ToUserDto(user, tenant?.Name, facilityName);
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, SuperAdminUpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().Include(u => u.Tenant).Include(u => u.Facility).FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(nameof(User), id);
        if (!Enum.TryParse<Role>(dto.Role, out var role)) throw new ValidationException("Ungültige Rolle.");

        if (dto.FacilityId is { } fid)
        {
            var facilityBelongsToTenant = await db.Facilities.IgnoreQueryFilters().AnyAsync(f => f.Id == fid && f.TenantId == user.TenantId, ct);
            if (!facilityBelongsToTenant) throw new ValidationException("Die Einrichtung gehört nicht zum Mandanten dieses Benutzers.");
        }

        user.Name = dto.Name.Trim();
        user.Role = role;
        user.FacilityId = dto.FacilityId;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        string? facilityName = user.FacilityId is { } resultFid
            ? await db.Facilities.IgnoreQueryFilters().Where(f => f.Id == resultFid).Select(f => f.Name).FirstOrDefaultAsync(ct)
            : null;
        return ToUserDto(user, user.Tenant?.Name, facilityName);
    }

    public async Task SetUserStatusAsync(Guid id, UserStatus status, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException(nameof(User), id);
        user.Status = status;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Admin-triggered password reset for an already-active user — reuses the exact same
    /// invitation-token machinery as UserManagementHandler.ResendInvitationAsync.
    /// AuthHandler.AcceptInvitationAsync already sets a new password for a user found by a valid
    /// token regardless of their current Status, so no separate "forgot password" flow is needed:
    /// regenerating the token and re-sending the link is the whole reset.</summary>
    public async Task TriggerPasswordResetAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException(nameof(User), id);
        user.InvitationToken = Guid.NewGuid().ToString("N");
        user.InvitationExpiresAt = DateTime.UtcNow.AddHours(72);
        await db.SaveChangesAsync(ct);
        await SendPasswordResetEmailAsync(user);
    }

    private async Task SendPasswordResetEmailAsync(User user)
    {
        var baseUrl = appOptions.Value.PublicBaseUrl.TrimEnd('/');
        var resetUrl = $"{baseUrl}/accept-invite/{user.InvitationToken}";
        var html = $"""
            <p>Für Ihr Daily-Gourmet-Konto wurde ein Zurücksetzen des Passworts angefordert. Klicken Sie auf den folgenden Link, um ein neues Passwort festzulegen:</p>
            <p><a href="{resetUrl}">Passwort zurücksetzen</a></p>
            <p>Der Link ist 72 Stunden gültig. Haben Sie dies nicht angefordert, können Sie diese E-Mail ignorieren.</p>
            """;
        var text = $"Für Ihr Daily-Gourmet-Konto wurde ein Zurücksetzen des Passworts angefordert. Neues Passwort festlegen: {resetUrl}\nDer Link ist 72 Stunden gültig.";
        await email.SendAsync(user.Email, user.Name, "Passwort zurücksetzen — Daily Gourmet", html, text);
    }

    private async Task SendInvitationEmailAsync(User user, string token)
    {
        var baseUrl = appOptions.Value.PublicBaseUrl.TrimEnd('/');
        var acceptUrl = $"{baseUrl}/accept-invite/{token}";
        var html = $"""
            <p>Sie wurden zu Daily Gourmet eingeladen. Klicken Sie auf den folgenden Link, um Ihr Passwort festzulegen und Ihr Konto zu aktivieren:</p>
            <p><a href="{acceptUrl}">Konto aktivieren</a></p>
            <p>Der Link ist 72 Stunden gültig.</p>
            """;
        var text = $"Sie wurden zu Daily Gourmet eingeladen. Passwort festlegen: {acceptUrl}\nDer Link ist 72 Stunden gültig.";
        await email.SendAsync(user.Email, user.Name, "Einladung zu Daily Gourmet", html, text);
    }

    public async Task<List<FeatureFlagDto>> ListFeatureFlagsAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var flags = await db.FeatureFlags.OrderBy(f => f.Name).ToListAsync(ct);
        var overrides = tenantId is { } tid
            ? await db.TenantFeatureFlags.Where(x => x.TenantId == tid).ToDictionaryAsync(x => x.FeatureFlagId, x => x.Enabled, ct)
            : new Dictionary<Guid, bool>();

        return flags.Select(f => new FeatureFlagDto
        {
            Id = f.Id, Key = f.Key, Name = f.Name, Description = f.Description, DefaultEnabled = f.DefaultEnabled,
            TenantEnabled = tenantId is null ? null : (overrides.TryGetValue(f.Id, out var v) ? v : (bool?)null),
        }).ToList();
    }

    public async Task<FeatureFlagDto> UpdateFeatureFlagAsync(Guid id, UpdateFeatureFlagDto dto, CancellationToken ct = default)
    {
        var flag = await db.FeatureFlags.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new NotFoundException(nameof(FeatureFlag), id);
        flag.Name = dto.Name;
        flag.Description = dto.Description;
        flag.DefaultEnabled = dto.DefaultEnabled;
        flag.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return new FeatureFlagDto { Id = flag.Id, Key = flag.Key, Name = flag.Name, Description = flag.Description, DefaultEnabled = flag.DefaultEnabled };
    }

    public async Task SetTenantFeatureFlagAsync(Guid tenantId, SetTenantFeatureFlagDto dto, CancellationToken ct = default)
    {
        var existing = await db.TenantFeatureFlags.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.FeatureFlagId == dto.FeatureFlagId, ct);
        if (existing is null)
            db.TenantFeatureFlags.Add(new TenantFeatureFlag { TenantId = tenantId, FeatureFlagId = dto.FeatureFlagId, Enabled = dto.Enabled });
        else
            existing.Enabled = dto.Enabled;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>How many tenants actually have each flag on, resolving each tenant's own override
    /// (falling back to the flag's DefaultEnabled where no override exists) — answers "how many
    /// tenants have kundenportal on" using data that's already fully captured in TenantFeatureFlag.</summary>
    public async Task<List<FeatureFlagAdoptionDto>> FeatureFlagAdoptionAsync(CancellationToken ct = default)
    {
        var totalTenants = await db.Tenants.IgnoreQueryFilters().CountAsync(ct);
        var flags = await db.FeatureFlags.OrderBy(f => f.Name).ToListAsync(ct);
        var overrides = await db.TenantFeatureFlags.ToListAsync(ct);

        return flags.Select(f =>
        {
            var flagOverrides = overrides.Where(o => o.FeatureFlagId == f.Id).ToList();
            var explicitlyOnCount = flagOverrides.Count(o => o.Enabled);
            var tenantsWithoutOverride = totalTenants - flagOverrides.Count;
            var enabledCount = explicitlyOnCount + (f.DefaultEnabled ? tenantsWithoutOverride : 0);
            return new FeatureFlagAdoptionDto { Key = f.Key, Name = f.Name, EnabledTenantCount = enabledCount, TotalTenantCount = totalTenants };
        }).ToList();
    }

    public async Task<List<LocationSummaryDto>> AllLocationsAsync(Guid? tenantId = null, CancellationToken ct = default)
    {
        var query = db.Locations.IgnoreQueryFilters().Include(l => l.Tenant).AsQueryable();
        if (tenantId is { } tid) query = query.Where(l => l.TenantId == tid);
        return await query.Select(l => new LocationSummaryDto { Id = l.Id, Name = l.Name, TenantName = l.Tenant.Name }).ToListAsync(ct);
    }

    private async Task<TenantDto> ToTenantDtoAsync(Tenant t, CancellationToken ct) => new()
    {
        Id = t.Id, Name = t.Name, Status = t.Status.ToString(), MainContactName = t.MainContactName, MainContactEmail = t.MainContactEmail, CreatedAt = t.CreatedAt,
        UserCount = await db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id, ct),
        FacilityCount = await db.Facilities.IgnoreQueryFilters().CountAsync(f => f.TenantId == t.Id, ct),
    };

    private static UserDto ToUserDto(User u, string? tenantName, string? facilityName) => new()
    {
        Id = u.Id, TenantId = u.TenantId, TenantName = tenantName, FacilityId = u.FacilityId, FacilityName = facilityName,
        Name = u.Name, Email = u.Email, Role = u.Role.ToString(), Status = u.Status.ToString(), LastLoginAt = u.LastLoginAt, FailedLoginCount = u.FailedLoginCount,
    };
}
