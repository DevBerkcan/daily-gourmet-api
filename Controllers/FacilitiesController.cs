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
public class FacilitiesController(FacilityHandler handler, FacilityClosureHandler closureHandler, ITenantContext tenantContext) : ControllerBase
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

    [HttpGet("{id:guid}/closures")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<List<FacilityClosureDto>>>> ListClosures(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<FacilityClosureDto>>.Ok(await closureHandler.ListAsync(id, ct)));

    [HttpPost("{id:guid}/closures")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<FacilityClosureDto>>> AddClosure(Guid id, [FromBody] SaveFacilityClosureDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FacilityClosureDto>.Ok(await closureHandler.CreateAsync(id, dto, ct)));

    [HttpDelete("{id:guid}/closures/{closureId:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse>> DeleteClosure(Guid id, Guid closureId, CancellationToken ct)
    {
        await closureHandler.DeleteAsync(id, closureId, ct);
        return Ok(ApiResponse.Ok());
    }
}

[ApiController]
[Route("api/portal/facility-closures")]
[Authorize(Roles = "FACILITY_ADMIN,FACILITY_USER")]
public class PortalFacilityClosuresController(FacilityClosureHandler closureHandler, ITenantContext tenantContext) : ControllerBase
{
    private Guid OwnFacilityId => tenantContext.FacilityId ?? throw new ForbiddenException("Kein Einrichtungskontext vorhanden.");

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FacilityClosureDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<FacilityClosureDto>>.Ok(await closureHandler.ListAsync(OwnFacilityId, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FacilityClosureDto>>> Add([FromBody] SaveFacilityClosureDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FacilityClosureDto>.Ok(await closureHandler.CreateAsync(OwnFacilityId, dto, ct)));

    [HttpDelete("{closureId:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid closureId, CancellationToken ct)
    {
        await closureHandler.DeleteAsync(OwnFacilityId, closureId, ct);
        return Ok(ApiResponse.Ok());
    }
}

[ApiController]
[Route("api/portal/facility")]
[Authorize(Roles = "FACILITY_ADMIN,FACILITY_USER")]
public class PortalFacilityController(FacilityHandler handler, ITenantContext tenantContext) : ControllerBase
{
    private Guid OwnFacilityId => tenantContext.FacilityId ?? throw new ForbiddenException("Kein Einrichtungskontext vorhanden.");

    [HttpPut]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> Update([FromBody] UpdatePortalFacilityDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FacilityDto>.Ok(await handler.UpdateOwnContactAsync(OwnFacilityId, dto, ct)));
}
