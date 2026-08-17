using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Logistics;
using DailyGourmet.Api.Models.DTOs.Procurement;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class DeliveryRouteHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly string[] RouteStatusOrder = ["GEPLANT", "BELADUNG", "UNTERWEGS", "ABGESCHLOSSEN"];
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    private static IQueryable<DeliveryRoute> FullQuery(DailyGourmetDbContext db) => db.Routes
        .Include(r => r.Driver).ThenInclude(d => d.User)
        .Include(r => r.Location)
        .Include(r => r.Stops).ThenInclude(s => s.Facility)
        .Include(r => r.Stops).ThenInclude(s => s.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<DeliveryRouteDto>> ListAsync(DateOnly? date, Guid? driverId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (date is { } d) query = query.Where(r => r.Date == d);
        if (driverId is { } did) query = query.Where(r => r.DriverId == did);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RouteStatus>(status, out var s)) query = query.Where(r => r.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.Date).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<DeliveryRouteDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<DeliveryRouteDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var route = await FullQuery(db).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), id);
        if (tenantContext.Role == "DRIVER")
        {
            var driver = await GetCallerDriverAsync(ct);
            if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");
        }
        return ToDto(route);
    }

    public async Task<DeliveryRouteDto> CreateAsync(CreateRouteDto dto, CancellationToken ct = default)
    {
        var route = new DeliveryRoute
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, Name = dto.Name, Date = dto.Date, DriverId = dto.DriverId,
            LocationId = dto.LocationId, PlannedDepartureTime = dto.PlannedDepartureTime, Status = RouteStatus.GEPLANT,
        };
        db.Routes.Add(route);

        for (var i = 0; i < dto.FacilityIds.Length; i++)
        {
            var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == dto.FacilityIds[i], ct) ?? throw new NotFoundException(nameof(Facility), dto.FacilityIds[i]);
            var stop = new RouteStop
            {
                Id = Guid.NewGuid(), RouteId = route.Id, FacilityId = facility.Id, SequenceNumber = i + 1,
                PlannedArrivalTime = dto.PlannedDepartureTime.Add(TimeSpan.FromMinutes(30 * (i + 1))),
                ContactName = facility.ContactPerson, ContactPhone = facility.Phone, Status = RouteStopStatus.OFFEN, CreatedAt = DateTime.UtcNow,
            };
            db.RouteStops.Add(stop);

            var orderItems = await db.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.FacilityId == facility.Id && BindingStatuses.Contains(oi.Order.Status) && oi.Date == dto.Date)
                .ToListAsync(ct);
            foreach (var orderItem in orderItems)
            {
                db.RouteStopItems.Add(new RouteStopItem
                {
                    Id = Guid.NewGuid(), RouteStopId = stop.Id, RecipeId = orderItem.RecipeId, OrderId = orderItem.OrderId, OrderItemId = orderItem.Id,
                    Portions = orderItem.Portions, ContainerDescription = $"{Math.Ceiling(orderItem.Portions / 15.0)} × GN 1/1",
                    TemperatureRequirement = "mind. 65 °C", CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(route.Id, ct);
    }

    public async Task<DeliveryRouteDto> UpdateStatusAsync(Guid id, UpdateStatusDto dto, CancellationToken ct = default)
    {
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), id);
        if (!Enum.TryParse<RouteStatus>(dto.Status, out var target)) throw new ValidationException("Ungültiger Status.");
        var currentIndex = Array.IndexOf(RouteStatusOrder, route.Status.ToString());
        var targetIndex = Array.IndexOf(RouteStatusOrder, target.ToString());
        if (targetIndex != currentIndex + 1) throw new ConflictException("Statuswechsel ist nur schrittweise vorwärts erlaubt.");

        route.Status = target;
        route.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<List<DeliveryRouteDto>> CurrentDriverRoutesAsync(bool todayOnly, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var query = FullQuery(db).Where(r => r.DriverId == driver.Id);
        if (todayOnly) query = query.Where(r => r.Date == DateOnly.FromDateTime(DateTime.UtcNow));
        var routes = await query.OrderBy(r => r.Date).ToListAsync(ct);
        return routes.Select(ToDto).ToList();
    }

    public async Task<DeliveryRouteDto> UpdateStopStatusAsync(Guid routeId, Guid stopId, UpdateStopStatusDto dto, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");

        var stop = await db.RouteStops.FirstOrDefaultAsync(s => s.Id == stopId && s.RouteId == routeId, ct) ?? throw new NotFoundException(nameof(RouteStop), stopId);
        if (!Enum.TryParse<RouteStopStatus>(dto.Status, out var target)) throw new ValidationException("Ungültiger Status.");

        if (target == RouteStopStatus.PROBLEM && string.IsNullOrWhiteSpace(dto.ProblemNote))
            throw new ValidationException("Für ein gemeldetes Problem ist eine Begründung erforderlich.");

        if (target == RouteStopStatus.ZUGESTELLT) stop.DeliveredAt = DateTime.UtcNow;
        if (target == RouteStopStatus.PROBLEM) stop.ProblemNote = dto.ProblemNote;
        if (target == RouteStopStatus.OFFEN) stop.ProblemNote = null;
        stop.Status = target;
        stop.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
    }

    public async Task<DeliveryRouteDto> SetItemPackedAsync(Guid routeId, Guid stopId, Guid itemId, CancellationToken ct = default)
    {
        var item = await db.RouteStopItems.FirstOrDefaultAsync(i => i.Id == itemId && i.RouteStopId == stopId, ct) ?? throw new NotFoundException(nameof(RouteStopItem), itemId);
        item.IsPacked = !item.IsPacked;
        item.PackedAt = item.IsPacked ? DateTime.UtcNow : null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
    }

    public async Task<DeliveryRouteDto> SetItemLoadedAsync(Guid routeId, Guid stopId, Guid itemId, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");

        var item = await db.RouteStopItems.FirstOrDefaultAsync(i => i.Id == itemId && i.RouteStopId == stopId, ct) ?? throw new NotFoundException(nameof(RouteStopItem), itemId);
        item.IsLoaded = !item.IsLoaded;
        item.LoadedAt = item.IsLoaded ? DateTime.UtcNow : null;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
    }

    private async Task<Driver> GetCallerDriverAsync(CancellationToken ct) =>
        await db.Set<Driver>().FirstOrDefaultAsync(d => d.UserId == tenantContext.UserId, ct) ?? throw new ForbiddenException("Kein Fahrerprofil für diesen Benutzer.");

    private static DeliveryRouteDto ToDto(DeliveryRoute r) => new()
    {
        Id = r.Id, Name = r.Name, Date = r.Date, DriverId = r.DriverId, DriverName = r.Driver?.User?.Name ?? string.Empty,
        LocationId = r.LocationId, LocationName = r.Location?.Name, PlannedDepartureTime = r.PlannedDepartureTime,
        PlannedReturnTime = r.PlannedReturnTime, DistanceKm = r.DistanceKm, Status = r.Status.ToString(),
        Stops = r.Stops.OrderBy(s => s.SequenceNumber).Select(s => new RouteStopDto
        {
            Id = s.Id, FacilityId = s.FacilityId, FacilityName = s.Facility?.Name ?? string.Empty, FacilityAddress = s.Facility?.Address ?? string.Empty, SequenceNumber = s.SequenceNumber,
            PlannedArrivalTime = s.PlannedArrivalTime, DeliveryWindowStart = s.DeliveryWindowStart, DeliveryWindowEnd = s.DeliveryWindowEnd,
            ContactName = s.ContactName, ContactPhone = s.ContactPhone, Note = s.Note, Status = s.Status.ToString(), ProblemNote = s.ProblemNote, DeliveredAt = s.DeliveredAt,
            Items = s.Items.Select(i => new RouteStopItemDto
            {
                Id = i.Id, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty, Portions = i.Portions,
                ContainerDescription = i.ContainerDescription, TemperatureRequirement = i.TemperatureRequirement, Note = i.Note,
                IsPacked = i.IsPacked, PackedAt = i.PackedAt, IsLoaded = i.IsLoaded, LoadedAt = i.LoadedAt,
            }).ToList(),
        }).ToList(),
    };
}
