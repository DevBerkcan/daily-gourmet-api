using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/suppliers")]
[Authorize]
public class SuppliersController(SupplierHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupplierDto>>>> List([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(search, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<SupplierDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);
        return Ok(ApiResponse<SupplierDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] SaveSupplierDto dto, CancellationToken ct)
    {
        var result = await handler.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<SupplierDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<SupplierDto>>> Update(Guid id, [FromBody] SaveSupplierDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<SupplierDto>.Ok(result));
    }
}
