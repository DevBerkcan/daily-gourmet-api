using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Procurement;

public class ProcurementListItemDto
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string IngredientArticleNumber { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantityBase { get; set; }
    public decimal PurchaseQuantity { get; set; }
}

public class ProcurementListDto
{
    public Guid Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public int CalendarWeek { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public Guid? SupplierId { get; set; }
    public string? SupplierName { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<ProcurementListItemDto> Items { get; set; } = [];
}

public class GenerateProcurementListDto
{
    [Required] public Guid ProductionPlanId { get; set; }
    [Required] public Guid LocationId { get; set; }
    [Range(1, 53)] public int CalendarWeek { get; set; }
    [Required] public string Label { get; set; } = string.Empty;
}

public class UpdateProcurementItemDto
{
    [Range(0, double.MaxValue)] public decimal PurchaseQuantity { get; set; }
}

public class UpdateStatusDto
{
    [Required] public string Status { get; set; } = string.Empty;
}

/// <summary>One row of ProcurementOverviewHandler.ListWeeksAsync — a calendar week that has at least
/// one confirmed order, with just enough at-a-glance numbers to pick which week to drill into.</summary>
public class ProcurementWeekDto
{
    public int Year { get; set; }
    public int CalendarWeek { get; set; }
    public int ConfirmedOrderCount { get; set; }
    public int TotalPortions { get; set; }
}

/// <summary>One recipe's demand across every confirmed order in a given week — "wie viele
/// Einrichtungen/Portionen brauchen wir von diesem Gericht", the level between the week overview and
/// the full ingredient breakdown.</summary>
public class WeekRecipeRequirementDto
{
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int TotalPortions { get; set; }
    public int FacilityCount { get; set; }
}
