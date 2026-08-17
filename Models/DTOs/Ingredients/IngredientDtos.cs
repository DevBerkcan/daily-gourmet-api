using System.ComponentModel.DataAnnotations;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.DTOs.Ingredients;

public class NutritionDto
{
    public decimal Kcal { get; set; }
    public decimal ProteinG { get; set; }
    public decimal FatG { get; set; }
    public decimal CarbsG { get; set; }
    public decimal SugarG { get; set; }
    public decimal SaltG { get; set; }
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
