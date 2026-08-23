using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.MealPlans;

public class MealPlanDayDto
{
    public Guid Id { get; set; }
    public string Weekday { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string? Note { get; set; }
    public List<MealPlanItemDto> Items { get; set; } = [];
}

public class MealPlanItemDto
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string RecipeName { get; set; } = string.Empty;
    public string DietLine { get; set; } = "NORMALKOST";
}

public class MealPlanDto
{
    public Guid Id { get; set; }
    public int CalendarWeek { get; set; }
    public int Year { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTemplate { get; set; }
    public int? TemplateSlot { get; set; }
    public Guid[] LocationIds { get; set; } = [];
    public Guid[] FacilityIds { get; set; } = [];
    public List<MealPlanDayDto> Days { get; set; } = [];
}

public class CreateMealPlanDto
{
    [Range(1, 53)] public int CalendarWeek { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }
    [Required] public Guid[] LocationIds { get; set; } = [];
    [Required] public Guid[] FacilityIds { get; set; } = [];
    public bool IsTemplate { get; set; }
    [Range(1, 8)] public int? TemplateSlot { get; set; }
}

public class UpdateMealPlanItemDto
{
    [Required] public Guid RecipeId { get; set; }
    public string DietLine { get; set; } = "NORMALKOST";
}

public class UpdateMealPlanDayDto
{
    [Required] public Guid DayId { get; set; }
    public List<UpdateMealPlanItemDto> Items { get; set; } = [];
    public string? Note { get; set; }
}

public class UpdateMealPlanDto
{
    public Guid[] LocationIds { get; set; } = [];
    public Guid[] FacilityIds { get; set; } = [];
    public List<UpdateMealPlanDayDto> Days { get; set; } = [];
}

public class DuplicateIntoWeekDto
{
    [Range(2000, 2100)] public int TargetYear { get; set; }
    [Range(1, 53)] public int TargetCalendarWeek { get; set; }
}
