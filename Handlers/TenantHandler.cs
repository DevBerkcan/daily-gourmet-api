using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Tenants;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>
/// Unternehmensprofil und Einstellungen eines Mandanten werden ausschließlich von Daily Gourmet
/// (SUPER_ADMIN) gepflegt, nicht vom Mandanten selbst — siehe Autorisierung in TenantsController
/// bzw. SuperAdminController. Die Kernlogik ist hier tenantId-parametrisiert, damit beide Seiten
/// (Self-Service-Anzeige "current" und Super-Admin-Pflege per Id) dieselbe Implementierung nutzen.
/// </summary>
public class TenantHandler(DailyGourmetDbContext db, ITenantSettingsRepository settingsRepo, ITenantContext tenantContext)
{
    private Guid CurrentTenantId => tenantContext.TenantId ?? throw new ValidationException("Kein Mandantenkontext vorhanden.");

    public Task<TenantDto> GetCurrentAsync(CancellationToken ct = default) => GetAsync(CurrentTenantId, ct);

    public async Task<TenantDto> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new NotFoundException(nameof(Tenant), tenantId);
        return new TenantDto
        {
            Id = tenant.Id, Name = tenant.Name, Status = tenant.Status.ToString(), MainContactName = tenant.MainContactName, MainContactEmail = tenant.MainContactEmail, CreatedAt = tenant.CreatedAt,
            UserCount = await db.Users.CountAsync(u => u.TenantId == tenant.Id, ct),
            FacilityCount = await db.Facilities.CountAsync(f => f.TenantId == tenant.Id, ct),
        };
    }

    public Task<TenantDto> UpdateCurrentAsync(UpdateTenantDto dto, CancellationToken ct = default) => UpdateAsync(CurrentTenantId, dto, ct);

    public async Task<TenantDto> UpdateAsync(Guid tenantId, UpdateTenantDto dto, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct) ?? throw new NotFoundException(nameof(Tenant), tenantId);
        tenant.Name = dto.Name.Trim();
        tenant.MainContactName = dto.MainContactName;
        tenant.MainContactEmail = dto.MainContactEmail;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetAsync(tenantId, ct);
    }

    public Task<TenantProfileDto> GetProfileAsync(CancellationToken ct = default) => GetProfileAsync(CurrentTenantId, ct);

    public async Task<TenantProfileDto> GetProfileAsync(Guid tenantId, CancellationToken ct = default)
    {
        var profile = await settingsRepo.GetProfileAsync(tenantId, ct) ?? throw new NotFoundException(nameof(TenantProfile), tenantId);
        return ToProfileDto(profile);
    }

    public Task<TenantProfileDto> UpdateProfileAsync(TenantProfileDto dto, CancellationToken ct = default) => UpdateProfileAsync(CurrentTenantId, dto, ct);

    public async Task<TenantProfileDto> UpdateProfileAsync(Guid tenantId, TenantProfileDto dto, CancellationToken ct = default)
    {
        var profile = await settingsRepo.GetProfileAsync(tenantId, ct) ?? throw new NotFoundException(nameof(TenantProfile), tenantId);
        profile.VatId = dto.VatId; profile.Street = dto.Street; profile.PostalCode = dto.PostalCode; profile.City = dto.City;
        profile.Phone = dto.Phone; profile.Email = dto.Email; profile.Timezone = dto.Timezone; profile.Currency = dto.Currency; profile.LogoUrl = dto.LogoUrl;
        await settingsRepo.SaveChangesAsync(ct);
        return ToProfileDto(profile);
    }

    public Task<TenantSettingsDto> GetSettingsAsync(CancellationToken ct = default) => GetSettingsAsync(CurrentTenantId, ct);

    public async Task<TenantSettingsDto> GetSettingsAsync(Guid tenantId, CancellationToken ct = default)
    {
        var settings = await settingsRepo.GetAsync(tenantId, ct) ?? throw new NotFoundException(nameof(TenantSettings), tenantId);
        var notifications = await db.TenantNotificationSettings.Where(n => n.TenantId == tenantId).ToListAsync(ct);
        return ToSettingsDto(settings, notifications);
    }

    public Task<TenantSettingsDto> UpdateSettingsAsync(TenantSettingsDto dto, CancellationToken ct = default) => UpdateSettingsAsync(CurrentTenantId, dto, ct);

    public async Task<TenantSettingsDto> UpdateSettingsAsync(Guid tenantId, TenantSettingsDto dto, CancellationToken ct = default)
    {
        var settings = await settingsRepo.GetAsync(tenantId, ct) ?? throw new NotFoundException(nameof(TenantSettings), tenantId);
        settings.DefaultOrderDeadlineOffsetDays = dto.DefaultOrderDeadlineOffsetDays;
        settings.DefaultOrderDeadlineTime = dto.DefaultOrderDeadlineTime;
        settings.ExcludeWeekendsFromDeadline = dto.ExcludeWeekendsFromDeadline;
        settings.RequireReviewBeforePublish = dto.RequireReviewBeforePublish;
        settings.UnpublishRequiresNoOrders = dto.UnpublishRequiresNoOrders;
        settings.FacilityNumberPrefix = dto.FacilityNumberPrefix;
        settings.ArticleNumberPrefix = dto.ArticleNumberPrefix;
        settings.RouteNumberPrefix = dto.RouteNumberPrefix;
        settings.SameDayAdjustmentDeadlineTime = dto.SameDayAdjustmentDeadlineTime;

        var existing = await db.TenantNotificationSettings.Where(n => n.TenantId == tenantId).ToListAsync(ct);
        foreach (var item in dto.NotificationSettings)
        {
            var row = existing.FirstOrDefault(n => n.EventKey == item.EventKey);
            if (row is null)
                db.TenantNotificationSettings.Add(new TenantNotificationSetting { Id = Guid.NewGuid(), TenantId = tenantId, EventKey = item.EventKey, Enabled = item.Enabled, CreatedAt = DateTime.UtcNow });
            else
                row.Enabled = item.Enabled;
        }

        await settingsRepo.SaveChangesAsync(ct);
        return await GetSettingsAsync(tenantId, ct);
    }

    private static TenantProfileDto ToProfileDto(TenantProfile p) => new()
    {
        VatId = p.VatId, Street = p.Street, PostalCode = p.PostalCode, City = p.City, Phone = p.Phone, Email = p.Email, Timezone = p.Timezone, Currency = p.Currency, LogoUrl = p.LogoUrl,
    };

    private static TenantSettingsDto ToSettingsDto(TenantSettings s, List<TenantNotificationSetting> notifications) => new()
    {
        DefaultOrderDeadlineOffsetDays = s.DefaultOrderDeadlineOffsetDays, DefaultOrderDeadlineTime = s.DefaultOrderDeadlineTime,
        SameDayAdjustmentDeadlineTime = s.SameDayAdjustmentDeadlineTime,
        ExcludeWeekendsFromDeadline = s.ExcludeWeekendsFromDeadline, RequireReviewBeforePublish = s.RequireReviewBeforePublish,
        UnpublishRequiresNoOrders = s.UnpublishRequiresNoOrders, FacilityNumberPrefix = s.FacilityNumberPrefix, ArticleNumberPrefix = s.ArticleNumberPrefix,
        RouteNumberPrefix = s.RouteNumberPrefix,
        NotificationSettings = notifications.Select(n => new TenantNotificationSettingDto { EventKey = n.EventKey, Enabled = n.Enabled }).ToList(),
    };
}
