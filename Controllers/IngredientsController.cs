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
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> Create([FromBody] SaveIngredientDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<IngredientDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<IngredientDto>>> Update(Guid id, [FromBody] SaveIngredientDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<IngredientDto>.Ok(result));
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> Deactivate(Guid id, CancellationToken ct)
    {
        await handler.DeactivateAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("sync")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<SyncResultDto>>> Sync([FromBody] List<RezeptrechnerImportRowDto> rows, CancellationToken ct)
    {
        var result = await handler.SyncAsync(rows, ct);
        return Ok(ApiResponse<SyncResultDto>.Ok(result));
    }

    [HttpGet("{id:guid}/prices")]
    public async Task<ActionResult<ApiResponse<List<IngredientSupplierPriceDto>>>> ListPrices(Guid id, CancellationToken ct)
    {
        var result = await handler.ListPricesAsync(id, ct);
        return Ok(ApiResponse<List<IngredientSupplierPriceDto>>.Ok(result));
    }

    [HttpPost("{id:guid}/prices")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<IngredientSupplierPriceDto>>> AddPrice(Guid id, [FromBody] SaveIngredientSupplierPriceDto dto, CancellationToken ct)
    {
        var result = await handler.AddPriceAsync(id, dto, ct);
        return Ok(ApiResponse<IngredientSupplierPriceDto>.Ok(result));
    }

    [HttpPut("{id:guid}/prices/{priceId:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<IngredientSupplierPriceDto>>> UpdatePrice(Guid id, Guid priceId, [FromBody] SaveIngredientSupplierPriceDto dto, CancellationToken ct)
    {
        var result = await handler.UpdatePriceAsync(id, priceId, dto, ct);
        return Ok(ApiResponse<IngredientSupplierPriceDto>.Ok(result));
    }

    [HttpDelete("{id:guid}/prices/{priceId:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> DeletePrice(Guid id, Guid priceId, CancellationToken ct)
    {
        await handler.DeletePriceAsync(id, priceId, ct);
        return Ok(ApiResponse.Ok());
    }
}
