using System.ComponentModel.DataAnnotations;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.DTOs.Recipes;

public class RecipeIngredientDto
{
    /// <summary>The RecipeIngredient row's own id — not the ingredient's — so duplicate ingredient
    /// rows in one recipe (see comment on RecipeIngredient) still get a stable, unique key.</summary>
    public Guid Id { get; set; }
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
    public decimal ReductionFactor { get; set; } = 1;
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
    public string? NutriScoreCategory { get; set; }
    public bool NutritionIsAuthoritative { get; set; }
    public string[] NutritionClaims { get; set; } = [];
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
    [Range(0.01, 3)] public decimal ReductionFactor { get; set; } = 1;
    public string[] PrepSteps { get; set; } = [];
    public List<SaveRecipeIngredientDto> Ingredients { get; set; } = [];
    public Guid[] TargetGroupIds { get; set; } = [];
}

public class RecipeScaleIngredientDto
{
    public Guid Id { get; set; }
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

// ---- Nährwerte-Detailansicht (siehe RecipeHandler.GetNutritionDetailAsync) ----

/// <summary>Ein Zutaten-Zeile in der Nährwerte-Detailansicht, mit den Nährwerten dieser Zutat
/// hochgerechnet auf ihre Menge im Rezept. HasNutritionData ist false, solange für diese Zutat noch
/// keine echten Nährwerte hinterlegt sind (Rezeptrechner-Exporte liefern nur je Rezept, nicht je
/// Rohzutat — s. Kommentar an RecipeHandler.ImportFromRezeptrechnerAsync) — die Kcal-Spalten zeigen
/// dann 0, nicht weil die Zutat keine Kalorien hätte, sondern weil der Wert schlicht noch fehlt.</summary>
public class RecipeNutritionIngredientRowDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? WeightG { get; set; }
    public bool HasNutritionData { get; set; }
    public RecipeNutritionDto Nutrition { get; set; } = new();
}

/// <summary>Diabetiker-Austauscheinheiten — Standardformeln der deutschen Ernährungsberatung:
/// BE = Kohlenhydrate(g)/12, KE = Kohlenhydrate(g)/10, FPE = (Fett(g)×9 + Eiweiß(g)×4)/100.</summary>
public class DiabeticUnitsDto
{
    public decimal Be { get; set; }
    public decimal Ke { get; set; }
    public decimal Fpe { get; set; }
}

/// <summary>Eine nährwertbezogene Angabe (z. B. "Zuckerarm") mit dem tatsächlich gemessenen Wert und
/// der EU-Schwelle (Verordnung (EG) Nr. 1924/2006, Anhang), sofern die Angabe einer der bekannten
/// Regelclaims entspricht — sonst nur der Text ohne die drei weiteren Spalten (kein Rätselraten).</summary>
public class NutritionClaimEvaluationDto
{
    public string ClaimText { get; set; } = string.Empty;
    public string? MeasureLabel { get; set; }
    public string? MeasuredValue { get; set; }
    public string? Threshold { get; set; }
}

public class RecipeNutritionDetailDto
{
    public decimal RawWeightG { get; set; }
    public decimal ReductionFactor { get; set; }
    public decimal PreparedWeightG { get; set; }
    public int StandardPortions { get; set; }
    public decimal? PortionWeightG { get; set; }
    public List<RecipeNutritionIngredientRowDto> Ingredients { get; set; } = [];
    /// <summary>Autoritativ aus dem Rezeptimport, NICHT aus den Zutaten-Zeilen summiert — solange
    /// nur ein Teil der Zutaten eigene Nährwerte hat, wäre eine Summe systematisch zu niedrig. Siehe
    /// RecipeHandler.GetNutritionDetailAsync.</summary>
    public RecipeNutritionDto? PerRecipe { get; set; }
    public RecipeNutritionDto? PerPortion { get; set; }
    public RecipeNutritionDto? Per100g { get; set; }
    public DiabeticUnitsDto? DiabeticPerPortion { get; set; }
    public DiabeticUnitsDto? DiabeticPer100g { get; set; }
    public List<NutritionClaimEvaluationDto> ClaimEvaluations { get; set; } = [];
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
    public int IngredientsNutritionFromRecipeMatch { get; set; }
    public int AllergensFromListApplied { get; set; }
    public List<RecipeImportWarningDto> Warnings { get; set; } = [];
}
