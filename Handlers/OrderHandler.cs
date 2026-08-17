using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Orders;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class OrderHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    private static IQueryable<Order> FullQuery(DailyGourmetDbContext db) => db.Orders
        .Include(o => o.Facility)
        .Include(o => o.MealPlan)
        .Include(o => o.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<OrderDto>> ListAsync(Guid? facilityId, Guid? mealPlanId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (tenantContext.FacilityId is { } own) query = query.Where(o => o.FacilityId == own);
        else if (facilityId is { } fid) query = query.Where(o => o.FacilityId == fid);
        if (mealPlanId is { } mid) query = query.Where(o => o.MealPlanId == mid);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, out var s)) query = query.Where(o => o.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(o => o.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<OrderDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<OrderDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var order = await FullQuery(db).FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        EnsureFacilityAccess(order.FacilityId);
        return ToDto(order);
    }

    public async Task<OrderDto> SaveAsync(SaveOrderDto dto, CancellationToken ct = default)
    {
        var facilityId = tenantContext.FacilityId ?? dto.FacilityId ?? throw new ValidationException("FacilityId ist erforderlich.");
        EnsureFacilityAccess(facilityId);

        var order = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.FacilityId == facilityId && o.MealPlanId == dto.MealPlanId, ct);
        var deadline = await ComputeDeadlineAsync(facilityId, dto.Items, ct);

        if (dto.Submit && DateTime.UtcNow > deadline)
            throw new ConflictException("Die Bestellfrist ist abgelaufen.");

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
            db.OrderItems.RemoveRange(order.Items);
            order.DeadlineAtUtc = deadline;
            order.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var item in dto.Items)
            db.OrderItems.Add(new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, Date = item.Date, RecipeId = item.RecipeId, Portions = item.Portions, Note = item.Note, CreatedAt = DateTime.UtcNow });

        order.Status = dto.Submit ? OrderStatus.SUBMITTED : OrderStatus.DRAFT;
        if (dto.Submit) order.SubmittedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
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
        return await GetByIdAsync(id, ct);
    }

    public async Task<OrderDto> ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == id, ct) ?? throw new NotFoundException(nameof(Order), id);
        if (order.Status != OrderStatus.SUBMITTED) throw new ConflictException("Nur abgesendete Bestellungen können bestätigt werden.");
        order.Status = OrderStatus.CONFIRMED;
        order.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
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

        var offsetDays = facility.OrderDeadlineOffsetDays ?? settings?.DefaultOrderDeadlineOffsetDays ?? 1;
        var deadlineTime = facility.OrderDeadlineTime ?? settings?.DefaultOrderDeadlineTime ?? new TimeSpan(9, 0, 0);
        var excludeWeekends = settings?.ExcludeWeekendsFromDeadline ?? true;

        var earliestDate = items.Count > 0 ? items.Min(i => i.Date) : DateOnly.FromDateTime(DateTime.UtcNow);
        var deadlineDate = earliestDate.AddDays(-offsetDays);
        if (excludeWeekends)
            while (deadlineDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                deadlineDate = deadlineDate.AddDays(-1);

        return deadlineDate.ToDateTime(TimeOnly.FromTimeSpan(deadlineTime), DateTimeKind.Utc);
    }

    private static OrderDto ToDto(Order o) => new()
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
        Items = o.Items.Select(i => new OrderItemDto { Id = i.Id, Date = i.Date, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty, Portions = i.Portions, Note = i.Note }).ToList(),
    };
}
