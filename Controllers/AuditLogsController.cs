using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class AuditLogsController(AuditLogHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogDto>>>> List(
        [FromQuery] Guid? userId, [FromQuery] string? action, [FromQuery] string? entity, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
    {
        var result = await handler.ListAsync(userId, action, entity, from, to, page, pageSize, ct);
        return Ok(ApiResponse<PagedResult<AuditLogDto>>.Ok(result));
    }
}
