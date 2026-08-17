using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Production;

public class ProductionPlanItemDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int OrderedQuantity { get; set; }
    public int AdjustmentQuantity { get; set; }
    public string? AdjustmentReason { get; set; }
    public string Status { get; set; } = string.Empty;
    public string WorkStatus { get; set; } = string.Empty;
    public int? StagedQuantity { get; set; }
    public string? Workstation { get; set; }
    public string? Equipment { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? FinishByTime { get; set; }
    public int? BatchCount { get; set; }
    public int? PortionsPerBatch { get; set; }
    public string? ResponsiblePerson { get; set; }
}

public class ProductionPlanDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public List<ProductionPlanItemDto> Items { get; set; } = [];
}

public class CreateProductionPlanDto
{
    [Required] public DateOnly Date { get; set; }
    [Required] public Guid LocationId { get; set; }
}

public class UpdateProductionPlanItemDto
{
    public string? Status { get; set; }
    public string? WorkStatus { get; set; }
    public int? StagedQuantity { get; set; }
    public string? Workstation { get; set; }
    public string? Equipment { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? FinishByTime { get; set; }
    public int? BatchCount { get; set; }
    public int? PortionsPerBatch { get; set; }
    public string? ResponsiblePerson { get; set; }
}

public class ProductionAdjustmentRequestDto
{
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class IngredientRequirementDto
{
    public Guid IngredientId { get; set; }
    public string IngredientName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public string? StorageLocationName { get; set; }
    public string[] ContributingRecipeNames { get; set; } = [];
}

public class DeviationDto
{
    public Guid Id { get; set; }
    public Guid? ProductionPlanId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ReportedByUserName { get; set; } = string.Empty;
    public DateTime ReportedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ResolvedAt { get; set; }
}

public class CreateDeviationDto
{
    public Guid? ProductionPlanId { get; set; }
    [Required] public string Category { get; set; } = string.Empty;
    [Required] public string Subject { get; set; } = string.Empty;
    public string? Quantity { get; set; }
    [Required] public string Action { get; set; } = string.Empty;
}

public class QualityControlDto
{
    public Guid Id { get; set; }
    public Guid? ProductionPlanId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string TargetValue { get; set; } = string.Empty;
    public string MeasuredValue { get; set; } = string.Empty;
    public string PerformedByUserName { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class CreateQualityControlDto
{
    public Guid? ProductionPlanId { get; set; }
    [Required] public string Type { get; set; } = string.Empty;
    [Required] public string Area { get; set; } = string.Empty;
    [Required] public string TargetValue { get; set; } = string.Empty;
    [Required] public string MeasuredValue { get; set; } = string.Empty;
    [Required] public string Status { get; set; } = string.Empty;
}

public class StorageLocationDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SaveStorageLocationDto
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
}
