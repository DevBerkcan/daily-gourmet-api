using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

public class MealPlan : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public int CalendarWeek { get; set; }
    public int Year { get; set; }
    public MealPlanStatus Status { get; set; } = MealPlanStatus.DRAFT;

    /// <summary>Set by MealPlanHandler.RejectAsync (REVIEW → DRAFT) so the creator sees why; cleared
    /// again by SubmitReviewAsync once they resubmit, so it never lingers past the fix.</summary>
    public string? RejectionReason { get; set; }

    /// <summary>Marks this as one of up to 8 reusable base weeks ("Vorlage 1-8") rather than a
    /// live week — duplicate-into-week works the same for templates and ordinary past weeks, this
    /// flag just changes how it's listed/picked in the UI.</summary>
    public bool IsTemplate { get; set; }
    /// <summary>1-8 when IsTemplate is true, unique per tenant; null otherwise.</summary>
    public int? TemplateSlot { get; set; }

    /// <summary>The facilities this plan is shared with — empty only when IsTemplate is true, since a
    /// template is facility-neutral until someone uses it. A plan can serve several facilities at
    /// once with identical dishes (see MealPlanFacility); a facility that needs its own divergent
    /// version instead goes through MealPlanHandler.MarkAsTemplateAsync/DuplicateAsync, which always
    /// produces an independent copy rather than mutating the shared plan.</summary>
    public ICollection<MealPlanFacility> Facilities { get; set; } = new List<MealPlanFacility>();

    public ICollection<MealPlanLocation> Locations { get; set; } = new List<MealPlanLocation>();
    public ICollection<MealPlanDay> Days { get; set; } = new List<MealPlanDay>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

/// <summary>Denormalizes TenantId/Year/CalendarWeek from the parent MealPlan (set once at creation,
/// never changed afterward) so the DB can enforce "one plan per facility per calendar week" with a
/// unique index directly on this junction table — a plain composite PK on (MealPlanId, FacilityId)
/// can't express that, since the same facility could otherwise appear on two different plans for the
/// same week.</summary>
public class MealPlanFacility : ITenantScoped
{
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;
    public Guid TenantId { get; set; }
    public int Year { get; set; }
    public int CalendarWeek { get; set; }
}

public class MealPlanLocation
{
    public Guid MealPlanId { get; set; }
    public MealPlan MealPlan { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;
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

    /// <summary>Which parallel diet track this dish belongs to for the day. A day/line can hold
    /// more than one item (e.g. a shared dessert alongside the main dish) since Items is already a
    /// collection — no separate hierarchy needed.</summary>
    public DietLine DietLine { get; set; } = DietLine.NORMALKOST;

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
