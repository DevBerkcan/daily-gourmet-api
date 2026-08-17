namespace DailyGourmet.Api.Models.DTOs.Dashboard;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AdminDashboardSummaryDto
{
    public int ThisWeekOrderCount { get; set; }
    public int ThisWeekBindingOrderCount { get; set; }
    public int TodayTotalPortions { get; set; }
    public int FacilitiesWithoutOrderCount { get; set; }
    public string? NextWeekMealPlanStatus { get; set; }
}

public class PortalDashboardSummaryDto
{
    public string? CurrentPublishedWeekLabel { get; set; }
    public DateTime? NextDeadlineUtc { get; set; }
    public string? CurrentWeekOrderStatus { get; set; }
}

public class WeeklyRevenueDto
{
    public int Year { get; set; }
    public int CalendarWeek { get; set; }
    public decimal TotalRevenue { get; set; }
    public int TotalPortions { get; set; }
    public int FacilityCount { get; set; }
}

public class OrderRevenueDto
{
    public Guid OrderId { get; set; }
    public int Year { get; set; }
    public int CalendarWeek { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string LocationName { get; set; } = string.Empty;
    public int Portions { get; set; }
    public decimal PortionPrice { get; set; }
    public decimal Revenue { get; set; }
}

public class RevenueResponseDto
{
    public List<WeeklyRevenueDto> WeeklyTotals { get; set; } = [];
    public List<OrderRevenueDto> OrderDetails { get; set; } = [];
}
