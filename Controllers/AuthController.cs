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
}
