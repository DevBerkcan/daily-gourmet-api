using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/locations")]
[Authorize]
public class LocationsController(LocationHandler handler) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER,KITCHEN_STAFF")]
    public async Task<ActionResult<ApiResponse<PagedResult<LocationDto>>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<LocationDto>>.Ok(await handler.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER,KITCHEN_STAFF")]
    public async Task<ActionResult<ApiResponse<LocationDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<LocationDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<LocationDto>>> Create([FromBody] SaveLocationDto dto, CancellationToken ct) =>
        Ok(ApiResponse<LocationDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<LocationDto>>> Update(Guid id, [FromBody] UpdateLocationDto dto, CancellationToken ct) =>
        Ok(ApiResponse<LocationDto>.Ok(await handler.UpdateAsync(id, dto, ct)));
}
