using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Logistics;
using DailyGourmet.Api.Models.DTOs.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/routes")]
[Authorize]
public class RoutesController(DeliveryRouteHandler handler) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,DRIVER")]
    public async Task<ActionResult<ApiResponse<PagedResult<DeliveryRouteDto>>>> List(
        [FromQuery] DateOnly? date, [FromQuery] Guid? driverId, [FromQuery] string? status, [FromQuery] bool? unassigned,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(date, driverId, status, unassigned, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<DeliveryRouteDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,DRIVER")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> Create([FromBody] CreateRouteDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.UpdateStatusAsync(id, dto, ct)));

    [HttpPut("{routeId:guid}/stops/{stopId:guid}/status")]
    [Authorize(Roles = "DRIVER")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> UpdateStopStatus(Guid routeId, Guid stopId, [FromBody] UpdateStopStatusDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.UpdateStopStatusAsync(routeId, stopId, dto, ct)));

    [HttpPut("{routeId:guid}/stops/{stopId:guid}/items/{itemId:guid}/packed")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> SetPacked(Guid routeId, Guid stopId, Guid itemId, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.SetItemPackedAsync(routeId, stopId, itemId, ct)));

    [HttpPut("{routeId:guid}/stops/{stopId:guid}/items/{itemId:guid}/loaded")]
    [Authorize(Roles = "DRIVER")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> SetLoaded(Guid routeId, Guid stopId, Guid itemId, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.SetItemLoadedAsync(routeId, stopId, itemId, ct)));

    [HttpPost("{id:guid}/claim")]
    [Authorize(Roles = "DRIVER")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> Claim(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.ClaimAsync(id, ct)));

    [HttpPut("{id:guid}/handoff")]
    [Authorize(Roles = "DRIVER")]
    public async Task<ActionResult<ApiResponse<DeliveryRouteDto>>> Handoff(Guid id, [FromBody] ConfirmHandoffDto dto, CancellationToken ct) =>
        Ok(ApiResponse<DeliveryRouteDto>.Ok(await handler.ConfirmHandoffAsync(id, dto, ct)));
}

[ApiController]
[Route("api/drivers/current")]
[Authorize(Roles = "DRIVER")]
public class CurrentDriverController(DeliveryRouteHandler handler) : ControllerBase
{
    [HttpGet("routes")]
    public async Task<ActionResult<ApiResponse<List<DeliveryRouteDto>>>> Routes(CancellationToken ct) =>
        Ok(ApiResponse<List<DeliveryRouteDto>>.Ok(await handler.CurrentDriverRoutesAsync(false, ct)));

    [HttpGet("routes/today")]
    public async Task<ActionResult<ApiResponse<List<DeliveryRouteDto>>>> RoutesToday(CancellationToken ct) =>
        Ok(ApiResponse<List<DeliveryRouteDto>>.Ok(await handler.CurrentDriverRoutesAsync(true, ct)));
}
