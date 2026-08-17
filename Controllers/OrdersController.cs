using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/orders")]
[Authorize]
public class OrdersController(OrderHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<OrderDto>>>> List(
        [FromQuery] Guid? facilityId, [FromQuery] Guid? mealPlanId, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(facilityId, mealPlanId, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<OrderDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Save([FromBody] SaveOrderDto dto, CancellationToken ct)
    {
        var result = await handler.SaveAsync(dto, ct);
        return Ok(ApiResponse<OrderDto>.Ok(result));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Update(Guid id, [FromBody] SaveOrderDto dto, CancellationToken ct)
    {
        var result = await handler.SaveAsync(dto, ct);
        return Ok(ApiResponse<OrderDto>.Ok(result));
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Submit(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await handler.SubmitAsync(id, ct)));

    [HttpPost("{id:guid}/confirm")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Confirm(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await handler.ConfirmAsync(id, ct)));

    [HttpPost("{id:guid}/lock")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Lock(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await handler.LockAsync(id, ct)));

    [HttpPost("{id:guid}/override")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse<OrderDto>>> Override(Guid id, [FromBody] OverrideOrderDto dto, CancellationToken ct) =>
        Ok(ApiResponse<OrderDto>.Ok(await handler.OverrideAsync(id, dto, ct)));

    [HttpGet("{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<List<AuditLogEntryDto>>>> History(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<AuditLogEntryDto>>.Ok(await handler.HistoryAsync(id, ct)));
}
