using DailyGourmet.Api.Models.Entities;

namespace DailyGourmet.Api.Helpers;

/// <summary>Shared rules for "which days does a facility actually need food" and "when is the order
/// deadline" — used by OrderHandler (saving/submitting) and OrderReminderService (reminders), so
/// both always agree on the same deadline.</summary>
public static class OrderDeadline
{
    private static readonly Dictionary<DayOfWeek, string> Abbreviation = new()
    {
        [DayOfWeek.Monday] = "Mo", [DayOfWeek.Tuesday] = "Di", [DayOfWeek.Wednesday] = "Mi", [DayOfWeek.Thursday] = "Do",
        [DayOfWeek.Friday] = "Fr", [DayOfWeek.Saturday] = "Sa", [DayOfWeek.Sunday] = "So",
    };

    public static HashSet<string> ActiveWeekdays(Facility facility) =>
        facility.ActiveWeekdays.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public static bool IsClosed(DateOnly date, IEnumerable<FacilityClosure> closures) =>
        closures.Any(c => c.StartDate <= date && date <= c.EndDate);

    /// <summary>True when the facility is open and supplied on that date: the weekday is one of its
    /// active weekdays and no Schließtag/closure period covers it.</summary>
    public static bool IsDeliveryDay(DateOnly date, HashSet<string> activeWeekdays, IEnumerable<FacilityClosure> closures) =>
        activeWeekdays.Contains(Abbreviation[date.DayOfWeek]) && !IsClosed(date, closures);

    /// <summary>Order deadline (UTC) for an order whose earliest delivery date is
    /// <paramref name="earliestDate"/>: facility override → tenant default → 1 day / 09:00, in the
    /// tenant's local time, optionally skipping back over weekends.</summary>
    public static DateTime Compute(Facility facility, TenantSettings? settings, TimeZoneInfo tz, DateOnly earliestDate)
    {
        var offsetDays = facility.OrderDeadlineOffsetDays ?? settings?.DefaultOrderDeadlineOffsetDays ?? 1;
        var deadlineTime = facility.OrderDeadlineTime ?? settings?.DefaultOrderDeadlineTime ?? new TimeSpan(9, 0, 0);
        var excludeWeekends = settings?.ExcludeWeekendsFromDeadline ?? true;

        var deadlineDate = earliestDate.AddDays(-offsetDays);
        if (excludeWeekends)
            while (deadlineDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                deadlineDate = deadlineDate.AddDays(-1);

        // deadlineTime is local wall-clock time of the tenant, not UTC (see TenantClock).
        return TenantClock.LocalToUtc(deadlineDate, deadlineTime, tz);
    }

    /// <summary>Parses a comma/semicolon/whitespace separated address list, dropping blanks and duplicates.</summary>
    public static List<string> ParseEmailList(string? raw) =>
        string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split([',', ';', ' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(e => e.Contains('@'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
