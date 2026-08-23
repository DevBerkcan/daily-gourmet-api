using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/production-plans")]
[Authorize]
public class ProductionPlansController(ProductionPlanHandler handler, ProductionPlanPrintHandler printHandler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProductionPlanDto>>>> List(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] Guid? locationId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(from, to, locationId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ProductionPlanDto>>.Ok(result));
    }

    [HttpGet("{date}")]
    public async Task<ActionResult<ApiResponse<ProductionPlanDto>>> GetByDate(DateOnly date, [FromQuery] Guid locationId, CancellationToken ct) =>
        Ok(ApiResponse<ProductionPlanDto>.Ok(await handler.GetByDateAsync(date, locationId, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductionPlanDto>>> Create([FromBody] CreateProductionPlanDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProductionPlanDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPut("{planId:guid}/items/{itemId:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductionPlanDto>>> UpdateItem(Guid planId, Guid itemId, [FromBody] UpdateProductionPlanItemDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProductionPlanDto>.Ok(await handler.UpdateItemAsync(planId, itemId, dto, ct)));

    [HttpPost("{planId:guid}/items/{itemId:guid}/adjustments")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductionPlanDto>>> AddAdjustment(Guid planId, Guid itemId, [FromBody] ProductionAdjustmentRequestDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProductionPlanDto>.Ok(await handler.AddAdjustmentAsync(planId, itemId, dto, ct)));

    [HttpPost("{planId:guid}/refresh")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductionPlanDto>>> Refresh(Guid planId, CancellationToken ct) =>
        Ok(ApiResponse<ProductionPlanDto>.Ok(await handler.RefreshAsync(planId, ct)));

    [HttpGet("{planId:guid}/requirements")]
    public async Task<ActionResult<ApiResponse<List<IngredientRequirementDto>>>> Requirements(Guid planId, CancellationToken ct) =>
        Ok(ApiResponse<List<IngredientRequirementDto>>.Ok(await handler.RequirementsAsync(planId, ct)));

    /// <summary>Produktionsplan drucken — replaces the removed Küche module's screen with a
    /// server-rendered PDF matching Input/Produktionsplan Beispiel.pdf's layout exactly (one
    /// weekday per PDF, grouped by Tour). Keyed by MealPlan, not ProductionPlan, since it's built
    /// from meal-plan/order data joined by Facility.RouteNumber — see ProductionPlanPrintHandler.</summary>
    [HttpGet("print")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<IActionResult> Print([FromQuery] Guid mealPlanId, [FromQuery] DateOnly date, CancellationToken ct)
    {
        var bytes = await printHandler.RenderAsync(mealPlanId, date, ct);
        return File(bytes, "application/pdf", $"produktionsplan-{date:yyyy-MM-dd}.pdf");
    }
}
