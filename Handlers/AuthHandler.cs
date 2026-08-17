using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs.Auth;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class AuthHandler(
    IUserRepository users,
    IJwtTokenService tokenService,
    IPasswordHasher<User> passwordHasher,
    ITenantContext tenantContext,
    DailyGourmetDbContext db)
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public async Task<LoginResponseDto> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await users.GetByEmailIgnoringTenantAsync(email, ct)
            ?? throw new UnauthorizedException("E-Mail oder Passwort ist falsch.");

        if (user.Status != UserStatus.AKTIV)
            throw new UnauthorizedException("Dieses Konto ist nicht aktiv.");

        if (user.LockedUntil is { } lockedUntil && lockedUntil > DateTime.UtcNow)
            throw new UnauthorizedException("Konto ist wegen zu vieler Fehlversuche vorübergehend gesperrt.");

        var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedAttempts)
                user.LockedUntil = DateTime.UtcNow.Add(LockoutDuration);
            await db.SaveChangesAsync(ct);
            throw new UnauthorizedException("E-Mail oder Passwort ist falsch.");
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var token = tokenService.GenerateToken(user);
        return new LoginResponseDto { Token = token, User = await BuildCurrentUserDtoAsync(user, ct) };
    }

    public async Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (tenantContext.UserId is not { } userId)
            throw new UnauthorizedException();

        var user = await users.GetByIdIgnoringTenantAsync(userId, ct)
            ?? throw new UnauthorizedException();

        return await BuildCurrentUserDtoAsync(user, ct);
    }

    private async Task<CurrentUserDto> BuildCurrentUserDtoAsync(User user, CancellationToken ct)
    {
        string? tenantName = null;
        if (user.TenantId is { } tenantId)
            tenantName = await db.Tenants.IgnoreQueryFilters().Where(t => t.Id == tenantId).Select(t => t.Name).FirstOrDefaultAsync(ct);

        string? facilityName = null;
        if (user.FacilityId is { } facilityId)
            facilityName = await db.Facilities.IgnoreQueryFilters().Where(f => f.Id == facilityId).Select(f => f.Name).FirstOrDefaultAsync(ct);

        var activeSession = user.TenantId is { } tid &&
            await db.SupportSessions.IgnoreQueryFilters()
                .AnyAsync(s => s.TenantId == tid && s.EndedAtUtc == null && s.ExpiresAtUtc > DateTime.UtcNow, ct);

        return new CurrentUserDto
        {
            Id = user.Id,
            TenantId = user.TenantId,
            TenantName = tenantName,
            FacilityId = user.FacilityId,
            FacilityName = facilityName,
            Name = user.Name,
            Email = user.Email,
            Role = user.Role.ToString(),
            ActiveSupportSession = activeSession,
        };
    }
}
