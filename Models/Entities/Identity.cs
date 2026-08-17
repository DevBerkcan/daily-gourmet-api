using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

/// <summary>A catering company / platform customer — root of multi-tenant isolation.</summary>
public class Tenant : BaseEntity
{
    public string Name { get; set; } = null!;
    public TenantStatus Status { get; set; } = TenantStatus.AKTIV;
    public string MainContactName { get; set; } = null!;
    public string MainContactEmail { get; set; } = null!;

    public TenantProfile? Profile { get; set; }
    public TenantSettings? Settings { get; set; }
    public ICollection<TenantNotificationSetting> NotificationSettings { get; set; } = new List<TenantNotificationSetting>();
    public ICollection<TenantFeatureFlag> FeatureFlags { get; set; } = new List<TenantFeatureFlag>();
    public ICollection<User> Users { get; set; } = new List<User>();
}

/// <summary>1:1 with Tenant — company master data + branding (admin/company page).</summary>
public class TenantProfile
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

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

/// <summary>1:1 with Tenant — configurable business rules (admin/settings page).</summary>
public class TenantSettings
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int DefaultOrderDeadlineOffsetDays { get; set; } = 1;
    public TimeSpan DefaultOrderDeadlineTime { get; set; } = new(9, 0, 0);
    public bool ExcludeWeekendsFromDeadline { get; set; } = true;
    public bool RequireReviewBeforePublish { get; set; } = true;
    public bool UnpublishRequiresNoOrders { get; set; } = true;
    public string FacilityNumberPrefix { get; set; } = "DG-1";
    public string ArticleNumberPrefix { get; set; } = "ART-";
}

/// <summary>Per-event notification toggle for a tenant.</summary>
public class TenantNotificationSetting : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string EventKey { get; set; } = null!;
    public bool Enabled { get; set; }
}

/// <summary>Global feature catalog.</summary>
public class FeatureFlag : BaseEntity
{
    public string Key { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool DefaultEnabled { get; set; }

    public ICollection<TenantFeatureFlag> TenantOverrides { get; set; } = new List<TenantFeatureFlag>();
}

/// <summary>Per-tenant override of a global feature flag.</summary>
public class TenantFeatureFlag
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid FeatureFlagId { get; set; }
    public FeatureFlag FeatureFlag { get; set; } = null!;
    public bool Enabled { get; set; }
}

/// <summary>Every login-capable identity across all roles.</summary>
public class User : BaseEntity
{
    /// <summary>Null only for SUPER_ADMIN (platform user, no tenant).</summary>
    public Guid? TenantId { get; set; }
    public Tenant? Tenant { get; set; }

    /// <summary>Set only for FACILITY_ADMIN / FACILITY_USER.</summary>
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public Role Role { get; set; }
    public UserStatus Status { get; set; } = UserStatus.EINGELADEN;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntil { get; set; }
    public string? InvitationToken { get; set; }
    public DateTime? InvitationExpiresAt { get; set; }

    public Driver? Driver { get; set; }
}

/// <summary>Driver-specific profile, 1:1 with a User whose Role == DRIVER.</summary>
public class Driver : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string Phone { get; set; } = null!;
    public string VehicleDescription { get; set; } = null!;
    public string LicensePlate { get; set; } = null!;

    public ICollection<DeliveryRoute> Routes { get; set; } = new List<DeliveryRoute>();
}
