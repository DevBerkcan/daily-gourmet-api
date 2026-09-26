using DailyGourmet.Api.Data;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

/// <summary>Every DRIVER user gets a Driver row automatically — previously a tenant admin had to
/// create it by hand in the Fahrerprofile card after the super admin created the user, and until
/// then the driver couldn't be assigned to routes. Phone/vehicle/plate start empty and are filled
/// in later via DriverHandler.UpdateAsync. Only adds to the change tracker; the caller saves.</summary>
public static class DriverProfileHelper
{
    public static async Task EnsureForUserAsync(DailyGourmetDbContext db, User user, CancellationToken ct = default)
    {
        if (user.Role != Role.DRIVER || user.TenantId is not { } tenantId) return;
        if (await db.Drivers.IgnoreQueryFilters().AnyAsync(d => d.UserId == user.Id, ct)) return;
        db.Drivers.Add(new Driver
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = user.Id,
            Phone = string.Empty,
            VehicleDescription = string.Empty,
            LicensePlate = string.Empty,
            CreatedAt = DateTime.UtcNow,
        });
    }

    /// <summary>Startup backfill for DRIVER users created before profiles were automatic.</summary>
    public static async Task BackfillMissingAsync(DailyGourmetDbContext db, CancellationToken ct = default)
    {
        var ohneProfil = await db.Users.IgnoreQueryFilters()
            .Where(u => u.Role == Role.DRIVER && u.TenantId != null && !db.Drivers.IgnoreQueryFilters().Any(d => d.UserId == u.Id))
            .ToListAsync(ct);
        if (ohneProfil.Count == 0) return;
        foreach (var user in ohneProfil)
            await EnsureForUserAsync(db, user, ct);
        await db.SaveChangesAsync(ct);
    }
}
