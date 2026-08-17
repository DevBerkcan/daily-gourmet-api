using System.Globalization;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Dashboard;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class DashboardHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    public async Task<AdminDashboardSummaryDto> AdminSummaryAsync(CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var now = DateTime.UtcNow;
        var week = ISOWeek.GetWeekOfYear(now);
        var year = ISOWeek.GetYear(now);

        var thisWeekOrders = await db.Orders.Where(o => o.MealPlan.CalendarWeek == week && o.MealPlan.Year == year).ToListAsync(ct);
        var bindingCount = thisWeekOrders.Count(o => BindingStatuses.Contains(o.Status));

        var today = DateOnly.FromDateTime(now);
        var todayPortions = await db.OrderItems.Where(oi => oi.Date == today && BindingStatuses.Contains(oi.Order.Status)).SumAsync(oi => (int?)oi.Portions, ct) ?? 0;

        var facilitiesWithOrder = thisWeekOrders.Select(o => o.FacilityId).Distinct().ToHashSet();
        var facilitiesWithoutOrder = await db.Facilities.Where(f => f.Status == FacilityStatus.AKTIV && !facilitiesWithOrder.Contains(f.Id)).CountAsync(ct);

        var nextWeekDate = now.AddDays(7);
        var nextWeek = ISOWeek.GetWeekOfYear(nextWeekDate);
        var nextYear = ISOWeek.GetYear(nextWeekDate);
        var nextPlan = await db.MealPlans.FirstOrDefaultAsync(m => m.CalendarWeek == nextWeek && m.Year == nextYear, ct);

        return new AdminDashboardSummaryDto
        {
            ThisWeekOrderCount = thisWeekOrders.Count, ThisWeekBindingOrderCount = bindingCount, TodayTotalPortions = todayPortions,
            FacilitiesWithoutOrderCount = facilitiesWithoutOrder, NextWeekMealPlanStatus = nextPlan?.Status.ToString(),
        };
    }

    public async Task<PortalDashboardSummaryDto> PortalSummaryAsync(CancellationToken ct = default)
    {
        var facilityId = tenantContext.FacilityId ?? throw new ForbiddenException("Kein Einrichtungskontext vorhanden.");

        var publishedPlan = await db.MealPlans
            .Where(m => m.Status == MealPlanStatus.PUBLISHED && m.Facilities.Any(f => f.FacilityId == facilityId))
            .OrderByDescending(m => m.Year).ThenByDescending(m => m.CalendarWeek).FirstOrDefaultAsync(ct);

        var nextDeadlineOrder = await db.Orders
            .Where(o => o.FacilityId == facilityId && (o.Status == OrderStatus.DRAFT || o.Status == OrderStatus.SUBMITTED) && o.DeadlineAtUtc > DateTime.UtcNow)
            .OrderBy(o => o.DeadlineAtUtc).FirstOrDefaultAsync(ct);

        string? currentWeekOrderStatus = null;
        if (publishedPlan is not null)
        {
            var order = await db.Orders.FirstOrDefaultAsync(o => o.FacilityId == facilityId && o.MealPlanId == publishedPlan.Id, ct);
            currentWeekOrderStatus = order?.Status.ToString();
        }

        return new PortalDashboardSummaryDto
        {
            CurrentPublishedWeekLabel = publishedPlan is null ? null : $"{publishedPlan.CalendarWeek}/{publishedPlan.Year}",
            NextDeadlineUtc = nextDeadlineOrder?.DeadlineAtUtc,
            CurrentWeekOrderStatus = currentWeekOrderStatus,
        };
    }

    public async Task<RevenueResponseDto> RevenueAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var effectiveFrom = from ?? DateTime.UtcNow.AddDays(-84);
        var effectiveTo = to ?? DateTime.UtcNow;

        var orders = await db.Orders
            .Include(o => o.Facility).ThenInclude(f => f.Location)
            .Include(o => o.MealPlan)
            .Include(o => o.Items)
            .Where(o => BindingStatuses.Contains(o.Status) && o.CreatedAt >= effectiveFrom && o.CreatedAt <= effectiveTo)
            .ToListAsync(ct);

        var orderDetails = orders.Select(o =>
        {
            var portions = o.Items.Sum(i => i.Portions);
            var revenue = decimal.Round(portions * o.Facility.PortionPrice, 2, MidpointRounding.AwayFromZero);
            return new OrderRevenueDto
            {
                OrderId = o.Id, Year = o.MealPlan.Year, CalendarWeek = o.MealPlan.CalendarWeek,
                FacilityName = o.Facility.Name, LocationName = o.Facility.Location?.Name ?? string.Empty,
                Portions = portions, PortionPrice = o.Facility.PortionPrice, Revenue = revenue,
            };
        }).ToList();

        var weeklyTotals = orderDetails
            .GroupBy(o => (o.Year, o.CalendarWeek))
            .Select(g => new WeeklyRevenueDto
            {
                Year = g.Key.Year, CalendarWeek = g.Key.CalendarWeek, TotalRevenue = g.Sum(x => x.Revenue), TotalPortions = g.Sum(x => x.Portions),
                FacilityCount = orders.Where(o => o.MealPlan.Year == g.Key.Year && o.MealPlan.CalendarWeek == g.Key.CalendarWeek).Select(o => o.FacilityId).Distinct().Count(),
            })
            .OrderByDescending(w => w.Year).ThenByDescending(w => w.CalendarWeek).ToList();

        return new RevenueResponseDto { WeeklyTotals = weeklyTotals, OrderDetails = orderDetails };
    }
}
