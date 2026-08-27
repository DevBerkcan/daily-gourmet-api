using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Handlers;

/// <summary>Shared "build a freshly-invited user" construction — the EINGELADEN/token/expiry
/// convention used by both UserManagementHandler.InviteAsync and FacilityHandler.CreateAsync
/// (auto-invite of a facility's FACILITY_ADMIN). Kept as a single static builder so the two
/// callers can't drift on how an invitation is represented; each caller still sends its own
/// invite email since the wording differs slightly by context.</summary>
public static class UserInvitationHelper
{
    public static User BuildInvitedUser(Guid tenantId, Guid? facilityId, string name, string email, Role role) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        FacilityId = facilityId,
        Name = name,
        Email = email,
        PasswordHash = string.Empty,
        Role = role,
        Status = UserStatus.EINGELADEN,
        InvitationToken = Guid.NewGuid().ToString("N"),
        InvitationExpiresAt = DateTime.UtcNow.AddHours(72),
    };
}
