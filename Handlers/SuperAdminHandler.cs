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
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class SuperAdminHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IEmailService email)
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

        return new SuperAdminDashboardDto
        {
            TenantCountsByStatus = tenantCounts.ToDictionary(x => x.Status, x => x.Count),
            TotalUsers = totalUsers, ActiveUsersLast7Days = activeLast7Days, TotalFacilities = totalFacilities,
            ThisWeekOrderCount = thisWeekOrders, FailedLoginsLast24h = failedLogins,
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

        await email.SendAsync(user.Email, user.Name, "Einladung zu Daily Gourmet",
            $"<p>Sie wurden zu Daily Gourmet eingeladen. <a href=\"https://app.example/accept-invite/{token}\">Einladung annehmen</a></p>",
            $"Sie wurden zu Daily Gourmet eingeladen. Link: https://app.example/accept-invite/{token}");

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
        return users.Select(u => ToUserDto(u, null)).ToList();
    }

    public async Task<PagedResult<UserDto>> GlobalUsersAsync(Guid? tenantId, string? role, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Users.IgnoreQueryFilters().Include(u => u.Tenant).Include(u => u.Facility).AsQueryable();
        if (tenantId is { } tid) query = query.Where(u => u.TenantId == tid);
        if (!string.IsNullOrWhiteSpace(role) && Enum.TryParse<Role>(role, out var r)) query = query.Where(u => u.Role == r);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UserStatus>(status, out var s)) query = query.Where(u => u.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items.Select(u => ToUserDto(u, u.Tenant?.Name)).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<List<FeatureFlagDto>> ListFeatureFlagsAsync(CancellationToken ct = default) =>
        await db.FeatureFlags.OrderBy(f => f.Name).Select(f => new FeatureFlagDto { Id = f.Id, Key = f.Key, Name = f.Name, Description = f.Description, DefaultEnabled = f.DefaultEnabled }).ToListAsync(ct);

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

    public async Task<List<LocationSummaryDto>> AllLocationsAsync(CancellationToken ct = default) =>
        await db.Locations.IgnoreQueryFilters().Include(l => l.Tenant)
            .Select(l => new LocationSummaryDto { Id = l.Id, Name = l.Name, TenantName = l.Tenant.Name })
            .ToListAsync(ct);

    private async Task<TenantDto> ToTenantDtoAsync(Tenant t, CancellationToken ct) => new()
    {
        Id = t.Id, Name = t.Name, Status = t.Status.ToString(), MainContactName = t.MainContactName, MainContactEmail = t.MainContactEmail, CreatedAt = t.CreatedAt,
        UserCount = await db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == t.Id, ct),
        FacilityCount = await db.Facilities.IgnoreQueryFilters().CountAsync(f => f.TenantId == t.Id, ct),
    };

    private static UserDto ToUserDto(User u, string? tenantName) => new()
    {
        Id = u.Id, TenantId = u.TenantId, TenantName = tenantName, FacilityId = u.FacilityId, FacilityName = u.Facility?.Name,
        Name = u.Name, Email = u.Email, Role = u.Role.ToString(), Status = u.Status.ToString(), LastLoginAt = u.LastLoginAt, FailedLoginCount = u.FailedLoginCount,
    };
}
