using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using DailyGourmet.Api.Models.DTOs.Recipes;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using DailyGourmet.Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class RecipeHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IPdfService pdfService, IngredientHandler ingredientHandler)
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
        .Include(r => r.NutritionClaims)
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
            ReductionFactor = dto.ReductionFactor,
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
        recipe.ReductionFactor = dto.ReductionFactor;
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
            ReductionFactor = source.ReductionFactor,
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

    // ---- Rezeptrechner import (recipes + the ingredients they reference) ----
    //
    // Real export sample confirmed the two files' shape (see RezeptrechnerCsvParser). The third
    // export the customer sent (Preise-Wareneinsatz) has €0.00 in every row — no real pricing exists
    // yet, and there's nowhere on Recipe to store it anyway (EstimatedCostPerPortion above is always
    // computed live from Ingredient/IngredientSupplierPrice, never stored) — so it's intentionally
    // not read here. Real prices, once available, go in through the existing per-ingredient
    // Lieferantenpreise screen instead.

    private static readonly Dictionary<string, string> CategoryTypoFixes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Haubtgericht"] = "Hauptgericht",
        ["Nachspeise"] = "Dessert",
    };
    private static readonly HashSet<string> VegetarianTags = new(StringComparer.OrdinalIgnoreCase) { "Veggie", "Vegetarisch" };
    private static readonly HashSet<string> VeganTags = new(StringComparer.OrdinalIgnoreCase) { "Vegan" };
    private static readonly HashSet<string> GlutenFreeTags = new(StringComparer.OrdinalIgnoreCase) { "Glutenfrei" };
    private static readonly HashSet<string> LactoseFreeTags = new(StringComparer.OrdinalIgnoreCase) { "Lactosefrei", "Laktosefrei" };

    private record ParsedCategory(string? CategoryName, bool Vegetarian, bool Vegan, bool GlutenFree, bool LactoseFree);

    /// <summary>The source "Kategorie" column is free text that mixes a real category name with
    /// comma-joined dietary tags ("Hauptgericht, Veggie") or holds only tags ("Glutenfrei,
    /// Lactosefrei") with no category at all — plus real-world typos ("Haubtgericht" ×26). Each
    /// token is checked against known category names (after typo-fixing) and known dietary tags;
    /// the first token that matches a category wins, unrecognized tokens (portion-size notes,
    /// stray free text) are silently ignored rather than rejected.</summary>
    private static ParsedCategory ResolveCategory(string? raw, IReadOnlyCollection<string> knownCategoryNames)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new ParsedCategory(null, false, false, false, false);
        string? matchedCategory = null;
        bool vegetarian = false, vegan = false, glutenFree = false, lactoseFree = false;
        foreach (var rawToken in raw.Split(','))
        {
            var token = rawToken.Trim();
            if (token.Length == 0) continue;
            if (CategoryTypoFixes.TryGetValue(token, out var fixedName)) token = fixedName;

            if (VegetarianTags.Contains(token)) { vegetarian = true; continue; }
            if (VeganTags.Contains(token)) { vegan = true; vegetarian = true; continue; }
            if (GlutenFreeTags.Contains(token)) { glutenFree = true; continue; }
            if (LactoseFreeTags.Contains(token)) { lactoseFree = true; continue; }

            if (matchedCategory is null)
                matchedCategory = knownCategoryNames.FirstOrDefault(n => string.Equals(n, token, StringComparison.OrdinalIgnoreCase));
        }
        return new ParsedCategory(matchedCategory, vegetarian, vegan, glutenFree, lactoseFree);
    }

    private static IEnumerable<string> SplitTags(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',').Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase);

    private static string NextArticleNumber(HashSet<string> used)
    {
        var i = used.Count + 1;
        while (true)
        {
            var candidate = $"ZUT-{i:D4}";
            if (used.Add(candidate)) return candidate;
            i++;
        }
    }

    public async Task<RecipeImportResultDto> ImportFromRezeptrechnerAsync(Stream zutatenMengenCsv, Stream artikeldatenCsv, Stream? allergeneListeCsv = null, CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var result = new RecipeImportResultDto();

        var zutatenRows = RezeptrechnerCsvParser.ParseZutatenMengen(zutatenMengenCsv);
        var artikelRows = RezeptrechnerCsvParser.ParseArtikeldaten(artikeldatenCsv);
        // Optional, structured allergen matrix — far more precise than Artikeldaten's free-text
        // "Allergene" column (e.g. distinguishes "Gluten/ Weizen" from plain "Gluten"), so it takes
        // priority per recipe when supplied; recipes missing from it fall back to the free text.
        var allergenByRecipe = allergeneListeCsv is null
            ? new Dictionary<string, RezeptrechnerAllergenZeile>(StringComparer.OrdinalIgnoreCase)
            : RezeptrechnerCsvParser.ParseAllergeneListe(allergeneListeCsv)
                .GroupBy(a => a.RecipeName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // ---- Step 1: sync every raw ingredient referenced by a recipe line (reuses the existing,
        // manual-edit-protecting Ingredient sync — see IngredientHandler.SyncAsync) ----
        var usedArticleNumbers = new HashSet<string>(await db.Ingredients.Select(i => i.ArticleNumber).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);
        var ingredientRows = zutatenRows
            .GroupBy(r => r.IngredientExternalRefId)
            .Select(g => g.First())
            .Select(u => new RezeptrechnerImportRowDto
            {
                ExternalRefId = u.IngredientExternalRefId,
                Name = u.IngredientName,
                ArticleNumber = NextArticleNumber(usedArticleNumbers),
                BaseUnit = Unit.kg,
                PurchaseUnit = "kg",
                ConversionFactor = 1,
            })
            .ToList();

        var ingredientSync = await ingredientHandler.SyncAsync(ingredientRows, ct);
        result.IngredientsAdded = ingredientSync.Added;
        result.IngredientsUpdated = ingredientSync.Updated;
        result.IngredientsSkippedManuallyEdited = ingredientSync.SkippedManuallyEdited;

        var ingredientIdByRef = await db.Ingredients.Where(i => i.ExternalRefId != null).ToDictionaryAsync(i => i.ExternalRefId!, i => i.Id, ct);

        // ---- Step 2: upsert every recipe by name, matching the two files together ----
        var ingredientsByRecipe = zutatenRows.GroupBy(r => r.RecipeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var artikelByRecipe = artikelRows.GroupBy(a => a.RecipeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var allRecipeNames = new HashSet<string>(ingredientsByRecipe.Keys, StringComparer.OrdinalIgnoreCase);
        allRecipeNames.UnionWith(artikelByRecipe.Keys);

        var categories = await db.RecipeCategories.ToListAsync(ct);
        var categoryNames = categories.Select(c => c.Name).ToList();
        var fallbackCategoryId = categories.FirstOrDefault(c => c.Name == "Sonstiges")?.Id ?? categories.FirstOrDefault()?.Id
            ?? throw new ValidationException("Keine Rezeptkategorie vorhanden — bitte zuerst mindestens eine Kategorie anlegen.");

        var authorUserId = await db.Users.Where(u => u.Role == Role.TENANT_OWNER).OrderBy(u => u.CreatedAt).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct)
            ?? await db.Users.OrderBy(u => u.CreatedAt).Select(u => (Guid?)u.Id).FirstOrDefaultAsync(ct)
            ?? throw new ValidationException("Kein Benutzer im Mandanten vorhanden, dem importierte Rezepte zugeordnet werden können.");

        var existingRecipes = await db.Recipes
            .Include(r => r.Ingredients).Include(r => r.AllergenOverrides).Include(r => r.AdditiveOverrides).Include(r => r.NutritionClaims)
            .ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase, ct);

        var now = DateTime.UtcNow;
        var recipeNutritionByName = new Dictionary<string, RecipeNutrition>(StringComparer.OrdinalIgnoreCase);

        foreach (var recipeName in allRecipeNames)
        {
            artikelByRecipe.TryGetValue(recipeName, out var artikel);
            ingredientsByRecipe.TryGetValue(recipeName, out var ingredientLines);
            ingredientLines ??= [];

            var parsedCategory = ResolveCategory(artikel?.CategoryRaw, categoryNames);
            var categoryId = parsedCategory.CategoryName is { } catName ? categories.First(c => c.Name == catName).Id : fallbackCategoryId;
            if (parsedCategory.CategoryName is null && artikel?.CategoryRaw is not null)
                result.Warnings.Add(new RecipeImportWarningDto { Reason = $"Rezept „{recipeName}“: Kategorie „{artikel.CategoryRaw}“ nicht erkannt — „Sonstiges“ verwendet." });

            var standardPortions = artikel?.StandardPortions ?? ingredientLines.Select(l => l.PortionsInFile).FirstOrDefault();
            if (standardPortions <= 0) standardPortions = 10;

            var description = artikel?.IngredientListText is { Length: > 0 } list ? (list.Length > 2000 ? list[..2000] : list) : recipeName;
            var nutrition = artikel is null ? null : new RecipeNutrition
            {
                Kcal = artikel.KcalPer100, Kj = artikel.KjPer100, FatG = artikel.FatPer100, SaturatedFatG = artikel.SaturatedFatPer100,
                CarbsG = artikel.CarbsPer100, SugarG = artikel.SugarPer100, FiberG = artikel.FiberPer100, ProteinG = artikel.ProteinPer100,
                SaltG = artikel.SaltPer100, AlcoholG = artikel.AlcoholPer100,
            };
            NutriScore? nutriScore = artikel?.NutriScore is { } ns && Enum.TryParse<NutriScore>(ns, true, out var parsedScore) ? parsedScore : null;

            if (!ingredientLines.Any() && artikel is not null)
                result.Warnings.Add(new RecipeImportWarningDto { Reason = $"Rezept „{recipeName}“: keine Zutatenzeilen gefunden — Rezept ohne Zutaten angelegt." });

            // Reduktionsfaktor = Gewicht zubereitet (GewichtPortion_g × Portionen, aus Kennzeichnung) ÷
            // Rohgewicht (Summe der Zutatenmengen, aus Zutaten-Mengen) — keine der beiden Exportdateien
            // liefert den Faktor direkt, aber beide Größen, aus denen er sich exakt herleiten lässt.
            // Verifiziert am Kundenbeispiel "Cevapcici vom Geflügel…": 323,8g × 10 ÷ 4625g = 0,700 —
            // exakt der Wert, den der Rezeptrechner selbst für dieses Rezept anzeigt. Ein Ergebnis
            // außerhalb eines plausiblen Bereichs (z. B. weil nur Stück-Zutaten ohne Gewicht vorliegen)
            // wird verworfen, statt einen falschen Wert zu speichern.
            var rawWeightG = ingredientLines.Sum(l => l.Quantity) * 1000;
            var preparedWeightG = artikel?.PortionWeightG is { } pw ? pw * standardPortions : (decimal?)null;
            var computedReductionFactor = preparedWeightG is { } prepared && rawWeightG > 0 ? decimal.Round(prepared / rawWeightG, 3) : (decimal?)null;
            if (computedReductionFactor is < 0.1m or > 3m) computedReductionFactor = null;

            Recipe recipe;
            if (existingRecipes.TryGetValue(recipeName, out var found))
            {
                recipe = found;
                recipe.CategoryId = categoryId;
                recipe.RecipeNumber = artikel?.RecipeNumber ?? recipe.RecipeNumber;
                recipe.StandardPortions = standardPortions;
                recipe.PortionWeightG = artikel?.PortionWeightG ?? recipe.PortionWeightG;
                recipe.Vegetarian = parsedCategory.Vegetarian || recipe.Vegetarian;
                recipe.Vegan = parsedCategory.Vegan || recipe.Vegan;
                recipe.GlutenFree = parsedCategory.GlutenFree || recipe.GlutenFree;
                recipe.LactoseFree = parsedCategory.LactoseFree || recipe.LactoseFree;
                recipe.Description = description;
                recipe.Nutrition = nutrition ?? recipe.Nutrition;
                recipe.NutriScore = nutriScore ?? recipe.NutriScore;
                recipe.NutriScoreCategory = artikel?.NutriScoreCategory ?? recipe.NutriScoreCategory;
                recipe.ReductionFactor = computedReductionFactor ?? recipe.ReductionFactor;
                recipe.Version += 1;
                recipe.UpdatedAt = now;

                db.RecipeIngredients.RemoveRange(recipe.Ingredients);
                db.RecipeAllergenOverrides.RemoveRange(recipe.AllergenOverrides);
                db.RecipeAdditiveOverrides.RemoveRange(recipe.AdditiveOverrides);
                db.RecipeNutritionClaims.RemoveRange(recipe.NutritionClaims);
                result.RecipesUpdated++;
            }
            else
            {
                recipe = new Recipe
                {
                    Id = Guid.NewGuid(), TenantId = tenantId, CategoryId = categoryId, CreatedByUserId = authorUserId,
                    Name = recipeName, Description = description, RecipeNumber = artikel?.RecipeNumber,
                    StandardPortions = standardPortions, PortionWeightG = artikel?.PortionWeightG,
                    PrepTimeMinutes = 0, Difficulty = Difficulty.Mittel,
                    Vegetarian = parsedCategory.Vegetarian, Vegan = parsedCategory.Vegan,
                    GlutenFree = parsedCategory.GlutenFree, LactoseFree = parsedCategory.LactoseFree,
                    Active = true, Version = 1, CreatedAt = now,
                    Nutrition = nutrition, NutriScore = nutriScore, NutriScoreCategory = artikel?.NutriScoreCategory,
                    ReductionFactor = computedReductionFactor ?? 1m,
                };
                db.Recipes.Add(recipe);
                result.RecipesAdded++;
            }
            if (recipe.Nutrition is { } recipeNutrition) recipeNutritionByName[recipeName] = recipeNutrition;

            foreach (var line in ingredientLines)
            {
                if (!ingredientIdByRef.TryGetValue(line.IngredientExternalRefId, out var ingredientId))
                {
                    result.Warnings.Add(new RecipeImportWarningDto { Reason = $"Rezept „{recipeName}“: Zutat „{line.IngredientName}“ konnte keiner Zutat zugeordnet werden." });
                    continue;
                }
                db.RecipeIngredients.Add(new RecipeIngredient { Id = Guid.NewGuid(), RecipeId = recipe.Id, IngredientId = ingredientId, Quantity = line.Quantity, Unit = Unit.kg, CreatedAt = now });
            }

            var allergenTexts = allergenByRecipe.TryGetValue(recipeName, out var allergenRow)
                ? allergenRow.MarkedAllergens
                : SplitTags(artikel?.AllergensText);
            if (allergenRow is not null) result.AllergensFromListApplied++;
            foreach (var text in allergenTexts)
                db.RecipeAllergenOverrides.Add(new RecipeAllergenOverride { Id = Guid.NewGuid(), RecipeId = recipe.Id, Text = text, CreatedAt = now });
            foreach (var text in SplitTags(artikel?.AdditivesText))
                db.RecipeAdditiveOverrides.Add(new RecipeAdditiveOverride { Id = Guid.NewGuid(), RecipeId = recipe.Id, Text = text, CreatedAt = now });
            foreach (var text in SplitTags(artikel?.NutritionClaimsText))
                db.RecipeNutritionClaims.Add(new RecipeNutritionClaim { Id = Guid.NewGuid(), RecipeId = recipe.Id, Text = text, CreatedAt = now });
        }

        // ---- Step 3: backfill nutrition for ingredients that are themselves a sub-recipe used as an
        // ingredient elsewhere (e.g. "Bratkartoffeln Grundrezept" appearing inside another dish) —
        // the Rezeptrechner exports never carry true per-ingredient nutrition for raw commodities, but
        // for these the matching recipe's own per-100g figures (just computed above) are the real
        // thing, not a guess. Never touches a manually-edited ingredient. ----
        var ingredientsByName = (await db.Ingredients
            .Where(i => i.TenantId == tenantId && !i.IsManuallyEdited)
            .ToListAsync(ct))
            .GroupBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var (name, recipeNutrition) in recipeNutritionByName)
        {
            if (!ingredientsByName.TryGetValue(name, out var ingredient)) continue;
            ingredient.Nutrition = new IngredientNutrition
            {
                Kcal = recipeNutrition.Kcal, Kj = recipeNutrition.Kj, ProteinG = recipeNutrition.ProteinG,
                FatG = recipeNutrition.FatG, SaturatedFatG = recipeNutrition.SaturatedFatG,
                CarbsG = recipeNutrition.CarbsG, SugarG = recipeNutrition.SugarG, FiberG = recipeNutrition.FiberG,
                SaltG = recipeNutrition.SaltG, AlcoholG = recipeNutrition.AlcoholG,
                Source = NutritionSource.Manuell,
            };
            ingredient.LastSyncedAt = now;
            ingredient.UpdatedAt = now;
            result.IngredientsNutritionFromRecipeMatch++;
        }

        await db.SaveChangesAsync(ct);
        return result;
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

    /// <summary>Grams a RecipeIngredient's quantity converts to — kg/l are treated as 1000 g resp.
    /// 1000 ml (matching the existing per-ingredient kcal estimate already in the frontend, see
    /// rezept-detail.tsx's inline `faktor` calc), g/ml pass straight through. Stück has no reliable
    /// weight without a per-ingredient conversion this schema doesn't carry — null, so the caller can
    /// show a dash instead of a made-up number.</summary>
    private static decimal? ToGrams(decimal quantity, Unit unit) => unit switch
    {
        Unit.kg or Unit.l => quantity * 1000,
        Unit.g or Unit.ml => quantity,
        _ => null,
    };

    private static DiabeticUnitsDto ToDiabeticUnits(RecipeNutritionDto n) => new()
    {
        Be = decimal.Round(n.CarbsG / 12m, 1),
        Ke = decimal.Round(n.CarbsG / 10m, 1),
        Fpe = decimal.Round((n.FatG * 9 + n.ProteinG * 4) / 100m, 1),
    };

    /// <summary>Backs the "Nährwerte ansehen" view — full per-ingredient breakdown plus the
    /// recipe-level aggregates, diabetic exchange units and nutrition-claim thresholds. The
    /// aggregates (PerPortion/PerRecipe/Per100g) are derived from the recipe's own authoritative
    /// Recipe.Nutrition, never summed from the ingredient rows: most ingredients here still lack
    /// their own real nutrition data (see comment on RecipeNutritionIngredientRowDto), so a sum would
    /// systematically undercount. The ingredient table is shown alongside for transparency /
    /// to make it obvious which ingredients still need real data — not as the source of truth.</summary>
    public async Task<RecipeNutritionDetailDto> GetNutritionDetailAsync(Guid id, CancellationToken ct = default)
    {
        var recipe = await db.Recipes
            .Include(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .Include(r => r.NutritionClaims)
            .FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);

        var ingredientRows = recipe.Ingredients.Select(ri =>
        {
            var gramsNullable = ToGrams(ri.Quantity, ri.Unit);
            var factor = (gramsNullable ?? 0) / 100m;
            var n = ri.Ingredient.Nutrition;
            var hasData = n.Kcal != 0 || n.ProteinG != 0 || n.FatG != 0 || n.CarbsG != 0 || n.SugarG != 0
                || n.SaltG != 0 || n.FiberG != 0 || n.SaturatedFatG != 0 || n.AlcoholG != 0 || n.Kj != 0;
            return new RecipeNutritionIngredientRowDto
            {
                IngredientId = ri.IngredientId,
                IngredientName = ri.Ingredient.Name,
                Quantity = ri.Quantity,
                Unit = ri.Unit.ToString(),
                WeightG = gramsNullable is { } g ? decimal.Round(g, 1) : null,
                HasNutritionData = hasData,
                Nutrition = new RecipeNutritionDto
                {
                    Kcal = decimal.Round(n.Kcal * factor, 2),
                    Kj = decimal.Round(n.Kj * factor, 2),
                    FatG = decimal.Round(n.FatG * factor, 2),
                    SaturatedFatG = decimal.Round(n.SaturatedFatG * factor, 2),
                    CarbsG = decimal.Round(n.CarbsG * factor, 2),
                    SugarG = decimal.Round(n.SugarG * factor, 2),
                    FiberG = decimal.Round(n.FiberG * factor, 2),
                    ProteinG = decimal.Round(n.ProteinG * factor, 2),
                    SaltG = decimal.Round(n.SaltG * factor, 2),
                    AlcoholG = decimal.Round(n.AlcoholG * factor, 2),
                },
            };
        }).ToList();

        var rawWeightG = ingredientRows.Sum(r => r.WeightG ?? 0);
        var preparedWeightG = rawWeightG * recipe.ReductionFactor;
        var effectivePortionWeightG = recipe.PortionWeightG
            ?? (recipe.StandardPortions > 0 && preparedWeightG > 0 ? preparedWeightG / recipe.StandardPortions : (decimal?)null);

        RecipeNutritionDto? perPortion = null, perRecipe = null, per100g = null;
        DiabeticUnitsDto? diabeticPortion = null, diabetic100g = null;
        var claimEvaluations = new List<NutritionClaimEvaluationDto>();

        if (recipe.Nutrition is { } nutrition)
        {
            per100g = new RecipeNutritionDto
            {
                Kcal = nutrition.Kcal, Kj = nutrition.Kj, FatG = nutrition.FatG, SaturatedFatG = nutrition.SaturatedFatG,
                CarbsG = nutrition.CarbsG, SugarG = nutrition.SugarG, FiberG = nutrition.FiberG, ProteinG = nutrition.ProteinG,
                SaltG = nutrition.SaltG, AlcoholG = nutrition.AlcoholG,
            };
            diabetic100g = ToDiabeticUnits(per100g);
            claimEvaluations = recipe.NutritionClaims.Select(c => GermanNutritionClaims.Evaluate(c.Text, per100g)).ToList();

            if (effectivePortionWeightG is { } portionWeight)
            {
                perPortion = ScaleNutritionToPortion(per100g, portionWeight);
                diabeticPortion = ToDiabeticUnits(perPortion);
                perRecipe = new RecipeNutritionDto
                {
                    Kcal = perPortion.Kcal * recipe.StandardPortions, Kj = perPortion.Kj * recipe.StandardPortions,
                    FatG = perPortion.FatG * recipe.StandardPortions, SaturatedFatG = perPortion.SaturatedFatG * recipe.StandardPortions,
                    CarbsG = perPortion.CarbsG * recipe.StandardPortions, SugarG = perPortion.SugarG * recipe.StandardPortions,
                    FiberG = perPortion.FiberG * recipe.StandardPortions, ProteinG = perPortion.ProteinG * recipe.StandardPortions,
                    SaltG = perPortion.SaltG * recipe.StandardPortions, AlcoholG = perPortion.AlcoholG * recipe.StandardPortions,
                };
            }
        }

        return new RecipeNutritionDetailDto
        {
            RawWeightG = decimal.Round(rawWeightG, 1),
            ReductionFactor = recipe.ReductionFactor,
            PreparedWeightG = decimal.Round(preparedWeightG, 1),
            StandardPortions = recipe.StandardPortions,
            PortionWeightG = effectivePortionWeightG is { } w ? decimal.Round(w, 1) : null,
            Ingredients = ingredientRows,
            PerRecipe = perRecipe,
            PerPortion = perPortion,
            Per100g = per100g,
            DiabeticPerPortion = diabeticPortion,
            DiabeticPer100g = diabetic100g,
            ClaimEvaluations = claimEvaluations,
        };
    }

    public async Task<byte[]> RenderLabelAsync(
        Guid id, EtikettOrientierung orientierung, EtikettInhalt inhalt, bool proPortion, decimal? portionsgroesseG, string? mindestensHaltbarBis, CancellationToken ct = default)
    {
        var recipe = await FullQuery(db).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(Recipe), id);
        var dto = ToDto(recipe);

        var portionsgewicht = portionsgroesseG ?? recipe.PortionWeightG;
        var (nutrition, basisLabel) = dto.Nutrition switch
        {
            { } n when proPortion && portionsgewicht is { } gewicht => (ScaleNutritionToPortion(n, gewicht), $"pro Portion ({gewicht:0.#} g)"),
            { } n => (n, "pro 100 g"),
            null => (null, proPortion ? "pro Portion" : "pro 100 g"),
        };

        // Die vom Rezeptrechner importierte Zutatenliste (Description, s. ImportFromRezeptrechnerAsync)
        // enthält bereits die für ein Etikett übliche, verschachtelte Deklaration inkl. Unterrezepten
        // (z. B. "Bratkartoffeln Grundrezept - (Kartoffel, Rapsöl, ...)") — das lässt sich aus den
        // einzelnen RecipeIngredient-Zeilen (nur Menge + Name, ohne diese Verschachtelung) nicht
        // rekonstruieren. Für manuell angelegte Rezepte ohne Import (Description == Name, Fallback aus
        // dem Import) wird stattdessen die einfache Zutatenliste aus den Rezeptzutaten gebildet.
        var ingredientsText = !string.IsNullOrWhiteSpace(recipe.Description) && !string.Equals(recipe.Description, recipe.Name, StringComparison.OrdinalIgnoreCase)
            ? recipe.Description
            : (recipe.Ingredients.Count > 0 ? string.Join(", ", recipe.Ingredients.Select(ri => ri.Ingredient.Name)) : "—");

        var model = new RecipeLabelModel(
            RecipeName: recipe.Name,
            Orientierung: orientierung,
            Inhalt: inhalt,
            NaehrwerteBasisLabel: basisLabel,
            Nutrition: nutrition,
            IngredientsText: ingredientsText,
            MindestensHaltbarBisText: string.IsNullOrWhiteSpace(mindestensHaltbarBis) ? null : mindestensHaltbarBis);

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
            ReductionFactor = r.ReductionFactor,
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
            NutriScoreCategory = r.NutriScoreCategory,
            NutritionIsAuthoritative = r.Nutrition != null,
            NutritionClaims = r.NutritionClaims.Select(c => c.Text).ToArray(),
            TargetGroupIds = r.TargetGroups.Select(tg => tg.TargetAudienceGroupId).ToArray(),
            TargetGroupNames = r.TargetGroups.Select(tg => tg.TargetAudienceGroupEntity?.Name ?? string.Empty).Where(n => n != string.Empty).ToArray(),
        };
    }
}
