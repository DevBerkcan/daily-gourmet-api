using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/deviations")]
[Authorize]
public class DeviationsController(DeviationHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<DeviationDto>>>> List(
        [FromQuery] Guid? productionPlanId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(productionPlanId, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<DeviationDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "KITCHEN_MANAGER,KITCHEN_STAFF,TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DeviationDto>>> Create([FromBody] CreateDeviationDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DeviationDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPost("{id:guid}/resolve")]
    [Authorize(Roles = "KITCHEN_MANAGER,TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DeviationDto>>> Resolve(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<DeviationDto>.Ok(await handler.ResolveAsync(id, ct)));
}
