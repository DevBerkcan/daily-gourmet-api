using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/quality-controls")]
[Authorize]
public class QualityControlsController(QualityControlHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<QualityControlDto>>>> List(
        [FromQuery] Guid? productionPlanId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(productionPlanId, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<QualityControlDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Roles = "KITCHEN_MANAGER,KITCHEN_STAFF,TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<QualityControlDto>>> Create([FromBody] CreateQualityControlDto dto, CancellationToken ct) =>
        Ok(ApiResponse<QualityControlDto>.Ok(await handler.CreateAsync(dto, ct)));
}
