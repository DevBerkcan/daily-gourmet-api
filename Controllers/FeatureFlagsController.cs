using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/feature-flags")]
[Authorize]
public class FeatureFlagsController(FeatureFlagHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TenantFeatureFlagStatusDto>>>> List(CancellationToken ct) =>
        Ok(ApiResponse<List<TenantFeatureFlagStatusDto>>.Ok(await handler.ListForCurrentTenantAsync(ct)));
}
