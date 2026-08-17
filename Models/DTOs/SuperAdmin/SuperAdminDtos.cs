namespace DailyGourmet.Api.Models.DTOs.SuperAdmin;

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
