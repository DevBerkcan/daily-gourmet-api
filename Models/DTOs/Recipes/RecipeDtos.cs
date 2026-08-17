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
    public string? ProductionNotes { get; set; }
    public string? ImageUrl { get; set; }
    public decimal? CoreTemperatureC { get; set; }
    public string? StorageNote { get; set; }
    public string? ShelfLifeAfterPrep { get; set; }
    public bool Active { get; set; }
    public int Version { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public string[] PrepSteps { get; set; } = [];
    public List<RecipeIngredientDto> Ingredients { get; set; } = [];
    public string[] ResolvedAllergens { get; set; } = [];
    public bool AllergensAreOverridden { get; set; }
    public string[] ResolvedAdditives { get; set; } = [];
    public bool AdditivesAreOverridden { get; set; }
    public string? NutriScore { get; set; }
    public bool NutritionIsAuthoritative { get; set; }
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
