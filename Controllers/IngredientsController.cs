using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/ingredients")]
[Authorize]
public class IngredientsController(IngredientHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<IngredientDto>>>> List(
        [FromQuery] string? search, [FromQuery] Guid? category, [FromQuery] Guid? allergen, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(search, category, allergen, active, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<IngredientDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);
        return Ok(ApiResponse<IngredientDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> Create([FromBody] SaveIngredientDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<IngredientDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> Update(Guid id, [FromBody] SaveIngredientDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<IngredientDto>.Ok(result));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse>> Deactivate(Guid id, CancellationToken ct)
    {
        await handler.DeactivateAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
