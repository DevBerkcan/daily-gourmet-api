using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Auth;

public class LoginRequestDto
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public CurrentUserDto User { get; set; } = null!;
}

public class CurrentUserDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool ActiveSupportSession { get; set; }
}
