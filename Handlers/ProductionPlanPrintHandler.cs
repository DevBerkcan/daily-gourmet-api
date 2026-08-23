using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using DailyGourmet.Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Builds the admin-facing "Produktionsplan drucken" PDF that replaces the removed Küche
/// module's paper-taped-to-the-wall Google-Sheets process — one PDF per weekday, grouped by Tour.
/// Deliberately its own handler (not an addition to ProductionPlanHandler) since it reads a
/// different shape: MealPlan/Order data joined by Facility.RouteNumber, not ProductionPlan/Location.
/// NOTE: dish labels show full allergen names (e.g. "Milch, Gluten"), not the letter-code system
/// (a1, b, c…) the customer's own example PDF uses — that coding table doesn't exist anywhere in
/// this codebase yet and is out of scope here; this is an internal admin tool, not the legally
/// formatted customer-facing menu card (see Phase 1's Speisekarte work for that).</summary>
public class ProductionPlanPrintHandler(DailyGourmetDbContext db, IPdfService pdfService)
{
    private static readonly Dictionary<DietLine, string> LineLabels = new()
    {
        [DietLine.NORMALKOST] = "Normalkost",
        [DietLine.VEGGIE] = "Veggie",
        [DietLine.GLUTENFREI_LAKTOSEFREI] = "Glutenfrei/Laktosefrei",
        [DietLine.ALTERNATIV] = "Alternativ Gericht",
    };
    private static readonly DietLine[] LineOrder = [DietLine.NORMALKOST, DietLine.VEGGIE, DietLine.GLUTENFREI_LAKTOSEFREI, DietLine.ALTERNATIV];

    public async Task<byte[]> RenderAsync(Guid mealPlanId, DateOnly date, CancellationToken ct = default)
    {
        var mealPlan = await db.MealPlans.FirstOrDefaultAsync(m => m.Id == mealPlanId, ct) ?? throw new NotFoundException(nameof(MealPlan), mealPlanId);
        var day = await db.MealPlanDays
            .Include(d => d.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Allergens).ThenInclude(a => a.Allergen)
            .FirstOrDefaultAsync(d => d.MealPlanId == mealPlanId && d.Date == date, ct)
            ?? throw new NotFoundException(nameof(MealPlanDay), date);

        var facilityIds = await db.MealPlanFacilities.Where(f => f.MealPlanId == mealPlanId).Select(f => f.FacilityId).ToListAsync(ct);
        var facilities = await db.Facilities.Where(f => facilityIds.Contains(f.Id)).ToListAsync(ct);

        var orderItems = await db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Date == date && oi.Order.MealPlanId == mealPlanId && facilityIds.Contains(oi.Order.FacilityId))
            .ToListAsync(ct);

        var presentLines = LineOrder.Where(l => day.Items.Any(i => i.DietLine == l)).ToList();
        var recipeIdsByLine = presentLines.ToDictionary(l => l, l => day.Items.Where(i => i.DietLine == l).Select(i => i.RecipeId).ToHashSet());

        var columns = presentLines.Select(l =>
        {
            var items = day.Items.Where(i => i.DietLine == l).ToList();
            var names = string.Join(" + ", items.Select(i => i.Recipe.Name));
            var allergens = items.SelectMany(i => i.Recipe.Ingredients.SelectMany(ri => ri.Ingredient.Allergens.Select(a => a.Allergen.Name))).Distinct().ToList();
            var label = allergens.Count > 0 ? $"{names} ({string.Join(", ", allergens)})" : names;
            return new ProductionPlanColumnModel(LineLabels[l], label);
        }).ToList();

        var rows = facilities
            .OrderBy(f => f.RouteNumber ?? "zzz").ThenBy(f => f.Name)
            .Select(f =>
            {
                var facilityOrderItems = orderItems.Where(oi => oi.Order.FacilityId == f.Id).ToList();
                var portionsByColumn = presentLines.Select(l =>
                {
                    var matching = facilityOrderItems.Where(oi => recipeIdsByLine[l].Contains(oi.RecipeId)).ToList();
                    // No row at all means "not yet decided" (see the Order submission-validation
                    // convention) — shown blank, distinct from an explicit 0 which prints as "0".
                    return matching.Count > 0 ? (int?)matching.Sum(oi => oi.Portions) : null;
                }).ToList();

                // Bemerkungen: the facility's standing note (e.g. a recurring allergy) plus any
                // per-item notes — including 0-portion "Wunschgericht" rows (see the plan's
                // wish-dish convention), which would otherwise silently disappear from this view.
                var noteFragments = new List<string>();
                if (!string.IsNullOrWhiteSpace(f.Notes)) noteFragments.Add(f.Notes!);
                noteFragments.AddRange(facilityOrderItems.Where(oi => !string.IsNullOrWhiteSpace(oi.Note)).Select(oi => oi.Note!).Distinct());

                return new ProductionPlanRowModel(f.RouteNumber ?? "–", f.Name, portionsByColumn, noteFragments.Count > 0 ? string.Join(" · ", noteFragments) : null);
            })
            .ToList();

        var model = new ProductionPlanModel(mealPlan.CalendarWeek, day.Weekday, date, columns, rows);
        return pdfService.Render(new ProductionPlanDocument(model));
    }
}
