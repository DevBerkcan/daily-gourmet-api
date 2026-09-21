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
        .Include(r => r.Driver!).ThenInclude(d => d.User)
        .Include(r => r.Location)
        .Include(r => r.Stops).ThenInclude(s => s.Facility)
        .Include(r => r.Stops).ThenInclude(s => s.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<DeliveryRouteDto>> ListAsync(DateOnly? date, Guid? driverId, string? status, bool? unassigned, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (date is { } d) query = query.Where(r => r.Date == d);
        if (driverId is { } did) query = query.Where(r => r.DriverId == did);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RouteStatus>(status, out var s)) query = query.Where(r => r.Status == s);
        if (unassigned == true) query = query.Where(r => r.DriverId == null);

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

        // Einrichtungen, die an diesem Datum laut FacilityClosure geschlossen haben, werden nicht als
        // Stopp aufgenommen — der Admin sieht stattdessen im Rückgabewert (SkippedClosedFacilities),
        // welche Einrichtungen deshalb übersprungen wurden.
        var closedFacilityIds = await db.FacilityClosures
            .Where(c => dto.FacilityIds.Contains(c.FacilityId) && c.StartDate <= dto.Date && dto.Date <= c.EndDate)
            .Select(c => c.FacilityId)
            .Distinct()
            .ToListAsync(ct);
        var skippedFacilityNames = new List<string>();
        var windowWarnings = new List<string>();

        var sequenceNumber = 1;
        // Grobe Zeitschätzung mangels echter Distanzdaten: feste 30 Minuten Fahrzeit zwischen zwei
        // Stopps, plus die bei der Einrichtung hinterlegte Lieferdauer (Standard 15 Minuten), bevor
        // die Fahrt zum nächsten Stopp beginnt.
        var uhrzeit = dto.PlannedDepartureTime;
        foreach (var facilityId in dto.FacilityIds)
        {
            var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId, ct) ?? throw new NotFoundException(nameof(Facility), facilityId);
            if (closedFacilityIds.Contains(facilityId))
            {
                skippedFacilityNames.Add(facility.Name);
                continue;
            }

            uhrzeit = uhrzeit.Add(TimeSpan.FromMinutes(30));
            var stop = new RouteStop
            {
                Id = Guid.NewGuid(), RouteId = route.Id, FacilityId = facility.Id, SequenceNumber = sequenceNumber,
                PlannedArrivalTime = uhrzeit,
                DeliveryWindowStart = facility.DeliveryWindowStart, DeliveryWindowEnd = facility.DeliveryWindowEnd,
                ContactName = facility.ContactPerson, ContactPhone = facility.Phone, Note = facility.DeliveryRequirements,
                Status = RouteStopStatus.OFFEN, CreatedAt = DateTime.UtcNow,
            };
            if (facility.DeliveryWindowStart is { } ws && facility.DeliveryWindowEnd is { } we && (uhrzeit < ws || uhrzeit > we))
                windowWarnings.Add($"{facility.Name} (geplant {uhrzeit:hh\\:mm} Uhr, Fenster {ws:hh\\:mm}–{we:hh\\:mm} Uhr)");

            uhrzeit = uhrzeit.Add(TimeSpan.FromMinutes(facility.DeliveryDurationMinutes ?? 15));
            sequenceNumber++;
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
        var result = await GetByIdAsync(route.Id, ct);
        result.SkippedClosedFacilities = skippedFacilityNames;
        result.ArrivalOutsideWindowWarnings = windowWarnings;
        return result;
    }

    public async Task<DeliveryRouteDto> UpdateStatusAsync(Guid id, UpdateStatusDto dto, CancellationToken ct = default)
    {
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), id);
        if (tenantContext.Role == "DRIVER")
        {
            var driver = await GetCallerDriverAsync(ct);
            if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");
        }
        if (!Enum.TryParse<RouteStatus>(dto.Status, out var target)) throw new ValidationException("Ungültiger Status.");
        var currentIndex = Array.IndexOf(RouteStatusOrder, route.Status.ToString());
        var targetIndex = Array.IndexOf(RouteStatusOrder, target.ToString());
        if (targetIndex != currentIndex + 1) throw new ConflictException("Statuswechsel ist nur schrittweise vorwärts erlaubt.");

        if (target == RouteStatus.BELADUNG && !(route.HandoffWarmConfirmed && route.HandoffKaltConfirmed && route.HandoffDessertConfirmed))
            throw new ConflictException("Bitte zuerst die Übernahme aus der Küche bestätigen (warm, kalt, Dessert).");

        route.Status = target;
        route.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Self-service pick from the pool of unassigned routes ("Route übernehmen"),
    /// replacing admin-assigns-driver as the default flow.</summary>
    public async Task<DeliveryRouteDto> ClaimAsync(Guid routeId, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != null) throw new ConflictException("Diese Route ist bereits vergeben.");
        if (route.Status != RouteStatus.GEPLANT) throw new ConflictException("Diese Route kann nicht mehr übernommen werden.");

        route.DriverId = driver.Id;
        route.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
    }

    /// <summary>Reverses ClaimAsync — lets a driver give an unstarted route back to the unassigned
    /// pool (e.g. sick) so another driver can claim the whole tour. Only allowed before loading
    /// starts, mirroring the GEPLANT-only restriction on ClaimAsync itself.</summary>
    public async Task<DeliveryRouteDto> ReleaseAsync(Guid routeId, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");
        if (route.Status != RouteStatus.GEPLANT) throw new ConflictException("Nur eine noch nicht gestartete Route kann zurückgegeben werden.");

        route.DriverId = null;
        route.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        // Not GetByIdAsync: its DRIVER-role check would now reject the caller, since DriverId was
        // just cleared — but the caller who released it is still entitled to see the result.
        var updated = await FullQuery(db).FirstAsync(r => r.Id == routeId, ct);
        return ToDto(updated);
    }

    /// <summary>Lets the driver holding a stop hand it directly to another driver's route for the
    /// same day, without office involvement — "Fahrer A kann kurzfristig nicht, Fahrer B übernimmt
    /// diesen Stopp". The stop (and its RouteStopItems, which follow via RouteStopId) moves onto the
    /// end of the target route; no accept step, the drivers coordinate the handoff themselves.</summary>
    public async Task<DeliveryRouteDto> TransferStopAsync(Guid routeId, Guid stopId, TransferStopDto dto, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");

        var stop = await db.RouteStops.FirstOrDefaultAsync(s => s.Id == stopId && s.RouteId == routeId, ct) ?? throw new NotFoundException(nameof(RouteStop), stopId);
        if (stop.Status != RouteStopStatus.OFFEN) throw new ConflictException("Nur ein noch offener Stopp kann übergeben werden.");

        if (dto.TargetRouteId == routeId) throw new ValidationException("Zielroute muss eine andere Route sein.");
        var targetRoute = await db.Routes.FirstOrDefaultAsync(r => r.Id == dto.TargetRouteId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), dto.TargetRouteId);
        if (targetRoute.Date != route.Date) throw new ValidationException("Zielroute muss für denselben Tag sein.");
        if (targetRoute.DriverId is null) throw new ValidationException("Zielroute hat noch keinen Fahrer — der Zielfahrer muss die Route zuerst übernehmen.");
        if (targetRoute.Status == RouteStatus.ABGESCHLOSSEN) throw new ConflictException("Zielroute ist bereits abgeschlossen.");

        var maxSequence = await db.RouteStops.Where(s => s.RouteId == targetRoute.Id).Select(s => (int?)s.SequenceNumber).MaxAsync(ct) ?? 0;
        stop.RouteId = targetRoute.Id;
        stop.SequenceNumber = maxSequence + 1;
        stop.PlannedArrivalTime = targetRoute.PlannedDepartureTime.Add(TimeSpan.FromMinutes(30 * stop.SequenceNumber));
        stop.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await GetByIdAsync(routeId, ct);
    }

    /// <summary>Replaces the removed Küche module's confirmation step — see
    /// DeliveryRoute.HandoffWarmConfirmed and the gate in UpdateStatusAsync.</summary>
    public async Task<DeliveryRouteDto> ConfirmHandoffAsync(Guid routeId, ConfirmHandoffDto dto, CancellationToken ct = default)
    {
        var driver = await GetCallerDriverAsync(ct);
        var route = await db.Routes.FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.DriverId != driver.Id) throw new ForbiddenException("Kein Zugriff auf diese Route.");

        route.HandoffWarmConfirmed = dto.WarmConfirmed;
        route.HandoffKaltConfirmed = dto.KaltConfirmed;
        route.HandoffDessertConfirmed = dto.DessertConfirmed;
        route.HandoffConfirmedAt = dto.WarmConfirmed && dto.KaltConfirmed && dto.DessertConfirmed ? DateTime.UtcNow : null;
        route.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
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
        Id = r.Id, Name = r.Name, Date = r.Date, DriverId = r.DriverId, DriverName = r.Driver?.User?.Name,
        LocationId = r.LocationId, LocationName = r.Location?.Name, PlannedDepartureTime = r.PlannedDepartureTime,
        PlannedReturnTime = r.PlannedReturnTime, DistanceKm = r.DistanceKm, Status = r.Status.ToString(),
        HandoffWarmConfirmed = r.HandoffWarmConfirmed, HandoffKaltConfirmed = r.HandoffKaltConfirmed,
        HandoffDessertConfirmed = r.HandoffDessertConfirmed, HandoffConfirmedAt = r.HandoffConfirmedAt,
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
