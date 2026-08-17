using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

public class MealPlan : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CalendarWeek { get; set; }
    public int Year { get; set; }
    public MealPlanStatus Status { get; set; } = MealPlanStatus.DRAFT;

    public ICollection<MealPlanLocation> Locations { get; set; } = new List<MealPlanLocation>();
    public ICollection<MealPlanFacility> Facilities { get; set; } = new List<MealPlanFacility>();
    public ICollection<MealPlanDay> Days { get; set; } = new List<MealPlanDay>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class MealPlanLocation
{
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
}

public class MealPlanFacility
{
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
}

/// <summary>SpeiseplanTag.</summary>
public class MealPlanDay : BaseEntity
{
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;

    public string Weekday { get; set; } = null!;
    public DateOnly Date { get; set; }
    public string? Note { get; set; }

    public ICollection<MealPlanItem> Items { get; set; } = new List<MealPlanItem>();
}

public class MealPlanItem : BaseEntity
{
    public Guid MealPlanDayId { get; set; }
    public MealPlanDay MealPlanDay { get; set; } = null!;
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    /// <summary>Populated at publish time with the recipe's name/nutrition/allergens frozen as of
    /// that moment, so later recipe edits can't retroactively change a published week.</summary>
    public string? RecipeSnapshotJson { get; set; }
}

public class Order : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;

    public OrderStatus Status { get; set; } = OrderStatus.DRAFT;
    public DateTime? SubmittedAt { get; set; }
    public DateTime DeadlineAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}

/// <summary>BestellPosition.</summary>
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public DateOnly Date { get; set; }
    public Guid RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public int Portions { get; set; }
    public string? Note { get; set; }
}
