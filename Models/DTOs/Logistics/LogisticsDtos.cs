using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Logistics;

public class RouteStopItemDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int Portions { get; set; }
    public string ContainerDescription { get; set; } = string.Empty;
    public string TemperatureRequirement { get; set; } = string.Empty;
    public string? Note { get; set; }
    public bool IsPacked { get; set; }
    public DateTime? PackedAt { get; set; }
    public bool IsLoaded { get; set; }
    public DateTime? LoadedAt { get; set; }
}

public class RouteStopDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public TimeSpan PlannedArrivalTime { get; set; }
    public TimeSpan? DeliveryWindowStart { get; set; }
    public TimeSpan? DeliveryWindowEnd { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProblemNote { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public List<RouteStopItemDto> Items { get; set; } = [];
}

public class DeliveryRouteDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public Guid DriverId { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public Guid? LocationId { get; set; }
    public string? LocationName { get; set; }
    public TimeSpan PlannedDepartureTime { get; set; }
    public TimeSpan? PlannedReturnTime { get; set; }
    public decimal? DistanceKm { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<RouteStopDto> Stops { get; set; } = [];
}

public class CreateRouteDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required] public DateOnly Date { get; set; }
    [Required] public Guid DriverId { get; set; }
    public Guid? LocationId { get; set; }
    [Required] public TimeSpan PlannedDepartureTime { get; set; }
    [Required] public Guid[] FacilityIds { get; set; } = [];
}

public class UpdateStopStatusDto
{
    [Required] public string Status { get; set; } = string.Empty;
    public string? ProblemNote { get; set; }
}

public class DriverDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string VehicleDescription { get; set; } = string.Empty;
    public string LicensePlate { get; set; } = string.Empty;
}

public class SaveDriverDto
{
    [Required] public Guid UserId { get; set; }
    [Required] public string Phone { get; set; } = string.Empty;
    [Required] public string VehicleDescription { get; set; } = string.Empty;
    [Required] public string LicensePlate { get; set; } = string.Empty;
}
