using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Users;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class UserManagementHandler(IRepository<User> users, ITenantContext tenantContext, IEmailService email)
{
    public async Task<PagedResult<UserDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<User> query = users.Query().Include(u => u.Facility);
        if (tenantContext.Role is "FACILITY_ADMIN" or "FACILITY_USER")
            query = query.Where(u => u.FacilityId == tenantContext.FacilityId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(u => u.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<UserDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<UserDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await users.Query().Include(u => u.Facility).FirstOrDefaultAsync(u => u.Id == id, ct) ?? throw new NotFoundException(nameof(User), id));

    public async Task<UserDto> InviteAsync(InviteUserDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<Role>(dto.Role, out var role)) throw new ValidationException("Ungültige Rolle.");
        var facilityId = dto.FacilityId;

        if (tenantContext.Role == "FACILITY_ADMIN")
        {
            role = Role.FACILITY_USER;
            facilityId = tenantContext.FacilityId ?? throw new ForbiddenException("Kein Einrichtungskontext vorhanden.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, FacilityId = facilityId, Name = dto.Name.Trim(), Email = dto.Email.Trim(),
            PasswordHash = string.Empty, Role = role, Status = UserStatus.EINGELADEN,
            InvitationToken = Guid.NewGuid().ToString("N"), InvitationExpiresAt = DateTime.UtcNow.AddHours(72),
        };
        await users.AddAsync(user, ct);
        await users.SaveChangesAsync(ct);

        await SendInviteEmailAsync(user);
        return await GetByIdAsync(user.Id, ct);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(User), id);

        if (tenantContext.Role == "FACILITY_ADMIN")
        {
            if (user.FacilityId != tenantContext.FacilityId) throw new ForbiddenException("Kein Zugriff auf Benutzer einer anderen Einrichtung.");
            if (dto.Role is not null || dto.FacilityId is not null) throw new ForbiddenException("Rolle und Einrichtung können nicht geändert werden.");
        }
        else
        {
            if (dto.Role is not null && Enum.TryParse<Role>(dto.Role, out var role)) user.Role = role;
            if (dto.FacilityId is not null) user.FacilityId = dto.FacilityId;
        }

        user.Name = dto.Name.Trim();
        users.Update(user);
        await users.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task SetStatusAsync(Guid id, UserStatus status, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(User), id);
        user.Status = status;
        users.Update(user);
        await users.SaveChangesAsync(ct);
    }

    public async Task ResendInvitationAsync(Guid id, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(User), id);
        user.InvitationToken = Guid.NewGuid().ToString("N");
        user.InvitationExpiresAt = DateTime.UtcNow.AddHours(72);
        users.Update(user);
        await users.SaveChangesAsync(ct);
        await SendInviteEmailAsync(user);
    }

    private async Task SendInviteEmailAsync(User user) =>
        await email.SendAsync(user.Email, user.Name, "Einladung zu Daily Gourmet",
            $"<p>Sie wurden zu Daily Gourmet eingeladen. <a href=\"https://app.example/accept-invite/{user.InvitationToken}\">Einladung annehmen</a></p>",
            $"Sie wurden zu Daily Gourmet eingeladen. Link: https://app.example/accept-invite/{user.InvitationToken}");

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id, TenantId = u.TenantId, FacilityId = u.FacilityId, FacilityName = u.Facility?.Name,
        Name = u.Name, Email = u.Email, Role = u.Role.ToString(), Status = u.Status.ToString(), LastLoginAt = u.LastLoginAt, FailedLoginCount = u.FailedLoginCount,
    };
}
