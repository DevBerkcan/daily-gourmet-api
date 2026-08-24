using System.ComponentModel.DataAnnotations;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.DTOs.Recipes;

public class RecipeIngredientDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

/// <summary>Nutrition per 100 g of the finished recipe (matching the frontend's existing
/// `naehrwertePro100g` contract, and Recipe.Nutrition's own semantics) — only populated when the
/// recipe carries authoritative imported values. There is no live ingredient-based computation yet:
/// that needs a cross-unit (g/kg/ml/l/Stück) conversion layer that doesn't exist anywhere in the
/// codebase today, which is out of scope here — this simply passes the authoritative value through
/// when present. Scale by Recipe.PortionWeightG/100 for a per-portion figure (see
/// RecipeHandler.ScaleNutritionToPortion, used by the label PDF).</summary>
public class RecipeNutritionDto
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

public class RecipeDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? RecipeNumber { get; set; }
    public int StandardPortions { get; set; }
    public decimal? PortionWeightG { get; set; }
    public int PrepTimeMinutes { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool GlutenFree { get; set; }
    public bool LactoseFree { get; set; }
    public bool DgeCertified { get; set; }
    /// <summary>Computed, not stored — see RecipeHandler.ComputeEstimatedCostPerPortion. Null when
    /// no ingredient price (supplier or standard) is available yet to base an estimate on.</summary>
    public decimal? EstimatedCostPerPortion { get; set; }
    public RecipeNutritionDto? Nutrition { get; set; }
    public string? ProductionNotes { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? CoreTemperatureC { get; set; }
    public string? StorageNote { get; set; }
    public string? ShelfLifeAfterPrep { get; set; }
    public bool Active { get; set; }
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public string[] PrepSteps { get; set; } = [];
    public List<RecipeIngredientDto> Ingredients { get; set; } = [];
    public string[] ResolvedAllergens { get; set; } = [];
    public bool AllergensAreOverridden { get; set; }
    public string[] ResolvedAdditives { get; set; } = [];
    public bool AdditivesAreOverridden { get; set; }
    public string? NutriScore { get; set; }
    public bool NutritionIsAuthoritative { get; set; }
    public Guid[] TargetGroupIds { get; set; } = [];
    public string[] TargetGroupNames { get; set; } = [];
}

public class SaveRecipeIngredientDto
{
    [Required] public Guid IngredientId { get; set; }
    [Range(0.001, double.MaxValue)] public decimal Quantity { get; set; }
    [Required] public Unit Unit { get; set; }
}

public class SaveRecipeDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    [Required] public Guid CategoryId { get; set; }
    public string? RecipeNumber { get; set; }
    [Range(1, 100000)] public int StandardPortions { get; set; }
    public decimal? PortionWeightG { get; set; }
    [Range(0, 1000)] public int PrepTimeMinutes { get; set; }
    [Required] public Difficulty Difficulty { get; set; }
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool GlutenFree { get; set; }
    public bool LactoseFree { get; set; }
    public bool DgeCertified { get; set; }
    public bool Active { get; set; } = true;
    public string? ProductionNotes { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? CoreTemperatureC { get; set; }
    public string? StorageNote { get; set; }
    public string? ShelfLifeAfterPrep { get; set; }
    public string[] PrepSteps { get; set; } = [];
    public List<SaveRecipeIngredientDto> Ingredients { get; set; } = [];
    public Guid[] TargetGroupIds { get; set; } = [];
}

public class RecipeScaleIngredientDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal OriginalQuantity { get; set; }
    public decimal ScaledQuantity { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public class RecipeScaleResultDto
{
    public decimal Factor { get; set; }
    public List<RecipeScaleIngredientDto> Ingredients { get; set; } = [];
}

public class RecipeImportWarningDto
{
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Result of RecipeHandler.ImportFromRezeptrechnerAsync — the recipe-side counterpart to
/// Ingredients.SyncResultDto, plus the ingredients that import implicitly syncs along the way.</summary>
public class RecipeImportResultDto
{
    public int RecipesAdded { get; set; }
    public int RecipesUpdated { get; set; }
    public int IngredientsAdded { get; set; }
    public int IngredientsUpdated { get; set; }
    public int IngredientsSkippedManuallyEdited { get; set; }
    public List<RecipeImportWarningDto> Warnings { get; set; } = [];
}
