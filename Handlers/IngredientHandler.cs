using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class IngredientHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IFeatureFlagService featureFlags)
{
    public async Task<PagedResult<IngredientDto>> ListAsync(string? search, Guid? category, Guid? allergen, bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Ingredients
            .Include(i => i.Category).Include(i => i.Supplier)
            .Include(i => i.Allergens).ThenInclude(a => a.Allergen)
            .Include(i => i.Additives)
            .Include(i => i.SupplierPrices).ThenInclude(p => p.Supplier)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(i => i.Name.Contains(search) || i.ArticleNumber.Contains(search));
        if (category is { } cat) query = query.Where(i => i.CategoryId == cat);
        if (allergen is { } al) query = query.Where(i => i.Allergens.Any(a => a.AllergenId == al));
        if (active is { } isActive) query = query.Where(i => i.Active == isActive);

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(i => i.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<IngredientDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<IngredientDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var ingredient = await db.Ingredients
            .Include(i => i.Category).Include(i => i.Supplier)
            .Include(i => i.Allergens).ThenInclude(a => a.Allergen)
            .Include(i => i.Additives)
            .Include(i => i.SupplierPrices).ThenInclude(p => p.Supplier)
            .FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(Ingredient), id);
        return ToDto(ingredient);
    }

    public async Task<IngredientDto> CreateAsync(SaveIngredientDto dto, CancellationToken ct = default)
    {
        var ingredient = new Ingredient
        {
            Id = Guid.NewGuid(),
            TenantId = tenantContext.TenantId!.Value,
            CategoryId = dto.CategoryId,
            SupplierId = dto.SupplierId,
            Name = dto.Name.Trim(),
            ArticleNumber = dto.ArticleNumber.Trim(),
            BaseUnit = dto.BaseUnit,
            PurchaseUnit = dto.PurchaseUnit,
            ConversionFactor = dto.ConversionFactor,
            PurchasePrice = dto.PurchasePrice,
            Vegetarian = dto.Vegetarian,
            Vegan = dto.Vegan,
            Bio = dto.Bio,
            Regional = dto.Regional,
            Active = true,
            Source = IngredientSource.Manuell,
            Nutrition = ToEntityNutrition(dto.Nutrition),
        };
        db.Ingredients.Add(ingredient);
        foreach (var allergenId in dto.AllergenIds.Distinct())
            db.IngredientAllergens.Add(new IngredientAllergen { IngredientId = ingredient.Id, AllergenId = allergenId });
        foreach (var text in dto.Additives.Where(t => !string.IsNullOrWhiteSpace(t)))
            db.IngredientAdditives.Add(new IngredientAdditive { Id = Guid.NewGuid(), IngredientId = ingredient.Id, Text = text, CreatedAt = DateTime.UtcNow });

        await SaveOrConflictAsync(ct);
        return await GetByIdAsync(ingredient.Id, ct);
    }

    public async Task<IngredientDto> UpdateAsync(Guid id, SaveIngredientDto dto, CancellationToken ct = default)
    {
        var ingredient = await db.Ingredients.Include(i => i.Allergens).Include(i => i.Additives)
            .FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(Ingredient), id);

        ingredient.CategoryId = dto.CategoryId;
        ingredient.SupplierId = dto.SupplierId;
        ingredient.Name = dto.Name.Trim();
        ingredient.ArticleNumber = dto.ArticleNumber.Trim();
        ingredient.BaseUnit = dto.BaseUnit;
        ingredient.PurchaseUnit = dto.PurchaseUnit;
        ingredient.ConversionFactor = dto.ConversionFactor;
        ingredient.PurchasePrice = dto.PurchasePrice;
        ingredient.Vegetarian = dto.Vegetarian;
        ingredient.Vegan = dto.Vegan;
        ingredient.Bio = dto.Bio;
        ingredient.Regional = dto.Regional;
        ingredient.Nutrition = ToEntityNutrition(dto.Nutrition);
        // A human just edited this ingredient — protect it from a future Rezeptrechner sync ever
        // overwriting these changes again, regardless of where the row originally came from.
        ingredient.IsManuallyEdited = true;
        ingredient.UpdatedAt = DateTime.UtcNow;

        db.IngredientAllergens.RemoveRange(ingredient.Allergens);
        foreach (var allergenId in dto.AllergenIds.Distinct())
            db.IngredientAllergens.Add(new IngredientAllergen { IngredientId = ingredient.Id, AllergenId = allergenId });

        db.IngredientAdditives.RemoveRange(ingredient.Additives);
        foreach (var text in dto.Additives.Where(t => !string.IsNullOrWhiteSpace(t)))
            db.IngredientAdditives.Add(new IngredientAdditive { Id = Guid.NewGuid(), IngredientId = ingredient.Id, Text = text, CreatedAt = DateTime.UtcNow });

        await SaveOrConflictAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException(nameof(Ingredient), id);
        ingredient.Active = false;
        ingredient.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task SaveOrConflictAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_Ingredients_TenantId_ArticleNumber") == true)
        {
            throw new ConflictException("Artikelnummer bereits vergeben.");
        }
    }

    // ---- Rezeptrechner sync (never overwrites a manually-edited ingredient) ----

    public async Task<SyncResultDto> SyncAsync(List<RezeptrechnerImportRowDto> rows, CancellationToken ct = default)
    {
        var tenantId = tenantContext.TenantId!.Value;
        var externalRefIds = rows.Select(r => r.ExternalRefId).ToList();
        var existing = await db.Ingredients
            .Where(i => i.ExternalRefId != null && externalRefIds.Contains(i.ExternalRefId))
            .ToDictionaryAsync(i => i.ExternalRefId!, ct);

        var categories = await db.IngredientCategories.ToListAsync(ct);
        var fallbackCategoryId = categories.FirstOrDefault()?.Id
            ?? throw new ValidationException("Keine Zutatenkategorie vorhanden — bitte zuerst mindestens eine Kategorie anlegen.");

        var result = new SyncResultDto();
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            var categoryId = categories.FirstOrDefault(c => string.Equals(c.Name, row.CategoryName, StringComparison.OrdinalIgnoreCase))?.Id ?? fallbackCategoryId;

            if (existing.TryGetValue(row.ExternalRefId, out var ingredient))
            {
                if (ingredient.IsManuallyEdited)
                {
                    result.SkippedManuallyEdited++;
                    continue;
                }

                ingredient.Name = row.Name.Trim();
                ingredient.ArticleNumber = row.ArticleNumber.Trim();
                ingredient.CategoryId = categoryId;
                ingredient.BaseUnit = row.BaseUnit;
                ingredient.PurchaseUnit = row.PurchaseUnit;
                ingredient.ConversionFactor = row.ConversionFactor;
                ingredient.PurchasePrice = row.PurchasePrice;
                if (row.Nutrition is { } n) ingredient.Nutrition = ToEntityNutrition(n);
                ingredient.LastSyncedAt = now;
                ingredient.UpdatedAt = now;
                result.Updated++;
            }
            else
            {
                db.Ingredients.Add(new Ingredient
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    CategoryId = categoryId,
                    Name = row.Name.Trim(),
                    ArticleNumber = row.ArticleNumber.Trim(),
                    BaseUnit = row.BaseUnit,
                    PurchaseUnit = row.PurchaseUnit,
                    ConversionFactor = row.ConversionFactor <= 0 ? 1 : row.ConversionFactor,
                    PurchasePrice = row.PurchasePrice,
                    Active = true,
                    Source = IngredientSource.Rezeptrechner,
                    ExternalRefId = row.ExternalRefId,
                    IsManuallyEdited = false,
                    LastSyncedAt = now,
                    CreatedAt = now,
                    Nutrition = row.Nutrition is { } newNutrition ? ToEntityNutrition(newNutrition) : new IngredientNutrition(),
                });
                result.Added++;
            }
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Applies nutrition matched client-side against an external dataset (currently the
    /// Bundeslebensmittelschlüssel) to already-existing ingredients, identified directly by
    /// IngredientId rather than the Rezeptrechner's ExternalRefId sync key — this is a separate
    /// source from the Rezeptrechner import, so it needs its own match path. Never touches a
    /// manually-edited ingredient, same protection as SyncAsync.</summary>
    public async Task<ApplyNutritionResultDto> ApplyExternalNutritionAsync(List<ApplyIngredientNutritionRowDto> rows, NutritionSource source, CancellationToken ct = default)
    {
        if (!await featureFlags.IsEnabledAsync(tenantContext.TenantId!.Value, "naehrwert-api", ct))
            throw new ForbiddenException("Die Nährwert-API ist für Ihren Mandanten nicht aktiviert.");

        var ids = rows.Select(r => r.IngredientId).ToList();
        var ingredients = await db.Ingredients.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);

        var result = new ApplyNutritionResultDto();
        var now = DateTime.UtcNow;
        foreach (var row in rows)
        {
            if (!ingredients.TryGetValue(row.IngredientId, out var ingredient)) { result.SkippedNotFound++; continue; }
            if (ingredient.IsManuallyEdited) { result.SkippedManuallyEdited++; continue; }

            ingredient.Nutrition = ToEntityNutrition(row.Nutrition);
            ingredient.Nutrition.Source = source;
            ingredient.LastSyncedAt = now;
            ingredient.UpdatedAt = now;
            result.Applied++;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    // ---- Supplier prices ----

    public async Task<List<IngredientSupplierPriceDto>> ListPricesAsync(Guid ingredientId, CancellationToken ct = default)
    {
        await EnsureIngredientExistsAsync(ingredientId, ct);
        var prices = await db.IngredientSupplierPrices.Include(p => p.Supplier)
            .Where(p => p.IngredientId == ingredientId).OrderBy(p => p.Price).ToListAsync(ct);
        return prices.Select(ToPriceDto).ToList();
    }

    public async Task<IngredientSupplierPriceDto> AddPriceAsync(Guid ingredientId, SaveIngredientSupplierPriceDto dto, CancellationToken ct = default)
    {
        await EnsureIngredientExistsAsync(ingredientId, ct);
        var price = new IngredientSupplierPrice
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredientId,
            SupplierId = dto.SupplierId,
            SupplierArticleNumber = dto.SupplierArticleNumber.Trim(),
            Price = dto.Price,
            Unit = dto.Unit,
            AvailabilityNote = dto.AvailabilityNote,
            CreatedAt = DateTime.UtcNow,
        };
        db.IngredientSupplierPrices.Add(price);
        await db.SaveChangesAsync(ct);
        return await LoadPriceDtoAsync(price.Id, ct);
    }

    public async Task<IngredientSupplierPriceDto> UpdatePriceAsync(Guid ingredientId, Guid priceId, SaveIngredientSupplierPriceDto dto, CancellationToken ct = default)
    {
        var price = await db.IngredientSupplierPrices.FirstOrDefaultAsync(p => p.Id == priceId && p.IngredientId == ingredientId, ct)
            ?? throw new NotFoundException(nameof(IngredientSupplierPrice), priceId);
        price.SupplierId = dto.SupplierId;
        price.SupplierArticleNumber = dto.SupplierArticleNumber.Trim();
        price.Price = dto.Price;
        price.Unit = dto.Unit;
        price.AvailabilityNote = dto.AvailabilityNote;
        price.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await LoadPriceDtoAsync(price.Id, ct);
    }

    public async Task DeletePriceAsync(Guid ingredientId, Guid priceId, CancellationToken ct = default)
    {
        var price = await db.IngredientSupplierPrices.FirstOrDefaultAsync(p => p.Id == priceId && p.IngredientId == ingredientId, ct)
            ?? throw new NotFoundException(nameof(IngredientSupplierPrice), priceId);
        db.IngredientSupplierPrices.Remove(price);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureIngredientExistsAsync(Guid ingredientId, CancellationToken ct)
    {
        var exists = await db.Ingredients.AnyAsync(i => i.Id == ingredientId, ct);
        if (!exists) throw new NotFoundException(nameof(Ingredient), ingredientId);
    }

    private async Task<IngredientSupplierPriceDto> LoadPriceDtoAsync(Guid priceId, CancellationToken ct) =>
        ToPriceDto(await db.IngredientSupplierPrices.Include(p => p.Supplier).FirstAsync(p => p.Id == priceId, ct));

    private static IngredientSupplierPriceDto ToPriceDto(IngredientSupplierPrice p) => new()
    {
        Id = p.Id,
        IngredientId = p.IngredientId,
        SupplierId = p.SupplierId,
        SupplierName = p.Supplier?.Name ?? string.Empty,
        SupplierArticleNumber = p.SupplierArticleNumber,
        Price = p.Price,
        Unit = p.Unit.ToString(),
        AvailabilityNote = p.AvailabilityNote,
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt,
    };

    private static IngredientDto ToDto(Ingredient i)
    {
        var cheapest = i.SupplierPrices.OrderBy(p => p.Price).FirstOrDefault();
        return new IngredientDto
        {
            Id = i.Id,
            Name = i.Name,
            ArticleNumber = i.ArticleNumber,
            CategoryId = i.CategoryId,
            CategoryName = i.Category?.Name ?? string.Empty,
            SupplierId = i.SupplierId,
            SupplierName = i.Supplier?.Name,
            BaseUnit = i.BaseUnit.ToString(),
            PurchaseUnit = i.PurchaseUnit,
            ConversionFactor = i.ConversionFactor,
            PurchasePrice = i.PurchasePrice,
            Vegetarian = i.Vegetarian,
            Vegan = i.Vegan,
            Bio = i.Bio,
            Regional = i.Regional,
            Active = i.Active,
            Source = i.Source.ToString(),
            ExternalRefId = i.ExternalRefId,
            IsManuallyEdited = i.IsManuallyEdited,
            LastSyncedAt = i.LastSyncedAt,
            SupplierPrices = i.SupplierPrices.OrderBy(p => p.Price).Select(ToPriceDto).ToList(),
            CheapestSupplierPriceId = cheapest?.Id,
            CheapestSupplierName = cheapest?.Supplier?.Name,
            CheapestPrice = cheapest?.Price,
            Nutrition = new NutritionDto
            {
                Kcal = i.Nutrition.Kcal, Kj = i.Nutrition.Kj, ProteinG = i.Nutrition.ProteinG,
                FatG = i.Nutrition.FatG, SaturatedFatG = i.Nutrition.SaturatedFatG,
                CarbsG = i.Nutrition.CarbsG, SugarG = i.Nutrition.SugarG, FiberG = i.Nutrition.FiberG,
                SaltG = i.Nutrition.SaltG, AlcoholG = i.Nutrition.AlcoholG,
                Source = i.Nutrition.Source.ToString(),
            },
            AllergenNames = i.Allergens.Select(a => a.Allergen?.Name ?? string.Empty).Where(n => n != string.Empty).ToArray(),
            AllergenIds = i.Allergens.Select(a => a.AllergenId).ToArray(),
            Additives = i.Additives.Select(a => a.Text).ToArray(),
        };
    }

    private static IngredientNutrition ToEntityNutrition(NutritionDto dto) => new()
    {
        Kcal = dto.Kcal, Kj = dto.Kj, ProteinG = dto.ProteinG, FatG = dto.FatG, SaturatedFatG = dto.SaturatedFatG,
        CarbsG = dto.CarbsG, SugarG = dto.SugarG, FiberG = dto.FiberG, SaltG = dto.SaltG, AlcoholG = dto.AlcoholG,
        Source = NutritionSource.Manuell,
    };
}
