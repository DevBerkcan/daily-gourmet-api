using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/driver-issues")]
[Authorize(Roles = "DRIVER")]
public class DriverIssuesController(DriverIssueHandler handler) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiResponse>> Report([FromBody] ReportDriverIssueDto dto, CancellationToken ct)
    {
        await handler.ReportAsync(dto.Message, ct);
        return Ok(ApiResponse.Ok());
    }
}
