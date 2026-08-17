using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Tenants;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string MainContactName { get; set; } = string.Empty;
    public string MainContactEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int UserCount { get; set; }
    public int FacilityCount { get; set; }
}

public class CreateTenantDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string MainContactName { get; set; } = string.Empty;
    [Required, EmailAddress] public string MainContactEmail { get; set; } = string.Empty;
}

public class UpdateTenantDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string MainContactName { get; set; } = string.Empty;
    [Required, EmailAddress] public string MainContactEmail { get; set; } = string.Empty;
}

public class LockTenantDto
{
    [Required, MinLength(1)] public string Reason { get; set; } = string.Empty;
}

public class TenantProfileDto
{
    public string? VatId { get; set; }
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string Timezone { get; set; } = "Europe/Berlin";
    public string Currency { get; set; } = "EUR";
    public string? LogoUrl { get; set; }
}

public class TenantNotificationSettingDto
{
    public string EventKey { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public class TenantSettingsDto
{
    public int DefaultOrderDeadlineOffsetDays { get; set; }
    public TimeSpan DefaultOrderDeadlineTime { get; set; }
    public bool ExcludeWeekendsFromDeadline { get; set; }
    public bool RequireReviewBeforePublish { get; set; }
    public bool UnpublishRequiresNoOrders { get; set; }
    public string FacilityNumberPrefix { get; set; } = string.Empty;
    public string ArticleNumberPrefix { get; set; } = string.Empty;
    public List<TenantNotificationSettingDto> NotificationSettings { get; set; } = [];
}

public class FeatureFlagDto
{
    public Guid Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool DefaultEnabled { get; set; }
    public bool? TenantEnabled { get; set; }
}

public class UpdateFeatureFlagDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool DefaultEnabled { get; set; }
}

public class SetTenantFeatureFlagDto
{
    [Required] public Guid FeatureFlagId { get; set; }
    public bool Enabled { get; set; }
}

public class LocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public int CapacityPortions { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class SaveLocationDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Address { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string ContactPerson { get; set; } = string.Empty;
    [Range(0, int.MaxValue)] public int CapacityPortions { get; set; }
}

public class UpdateLocationDto : SaveLocationDto
{
    [Required] public string Status { get; set; } = "AKTIV";
}

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
