using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.MealPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/meal-plans")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class MealPlansController(MealPlanHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<MealPlanDto>>>> List(
        [FromQuery] int? year, [FromQuery] int? calendarWeek, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(year, calendarWeek, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<MealPlanDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Create([FromBody] CreateMealPlanDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<MealPlanDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Update(Guid id, [FromBody] UpdateMealPlanDto dto, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.UpdateAsync(id, dto, ct)));

    [HttpPost("{id:guid}/duplicate")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Duplicate(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.DuplicateAsync(id, ct)));

    [HttpPost("{id:guid}/submit-review")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> SubmitReview(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.SubmitReviewAsync(id, ct)));

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Publish(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.PublishAsync(id, ct)));

    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Unpublish(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.UnpublishAsync(id, ct)));

    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Archive(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.ArchiveAsync(id, ct)));

    [HttpGet("{id:guid}/preview")]
    public async Task<ActionResult<ApiResponse<MealPlanDto>>> Preview(Guid id, [FromQuery] Guid? facilityId, CancellationToken ct) =>
        Ok(ApiResponse<MealPlanDto>.Ok(await handler.PreviewAsync(id, facilityId, ct)));
}

[ApiController]
[Route("api/portal/meal-plans")]
[Authorize]
public class PortalMealPlansController(MealPlanHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<MealPlanDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<MealPlanDto>>.Ok(await handler.PortalListAsync(ct)));
}
