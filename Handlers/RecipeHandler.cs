using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Recipes;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using DailyGourmet.Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class RecipeHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IPdfService pdfService)
{
    private static IQueryable<Recipe> FullQuery(DailyGourmetDbContext db) => db.Recipes
        .Include(r => r.Category)
        .Include(r => r.CreatedByUser)
        .Include(r => r.PrepSteps)
        .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Allergens).ThenInclude(a => a.Allergen)
        .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Additives)
        .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.SupplierPrices)
        .Include(r => r.AllergenOverrides)
        .Include(r => r.AdditiveOverrides)
        .Include(r => r.TargetGroups).ThenInclude(tg => tg.TargetAudienceGroupEntity);

    public async Task<PagedResult<RecipeDto>> ListAsync(string? search, Guid? category, bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(r => r.Name.Contains(search));
        if (category is { } cat) query = query.Where(r => r.CategoryId == cat);
        if (active is { } isActive) query = query.Where(r => r.Active == isActive);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(r => r.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<RecipeDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<RecipeDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id));

    public async Task<RecipeDto> CreateAsync(SaveRecipeDto dto, CancellationToken ct = default)
    {
        var recipe = new Recipe
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId!.Value,
            CategoryId = dto.CategoryId,
            CreatedByUserId = tenantContext.UserId ?? throw new ValidationException("Kein Benutzerkontext vorhanden."),
            Name = dto.Name.Trim(),
            Description = dto.Description,
            RecipeNumber = dto.RecipeNumber,
            StandardPortions = dto.StandardPortions,
            PortionWeightG = dto.PortionWeightG,
            PrepTimeMinutes = dto.PrepTimeMinutes,
            Difficulty = dto.Difficulty,
            Vegetarian = dto.Vegetarian,
            Vegan = dto.Vegan,
            GlutenFree = dto.GlutenFree,
            LactoseFree = dto.LactoseFree,
            DgeCertified = dto.DgeCertified,
            ProductionNotes = dto.ProductionNotes,
            ImageUrl = dto.ImageUrl,
            CoreTemperatureC = dto.CoreTemperatureC,
            StorageNote = dto.StorageNote,
            ShelfLifeAfterPrep = dto.ShelfLifeAfterPrep,
            Active = dto.Active,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.Recipes.Add(recipe);
        ApplyChildren(recipe, dto);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(recipe.Id, ct);
    }

    public async Task<RecipeDto> UpdateAsync(Guid id, SaveRecipeDto dto, CancellationToken ct = default)
    {
        var recipe = await db.Recipes
            .Include(r => r.PrepSteps).Include(r => r.Ingredients).Include(r => r.TargetGroups)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);

        recipe.CategoryId = dto.CategoryId;
        recipe.Name = dto.Name.Trim();
        recipe.Description = dto.Description;
        recipe.RecipeNumber = dto.RecipeNumber;
        recipe.StandardPortions = dto.StandardPortions;
        recipe.PortionWeightG = dto.PortionWeightG;
        recipe.PrepTimeMinutes = dto.PrepTimeMinutes;
        recipe.Difficulty = dto.Difficulty;
        recipe.Vegetarian = dto.Vegetarian;
        recipe.Vegan = dto.Vegan;
        recipe.GlutenFree = dto.GlutenFree;
        recipe.LactoseFree = dto.LactoseFree;
        recipe.DgeCertified = dto.DgeCertified;
        recipe.ProductionNotes = dto.ProductionNotes;
        recipe.ImageUrl = dto.ImageUrl;
        recipe.CoreTemperatureC = dto.CoreTemperatureC;
        recipe.StorageNote = dto.StorageNote;
        recipe.ShelfLifeAfterPrep = dto.ShelfLifeAfterPrep;
        recipe.Active = dto.Active;
        recipe.Version += 1;
        recipe.UpdatedAt = DateTime.UtcNow;

        db.RecipePrepSteps.RemoveRange(recipe.PrepSteps);
        db.RecipeIngredients.RemoveRange(recipe.Ingredients);
        db.RecipeTargetGroups.RemoveRange(recipe.TargetGroups);
        ApplyChildren(recipe, dto);

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<RecipeDto> DuplicateAsync(Guid id, CancellationToken ct = default)
    {
        var source = await FullQuery(db).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);
        var copy = new Recipe
        {
            Id = Guid.NewGuid(),
            TenantId = source.TenantId,
            CategoryId = source.CategoryId,
            CreatedByUserId = tenantContext.UserId ?? source.CreatedByUserId,
            Name = source.Name + " (Kopie)",
            Description = source.Description,
            RecipeNumber = source.RecipeNumber,
            StandardPortions = source.StandardPortions,
            PortionWeightG = source.PortionWeightG,
            PrepTimeMinutes = source.PrepTimeMinutes,
            Difficulty = source.Difficulty,
            Vegetarian = source.Vegetarian,
            Vegan = source.Vegan,
            GlutenFree = source.GlutenFree,
            LactoseFree = source.LactoseFree,
            DgeCertified = source.DgeCertified,
            ProductionNotes = source.ProductionNotes,
            ImageUrl = source.ImageUrl,
            CoreTemperatureC = source.CoreTemperatureC,
            StorageNote = source.StorageNote,
            ShelfLifeAfterPrep = source.ShelfLifeAfterPrep,
            Active = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
        };
        db.Recipes.Add(copy);
        foreach (var step in source.PrepSteps)
            db.RecipePrepSteps.Add(new RecipePrepStep { Id = Guid.NewGuid(), RecipeId = copy.Id, StepNumber = step.StepNumber, Text = step.Text, CreatedAt = DateTime.UtcNow });
        foreach (var ri in source.Ingredients)
            db.RecipeIngredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = copy.Id, IngredientId = ri.IngredientId, Quantity = ri.Quantity, Unit = ri.Unit, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(copy.Id, ct);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await db.Recipes.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);
        recipe.Active = false;
        recipe.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<RecipeScaleResultDto> ScaleAsync(Guid id, int portions, CancellationToken ct = default)
    {
        var recipe = await db.Recipes.Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);
        if (recipe.StandardPortions <= 0) throw new ValidationException("Rezept hat keine gültige Standardportion.");

        var factor = (decimal)portions / recipe.StandardPortions;
        return new RecipeScaleResultDto
        {
            Factor = decimal.Round(factor, 4),
            Ingredients = recipe.Ingredients.Select(ri => new RecipeScaleIngredientDto
            {
                IngredientId = ri.IngredientId,
                IngredientName = ri.Ingredient.Name,
                OriginalQuantity = ri.Quantity,
                ScaledQuantity = decimal.Round(ri.Quantity * factor, 2, MidpointRounding.AwayFromZero),
                Unit = ri.Unit.ToString(),
            }).ToList(),
        };
    }

    /// <summary>Scales a per-100g nutrition figure to a per-portion one — mirrors the frontend's
    /// naehrwerteProPortion() in features/recipes/store.ts, kept in sync deliberately since both
    /// read from the same per-100g Recipe.Nutrition source.</summary>
    public static RecipeNutritionDto ScaleNutritionToPortion(RecipeNutritionDto n, decimal portionWeightG)
    {
        var factor = portionWeightG / 100m;
        return new RecipeNutritionDto
        {
            Kcal = decimal.Round(n.Kcal * factor),
            Kj = decimal.Round(n.Kj * factor),
            FatG = decimal.Round(n.FatG * factor, 1),
            SaturatedFatG = decimal.Round(n.SaturatedFatG * factor, 1),
            CarbsG = decimal.Round(n.CarbsG * factor, 1),
            SugarG = decimal.Round(n.SugarG * factor, 1),
            FiberG = decimal.Round(n.FiberG * factor, 1),
            ProteinG = decimal.Round(n.ProteinG * factor, 1),
            SaltG = decimal.Round(n.SaltG * factor, 2),
            AlcoholG = decimal.Round(n.AlcoholG * factor, 1),
        };
    }

    public async Task<byte[]> RenderLabelAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await FullQuery(db).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);
        var dto = ToDto(recipe);

        var (nutrition, nutritionLabel) = dto.Nutrition switch
        {
            { } n when recipe.PortionWeightG is { } weight => (ScaleNutritionToPortion(n, weight), "Nährwerte pro Portion"),
            { } n => (n, "Nährwerte je 100 g (Portionsgewicht nicht hinterlegt)"),
            null => (null, "Nährwerte pro Portion"),
        };

        var model = new RecipeLabelModel(
            RecipeName: recipe.Name,
            PortionWeightG: recipe.PortionWeightG,
            Ingredients: recipe.Ingredients.Select(ri => $"{ri.Quantity:0.#} {ri.Unit} {ri.Ingredient.Name}").ToList(),
            Allergens: dto.ResolvedAllergens,
            Additives: dto.ResolvedAdditives,
            Nutrition: nutrition,
            NutritionLabel: nutritionLabel);

        return pdfService.Render(new RecipeLabelDocument(model));
    }

    private void ApplyChildren(Recipe recipe, SaveRecipeDto dto)
    {
        var stepNumber = 1;
        foreach (var step in dto.PrepSteps.Where(s => !string.IsNullOrWhiteSpace(s)))
            db.RecipePrepSteps.Add(new RecipePrepStep { Id = Guid.NewGuid(), RecipeId = recipe.Id, StepNumber = stepNumber++, Text = step, CreatedAt = DateTime.UtcNow });

        foreach (var ri in dto.Ingredients)
            db.RecipeIngredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, IngredientId = ri.IngredientId, Quantity = ri.Quantity, Unit = ri.Unit, CreatedAt = DateTime.UtcNow });

        foreach (var groupId in dto.TargetGroupIds.Distinct())
            db.RecipeTargetGroups.Add(new RecipeTargetGroup { RecipeId = recipe.Id, TargetAudienceGroupId = groupId });
    }

    /// <summary>Cheapest known price for an ingredient, and the unit it's priced in: the cheapest
    /// supplier offer if one exists, else the ingredient's standard PurchasePrice (per PurchaseUnit,
    /// converted to a per-BaseUnit price via ConversionFactor). Null when no price is known at all.</summary>
    private static (decimal PricePerUnit, Unit Unit)? ResolveUnitPrice(Ingredient ingredient)
    {
        var cheapest = ingredient.SupplierPrices.OrderBy(p => p.Price).FirstOrDefault();
        if (cheapest != null) return (cheapest.Price, cheapest.Unit);
        if (ingredient.PurchasePrice is { } purchasePrice && ingredient.ConversionFactor > 0)
            return (purchasePrice / ingredient.ConversionFactor, ingredient.BaseUnit);
        return null;
    }

    /// <summary>Converts a quantity between units within the same family (g&lt;-&gt;kg, ml&lt;-&gt;l);
    /// returns null for cross-family or Stueck conversions, since there's no general unit-conversion
    /// system (e.g. density) anywhere in this codebase to fall back on.</summary>
    private static decimal? ConvertQuantity(decimal quantity, Unit from, Unit to)
    {
        if (from == to) return quantity;
        return (from, to) switch
        {
            (Unit.g, Unit.kg) => quantity / 1000m,
            (Unit.kg, Unit.g) => quantity * 1000m,
            (Unit.ml, Unit.l) => quantity / 1000m,
            (Unit.l, Unit.ml) => quantity * 1000m,
            _ => null,
        };
    }

    /// <summary>Sum of each ingredient's quantity × cheapest known price, unit-normalized where
    /// possible, divided by portion count. Ingredients that can't be priced (no price at all, or an
    /// incompatible unit) are simply left out of the sum — this is a rough estimate, not an
    /// authoritative cost (that lives in Einkauf).</summary>
    public static decimal? ComputeEstimatedCostPerPortion(Recipe recipe)
    {
        if (recipe.StandardPortions <= 0) return null;
        decimal total = 0;
        var pricedAny = false;
        foreach (var ri in recipe.Ingredients)
        {
            var priced = ResolveUnitPrice(ri.Ingredient);
            if (priced is not { } p) continue;
            var convertedQuantity = ConvertQuantity(ri.Quantity, ri.Unit, p.Unit);
            if (convertedQuantity is not { } qty) continue;
            total += qty * p.PricePerUnit;
            pricedAny = true;
        }
        return pricedAny ? decimal.Round(total / recipe.StandardPortions, 2, MidpointRounding.AwayFromZero) : null;
    }

    private static RecipeDto ToDto(Recipe r)
    {
        var allergensOverridden = r.AllergenOverrides.Count > 0;
        var resolvedAllergens = allergensOverridden
            ? r.AllergenOverrides.Select(a => a.Text).Distinct().ToArray()
            : r.Ingredients.SelectMany(ri => ri.Ingredient.Allergens.Select(a => a.Allergen?.Name ?? string.Empty)).Where(n => n != string.Empty).Distinct().ToArray();

        var additivesOverridden = r.AdditiveOverrides.Count > 0;
        var resolvedAdditives = additivesOverridden
            ? r.AdditiveOverrides.Select(a => a.Text).Distinct().ToArray()
            : r.Ingredients.SelectMany(ri => ri.Ingredient.Additives.Select(a => a.Text)).Distinct().ToArray();

        return new RecipeDto
        {
            Id = r.Id,
            Name = r.Name,
            Description = r.Description,
            CategoryId = r.CategoryId,
            CategoryName = r.Category?.Name ?? string.Empty,
            RecipeNumber = r.RecipeNumber,
            StandardPortions = r.StandardPortions,
            PortionWeightG = r.PortionWeightG,
            PrepTimeMinutes = r.PrepTimeMinutes,
            Difficulty = r.Difficulty.ToString(),
            Vegetarian = r.Vegetarian,
            Vegan = r.Vegan,
            GlutenFree = r.GlutenFree,
            LactoseFree = r.LactoseFree,
            DgeCertified = r.DgeCertified,
            EstimatedCostPerPortion = ComputeEstimatedCostPerPortion(r),
            Nutrition = r.Nutrition is { } n
                ? new RecipeNutritionDto
                {
                    Kcal = n.Kcal, Kj = n.Kj, FatG = n.FatG, SaturatedFatG = n.SaturatedFatG,
                    CarbsG = n.CarbsG, SugarG = n.SugarG, FiberG = n.FiberG, ProteinG = n.ProteinG,
                    SaltG = n.SaltG, AlcoholG = n.AlcoholG,
                }
                : null,
            ProductionNotes = r.ProductionNotes,
            ImageUrl = r.ImageUrl,
            CoreTemperatureC = r.CoreTemperatureC,
            StorageNote = r.StorageNote,
            ShelfLifeAfterPrep = r.ShelfLifeAfterPrep,
            Active = r.Active,
            Version = r.Version,
            CreatedAt = r.CreatedAt,
            UpdatedAt = r.UpdatedAt,
            CreatedByUserName = r.CreatedByUser?.Name ?? string.Empty,
            PrepSteps = r.PrepSteps.OrderBy(s => s.StepNumber).Select(s => s.Text).ToArray(),
            Ingredients = r.Ingredients.Select(ri => new RecipeIngredientDto
            {
                IngredientId = ri.IngredientId, IngredientName = ri.Ingredient?.Name ?? string.Empty, Quantity = ri.Quantity, Unit = ri.Unit.ToString(),
            }).ToList(),
            ResolvedAllergens = resolvedAllergens,
            AllergensAreOverridden = allergensOverridden,
            ResolvedAdditives = resolvedAdditives,
            AdditivesAreOverridden = additivesOverridden,
            NutriScore = r.NutriScore?.ToString(),
            NutritionIsAuthoritative = r.Nutrition != null,
            TargetGroupIds = r.TargetGroups.Select(tg => tg.TargetAudienceGroupId).ToArray(),
            TargetGroupNames = r.TargetGroups.Select(tg => tg.TargetAudienceGroupEntity?.Name ?? string.Empty).Where(n => n != string.Empty).ToArray(),
        };
    }
}
