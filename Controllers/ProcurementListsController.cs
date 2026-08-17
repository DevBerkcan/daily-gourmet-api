using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/procurement-lists")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
public class ProcurementListsController(ProcurementListHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ProcurementListDto>>>> List(
        [FromQuery] Guid? locationId, [FromQuery] int? calendarWeek, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(locationId, calendarWeek, status, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<ProcurementListDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ProcurementListDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<ProcurementListDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<ProcurementListDto>>> Generate([FromBody] GenerateProcurementListDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProcurementListDto>.Ok(await handler.GenerateAsync(dto, ct)));

    [HttpPut("{id:guid}/items/{itemId:guid}")]
    public async Task<ActionResult<ApiResponse<ProcurementListDto>>> UpdateItem(Guid id, Guid itemId, [FromBody] UpdateProcurementItemDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProcurementListDto>.Ok(await handler.UpdateItemAsync(id, itemId, dto, ct)));

    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<ApiResponse<ProcurementListDto>>> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto, CancellationToken ct) =>
        Ok(ApiResponse<ProcurementListDto>.Ok(await handler.UpdateStatusAsync(id, dto, ct)));

    [HttpGet("{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var bytes = await handler.ExportCsvAsync(id, ct);
        return File(bytes, "text/csv", $"einkauf-{id}.csv");
    }
}
