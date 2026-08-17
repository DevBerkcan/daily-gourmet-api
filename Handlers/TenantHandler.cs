using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Tenants;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class TenantHandler(DailyGourmetDbContext db, ITenantSettingsRepository settingsRepo, ITenantContext tenantContext)
{
    private Guid CurrentTenantId => tenantContext.TenantId ?? throw new ValidationException("Kein Mandantenkontext vorhanden.");

    public async Task<TenantDto> GetCurrentAsync(CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == CurrentTenantId, ct) ?? throw new NotFoundException(nameof(Tenant), CurrentTenantId);
        return new TenantDto
        {
            Id = tenant.Id, Name = tenant.Name, Status = tenant.Status.ToString(), MainContactName = tenant.MainContactName, MainContactEmail = tenant.MainContactEmail, CreatedAt = tenant.CreatedAt,
            UserCount = await db.Users.CountAsync(u => u.TenantId == tenant.Id, ct),
            FacilityCount = await db.Facilities.CountAsync(f => f.TenantId == tenant.Id, ct),
        };
    }

    public async Task<TenantDto> UpdateCurrentAsync(UpdateTenantDto dto, CancellationToken ct = default)
    {
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == CurrentTenantId, ct) ?? throw new NotFoundException(nameof(Tenant), CurrentTenantId);
        tenant.Name = dto.Name.Trim();
        tenant.MainContactName = dto.MainContactName;
        tenant.MainContactEmail = dto.MainContactEmail;
        tenant.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetCurrentAsync(ct);
    }

    public async Task<TenantProfileDto> GetProfileAsync(CancellationToken ct = default)
    {
        var profile = await settingsRepo.GetProfileAsync(CurrentTenantId, ct) ?? throw new NotFoundException(nameof(TenantProfile), CurrentTenantId);
        return ToProfileDto(profile);
    }

    public async Task<TenantProfileDto> UpdateProfileAsync(TenantProfileDto dto, CancellationToken ct = default)
    {
        var profile = await settingsRepo.GetProfileAsync(CurrentTenantId, ct) ?? throw new NotFoundException(nameof(TenantProfile), CurrentTenantId);
        profile.VatId = dto.VatId; profile.Street = dto.Street; profile.PostalCode = dto.PostalCode; profile.City = dto.City;
        profile.Phone = dto.Phone; profile.Email = dto.Email; profile.Timezone = dto.Timezone; profile.Currency = dto.Currency; profile.LogoUrl = dto.LogoUrl;
        await settingsRepo.SaveChangesAsync(ct);
        return ToProfileDto(profile);
    }

    public async Task<TenantSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var settings = await settingsRepo.GetAsync(CurrentTenantId, ct) ?? throw new NotFoundException(nameof(TenantSettings), CurrentTenantId);
        var notifications = await db.TenantNotificationSettings.Where(n => n.TenantId == CurrentTenantId).ToListAsync(ct);
        return ToSettingsDto(settings, notifications);
    }

    public async Task<TenantSettingsDto> UpdateSettingsAsync(TenantSettingsDto dto, CancellationToken ct = default)
    {
        var settings = await settingsRepo.GetAsync(CurrentTenantId, ct) ?? throw new NotFoundException(nameof(TenantSettings), CurrentTenantId);
        settings.DefaultOrderDeadlineOffsetDays = dto.DefaultOrderDeadlineOffsetDays;
        settings.DefaultOrderDeadlineTime = dto.DefaultOrderDeadlineTime;
        settings.ExcludeWeekendsFromDeadline = dto.ExcludeWeekendsFromDeadline;
        settings.RequireReviewBeforePublish = dto.RequireReviewBeforePublish;
        settings.UnpublishRequiresNoOrders = dto.UnpublishRequiresNoOrders;
        settings.FacilityNumberPrefix = dto.FacilityNumberPrefix;
        settings.ArticleNumberPrefix = dto.ArticleNumberPrefix;

        var existing = await db.TenantNotificationSettings.Where(n => n.TenantId == CurrentTenantId).ToListAsync(ct);
        foreach (var item in dto.NotificationSettings)
        {
            var row = existing.FirstOrDefault(n => n.EventKey == item.EventKey);
            if (row is null)
                db.TenantNotificationSettings.Add(new TenantNotificationSetting { Id = Guid.NewGuid(), TenantId = CurrentTenantId, EventKey = item.EventKey, Enabled = item.Enabled, CreatedAt = DateTime.UtcNow });
            else
                row.Enabled = item.Enabled;
        }

        await settingsRepo.SaveChangesAsync(ct);
        return await GetSettingsAsync(ct);
    }

    private static TenantProfileDto ToProfileDto(TenantProfile p) => new()
    {
        VatId = p.VatId, Street = p.Street, PostalCode = p.PostalCode, City = p.City, Phone = p.Phone, Email = p.Email, Timezone = p.Timezone, Currency = p.Currency, LogoUrl = p.LogoUrl,
    };

    private static TenantSettingsDto ToSettingsDto(TenantSettings s, List<TenantNotificationSetting> notifications) => new()
    {
        DefaultOrderDeadlineOffsetDays = s.DefaultOrderDeadlineOffsetDays, DefaultOrderDeadlineTime = s.DefaultOrderDeadlineTime,
        ExcludeWeekendsFromDeadline = s.ExcludeWeekendsFromDeadline, RequireReviewBeforePublish = s.RequireReviewBeforePublish,
        UnpublishRequiresNoOrders = s.UnpublishRequiresNoOrders, FacilityNumberPrefix = s.FacilityNumberPrefix, ArticleNumberPrefix = s.ArticleNumberPrefix,
        NotificationSettings = notifications.Select(n => new TenantNotificationSettingDto { EventKey = n.EventKey, Enabled = n.Enabled }).ToList(),
    };
}
