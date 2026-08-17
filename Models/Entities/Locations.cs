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

    /// <summary>CSV of active weekdays, e.g. "Mo,Di,Mi,Do,Fr".</summary>
    public string ActiveWeekdays { get; set; } = "Mo,Di,Mi,Do,Fr";

    public decimal PortionPrice { get; set; }
    public FacilityStatus Status { get; set; } = FacilityStatus.AKTIV;
    public string? Notes { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
