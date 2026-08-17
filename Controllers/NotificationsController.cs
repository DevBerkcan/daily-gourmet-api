using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(NotificationHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<NotificationDto>>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<NotificationDto>>.Ok(await handler.ListAsync(page, pageSize, ct)));

    [HttpPost("{id:guid}/read")]
    public async Task<ActionResult<ApiResponse>> MarkRead(Guid id, CancellationToken ct)
    {
        await handler.MarkReadAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
