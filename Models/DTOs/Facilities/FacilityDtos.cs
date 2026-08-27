using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Facilities;

public class FacilityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CustomerNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactPerson { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Guid LocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public string ActiveWeekdays { get; set; } = string.Empty;
    public decimal PortionPrice { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? RouteNumber { get; set; }
    /// <summary>True only on the response to a Create call that actually sent a FACILITY_ADMIN
    /// invite for this facility's Email — lets the frontend show a richer confirmation message.</summary>
    public bool AdminInvited { get; set; }
}

public class CreateFacilityDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContactPerson { get; set; } = string.Empty;

    // Required: this email is also used to auto-invite the facility's FACILITY_ADMIN login
    // (see FacilityHandler.CreateAsync), so it can no longer be left blank.
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public Guid LocationId { get; set; }

    public string ActiveWeekdays { get; set; } = "Mo,Di,Mi,Do,Fr";

    [Range(0, 1000)]
    public decimal PortionPrice { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [MaxLength(20)]
    public string? RouteNumber { get; set; }
}

public class UpdateFacilityDto : CreateFacilityDto
{
    [Required]
    public string Status { get; set; } = "AKTIV";
}

public class FacilityClosureDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Note { get; set; }
    public bool AddedByAdmin { get; set; }
}

public class SaveFacilityClosureDto
{
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
}

/// <summary>Portal-Selbstbedienung: bewusst nur Kontaktdaten — Preise, Tour, Standort und Status
/// bleiben Verwaltungssache und sind hier nicht änderbar.</summary>
public class UpdatePortalFacilityDto
{
    [Required, MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Phone { get; set; } = string.Empty;
}

/// <summary>Preview of what a hard Facility delete would take with it — shown in the confirmation
/// dialog before DeleteAsync actually runs.</summary>
public class FacilityDeleteImpactDto
{
    public int OrderCount { get; set; }
    public int ClosureCount { get; set; }
    public int UserCount { get; set; }
    public int RouteStopCount { get; set; }
}
