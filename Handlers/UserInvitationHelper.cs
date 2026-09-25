using System.Text;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Shared "build a freshly-invited user" construction — the EINGELADEN/token/expiry
/// convention used by both UserManagementHandler.InviteAsync and FacilityHandler.CreateAsync
/// (auto-invite of a facility's FACILITY_ADMIN). Kept as a single static builder so the two
/// callers can't drift on how an invitation is represented; each caller still sends its own
/// invite email since the wording differs slightly by context.</summary>
public static class UserInvitationHelper
{
    public static User BuildInvitedUser(Guid tenantId, Guid? facilityId, string name, string username, string email, Role role) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        FacilityId = facilityId,
        Name = name,
        Username = username,
        Email = email,
        PasswordHash = string.Empty,
        Role = role,
        Status = UserStatus.EINGELADEN,
        InvitationToken = Guid.NewGuid().ToString("N"),
        InvitationExpiresAt = DateTime.UtcNow.AddHours(72),
    };

    /// <summary>The public "set your password" URL for an invitation/reset token — every
    /// SendXInviteEmailAsync method builds this same URL, and it's also handed straight back to
    /// the calling admin (see InviteLinkDto) so they can copy it and share it manually (e.g. over
    /// chat) without depending on email delivery actually working.</summary>
    public static string BuildAcceptInviteUrl(string publicBaseUrl, string token) =>
        $"{publicBaseUrl.TrimEnd('/')}/accept-invite/{token}";

    /// <summary>Derives a globally-unique Username from a free-form candidate (typically an email
    /// address or a person's name) — used wherever a login account is created without the admin
    /// being asked to type a username themselves (tenant-owner-on-tenant-create, facility-admin
    /// auto-invite). Keeps only lowercase letters/digits/.-_ from the candidate's local part (before
    /// any @), falling back to "user" if nothing usable remains, then appends a numeric suffix on
    /// collision — same convention as the AddUsernameToUser migration's backfill for existing rows.</summary>
    public static async Task<string> GenerateUniqueUsernameAsync(DailyGourmetDbContext db, string candidate, CancellationToken ct = default)
    {
        var localPart = candidate.Contains('@') ? candidate[..candidate.IndexOf('@')] : candidate;
        var sb = new StringBuilder();
        foreach (var c in localPart.ToLowerInvariant())
            if (char.IsLetterOrDigit(c) || c is '.' or '_' or '-') sb.Append(c);
        var normalized = sb.Length > 0 ? sb.ToString() : "user";

        var username = normalized;
        var suffix = 1;
        while (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Username == username, ct))
            username = $"{normalized}{++suffix}";
        return username;
    }
}
