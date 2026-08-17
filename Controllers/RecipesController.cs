using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Recipes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/recipes")]
[Authorize]
public class RecipesController(RecipeHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<RecipeDto>>>> List(
        [FromQuery] string? search, [FromQuery] Guid? category, [FromQuery] bool? active,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(search, category, active, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<RecipeDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Create([FromBody] SaveRecipeDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Update(Guid id, [FromBody] SaveRecipeDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost("{id:guid}/duplicate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<RecipeDto>>> Duplicate(Guid id, CancellationToken ct)
    {
        var result = await handler.DuplicateAsync(id, ct);
        return Ok(ApiResponse<RecipeDto>.Ok(result));
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse>> Archive(Guid id, CancellationToken ct)
    {
        await handler.ArchiveAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("{id:guid}/scale")]
    public async Task<ActionResult<ApiResponse<RecipeScaleResultDto>>> Scale(Guid id, [FromQuery] int portions, CancellationToken ct)
    {
        var result = await handler.ScaleAsync(id, portions, ct);
        return Ok(ApiResponse<RecipeScaleResultDto>.Ok(result));
    }
}
