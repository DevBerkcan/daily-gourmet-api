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

public class SuperAdminDashboardDto
{
    public Dictionary<string, int> TenantCountsByStatus { get; set; } = new();
    public int TotalUsers { get; set; }
    public int ActiveUsersLast7Days { get; set; }
    public int TotalFacilities { get; set; }
    public int ThisWeekOrderCount { get; set; }
    public int FailedLoginsLast24h { get; set; }
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
