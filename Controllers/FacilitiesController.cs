using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Facilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/facilities")]
[Authorize]
public class FacilitiesController(FacilityHandler handler, ITenantContext tenantContext) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<PagedResult<FacilityDto>>>> List(
        [FromQuery] string? search, [FromQuery] Guid? locationId,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(search, locationId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<FacilityDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await handler.GetByIdAsync(id, ct);
        return Ok(ApiResponse<FacilityDto>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> Create([FromBody] CreateFacilityDto dto, CancellationToken ct)
    {
        var tenantId = tenantContext.TenantId ?? throw new ValidationException("Kein Mandantenkontext vorhanden.");
        var result = await handler.CreateAsync(tenantId, dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<FacilityDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> Update(Guid id, [FromBody] UpdateFacilityDto dto, CancellationToken ct)
    {
        var result = await handler.UpdateAsync(id, dto, ct);
        return Ok(ApiResponse<FacilityDto>.Ok(result));
    }
}
