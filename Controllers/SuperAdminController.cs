using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Facilities;
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
public class SuperAdminController(SuperAdminHandler handler, AuditLogHandler auditLogHandler, TenantHandler tenantHandler, FacilityHandler facilityHandler) : ControllerBase
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

    /// <summary>Unternehmensprofil (Stammdaten, Branding) und Einstellungen eines Mandanten werden
    /// ausschließlich hier von Daily Gourmet gepflegt — nicht vom Mandanten selbst.</summary>
    [HttpGet("tenants/{id:guid}/profile")]
    public async Task<ActionResult<ApiResponse<TenantProfileDto>>> GetTenantProfile(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<TenantProfileDto>.Ok(await tenantHandler.GetProfileAsync(id, ct)));

    [HttpPut("tenants/{id:guid}/profile")]
    public async Task<ActionResult<ApiResponse<TenantProfileDto>>> UpdateTenantProfile(Guid id, [FromBody] TenantProfileDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantProfileDto>.Ok(await tenantHandler.UpdateProfileAsync(id, dto, ct)));

    [HttpGet("tenants/{id:guid}/settings")]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> GetTenantSettings(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<TenantSettingsDto>.Ok(await tenantHandler.GetSettingsAsync(id, ct)));

    [HttpPut("tenants/{id:guid}/settings")]
    public async Task<ActionResult<ApiResponse<TenantSettingsDto>>> UpdateTenantSettings(Guid id, [FromBody] TenantSettingsDto dto, CancellationToken ct) =>
        Ok(ApiResponse<TenantSettingsDto>.Ok(await tenantHandler.UpdateSettingsAsync(id, dto, ct)));

    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> GlobalUsers(
        [FromQuery] Guid? tenantId, [FromQuery] string? role, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<UserDto>>.Ok(await handler.GlobalUsersAsync(tenantId, role, status, page, pageSize, ct)));

    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<UserDto>>> CreateUser([FromBody] CreateUserDto dto, CancellationToken ct) =>
        Ok(ApiResponse<UserDto>.Ok(await handler.CreateUserAsync(dto, ct)));

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(Guid id, [FromBody] SuperAdminUpdateUserDto dto, CancellationToken ct) =>
        Ok(ApiResponse<UserDto>.Ok(await handler.UpdateUserAsync(id, dto, ct)));

    [HttpPost("users/{id:guid}/deactivate")]
    public async Task<ActionResult<ApiResponse>> DeactivateUser(Guid id, CancellationToken ct)
    {
        await handler.SetUserStatusAsync(id, UserStatus.DEAKTIVIERT, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("users/{id:guid}/activate")]
    public async Task<ActionResult<ApiResponse>> ActivateUser(Guid id, CancellationToken ct)
    {
        await handler.SetUserStatusAsync(id, UserStatus.AKTIV, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("users/{id:guid}/password-reset")]
    public async Task<ActionResult<ApiResponse>> ResetUserPassword(Guid id, CancellationToken ct)
    {
        await handler.TriggerPasswordResetAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("feature-flags")]
    public async Task<ActionResult<ApiResponse<List<FeatureFlagDto>>>> ListFeatureFlags([FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(ApiResponse<List<FeatureFlagDto>>.Ok(await handler.ListFeatureFlagsAsync(tenantId, ct)));

    [HttpPut("feature-flags/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FeatureFlagDto>>> UpdateFeatureFlag(Guid id, [FromBody] UpdateFeatureFlagDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FeatureFlagDto>.Ok(await handler.UpdateFeatureFlagAsync(id, dto, ct)));

    [HttpGet("feature-flags/adoption")]
    public async Task<ActionResult<ApiResponse<List<FeatureFlagAdoptionDto>>>> FeatureFlagAdoption(CancellationToken ct) =>
        Ok(ApiResponse<List<FeatureFlagAdoptionDto>>.Ok(await handler.FeatureFlagAdoptionAsync(ct)));

    [HttpPut("tenants/{id:guid}/feature-flags")]
    public async Task<ActionResult<ApiResponse>> SetTenantFeatureFlag(Guid id, [FromBody] SetTenantFeatureFlagDto dto, CancellationToken ct)
    {
        await handler.SetTenantFeatureFlagAsync(id, dto, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpGet("locations")]
    public async Task<ActionResult<ApiResponse<List<LocationSummaryDto>>>> AllLocations([FromQuery] Guid? tenantId, CancellationToken ct) =>
        Ok(ApiResponse<List<LocationSummaryDto>>.Ok(await handler.AllLocationsAsync(tenantId, ct)));

    [HttpGet("tenants/{tenantId:guid}/facilities")]
    public async Task<ActionResult<ApiResponse<PagedResult<FacilityDto>>>> TenantFacilities(
        Guid tenantId, [FromQuery] int page = 1, [FromQuery] int pageSize = 100, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<FacilityDto>>.Ok(await facilityHandler.ListAsync(null, null, tenantId, page, pageSize, ct)));

    [HttpPost("tenants/{tenantId:guid}/facilities")]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> CreateTenantFacility(Guid tenantId, [FromBody] CreateFacilityDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FacilityDto>.Ok(await facilityHandler.CreateAsync(tenantId, dto, ct)));

    [HttpPut("tenants/{tenantId:guid}/facilities/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FacilityDto>>> UpdateTenantFacility(Guid tenantId, Guid id, [FromBody] UpdateFacilityDto dto, CancellationToken ct) =>
        Ok(ApiResponse<FacilityDto>.Ok(await facilityHandler.UpdateAsync(id, dto, ct)));

    [HttpGet("tenants/{tenantId:guid}/facilities/{id:guid}/delete-impact")]
    public async Task<ActionResult<ApiResponse<FacilityDeleteImpactDto>>> TenantFacilityDeleteImpact(Guid tenantId, Guid id, CancellationToken ct) =>
        Ok(ApiResponse<FacilityDeleteImpactDto>.Ok(await facilityHandler.GetDeleteImpactAsync(id, ct)));

    [HttpDelete("tenants/{tenantId:guid}/facilities/{id:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteTenantFacility(Guid tenantId, Guid id, CancellationToken ct)
    {
        await facilityHandler.DeleteAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
