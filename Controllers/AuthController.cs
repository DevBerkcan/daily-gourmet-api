using DailyGourmet.Api.Handlers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DailyGourmet.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthHandler handler) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponseDto>>> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await handler.LoginAsync(request.Email, request.Password, ct);
        return Ok(ApiResponse<LoginResponseDto>.Ok(result));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<CurrentUserDto>>> Me(CancellationToken ct)
    {
        var result = await handler.GetCurrentUserAsync(ct);
        return Ok(ApiResponse<CurrentUserDto>.Ok(result));
    }

    [HttpGet("invitations/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<InvitationDetailsDto>>> GetInvitation(string token, CancellationToken ct) =>
        Ok(ApiResponse<InvitationDetailsDto>.Ok(await handler.GetInvitationAsync(token, ct)));

    [HttpPost("invitations/{token}/accept")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse>> AcceptInvitation(string token, [FromBody] AcceptInvitationDto dto, CancellationToken ct)
    {
        await handler.AcceptInvitationAsync(token, dto.Password, ct);
        return Ok(ApiResponse.Ok());
    }
}
