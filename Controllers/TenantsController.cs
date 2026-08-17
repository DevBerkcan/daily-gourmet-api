using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/tenants/current")]
[Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
public class TenantsController(TenantHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Get(CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.GetCurrentAsync(ct)));

    [HttpPut]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Update([FromBody] UpdateTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.UpdateCurrentAsync(dto, ct)));

    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<TenantProfileDto>>> GetProfile(CancellationToken ct) =>
        Ok(ApiResponse<TenantProfileDto>.Ok(await handler.GetProfileAsync(ct)));

    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<TenantProfileDto>>> UpdateProfile([FromBody] TenantProfileDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantProfileDto>.Ok(await handler.UpdateProfileAsync(dto, ct)));

    [HttpGet("settings")]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> GetSettings(CancellationToken ct) =>
        Ok(ApiResponse<TenantSettingsDto>.Ok(await handler.GetSettingsAsync(ct)));

    [HttpPut("settings")]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> UpdateSettings([FromBody] TenantSettingsDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantSettingsDto>.Ok(await handler.UpdateSettingsAsync(dto, ct)));
}
