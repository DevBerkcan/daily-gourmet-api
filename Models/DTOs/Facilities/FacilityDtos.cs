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
}

public class CreateFacilityDto
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Address { get; set; } = string.Empty;

    [MaxLength(200)]
    public string ContactPerson { get; set; } = string.Empty;

    [EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Phone { get; set; } = string.Empty;

    [Required]
    public Guid LocationId { get; set; }

    public string ActiveWeekdays { get; set; } = "Mo,Di,Mi,Do,Fr";

    [Range(0, 1000)]
    public decimal PortionPrice { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }
}

public class UpdateFacilityDto : CreateFacilityDto
{
    [Required]
    public string Status { get; set; } = "AKTIV";
}
