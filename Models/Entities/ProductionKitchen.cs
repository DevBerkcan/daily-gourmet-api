using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

public class ProductionPlan : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public DateOnly Date { get; set; }

    public ICollection<ProductionPlanItem> Items { get; set; } = new List<ProductionPlanItem>();
    public ICollection<Deviation> Deviations { get; set; } = new List<Deviation>();
    public ICollection<QualityControl> QualityControls { get; set; } = new List<QualityControl>();
}

/// <summary>Produktionsposition — one recipe's planned/adjusted quantity for a plan/day/site.</summary>
public class ProductionPlanItem : BaseEntity
{
    public Guid ProductionPlanId { get; set; }
    public ProductionPlan ProductionPlan { get; set; } = null!;
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int OrderedQuantity { get; set; }
    public int AdjustmentQuantity { get; set; }
    public string? AdjustmentReason { get; set; }
    public ProductionItemStatus Status { get; set; } = ProductionItemStatus.PLANNED;

    /// <summary>Unified admin+kitchen work status (see BACKEND_IMPLEMENTATION_PLAN.md gap #13).</summary>
    public WorkStatus WorkStatus { get; set; } = WorkStatus.OFFEN;

    public int? StagedQuantity { get; set; }
    public string? Workstation { get; set; }
    public string? Equipment { get; set; }
    public TimeSpan? StartTime { get; set; }
    public TimeSpan? FinishByTime { get; set; }
    public int? BatchCount { get; set; }
    public int? PortionsPerBatch { get; set; }
    public string? ResponsiblePerson { get; set; }

    public ICollection<ProductionAdjustment> AdjustmentHistory { get; set; } = new List<ProductionAdjustment>();
}

/// <summary>Audit trail for AdjustmentQuantity changes.</summary>
public class ProductionAdjustment : BaseEntity
{
    public Guid ProductionPlanItemId { get; set; }
    public ProductionPlanItem ProductionPlanItem { get; set; } = null!;
    public int OldQuantity { get; set; }
    public int NewQuantity { get; set; }
    public string Reason { get; set; } = null!;
    public Guid ChangedByUserId { get; set; }
    public User ChangedByUser { get; set; } = null!;
    public DateTime ChangedAt { get; set; }
}

public class Deviation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? ProductionPlanId { get; set; }
    public ProductionPlan? ProductionPlan { get; set; }

    public DeviationCategory Category { get; set; }
    public string Subject { get; set; } = null!;
    public string? Quantity { get; set; }
    public string Action { get; set; } = null!;
    public Guid ReportedByUserId { get; set; }
    public User ReportedByUser { get; set; } = null!;
    public DateTime ReportedAt { get; set; }
    public DeviationStatus Status { get; set; } = DeviationStatus.OFFEN;
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public User? ResolvedByUser { get; set; }
}

/// <summary>HACCP check (Kontrolle).</summary>
public class QualityControl : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid? ProductionPlanId { get; set; }
    public ProductionPlan? ProductionPlan { get; set; }

    public ControlType Type { get; set; }
    public string Area { get; set; } = null!;
    public string TargetValue { get; set; } = null!;
    public string MeasuredValue { get; set; } = null!;
    public Guid PerformedByUserId { get; set; }
    public User PerformedByUser { get; set; } = null!;
    public DateTime PerformedAt { get; set; }
    public ControlStatus Status { get; set; }
}

public class StorageLocation : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public string Name { get; set; } = null!;
}
