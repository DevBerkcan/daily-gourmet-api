using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Procurement;
using DailyGourmet.Api.Models.DTOs.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

/// <summary>Read-only Einkauf-Übersicht über bestätigte Bestellungen — Kalenderwoche → Rezepte →
/// Zutatenbedarf. Ergänzt (statt ersetzt) den bestehenden ProcurementList-Workflow unter
/// /api/procurement-lists, der weiterhin für die eigentliche, lieferantenweise Bestellung zuständig
/// bleibt.</summary>
[ApiController]
[Route("api/procurement/overview")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class ProcurementOverviewController(ProcurementOverviewHandler handler) : ControllerBase
{
    [HttpGet("weeks")]
    public async Task<ActionResult<ApiResponse<List<ProcurementWeekDto>>>> ListWeeks([FromQuery] Guid? locationId, CancellationToken ct) =>
        Ok(ApiResponse<List<ProcurementWeekDto>>.Ok(await handler.ListWeeksAsync(locationId, ct)));

    [HttpGet("weeks/{year:int}/{calendarWeek:int}/recipes")]
    public async Task<ActionResult<ApiResponse<List<WeekRecipeRequirementDto>>>> WeekRecipes(int year, int calendarWeek, [FromQuery] Guid? locationId, CancellationToken ct) =>
        Ok(ApiResponse<List<WeekRecipeRequirementDto>>.Ok(await handler.WeekRecipesAsync(year, calendarWeek, locationId, ct)));

    [HttpGet("weeks/{year:int}/{calendarWeek:int}/ingredients")]
    public async Task<ActionResult<ApiResponse<List<IngredientRequirementDto>>>> WeekIngredients(int year, int calendarWeek, [FromQuery] Guid? locationId, CancellationToken ct) =>
        Ok(ApiResponse<List<IngredientRequirementDto>>.Ok(await handler.WeekIngredientsAsync(year, calendarWeek, locationId, ct)));
}
