using DailyGourmet.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Helpers;

/// <summary>Deadlines (Bestellfrist, Tagesanpassung) are configured as wall-clock times in the
/// tenant's time zone (TenantProfile.Timezone, default Europe/Berlin) — previously they were stored
/// as if they were UTC, so in Germany a 09:00 deadline actually closed at 10:00/11:00 local time,
/// and "today" flipped at 01:00/02:00 instead of midnight.</summary>
public static class TenantClock
{
    public const string DefaultTimeZoneId = "Europe/Berlin";

    public static TimeZoneInfo Resolve(string? timeZoneId)
    {
        foreach (var id in new[] { timeZoneId, DefaultTimeZoneId, "W. Europe Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(id)) continue;
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    /// <summary>IgnoreQueryFilters + explicit TenantId, so it also works for super-admin callers.</summary>
    public static async Task<TimeZoneInfo> ForTenantAsync(DailyGourmetDbContext db, Guid tenantId, CancellationToken ct = default)
    {
        var id = await db.TenantProfiles.IgnoreQueryFilters().Where(p => p.TenantId == tenantId).Select(p => p.Timezone).FirstOrDefaultAsync(ct);
        return Resolve(id);
    }

    /// <summary>Local date + local time → UTC instant. A time inside the spring-forward gap (doesn't
    /// exist locally) is shifted forward by an hour instead of throwing.</summary>
    public static DateTime LocalToUtc(DateOnly date, TimeSpan time, TimeZoneInfo tz)
    {
        var local = date.ToDateTime(TimeOnly.FromTimeSpan(time), DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(local)) local = local.AddHours(1);
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    public static DateTime UtcToLocal(DateTime utc, TimeZoneInfo tz) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), tz);

    public static DateOnly Today(TimeZoneInfo tz) => DateOnly.FromDateTime(UtcToLocal(DateTime.UtcNow, tz));
}
