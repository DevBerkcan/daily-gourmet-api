using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.DTOs.Procurement;
using DailyGourmet.Api.Models.DTOs.Production;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Read-only rollup of confirmed customer orders — the view a Chef needs before buying from
/// suppliers: which calendar weeks have confirmed demand, how many facilities/portions each recipe
/// needs that week, and (drilling in) the total ingredient quantities across the whole week.
/// Deliberately independent of ProductionPlan/ProcurementList (both per-day and require an admin to
/// have already built a plan for every day) — this reads straight off confirmed Orders, so it's
/// available for the whole week the moment orders are confirmed, with no prep step required.</summary>
public class ProcurementOverviewHandler(DailyGourmetDbContext db)
{
    private static readonly OrderStatus[] ConfirmedStatuses = [OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    public async Task<List<ProcurementWeekDto>> ListWeeksAsync(Guid? locationId, CancellationToken ct = default)
    {
        var query = db.OrderItems.Where(oi => ConfirmedStatuses.Contains(oi.Order.Status));
        if (locationId is { } lid) query = query.Where(oi => oi.Order.Facility.LocationId == lid);

        return await query
            .GroupBy(oi => new { oi.Order.MealPlan.Year, oi.Order.MealPlan.CalendarWeek })
            .Select(g => new ProcurementWeekDto
            {
                Year = g.Key.Year,
                CalendarWeek = g.Key.CalendarWeek,
                ConfirmedOrderCount = g.Select(x => x.OrderId).Distinct().Count(),
                TotalPortions = g.Sum(x => x.Portions),
            })
            .OrderByDescending(w => w.Year).ThenByDescending(w => w.CalendarWeek)
            .ToListAsync(ct);
    }

    public async Task<List<WeekRecipeRequirementDto>> WeekRecipesAsync(int year, int calendarWeek, Guid? locationId, CancellationToken ct = default)
    {
        var query = db.OrderItems.Where(oi => ConfirmedStatuses.Contains(oi.Order.Status) && oi.Order.MealPlan.Year == year && oi.Order.MealPlan.CalendarWeek == calendarWeek);
        if (locationId is { } lid) query = query.Where(oi => oi.Order.Facility.LocationId == lid);

        return await query
            .GroupBy(oi => new { oi.RecipeId, oi.Recipe.Name })
            .Select(g => new WeekRecipeRequirementDto
            {
                RecipeId = g.Key.RecipeId,
                RecipeName = g.Key.Name,
                TotalPortions = g.Sum(x => x.Portions),
                FacilityCount = g.Select(x => x.Order.FacilityId).Distinct().Count(),
            })
            .OrderByDescending(r => r.TotalPortions)
            .ToListAsync(ct);
    }

    /// <summary>Same scaling/aggregation as ProductionPlanHandler.RequirementsAsync, sourced from raw
    /// confirmed OrderItems across the whole week instead of one day's ProductionPlanItems.</summary>
    public async Task<List<IngredientRequirementDto>> WeekIngredientsAsync(int year, int calendarWeek, Guid? locationId, CancellationToken ct = default)
    {
        var query = db.OrderItems
            .Where(oi => ConfirmedStatuses.Contains(oi.Order.Status) && oi.Order.MealPlan.Year == year && oi.Order.MealPlan.CalendarWeek == calendarWeek)
            .Include(oi => oi.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category).ThenInclude(c => c.DefaultStorageLocation)
            .AsQueryable();
        if (locationId is { } lid) query = query.Where(oi => oi.Order.Facility.LocationId == lid);

        var rows = await query.ToListAsync(ct);

        var aggregate = new Dictionary<(Guid IngredientId, Unit Unit), (decimal Total, Ingredient Ingredient, HashSet<string> Recipes)>();
        foreach (var item in rows)
        {
            if (item.Recipe.StandardPortions <= 0) continue;
            var scaleFactor = (decimal)item.Portions / item.Recipe.StandardPortions;
            foreach (var ri in item.Recipe.Ingredients)
            {
                var key = (ri.IngredientId, ri.Unit);
                var scaledQuantity = ri.Quantity * scaleFactor;
                if (aggregate.TryGetValue(key, out var existing))
                {
                    existing.Recipes.Add(item.Recipe.Name);
                    aggregate[key] = (existing.Total + scaledQuantity, existing.Ingredient, existing.Recipes);
                }
                else
                {
                    aggregate[key] = (scaledQuantity, ri.Ingredient, [item.Recipe.Name]);
                }
            }
        }

        return aggregate.Select(kv => new IngredientRequirementDto
        {
            IngredientId = kv.Key.IngredientId,
            IngredientName = kv.Value.Ingredient.Name,
            CategoryName = kv.Value.Ingredient.Category?.Name ?? string.Empty,
            Unit = kv.Key.Unit.ToString(),
            TotalQuantity = decimal.Round(kv.Value.Total, 2, MidpointRounding.AwayFromZero),
            StorageLocationName = kv.Value.Ingredient.Category?.DefaultStorageLocation?.Name,
            ContributingRecipeNames = kv.Value.Recipes.ToArray(),
        }).OrderBy(r => r.StorageLocationName).ThenBy(r => r.IngredientName).ToList();
    }
}
