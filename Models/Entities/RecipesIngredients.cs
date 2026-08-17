using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

// ---- Global seeded lookup tables (industry/regulatory reference data, not tenant-scoped) ----

public class RecipeCategory : BaseEntity
{
    public string Name { get; set; } = null!;
}

public class IngredientCategory : BaseEntity
{
    public string Name { get; set; } = null!;
    public Guid? DefaultStorageLocationId { get; set; }
    public StorageLocation? DefaultStorageLocation { get; set; }
}

/// <summary>EU-14 mandatory allergens.</summary>
public class Allergen : BaseEntity
{
    public string Name { get; set; } = null!;
}

public class TargetAudienceGroup : BaseEntity
{
    public string Name { get; set; } = null!;
}

// ---- Suppliers ----

public class Supplier : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

// ---- Ingredients ----

public class Ingredient : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public IngredientCategory Category { get; set; } = null!;
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public string Name { get; set; } = null!;
    public string ArticleNumber { get; set; } = null!;
    public Unit BaseUnit { get; set; }
    public string PurchaseUnit { get; set; } = null!;
    public decimal ConversionFactor { get; set; }
    public decimal? PurchasePrice { get; set; }
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool Bio { get; set; }
    public bool Regional { get; set; }
    public bool Active { get; set; } = true;

    // Nutrition per 100 g/ml — EF owned type.
    public IngredientNutrition Nutrition { get; set; } = new();

    public ICollection<IngredientAllergen> Allergens { get; set; } = new List<IngredientAllergen>();
    public ICollection<IngredientAdditive> Additives { get; set; } = new List<IngredientAdditive>();
    public ICollection<RecipeIngredient> UsedInRecipes { get; set; } = new List<RecipeIngredient>();
}

public class IngredientNutrition
{
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FatG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal SugarG { get; set; }
    public decimal SaltG { get; set; }
    public NutritionSource Source { get; set; } = NutritionSource.Manuell;
}

public class IngredientAllergen
{
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public Guid AllergenId { get; set; }
    public Allergen Allergen { get; set; } = null!;
}

/// <summary>Free-text additive rows — the mock data is inconsistent free text, not a clean fixed set.</summary>
public class IngredientAdditive : BaseEntity
{
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public string Text { get; set; } = null!;
}

// ---- Recipes ----

public class Recipe : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid CategoryId { get; set; }
    public RecipeCategory Category { get; set; } = null!;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? RecipeNumber { get; set; }
    public int StandardPortions { get; set; }
    public decimal? PortionWeightG { get; set; }
    public int PrepTimeMinutes { get; set; }
    public Difficulty Difficulty { get; set; }
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public string? ProductionNotes { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? CoreTemperatureC { get; set; }
    public string? StorageNote { get; set; }
    public string? ShelfLifeAfterPrep { get; set; }
    public bool Active { get; set; } = true;
    public int Version { get; set; } = 1;

    /// <summary>Authoritative import values (nullable) — override live-computed values when present.</summary>
    public RecipeNutrition? Nutrition { get; set; }
    public NutriScore? NutriScore { get; set; }
    public string? NutriScoreCategory { get; set; }

    public ICollection<RecipePrepStep> PrepSteps { get; set; } = new List<RecipePrepStep>();
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeAllergenOverride> AllergenOverrides { get; set; } = new List<RecipeAllergenOverride>();
    public ICollection<RecipeAdditiveOverride> AdditiveOverrides { get; set; } = new List<RecipeAdditiveOverride>();
    public ICollection<RecipeNutritionClaim> NutritionClaims { get; set; } = new List<RecipeNutritionClaim>();
    public ICollection<RecipeTargetGroup> TargetGroups { get; set; } = new List<RecipeTargetGroup>();
}

public class RecipeNutrition
{
    public decimal Kcal { get; set; }
    public decimal Kj { get; set; }
    public decimal FatG { get; set; }
    public decimal SaturatedFatG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal SugarG { get; set; }
    public decimal FiberG { get; set; }
    public decimal ProteinG { get; set; }
    public decimal SaltG { get; set; }
    public decimal AlcoholG { get; set; }
}

public class RecipePrepStep : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int StepNumber { get; set; }
    public string Text { get; set; } = null!;
}

/// <summary>RezeptZutat — own surrogate key so duplicate ingredient rows are structurally allowed.</summary>
public class RecipeIngredient : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Unit Unit { get; set; }
}

public class RecipeAllergenOverride : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string Text { get; set; } = null!;
}

public class RecipeAdditiveOverride : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string Text { get; set; } = null!;
}

public class RecipeNutritionClaim : BaseEntity
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string Text { get; set; } = null!;
}

public class RecipeTargetGroup
{
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid TargetAudienceGroupId { get; set; }
    public TargetAudienceGroup TargetAudienceGroupEntity { get; set; } = null!;
}
