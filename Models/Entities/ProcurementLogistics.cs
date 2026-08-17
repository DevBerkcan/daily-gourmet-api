using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

public class ProcurementList : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Label { get; set; } = null!;
    public int CalendarWeek { get; set; }
    public ProcurementListStatus Status { get; set; } = ProcurementListStatus.DRAFT;

    public ICollection<ProcurementListItem> Items { get; set; } = new List<ProcurementListItem>();
}

public class ProcurementListItem : BaseEntity
{
    public Guid ProcurementListId { get; set; }
    public ProcurementList ProcurementList { get; set; } = null!;
    public Guid IngredientId { get; set; }
    public Ingredient Ingredient { get; set; } = null!;

    public Unit Unit { get; set; }
    public decimal TotalQuantityBase { get; set; }
    public decimal PurchaseQuantity { get; set; }
}

public class DeliveryRoute : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid DriverId { get; set; }
    public Driver Driver { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public Location? Location { get; set; }

    public string Name { get; set; } = null!;
    public DateOnly Date { get; set; }
    public TimeSpan PlannedDepartureTime { get; set; }
    public TimeSpan? PlannedReturnTime { get; set; }
    public decimal? DistanceKm { get; set; }
    public RouteStatus Status { get; set; } = RouteStatus.GEPLANT;

    public ICollection<RouteStop> Stops { get; set; } = new List<RouteStop>();
}

public class RouteStop : BaseEntity
{
    public Guid RouteId { get; set; }
    public DeliveryRoute DeliveryRoute { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    public int SequenceNumber { get; set; }
    public TimeSpan PlannedArrivalTime { get; set; }
    public TimeSpan? DeliveryWindowStart { get; set; }
    public TimeSpan? DeliveryWindowEnd { get; set; }
    public string ContactName { get; set; } = null!;
    public string ContactPhone { get; set; } = null!;
    public string? Note { get; set; }
    public RouteStopStatus Status { get; set; } = RouteStopStatus.OFFEN;
    public string? ProblemNote { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public ICollection<RouteStopItem> Items { get; set; } = new List<RouteStopItem>();
}

public class RouteStopItem : BaseEntity
{
    public Guid RouteStopId { get; set; }
    public RouteStop RouteStop { get; set; } = null!;
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public Guid? OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int Portions { get; set; }
    public string ContainerDescription { get; set; } = null!;
    public string TemperatureRequirement { get; set; } = null!;
    public string? Note { get; set; }
    public bool IsPacked { get; set; }
    public DateTime? PackedAt { get; set; }
    public bool IsLoaded { get; set; }
    public DateTime? LoadedAt { get; set; }
}
