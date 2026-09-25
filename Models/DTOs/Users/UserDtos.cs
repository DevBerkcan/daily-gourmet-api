using System.ComponentModel.DataAnnotations;

namespace DailyGourmet.Api.Models.DTOs.Users;

public class UserDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantName { get; set; }
    public Guid? FacilityId { get; set; }
    public string? FacilityName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public int FailedLoginCount { get; set; }
    /// <summary>Set only on the response to a call that just (re)created this invitation — the
    /// "set your password" link to copy and share manually (e.g. over chat), see InviteLinkDto.</summary>
    public string? InviteLink { get; set; }
}

public class InviteUserDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string Username { get; set; } = string.Empty;
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Role { get; set; } = string.Empty;
    public Guid? FacilityId { get; set; }
}

public class UpdateUserDto
{
    [Required, MaxLength(200)] public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public Guid? FacilityId { get; set; }
}
