using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Users;
using DailyGourmet.Api.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(UserManagementHandler handler) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN,FACILITY_USER")]
    public async Task<ActionResult<ApiResponse<PagedResult<UserDto>>>> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default) =>
        Ok(ApiResponse<PagedResult<UserDto>>.Ok(await handler.ListAsync(page, pageSize, ct)));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(Guid id, CancellationToken ct) =>
        Ok(ApiResponse<UserDto>.Ok(await handler.GetByIdAsync(id, ct)));

    [HttpPost]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Invite([FromBody] InviteUserDto dto, CancellationToken ct) =>
        Ok(ApiResponse<UserDto>.Ok(await handler.InviteAsync(dto, ct)));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN,FACILITY_ADMIN")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken ct) =>
        Ok(ApiResponse<UserDto>.Ok(await handler.UpdateAsync(id, dto, ct)));

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> Deactivate(Guid id, CancellationToken ct)
    {
        await handler.SetStatusAsync(id, UserStatus.DEAKTIVIERT, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> Activate(Guid id, CancellationToken ct)
    {
        await handler.SetStatusAsync(id, UserStatus.AKTIV, ct);
        return Ok(ApiResponse.Ok());
    }

    [HttpPost("{id:guid}/resend-invitation")]
    [Authorize(Roles = "TENANT_OWNER,TENANT_ADMIN")]
    public async Task<ActionResult<ApiResponse>> ResendInvitation(Guid id, CancellationToken ct)
    {
        await handler.ResendInvitationAsync(id, ct);
        return Ok(ApiResponse.Ok());
    }
}
