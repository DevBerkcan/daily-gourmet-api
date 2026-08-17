using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController(DashboardHandler handler) : ControllerBase
{
    [HttpGet("admin-summary")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,KITCHEN_MANAGER")]
    public async Task<ActionResult<ApiResponse<AdminDashboardSummaryDto>>> AdminSummary(CancellationToken ct) =>
        Ok(ApiResponse<AdminDashboardSummaryDto>.Ok(await handler.AdminSummaryAsync(ct)));

    [HttpGet("portal-summary")]
    public async Task<ActionResult<ApiResponse<PortalDashboardSummaryDto>>> PortalSummary(CancellationToken ct) =>
        Ok(ApiResponse<PortalDashboardSummaryDto>.Ok(await handler.PortalSummaryAsync(ct)));
}

[ApiController]
[Route("api/revenue")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class RevenueController(DashboardHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<RevenueResponseDto>>> Get([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct) =>
        Ok(ApiResponse<RevenueResponseDto>.Ok(await handler.RevenueAsync(from, to, ct)));
}
