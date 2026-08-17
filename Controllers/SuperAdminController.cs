using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.SuperAdmin;
using DailyGourmet.Api.Models.DTOs.Tenants;
using DailyGourmet.Api.Models.DTOs.Users;
using DailyGourmet.Api.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/super-admin")]
[Authorize(Roles = "SUPER_ADMIN")]
public class SuperAdminController(SuperAdminHandler handler, AuditLogHandler auditLogHandler) : ControllerBase
{
    [HttpGet("audit-logs")]
    public async Task<ActionResult<ApiResponse<PagedResult<GlobalAuditLogDto>>>> AuditLogs(
        [FromQuery] Guid? tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<GlobalAuditLogDto>>.Ok(await auditLogHandler.ListForSuperAdminAsync(tenantId, page, pageSize, ct)));


    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<SuperAdminDashboardDto>>> Dashboard(CancellationToken ct) =>
        Ok(ApiResponse<SuperAdminDashboardDto>.Ok(await handler.DashboardAsync(ct)));

    [HttpGet("system")]
    public async Task<ActionResult<ApiResponse<SystemStatusDto>>> System(CancellationToken ct) =>
        Ok(ApiResponse<SystemStatusDto>.Ok(await handler.SystemStatusAsync(ct)));

    [HttpGet("tenants")]
    public async Task<ActionResult<ApiResponse<PagedResult<TenantDto>>>> ListTenants([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<TenantDto>>.Ok(await handler.ListTenantsAsync(page, pageSize, ct)));

    [HttpPost("tenants")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> CreateTenant([FromBody] CreateTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.CreateTenantAsync(dto, ct)));

    [HttpGet("tenants/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> GetTenant(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.GetTenantByIdAsync(id, ct)));

    [HttpPut("tenants/{id:guid}")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> UpdateTenant(Guid id, [FromBody] UpdateTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.UpdateTenantAsync(id, dto, ct)));

    [HttpPost("tenants/{id:guid}/lock")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Lock(Guid id, [FromBody] LockTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.ChangeTenantStatusAsync(id, TenantStatus.GESPERRT, "Mandant gesperrt", dto, ct)));

    [HttpPost("tenants/{id:guid}/unlock")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Unlock(Guid id, [FromBody] LockTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.ChangeTenantStatusAsync(id, TenantStatus.AKTIV, "Mandant reaktiviert", dto, ct)));

    [HttpPost("tenants/{id:guid}/archive")]
    public async Task<ActionResult<ApiResponse<TenantDto>>> Archive(Guid id, [FromBody] LockTenantDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantDto>.Ok(await handler.ChangeTenantStatusAsync(id, TenantStatus.ARCHIVIERT, "Mandant archiviert", dto, ct)));

    [HttpGet("tenants/{id:guid}/users")]
    public async Task<ActionResult<ApiResponse<List<UserDto>>>> TenantUsers(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<List<UserDto>>.Ok(await handler.TenantUsersAsync(id, ct)));

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GlobalUsers(
        [FromQuery] Guid? tenantId, [FromQuery] string? role, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<UserDto>>.Ok(await handler.GlobalUsersAsync(tenantId, role, status, page, pageSize, ct)));

    [HttpGet("feature-flags")]
    public async Task<ActionResult<ApiResponse<List<FeatureFlagDto>>>> ListFeatureFlags(CancellationToken ct) =>
        Ok(ApiResponse<List<FeatureFlagDto>>.Ok(await handler.ListFeatureFlagsAsync(ct)));

    [HttpPut("feature-flags/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FeatureFlagDto>>> UpdateFeatureFlag(Guid id, [FromBody] UpdateFeatureFlagDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FeatureFlagDto>.Ok(await handler.UpdateFeatureFlagAsync(id, dto, ct)));

    [HttpPut("tenants/{id:guid}/feature-flags")]
    public async Task<ActionResult<ApiResponse>> SetTenantFeatureFlag(Guid id, [FromBody] SetTenantFeatureFlagDto dto, CancellationToken ct)
    {
        await handler.SetTenantFeatureFlagAsync(id, dto, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("locations")]
    public async Task<ActionResult<ApiResponse<List<LocationSummaryDto>>>> AllLocations(CancellationToken ct) =>
        Ok(ApiResponse<List<LocationSummaryDto>>.Ok(await handler.AllLocationsAsync(ct)));
}
