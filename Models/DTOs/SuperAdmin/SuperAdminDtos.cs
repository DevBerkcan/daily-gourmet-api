using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.SuperAdmin;

/// <summary>Super admin creates a login-capable user directly (as opposed to
/// UserManagementHandler.InviteAsync, which a tenant admin uses within their own tenant) — the
/// super admin additionally picks the target tenant and, for SUPER_ADMIN, none at all.</summary>
public class CreateUserDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
    public Guid? TenantId { get; set; }
    public Guid? FacilityId { get; set; }
}

/// <summary>Super admin edits any user's name/role/facility, across tenants. Deliberately excludes
/// TenantId — moving a user between tenants is a bigger, more consequential action than an edit and
/// is intentionally not supported here.</summary>
public class SuperAdminUpdateUserDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
    public Guid? FacilityId { get; set; }
}

public class SuperAdminDashboardDto
{
    public Dictionary<string, int> TenantCountsByStatus { get; set; } = new();
    public int TotalUsers { get; set; }
    public int ActiveUsersLast7Days { get; set; }
    public int TotalFacilities { get; set; }
    public int ThisWeekOrderCount { get; set; }
    public int FailedLoginsLast24h { get; set; }
    public List<TenantOrderCountDto> TopTenantsByOrdersThisWeek { get; set; } = new();
    /// <summary>Accounts currently rate-limited (User.LockedUntil in the future) — actionable, unlike
    /// FailedLoginsLast24h's running-counter total: schema has no per-attempt timestamp (FailedLoginCount
    /// is a plain counter, not an event log), so a real day-by-day trend isn't something this data
    /// can honestly support.</summary>
    public List<LockedUserDto> CurrentlyLockedOutUsers { get; set; } = new();
    /// <summary>Average time from ticket creation to the first SUPER_ADMIN reply, across every ticket
    /// that has one. Null when no ticket has been answered yet.</summary>
    public double? AverageFirstResponseMinutes { get; set; }
}

public class FeatureFlagAdoptionDto
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int EnabledTenantCount { get; set; }
    public int TotalTenantCount { get; set; }
}

public class TenantOrderCountDto
{
    public string TenantName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
}

public class LockedUserDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? TenantName { get; set; }
    public DateTime LockedUntil { get; set; }
}

public class SystemStatusDto
{
    public bool DatabaseConnected { get; set; }
    public string Version { get; set; } = string.Empty;
    public string BackgroundJobs { get; set; } = "Nicht konfiguriert";
}

public class LocationSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
}
