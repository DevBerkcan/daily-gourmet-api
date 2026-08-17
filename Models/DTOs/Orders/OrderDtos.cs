using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Orders;

public class OrderItemDto
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public int Portions { get; set; }
    public string? Note { get; set; }
}

public class OrderDto
{
    public Guid Id { get; set; }
    public Guid FacilityId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public Guid MealPlanId { get; set; }
    public int CalendarWeek { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedAt { get; set; }
    public DateTime DeadlineAtUtc { get; set; }
    public List<OrderItemDto> Items { get; set; } = [];
}

public class SaveOrderItemDto
{
    [Required] public DateOnly Date { get; set; }
    [Required] public Guid RecipeId { get; set; }
    [Range(0, int.MaxValue)] public int Portions { get; set; }
    public string? Note { get; set; }
}

public class SaveOrderDto
{
    public Guid? FacilityId { get; set; }
    [Required] public Guid MealPlanId { get; set; }
    public List<SaveOrderItemDto> Items { get; set; } = [];
    public bool Submit { get; set; }
}

public class OverrideOrderDto
{
    [Required, MinLength(1)] public string Reason { get; set; } = string.Empty;
}

public class AuditLogEntryDto
{
    public Guid Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
