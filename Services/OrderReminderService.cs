using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Services;

public class OrderReminderOptions
{
    public const string SectionName = "OrderReminders";

    /// <summary>Null = enabled everywhere except the Development environment. Local development
    /// talks to the live database and SMTP server, so the job must not send real reminders from a
    /// developer machine unless explicitly switched on.</summary>
    public bool? Enabled { get; set; }
    public int IntervalMinutes { get; set; } = 15;
}

/// <summary>Automatic "Bestellfrist läuft ab" reminder: every few minutes, finds facilities that
/// are assigned to a published meal plan but have not submitted an order for it yet, and whose order
/// deadline is within TenantSettings.OrderReminderHoursBefore (default 24h). Weeks in which the
/// facility is not supplied on any day (Schließtage / inactive weekdays) are skipped, and the
/// deadline is computed with the same rules as OrderHandler (OrderDeadline.Compute). Each
/// facility/week gets at most one reminder (OrderReminder log table). Gated per tenant by the
/// "DeadlineApproaching" notification setting and the "kundenportal" feature flag.
///
/// Hosting note: on IIS the app pool must stay alive (AlwaysRunning / no idle timeout, or an
/// external ping on /api/health) — otherwise the timer only runs while requests keep the app awake.</summary>
public class OrderReminderService(
    IServiceScopeFactory scopeFactory,
    IOptions<OrderReminderOptions> options,
    IOptions<AppOptions> appOptions,
    IHostEnvironment environment,
    ILogger<OrderReminderService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = options.Value.Enabled ?? !environment.IsDevelopment();
        if (!enabled)
        {
            logger.LogInformation("OrderReminderService is disabled (set OrderReminders:Enabled=true to enable).");
            return;
        }

        try
        {
            // Let startup (migrations/backfills in Program.cs) finish first.
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(Math.Max(1, options.Value.IntervalMinutes)));
            do
            {
                try
                {
                    var sent = await RunOnceAsync(stoppingToken);
                    if (sent > 0) logger.LogInformation("Sent {Count} order deadline reminder(s).", sent);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Order reminder run failed.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* shutting down */ }
    }

    /// <summary>One pass over all tenants. Returns the number of reminders sent.</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        // System job without an HTTP caller: run with the platform role so the tenant query filters
        // don't hide everything — every query below filters by TenantId explicitly instead.
        scope.ServiceProvider.GetRequiredService<ITenantContext>().Set(null, null, nameof(Role.SUPER_ADMIN), null);
        var db = scope.ServiceProvider.GetRequiredService<DailyGourmetDbContext>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        var sent = 0;
        var tenantIds = await db.Tenants.Where(t => t.Status == TenantStatus.AKTIV).Select(t => t.Id).ToListAsync(ct);
        foreach (var tenantId in tenantIds)
        {
            var disabled = await db.TenantNotificationSettings
                .AnyAsync(n => n.TenantId == tenantId && n.EventKey == "DeadlineApproaching" && !n.Enabled, ct);
            if (disabled) continue;
            if (!await featureFlags.IsEnabledAsync(tenantId, "kundenportal", ct)) continue;

            sent += await RunForTenantAsync(db, email, tenantId, ct);
        }
        return sent;
    }

    private async Task<int> RunForTenantAsync(DailyGourmetDbContext db, IEmailService email, Guid tenantId, CancellationToken ct)
    {
        var settings = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
        var tz = await TenantClock.ForTenantAsync(db, tenantId, ct);
        var window = TimeSpan.FromHours(Math.Clamp(settings?.OrderReminderHoursBefore ?? 24, 1, 168));
        var now = DateTime.UtcNow;
        var today = TenantClock.Today(tz);

        var candidates = await db.MealPlanFacilities
            .Where(mf => mf.TenantId == tenantId
                && mf.MealPlan.Status == MealPlanStatus.PUBLISHED && !mf.MealPlan.IsTemplate
                && mf.Facility.Status == FacilityStatus.AKTIV
                && mf.MealPlan.Days.Any(d => d.Date > today)
                && !db.Orders.Any(o => o.FacilityId == mf.FacilityId && o.MealPlanId == mf.MealPlanId
                    && (o.Status == OrderStatus.SUBMITTED || o.Status == OrderStatus.CONFIRMED || o.Status == OrderStatus.LOCKED))
                && !db.OrderReminders.Any(r => r.FacilityId == mf.FacilityId && r.MealPlanId == mf.MealPlanId))
            .Select(mf => new
            {
                mf.MealPlanId, mf.FacilityId, mf.MealPlan.CalendarWeek, mf.MealPlan.Year,
                Dates = mf.MealPlan.Days.Where(d => d.Items.Any()).Select(d => d.Date).ToList(),
            })
            .ToListAsync(ct);
        if (candidates.Count == 0) return 0;

        var facilityIds = candidates.Select(c => c.FacilityId).Distinct().ToList();
        var facilities = await db.Facilities.Include(f => f.Closures)
            .Where(f => facilityIds.Contains(f.Id)).ToDictionaryAsync(f => f.Id, ct);
        var draftKeys = (await db.Orders
                .Where(o => facilityIds.Contains(o.FacilityId) && o.Status == OrderStatus.DRAFT)
                .Select(o => new { o.FacilityId, o.MealPlanId }).ToListAsync(ct))
            .Select(o => (o.FacilityId, o.MealPlanId)).ToHashSet();
        var facilityUsers = await db.Users
            .Where(u => u.FacilityId != null && facilityIds.Contains(u.FacilityId.Value)
                && (u.Role == Role.FACILITY_ADMIN || u.Role == Role.FACILITY_USER) && u.Status == UserStatus.AKTIV)
            .Select(u => new { FacilityId = u.FacilityId!.Value, u.Email, u.Name })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var c in candidates)
        {
            if (!facilities.TryGetValue(c.FacilityId, out var facility)) continue;

            var activeWeekdays = OrderDeadline.ActiveWeekdays(facility);
            var deliveryDates = c.Dates.Where(d => OrderDeadline.IsDeliveryDay(d, activeWeekdays, facility.Closures)).OrderBy(d => d).ToList();
            // Closed (or not supplied) the whole week → nothing to order, no reminder.
            if (deliveryDates.Count == 0) continue;

            var deadline = OrderDeadline.Compute(facility, settings, tz, deliveryDates[0]);
            if (now >= deadline || deadline - now > window) continue;

            var recipients = facilityUsers.Where(u => u.FacilityId == facility.Id).Select(u => (u.Email, u.Name)).ToList();
            if (!string.IsNullOrWhiteSpace(facility.Email)) recipients.Add((facility.Email, facility.ContactPerson));
            recipients = recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email))
                .DistinctBy(r => r.Email.Trim().ToLowerInvariant()).ToList();

            // Log first, then send: if two instances race, the unique index makes one of them fail
            // here instead of both mailing the facility.
            db.OrderReminders.Add(new OrderReminder
            {
                Id = Guid.NewGuid(), TenantId = tenantId, FacilityId = facility.Id, MealPlanId = c.MealPlanId,
                DeadlineAtUtc = deadline, SentAtUtc = now, CreatedAt = now,
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex)
            {
                logger.LogWarning(ex, "Reminder for facility {FacilityId} / plan {MealPlanId} already logged — skipping.", facility.Id, c.MealPlanId);
                db.ChangeTracker.Clear();
                continue;
            }

            if (recipients.Count == 0) continue;
            var (subject, html, text) = BuildEmail(facility, c.CalendarWeek, c.Year, deliveryDates, TenantClock.UtcToLocal(deadline, tz), draftKeys.Contains((facility.Id, c.MealPlanId)));
            foreach (var (address, name) in recipients)
                await email.SendAsync(address, name, subject, html, text);
            sent++;
        }
        return sent;
    }

    private (string Subject, string Html, string Text) BuildEmail(Facility facility, int calendarWeek, int year, List<DateOnly> deliveryDates, DateTime deadlineLocal, bool hasDraft)
    {
        var culture = System.Globalization.CultureInfo.GetCultureInfo("de-DE");
        var portalUrl = $"{appOptions.Value.PublicBaseUrl.TrimEnd('/')}/portal/meal-plans";
        var days = string.Join(", ", deliveryDates.Select(d => d.ToString("ddd dd.MM.", culture)));
        var facilityName = System.Net.WebUtility.HtmlEncode(facility.Name);
        var status = hasDraft
            ? "Es ist ein Entwurf gespeichert, der aber noch <strong>nicht abgesendet</strong> wurde."
            : "Bisher liegt uns noch <strong>keine Bestellung</strong> vor.";
        var statusText = hasDraft ? "Es ist ein Entwurf gespeichert, der aber noch nicht abgesendet wurde." : "Bisher liegt uns noch keine Bestellung vor.";

        var subject = $"Erinnerung: Bestellung KW {calendarWeek}/{year} bis {deadlineLocal.ToString("ddd dd.MM., HH:mm", culture)} Uhr";
        var body = $"""
            <p>Guten Tag,</p>
            <p>die Bestellfrist für <strong>KW {calendarWeek}/{year}</strong> ({facilityName}) endet am
            <strong>{deadlineLocal.ToString("dddd, dd.MM.yyyy 'um' HH:mm", culture)} Uhr</strong>. {status}</p>
            <p>Liefertage laut Ihren Einstellungen: {days}</p>
            <p>Falls Ihre Einrichtung in dieser Woche geschlossen ist, tragen Sie bitte die Schließtage im Portal ein — dann entfällt diese Erinnerung künftig.</p>
            """;
        var html = EmailTemplate.Render($"Bestellfrist KW {calendarWeek} endet bald.", body, "Jetzt bestellen", portalUrl);
        var text = $"Die Bestellfrist für KW {calendarWeek}/{year} ({facility.Name}) endet am {deadlineLocal.ToString("dddd, dd.MM.yyyy 'um' HH:mm", culture)} Uhr. {statusText}\n"
            + $"Liefertage: {days}\nJetzt bestellen: {portalUrl}";
        return (subject, html, text);
    }
}
