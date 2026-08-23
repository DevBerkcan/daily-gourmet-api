using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/support/tickets")]
[Authorize]
public class SupportController(SupportTicketHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SupportTicketDto>>>> List([FromQuery] string? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<SupportTicketDto>>.Ok(await handler.ListAsync(status, page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SupportTicketDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<SupportTicketDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<SupportTicketDto>>> Create([FromBody] CreateSupportTicketDto dto, CancellationToken ct) =>
        Ok(ApiResponse<SupportTicketDto>.Ok(await handler.CreateAsync(dto, ct)));

    [HttpPost("{id:guid}/replies")]
    public async Task<ActionResult<ApiResponse<SupportTicketDto>>> AddReply(Guid id, [FromBody] AddReplyDto dto, CancellationToken ct) =>
        Ok(ApiResponse<SupportTicketDto>.Ok(await handler.AddReplyAsync(id, dto, ct)));

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "SUPER_ADMIN")]
    public async Task<ActionResult<ApiResponse<SupportTicketDto>>> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusDto dto, CancellationToken ct) =>
        Ok(ApiResponse<SupportTicketDto>.Ok(await handler.UpdateStatusAsync(id, dto, ct)));

    [HttpPost("{id:guid}/attachments")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ApiResponse<SupportTicketAttachmentDto>>> AddAttachment(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new ValidationException("Keine Datei übermittelt.");
        var result = await handler.AddAttachmentAsync(id, file, ct);
        return Ok(ApiResponse<SupportTicketAttachmentDto>.Ok(result));
    }

    [HttpGet("{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> GetAttachment(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var (content, contentType, fileName) = await handler.GetAttachmentAsync(id, attachmentId, ct);
        return File(content, contentType, fileName);
    }
}

[ApiController]
[Route("api/support/session")]
[Authorize]
public class TenantSupportSessionController(SupportSessionHandler handler) : ControllerBase
{
    [HttpGet("current")]
    public async Task<ActionResult<ApiResponse<SupportSessionDto?>>> Current(CancellationToken ct) =>
        Ok(ApiResponse<SupportSessionDto?>.Ok(await handler.GetCurrentForCallerTenantAsync(ct)));

    [HttpPost("end")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> End(CancellationToken ct)
    {
        await handler.EndCurrentForCallerTenantAsync(ct);
        return Ok(ApiResponse.Ok());
    }
}

[ApiController]
[Route("api/super-admin")]
[Authorize(Roles = "SUPER_ADMIN")]
public class SupportSessionsController(SupportSessionHandler handler) : ControllerBase
{
    [HttpGet("tenants/{tenantId:guid}/support-sessions/current")]
    public async Task<ActionResult<ApiResponse<SupportSessionDto?>>> Current(Guid tenantId, CancellationToken ct) =>
        Ok(ApiResponse<SupportSessionDto?>.Ok(await handler.GetCurrentForTenantAsync(tenantId, ct)));

    [HttpPost("tenants/{tenantId:guid}/support-sessions")]
    public async Task<ActionResult<ApiResponse<SupportSessionDto>>> Start(Guid tenantId, CancellationToken ct) =>
        Ok(ApiResponse<SupportSessionDto>.Ok(await handler.StartAsync(tenantId, ct)));

    [HttpDelete("support-sessions/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> End(Guid id, CancellationToken ct)
    {
        await handler.EndAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
