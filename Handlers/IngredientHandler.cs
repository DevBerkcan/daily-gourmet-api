using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using DailyGourmet.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class IngredientHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<PagedResult<IngredientDto>> ListAsync(string? search, Guid? category, Guid? allergen, bool? active, int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Ingredients
            .Include(i => i.Category).Include(i => i.Supplier)
            .Include(i => i.Allergens).ThenInclude(a => a.Allergen)
            .Include(i => i.Additives)
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
            Nutrition = new IngredientNutrition
            {
                Kcal = dto.Nutrition.Kcal, ProteinG = dto.Nutrition.ProteinG, FatG = dto.Nutrition.FatG,
                CarbsG = dto.Nutrition.CarbsG, SugarG = dto.Nutrition.SugarG, SaltG = dto.Nutrition.SaltG,
            },
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
        ingredient.Nutrition = new IngredientNutrition
        {
            Kcal = dto.Nutrition.Kcal, ProteinG = dto.Nutrition.ProteinG, FatG = dto.Nutrition.FatG,
            CarbsG = dto.Nutrition.CarbsG, SugarG = dto.Nutrition.SugarG, SaltG = dto.Nutrition.SaltG,
        };
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

    private static IngredientDto ToDto(Ingredient i) => new()
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
        Nutrition = new NutritionDto
        {
            Kcal = i.Nutrition.Kcal, ProteinG = i.Nutrition.ProteinG, FatG = i.Nutrition.FatG,
            CarbsG = i.Nutrition.CarbsG, SugarG = i.Nutrition.SugarG, SaltG = i.Nutrition.SaltG,
            Source = i.Nutrition.Source.ToString(),
        },
        AllergenNames = i.Allergens.Select(a => a.Allergen?.Name ?? string.Empty).Where(n => n != string.Empty).ToArray(),
        AllergenIds = i.Allergens.Select(a => a.AllergenId).ToArray(),
        Additives = i.Additives.Select(a => a.Text).ToArray(),
    };
}
