using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Models.Entities;

/// <summary>A kitchen / production site (Standort).</summary>
public class Location : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string ContactPerson { get; set; } = null!;
    public int CapacityPortions { get; set; }
    public LocationStatus Status { get; set; } = LocationStatus.AKTIV;

    public ICollection<Facility> Facilities { get; set; } = new List<Facility>();
}

/// <summary>A customer delivery site — school, clinic, etc. (Einrichtung).</summary>
public class Facility : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid LocationId { get; set; }
    public Location Location { get; set; } = null!;

    public string Name { get; set; } = null!;
    public string CustomerNumber { get; set; } = null!;
    public string Address { get; set; } = null!;
    public string ContactPerson { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Phone { get; set; } = null!;

    /// <summary>Overrides TenantSettings default when set.</summary>
    public int? OrderDeadlineOffsetDays { get; set; }
    public TimeSpan? OrderDeadlineTime { get; set; }
    /// <summary>Per-facility override of TenantSettings.SameDayAdjustmentDeadlineTime.</summary>
    public TimeSpan? SameDayAdjustmentDeadlineTime { get; set; }

    /// <summary>CSV of active weekdays, e.g. "Mo,Di,Mi,Do,Fr".</summary>
    public string ActiveWeekdays { get; set; } = "Mo,Di,Mi,Do,Fr";

    public decimal PortionPrice { get; set; }
    public FacilityStatus Status { get; set; } = FacilityStatus.AKTIV;
    public string? Notes { get; set; }

    /// <summary>Stable default tour, e.g. "RT1" (Nummernkreis, see TenantSettings) — this is what the
    /// production-plan print groups by, deliberately not that day's DeliveryRoute, since routes are
    /// driver-claimed (see DeliveryRoute.DriverId) and may not exist yet when the plan is printed.</summary>
    public string? RouteNumber { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
    public ICollection<FacilityClosure> Closures { get; set; } = new List<FacilityClosure>();
}

/// <summary>A facility-declared closure period (Schließtage/Abwesenheit) — e.g. summer break —
/// entered by the facility itself a year ahead, or by admin staff on the facility's behalf when
/// notified late. Blocks/flags meal-plan and order weeks that overlap it.</summary>
public class FacilityClosure : BaseEntity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = null!;
    public Guid FacilityId { get; set; }
    public Facility Facility { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Note { get; set; }
    /// <summary>Null when the facility entered it themselves; set when admin/Verwaltung added it on
    /// the facility's behalf (e.g. from a late-arriving email).</summary>
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
}
