using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Procurement;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/procurement-lists")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
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
    public async Task<IActionResult> Export(Guid id, [FromQuery] string format = "csv", CancellationToken ct = default)
    {
        if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            var pdfBytes = await handler.ExportPdfAsync(id, ct);
            return File(pdfBytes, "application/pdf", $"einkauf-{id}.pdf");
        }
        var bytes = await handler.ExportCsvAsync(id, ct);
        return File(bytes, "text/csv", $"einkauf-{id}.csv");
    }

    /// <summary>Token-authorized, no login required — see ProcurementListHandler.ApproveAsync for
    /// why this deliberately bypasses [Authorize].</summary>
    [HttpPost("{id:guid}/approve")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProcurementListDto>>> Approve(Guid id, [FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new ValidationException("Kein Freigabe-Token angegeben.");
        var result = await handler.ApproveAsync(id, token, ct);
        return Ok(ApiResponse<ProcurementListDto>.Ok(result));
    }
}
