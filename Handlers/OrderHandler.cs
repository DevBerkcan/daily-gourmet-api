using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Orders;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class OrderHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IEmailService email)
{
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    private static IQueryable<Order> FullQuery(DailyGourmetDbContext db) => db.Orders
        .Include(o => o.Facility)
        .Include(o => o.MealPlan)
        .Include(o => o.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<OrderDto>> ListAsync(Guid? facilityId, Guid? mealPlanId, int? calendarWeek, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (tenantContext.FacilityId is { } own) query = query.Where(o => o.FacilityId == own);
        else if (facilityId is { } fid) query = query.Where(o => o.FacilityId == fid);
        if (mealPlanId is { } mid) query = query.Where(o => o.MealPlanId == mid);
        if (calendarWeek is { } cw) query = query.Where(o => o.MealPlan.CalendarWeek == cw);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var s)) query = query.Where(o => o.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var defaultCutoff = await DefaultSameDayCutoffAsync(ct);
        return new PagedResult<OrderDto> { Items = items.Select(o => ToDto(o, defaultCutoff)).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await FullQuery(db).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        EnsureFacilityAccess(order.FacilityId);
        return ToDto(order, await DefaultSameDayCutoffAsync(ct));
    }

    private async Task<TimeSpan> DefaultSameDayCutoffAsync(CancellationToken ct) =>
        await db.TenantSettings.Where(s => s.TenantId == tenantContext.TenantId).Select(s => (TimeSpan?)s.SameDayAdjustmentDeadlineTime).FirstOrDefaultAsync(ct)
        ?? new TimeSpan(9, 0, 0);

    public async Task<OrderDto> SaveAsync(SaveOrderDto dto, CancellationToken ct = default)
    {
        var facilityId = tenantContext.FacilityId ?? dto.FacilityId ?? throw new ValidationException("FacilityId ist erforderlich.");
        EnsureFacilityAccess(facilityId);

        if (dto.Items.GroupBy(i => (i.Date, i.RecipeId)).Any(g => g.Count() > 1))
            throw new ValidationException("Jedes Gericht darf pro Tag nur einmal in der Bestellung vorkommen.");

        var plan = await db.MealPlans.Include(m => m.Facilities).FirstOrDefaultAsync(m => m.Id == dto.MealPlanId, ct)
            ?? throw new NotFoundException(nameof(MealPlan), dto.MealPlanId);
        if (plan.Status != MealPlanStatus.PUBLISHED)
            throw new ConflictException("Bestellungen sind nur für veröffentlichte Speisepläne möglich.");
        if (plan.Facilities.All(f => f.FacilityId != facilityId))
            throw new ForbiddenException("Dieser Speiseplan ist dieser Einrichtung nicht zugeordnet.");

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.FacilityId == facilityId && o.MealPlanId == dto.MealPlanId, ct);
        var wasAlreadySubmitted = order?.Status is OrderStatus.SUBMITTED or OrderStatus.CONFIRMED or OrderStatus.LOCKED;
        var deadline = await ComputeDeadlineAsync(facilityId, dto.Items, ct);

        // Facilities are bound by the deadline (afterwards only the same-day reduction via
        // AdjustSameDayAsync is possible); tenant staff may still correct an order on their behalf.
        var isFacilityCaller = tenantContext.FacilityId is not null;
        if (isFacilityCaller && DateTime.UtcNow > deadline && (dto.Submit || wasAlreadySubmitted))
            throw new ConflictException("Die Bestellfrist ist abgelaufen — Änderungen sind nur noch über die Tagesanpassung möglich.");

        if (dto.Submit)
            await EnsureEverySlotDecidedAsync(facilityId, dto.MealPlanId, dto.Items, ct);

        var removedItemIds = new List<Guid>();
        if (order is null)
        {
            order = new Order
            {
                Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, FacilityId = facilityId, MealPlanId = dto.MealPlanId,
                Status = OrderStatus.DRAFT, DeadlineAtUtc = deadline,
            };
            db.Orders.Add(order);
        }
        else
        {
            if (order.Status is OrderStatus.LOCKED or OrderStatus.CANCELLED)
                throw new ConflictException("Bestellung kann in diesem Status nicht mehr geändert werden.");
            order.DeadlineAtUtc = deadline;
            order.UpdatedAt = DateTime.UtcNow;
        }

        // Update items in place, matched by (date, recipe), instead of delete-all + re-insert: route
        // loading positions reference OrderItem.Id (Restrict FK), so re-creating them failed with a
        // 500 as soon as the order was on a tour. RouteStopItemSync then mirrors the change.
        var incoming = dto.Items.ToDictionary(i => (i.Date, i.RecipeId));
        foreach (var existing in order.Items.ToList())
        {
            if (incoming.Remove((existing.Date, existing.RecipeId), out var change))
            {
                existing.Portions = change.Portions;
                existing.Note = change.Note;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                removedItemIds.Add(existing.Id);
                db.OrderItems.Remove(existing);
            }
        }
        foreach (var item in incoming.Values)
        {
            var orderItem = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, Date = item.Date, RecipeId = item.RecipeId, Portions = item.Portions, Note = item.Note, CreatedAt = DateTime.UtcNow };
            db.OrderItems.Add(orderItem);
            order.Items.Add(orderItem);
        }

        // An already submitted/confirmed order keeps its status when re-saved — previously a plain
        // save reset it to DRAFT and a submit reset CONFIRMED to SUBMITTED, silently dropping the
        // admin's confirmation.
        if (!wasAlreadySubmitted)
        {
            order.Status = dto.Submit ? OrderStatus.SUBMITTED : OrderStatus.DRAFT;
            if (dto.Submit) order.SubmittedAt = DateTime.UtcNow;
        }

        await RouteStopItemSync.SyncAsync(db, order, removedItemIds, ct);
        await db.SaveChangesAsync(ct);
        if (dto.Submit && !wasAlreadySubmitted)
            await NotifyTenantAdminsOfNewOrderAsync(order.TenantId, facilityId, dto.Items.Sum(i => i.Portions), order.DeadlineAtUtc, ct);
        return await GetByIdAsync(order.Id, ct);
    }

    public async Task<OrderDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        EnsureFacilityAccess(order.FacilityId);
        if (order.Status != OrderStatus.DRAFT) throw new ConflictException("Nur Entwürfe können abgesendet werden.");
        if (DateTime.UtcNow > order.DeadlineAtUtc) throw new ConflictException("Die Bestellfrist ist abgelaufen.");

        order.Status = OrderStatus.SUBMITTED;
        order.SubmittedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        var portionen = await db.OrderItems.Where(i => i.OrderId == order.Id).SumAsync(i => i.Portions, ct);
        await NotifyTenantAdminsOfNewOrderAsync(order.TenantId, order.FacilityId, portionen, order.DeadlineAtUtc, ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<OrderDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Facility).Include(o => o.MealPlan).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        if (order.Status != OrderStatus.SUBMITTED) throw new ConflictException("Nur abgesendete Bestellungen können bestätigt werden.");
        order.Status = OrderStatus.CONFIRMED;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(order.Facility.Email))
        {
            var body = $"""
                <p>Ihre Bestellung für KW {order.MealPlan.CalendarWeek}/{order.MealPlan.Year} wurde von Daily Gourmet bestätigt.</p>
                <p>Änderungen sind ab jetzt nur noch bis zur tagesaktuellen Frist möglich.</p>
                """;
            var html = EmailTemplate.Render($"Ihre Bestellung für KW {order.MealPlan.CalendarWeek}/{order.MealPlan.Year} wurde bestätigt.", body);
            var text = $"Ihre Bestellung für KW {order.MealPlan.CalendarWeek}/{order.MealPlan.Year} wurde von Daily Gourmet bestätigt. Änderungen sind ab jetzt nur noch bis zur tagesaktuellen Frist möglich.";
            await email.SendAsync(order.Facility.Email, order.Facility.ContactPerson, "Ihre Bestellung wurde bestätigt", html, text);
        }

        return await GetByIdAsync(id, ct);
    }

    /// <summary>Alle Tenant Owner/Admins des Mandanten werden benachrichtigt, sobald eine Einrichtung
    /// eine Bestellung verbindlich absendet — nur beim tatsächlichen Übergang zu SUBMITTED, nicht bei
    /// jedem erneuten Speichern einer bereits abgesendeten Bestellung (siehe wasAlreadySubmitted in
    /// SaveAsync).</summary>
    private async Task NotifyTenantAdminsOfNewOrderAsync(Guid tenantId, Guid facilityId, int portionen, DateTime deadlineAtUtc, CancellationToken ct)
    {
        var empfaenger = await db.Users
            .Where(u => u.TenantId == tenantId && (u.Role == Role.TENANT_OWNER || u.Role == Role.TENANT_ADMIN) && u.Status == UserStatus.AKTIV)
            .ToListAsync(ct);
        if (empfaenger.Count == 0) return;

        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId, ct);
        var facilityName = facility?.Name ?? "Eine Einrichtung";
        var deadlineLocal = TenantClock.UtcToLocal(deadlineAtUtc, await TenantClock.ForTenantAsync(db, tenantId, ct));
        var body = $"""
            <p><strong>{facilityName}</strong> hat eine Bestellung abgesendet.</p>
            <p>{portionen} Portionen insgesamt · Frist: {deadlineLocal:dd.MM.yyyy HH:mm} Uhr</p>
            """;
        var html = EmailTemplate.Render($"{facilityName} hat eine Bestellung abgesendet.", body);
        var text = $"{facilityName} hat eine Bestellung abgesendet. {portionen} Portionen insgesamt, Frist: {deadlineLocal:dd.MM.yyyy HH:mm} Uhr.";
        foreach (var user in empfaenger)
            await email.SendAsync(user.Email, user.Name, $"Neue Bestellung von {facilityName}", html, text);
    }

    public async Task<OrderDto> LockAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        if (order.Status != OrderStatus.CONFIRMED) throw new ConflictException("Nur bestätigte Bestellungen können gesperrt werden.");
        order.Status = OrderStatus.LOCKED;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<OrderDto> OverrideAsync(Guid id, OverrideOrderDto dto, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        if (order.Status != OrderStatus.LOCKED) throw new ConflictException("Nur gesperrte Bestellungen können korrigiert werden.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) throw new ValidationException("Eine Begründung ist erforderlich.");

        order.Status = OrderStatus.CONFIRMED;
        order.UpdatedAt = DateTime.UtcNow;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId, UserId = tenantContext.UserId,
            Action = "Bestellung nach Frist geändert", Entity = "Order", EntityId = order.Id.ToString(),
            Reason = dto.Reason, CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<List<AuditLogEntryDto>> HistoryAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        EnsureFacilityAccess(order.FacilityId);

        var logs = await db.AuditLogs.Include(a => a.User)
            .Where(a => a.TenantId == tenantContext.TenantId && a.Entity == "Order" && a.EntityId == id.ToString())
            .OrderByDescending(a => a.CreatedAtUtc).ToListAsync(ct);
        return logs.Select(a => new AuditLogEntryDto
        {
            Id = a.Id, Action = a.Action, EntityId = a.EntityId, Reason = a.Reason,
            UserName = a.User?.Name ?? "System", CreatedAtUtc = a.CreatedAtUtc,
        }).ToList();
    }

    private void EnsureFacilityAccess(Guid facilityId)
    {
        if (tenantContext.FacilityId is { } own && own != facilityId)
            throw new ForbiddenException("Kein Zugriff auf Bestellungen einer anderen Einrichtung.");
    }

    private async Task<DateTime> ComputeDeadlineAsync(Guid facilityId, List<SaveOrderItemDto> items, CancellationToken ct)
    {
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId, ct) ?? throw new NotFoundException(nameof(Facility), facilityId);
        var settings = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == facility.TenantId, ct);
        var closures = await db.FacilityClosures.Where(c => c.FacilityId == facilityId).ToListAsync(ct);
        var tz = await TenantClock.ForTenantAsync(db, facility.TenantId, ct);

        // The deadline hangs off the first day the facility is actually supplied — a closed Monday
        // (Schließtag) shouldn't pull the deadline a day earlier. Same rule as OrderReminderService.
        var activeWeekdays = OrderDeadline.ActiveWeekdays(facility);
        var deliveryDates = items.Select(i => i.Date).Where(d => OrderDeadline.IsDeliveryDay(d, activeWeekdays, closures)).ToList();
        var earliestDate = deliveryDates.Count > 0 ? deliveryDates.Min()
            : items.Count > 0 ? items.Min(i => i.Date)
            : TenantClock.Today(tz);
        return OrderDeadline.Compute(facility, settings, tz, earliestDate);
    }

    /// <summary>Blocks submit unless every (date, recipe) slot the published plan offers on this
    /// facility's active weekdays has a matching item in dto.Items — including an explicit
    /// Portions=0 row, which counts as "decided" (see OrderItem's Wunschgericht convention: a
    /// missing row means "forgot to fill in," a present row with 0 means "confirmed none needed").
    /// Names the first missing slot so the frontend can show a specific, non-generic message.</summary>
    private async Task EnsureEverySlotDecidedAsync(Guid facilityId, Guid mealPlanId, List<SaveOrderItemDto> items, CancellationToken ct)
    {
        var mealPlan = await db.MealPlans.Include(m => m.Days).ThenInclude(d => d.Items).ThenInclude(i => i.Recipe)
            .FirstOrDefaultAsync(m => m.Id == mealPlanId, ct) ?? throw new NotFoundException(nameof(MealPlan), mealPlanId);
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId, ct) ?? throw new NotFoundException(nameof(Facility), facilityId);
        var activeWeekdays = OrderDeadline.ActiveWeekdays(facility);
        var closures = await db.FacilityClosures.Where(c => c.FacilityId == facilityId).ToListAsync(ct);

        var expected = mealPlan.Days
            .Where(d => OrderDeadline.IsDeliveryDay(d.Date, activeWeekdays, closures))
            .SelectMany(d => d.Items.Select(i => new { d.Date, i.RecipeId, RecipeName = i.Recipe?.Name ?? "Gericht" }))
            .ToList();
        var provided = items.Select(i => (i.Date, i.RecipeId)).ToHashSet();

        var missing = expected.FirstOrDefault(e => !provided.Contains((e.Date, e.RecipeId)));
        if (missing is not null)
            throw new ValidationException($"Für {missing.Date:dd.MM.yyyy} fehlt eine Angabe zu „{missing.RecipeName}“ — bitte 0 eintragen, falls nicht benötigt.");
    }

    /// <summary>Tagesanpassung: facilities with a contractual agreement (Facility.SameDayAdjustmentEnabled)
    /// may reduce — never increase — today's portions until the cutoff (default 09:00 local). The
    /// first reduction freezes the originally ordered quantity in OrderItem.OriginalPortions, so
    /// purchasing (original) vs. billing (Portions) stays traceable, and the kitchen is emailed
    /// since they don't work in the system. Tenant staff may record a reduction on the facility's
    /// behalf (e.g. a phone call) regardless of the agreement flag or cutoff.</summary>
    public async Task<OrderDto> AdjustSameDayAsync(Guid id, AdjustOrderDto dto, CancellationToken ct = default)
    {
        var order = await db.Orders.Include(o => o.Facility).Include(o => o.Items).ThenInclude(i => i.Recipe)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw new NotFoundException(nameof(Order), id);
        EnsureFacilityAccess(order.FacilityId);
        if (order.Status is not (OrderStatus.SUBMITTED or OrderStatus.CONFIRMED))
            throw new ConflictException("Nur abgesendete oder bestätigte Bestellungen können am Liefertag angepasst werden.");

        var isFacilityCaller = tenantContext.FacilityId is not null;
        if (isFacilityCaller && !order.Facility.SameDayAdjustmentEnabled)
            throw new ForbiddenException("Für Ihre Einrichtung ist keine Anpassung am Liefertag vereinbart. Bitte wenden Sie sich an Daily Gourmet.");

        var settings = await db.TenantSettings.FirstOrDefaultAsync(s => s.TenantId == order.TenantId, ct);
        var cutoffTime = order.Facility.SameDayAdjustmentDeadlineTime ?? settings?.SameDayAdjustmentDeadlineTime ?? new TimeSpan(9, 0, 0);
        var tz = await TenantClock.ForTenantAsync(db, order.TenantId, ct);
        var today = TenantClock.Today(tz);
        var now = DateTime.UtcNow;

        var changes = new List<(OrderItem Item, int Before, string Note)>();
        foreach (var change in dto.Items)
        {
            var item = order.Items.FirstOrDefault(i => i.Id == change.ItemId)
                ?? throw new ValidationException("Am Liefertag können nur bestehende Positionen reduziert werden, keine neuen hinzugefügt werden.");
            var recipeName = item.Recipe?.Name ?? "Gericht";
            if (change.Portions > item.Portions)
                throw new ValidationException($"Portionen für „{recipeName}“ können am Liefertag nur reduziert, nicht erhöht werden.");

            if (isFacilityCaller)
            {
                // Only the delivery day itself — earlier this only checked the cutoff time, so future
                // days of the week could be reduced after the regular order deadline had passed.
                if (item.Date != today)
                    throw new ConflictException($"Am Liefertag können nur die Positionen von heute ({today:dd.MM.yyyy}) angepasst werden.");
                if (now > TenantClock.LocalToUtc(item.Date, cutoffTime, tz))
                    throw new ConflictException($"Die Frist für Anpassungen am {item.Date:dd.MM.yyyy} ({cutoffTime:hh\\:mm} Uhr) ist abgelaufen.");
            }

            if (change.Portions == item.Portions) continue;
            changes.Add((item, item.Portions, change.Note.Trim()));
            item.OriginalPortions ??= item.Portions;
            item.Portions = change.Portions;
            item.SameDayAdjustedAtUtc = now;
            item.UpdatedAt = now;
            item.Note = string.IsNullOrWhiteSpace(item.Note) ? change.Note.Trim() : $"{item.Note} · {change.Note.Trim()}";
        }

        if (changes.Count == 0) return await GetByIdAsync(id, ct);

        order.UpdatedAt = now;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId, UserId = tenantContext.UserId,
            Action = "Portionen am Liefertag reduziert", Entity = "Order", EntityId = order.Id.ToString(),
            Reason = string.Join("; ", changes.Select(c => $"{c.Item.Recipe?.Name ?? "Gericht"} {c.Item.Date:dd.MM.}: {c.Before} → {c.Item.Portions} ({c.Note})")),
            CreatedAtUtc = now,
        });
        // Reduction must reach the driver's loading list, which is a snapshot from route creation.
        await RouteStopItemSync.SyncAsync(db, order, [], ct);
        await db.SaveChangesAsync(ct);

        await NotifyKitchenOfReductionAsync(order, settings, changes, tz, ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>The kitchen team works exclusively by email and never sees the system, so every
    /// same-day reduction is mailed to TenantSettings.KitchenNotificationEmails (fallback: active
    /// tenant owners/admins). Can be switched off via the "OrderChangedAfterSubmit" notification
    /// setting; a missing setting row counts as enabled.</summary>
    private async Task NotifyKitchenOfReductionAsync(Order order, TenantSettings? settings, List<(OrderItem Item, int Before, string Note)> changes, TimeZoneInfo tz, CancellationToken ct)
    {
        var disabled = await db.TenantNotificationSettings
            .AnyAsync(n => n.TenantId == order.TenantId && n.EventKey == "OrderChangedAfterSubmit" && !n.Enabled, ct);
        if (disabled) return;

        var recipients = OrderDeadline.ParseEmailList(settings?.KitchenNotificationEmails)
            .Select(e => (Email: e, Name: "Küche")).ToList();
        if (recipients.Count == 0)
        {
            var admins = await db.Users
                .Where(u => u.TenantId == order.TenantId && (u.Role == Role.TENANT_OWNER || u.Role == Role.TENANT_ADMIN) && u.Status == UserStatus.AKTIV)
                .Select(u => new { u.Email, u.Name }).ToListAsync(ct);
            recipients = admins.Select(u => (Email: u.Email, Name: u.Name)).ToList();
        }
        if (recipients.Count == 0) return;

        var facilityName = System.Net.WebUtility.HtmlEncode(order.Facility.Name);
        var differenz = changes.Sum(c => c.Before - c.Item.Portions);
        var zeitpunkt = TenantClock.UtcToLocal(DateTime.UtcNow, tz);
        var rows = string.Join("", changes.Select(c => $"""
            <tr>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;">{c.Item.Date:dd.MM.yyyy}</td>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;">{System.Net.WebUtility.HtmlEncode(c.Item.Recipe?.Name ?? "Gericht")}</td>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;text-align:right;">{c.Item.OriginalPortions ?? c.Before}</td>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;text-align:right;">{c.Before}</td>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;text-align:right;"><strong>{c.Item.Portions}</strong></td>
              <td style="padding:6px 8px;border-bottom:1px solid #e3e7ed;text-align:right;color:#b42318;">−{c.Before - c.Item.Portions}</td>
            </tr>
            """));
        var reasons = string.Join("", changes.Select(c => c.Note).Where(n => n.Length > 0).Distinct()
            .Select(n => $"<li>{System.Net.WebUtility.HtmlEncode(n)}</li>"));
        var body = $"""
            <p><strong>{facilityName}</strong> hat die Bestellung am Liefertag um <strong>{differenz} Portion(en)</strong> reduziert ({zeitpunkt:dd.MM.yyyy HH:mm} Uhr).</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="width:100%;border-collapse:collapse;font-size:13px;margin:12px 0;">
              <tr style="background:#f6f7f9;">
                <th align="left" style="padding:6px 8px;">Datum</th><th align="left" style="padding:6px 8px;">Gericht</th>
                <th align="right" style="padding:6px 8px;">Ursprünglich</th><th align="right" style="padding:6px 8px;">Bisher</th>
                <th align="right" style="padding:6px 8px;">Neu</th><th align="right" style="padding:6px 8px;">Differenz</th>
              </tr>
              {rows}
            </table>
            {(reasons.Length > 0 ? $"<p>Begründung:</p><ul>{reasons}</ul>" : "")}
            """;
        var text = $"{order.Facility.Name} hat die Bestellung am Liefertag um {differenz} Portion(en) reduziert ({zeitpunkt:dd.MM.yyyy HH:mm} Uhr).\n"
            + string.Join("\n", changes.Select(c => $"{c.Item.Date:dd.MM.yyyy} {c.Item.Recipe?.Name}: {c.Before} → {c.Item.Portions} (ursprünglich {c.Item.OriginalPortions ?? c.Before}) — {c.Note}"));
        var html = EmailTemplate.Render($"{order.Facility.Name}: −{differenz} Portion(en) heute", body);
        var subject = $"Tagesanpassung: {order.Facility.Name} −{differenz} Portion(en) am {changes[0].Item.Date:dd.MM.}";
        foreach (var (address, name) in recipients)
            await email.SendAsync(address, name, subject, html, text);
    }

    private static OrderDto ToDto(Order o, TimeSpan defaultSameDayCutoff) => new()
    {
        Id = o.Id,
        FacilityId = o.FacilityId,
        FacilityName = o.Facility?.Name ?? string.Empty,
        MealPlanId = o.MealPlanId,
        CalendarWeek = o.MealPlan?.CalendarWeek ?? 0,
        Year = o.MealPlan?.Year ?? 0,
        Status = o.Status.ToString(),
        SubmittedAt = o.SubmittedAt,
        DeadlineAtUtc = o.DeadlineAtUtc,
        SameDayAdjustmentEnabled = o.Facility?.SameDayAdjustmentEnabled ?? false,
        SameDayAdjustmentDeadlineTime = o.Facility?.SameDayAdjustmentDeadlineTime ?? defaultSameDayCutoff,
        Items = o.Items.Select(i => new OrderItemDto
        {
            Id = i.Id, Date = i.Date, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty, Portions = i.Portions, Note = i.Note,
            OriginalPortions = i.OriginalPortions, SameDayAdjustedAtUtc = i.SameDayAdjustedAtUtc,
        }).ToList(),
    };
}
