using System.ComponentModel.DataAnnotations;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.DTOs.Ingredients;

public class NutritionDto
{
    public decimal Kcal { get; set; }
    public decimal Kj { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FatG { get; set; }
    public decimal SaturatedFatG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal SugarG { get; set; }
    public decimal FiberG { get; set; }
    public decimal SaltG { get; set; }
    public decimal AlcoholG { get; set; }
    public string Source { get; set; } = "Manuell";
}

public class IngredientDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ArticleNumber { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string BaseUnit { get; set; } = string.Empty;
    public string PurchaseUnit { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; }
    public decimal? PurchasePrice { get; set; }
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool Bio { get; set; }
    public bool Regional { get; set; }
    public bool Active { get; set; }
    public NutritionDto Nutrition { get; set; } = new();
    public string[] AllergenNames { get; set; } = [];
    public Guid[] AllergenIds { get; set; } = [];
    public string[] Additives { get; set; } = [];

    public string Source { get; set; } = "Manuell";
    public string? ExternalRefId { get; set; }
    public bool IsManuallyEdited { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public List<IngredientSupplierPriceDto> SupplierPrices { get; set; } = [];
    public Guid? CheapestSupplierPriceId { get; set; }
    public string? CheapestSupplierName { get; set; }
    public decimal? CheapestPrice { get; set; }
}

public class IngredientSupplierPriceDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string SupplierArticleNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string? AvailabilityNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveIngredientSupplierPriceDto
{
    [Required] public Guid SupplierId { get; set; }
    [Required, MaxLength(100)] public string SupplierArticleNumber { get; set; } = string.Empty;
    [Range(0, double.MaxValue)] public decimal Price { get; set; }
    [Required] public Unit Unit { get; set; }
    public string? AvailabilityNote { get; set; }
}

/// <summary>One row from the Rezeptrechner export. Shape is a best guess pending a real sample
/// from the customer — see BACKEND_IMPLEMENTATION_PLAN.md / the Phase 1 plan note on this.</summary>
public class RezeptrechnerImportRowDto
{
    [Required] public string ExternalRefId { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string ArticleNumber { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public Unit BaseUnit { get; set; }
    public string PurchaseUnit { get; set; } = string.Empty;
    public decimal ConversionFactor { get; set; } = 1;
    public decimal? PurchasePrice { get; set; }
    public NutritionDto? Nutrition { get; set; }
}

public class SyncResultDto
{
    public int Added { get; set; }
    public int Updated { get; set; }
    public int SkippedManuallyEdited { get; set; }
}

/// <summary>One matched row for IngredientHandler.ApplyExternalNutritionAsync — an external source
/// (e.g. the Bundeslebensmittelschlüssel) supplying real nutrition for an already-existing ingredient
/// identified by its own IngredientId, matched client-side by name against the external dataset.</summary>
public class ApplyIngredientNutritionRowDto
{
    [Required] public Guid IngredientId { get; set; }
    [Required] public NutritionDto Nutrition { get; set; } = new();
}

public class ApplyNutritionResultDto
{
    public int Applied { get; set; }
    public int SkippedManuallyEdited { get; set; }
    public int SkippedNotFound { get; set; }
}

public class UnmatchedRowDto
{
    public int RowNumber { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ImportResultDto
{
    public int Matched { get; set; }
    public List<UnmatchedRowDto> Unmatched { get; set; } = [];
}

public class SaveIngredientDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string ArticleNumber { get; set; } = string.Empty;
    [Required] public Guid CategoryId { get; set; }
    public Guid? SupplierId { get; set; }
    [Required] public Unit BaseUnit { get; set; }
    [Required, MaxLength(100)] public string PurchaseUnit { get; set; } = string.Empty;
    [Range(0.0001, double.MaxValue)] public decimal ConversionFactor { get; set; } = 1;
    [Range(0, double.MaxValue)] public decimal? PurchasePrice { get; set; }
    public bool Vegetarian { get; set; }
    public bool Vegan { get; set; }
    public bool Bio { get; set; }
    public bool Regional { get; set; }
    public NutritionDto Nutrition { get; set; } = new();
    public Guid[] AllergenIds { get; set; } = [];
    public string[] Additives { get; set; } = [];
}

public class SupplierDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class SaveSupplierDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}

public class LookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}
