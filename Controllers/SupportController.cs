using DailyGourmet.Api.Handlers;
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
}

[ApiController]
[Route("api/super-admin")]
[Authorize(Roles = "SUPER_ADMIN")]
public class SupportSessionsController(SupportSessionHandler handler) : ControllerBase
{
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
