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
    /// <summary>Set only when the white-label flag is enabled for this tenant AND a logo is
    /// configured — the frontend app shell shows this instead of the default Daily Gourmet logo.</summary>
    public string? LogoUrl { get; set; }
    /// <summary>True when this /auth/me response reflects an active impersonation token (see
    /// SupportSessionHandler.ImpersonateAsync) — every field above already describes the
    /// impersonated tenant, not the super admin's own account.</summary>
    public bool IsImpersonation { get; set; }
    public DateTime? ImpersonationExpiresAtUtc { get; set; }
}

/// <summary>Shown on the public "set your password" page before the user submits one — lets the
/// frontend greet them by name without requiring a login.</summary>
public class InvitationDetailsDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class AcceptInvitationDto
{
    [Required, MinLength(8, ErrorMessage = "Das Passwort muss mindestens 8 Zeichen lang sein.")]
    public string Password { get; set; } = string.Empty;
}
