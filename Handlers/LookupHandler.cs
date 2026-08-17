using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Read-only reference data — global, not tenant-scoped (seeded once for the platform).</summary>
public class LookupHandler(DailyGourmetDbContext db)
{
    public async Task<List<LookupDto>> IngredientCategoriesAsync(CancellationToken ct = default) =>
        await db.IngredientCategories.OrderBy(c => c.Name).Select(c => new LookupDto { Id = c.Id, Name = c.Name }).ToListAsync(ct);

    public async Task<List<LookupDto>> AllergensAsync(CancellationToken ct = default) =>
        await db.Allergens.OrderBy(a => a.Name).Select(a => new LookupDto { Id = a.Id, Name = a.Name }).ToListAsync(ct);

    public async Task<List<LookupDto>> RecipeCategoriesAsync(CancellationToken ct = default) =>
        await db.RecipeCategories.OrderBy(c => c.Name).Select(c => new LookupDto { Id = c.Id, Name = c.Name }).ToListAsync(ct);

    public async Task<List<LookupDto>> TargetAudienceGroupsAsync(CancellationToken ct = default) =>
        await db.TargetAudienceGroups.OrderBy(g => g.Name).Select(g => new LookupDto { Id = g.Id, Name = g.Name }).ToListAsync(ct);
}
