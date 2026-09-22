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

    public async Task<PagedResult<DeliveryRouteDto>> ListAsync(DateOnly? date, DateOnly? dateFrom, DateOnly? dateTo, Guid? driverId, string? status, bool? unassigned, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (date is { } d) query = query.Where(r => r.Date == d);
        // dateFrom/dateTo: used by the week view (Wochenplanung) to fetch a whole KW (Mo–So) in one
        // call instead of paging through the unfiltered list client-side.
        if (dateFrom is { } df) query = query.Where(r => r.Date >= df);
        if (dateTo is { } dt) query = query.Where(r => r.Date <= dt);
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

        var (skippedFacilityNames, windowWarnings) = await BuildStopsAsync(route.Id, dto, ct);

        await db.SaveChangesAsync(ct);
        var result = await GetByIdAsync(route.Id, ct);
        result.SkippedClosedFacilities = skippedFacilityNames;
        result.ArrivalOutsideWindowWarnings = windowWarnings;
        return result;
    }

    /// <summary>Kurzfristig einen Sonderauftrag/Zusatzkunden an eine bestehende Route anhängen —
    /// anders als UpdateAsync auch möglich, während die Tour schon BELADUNG/UNTERWEGS ist (nur
    /// ABGESCHLOSSEN sperrt), da genau das der im Termin beschriebene Fall ist ("zusätzlicher
    /// Kunde/Sonderauftrag" kurz vor oder während der Abfahrt). Zieht wie beim regulären Aufbau die
    /// verbindlichen Bestellungen der Einrichtung für das Routendatum als Ladepositionen mit.</summary>
    public async Task<DeliveryRouteDto> AddStopAsync(Guid routeId, AddStopDto dto, CancellationToken ct = default)
    {
        var route = await db.Routes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == routeId, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), routeId);
        if (route.Status == RouteStatus.ABGESCHLOSSEN) throw new ConflictException("Eine abgeschlossene Route kann nicht mehr geändert werden.");
        if (route.Stops.Any(s => s.FacilityId == dto.FacilityId)) throw new ConflictException("Diese Einrichtung ist bereits Teil der Route.");

        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == dto.FacilityId, ct) ?? throw new NotFoundException(nameof(Facility), dto.FacilityId);
        var sequenceNumber = (route.Stops.Count == 0 ? 0 : route.Stops.Max(s => s.SequenceNumber)) + 1;
        var stop = new RouteStop
        {
            Id = Guid.NewGuid(), RouteId = route.Id, FacilityId = facility.Id, SequenceNumber = sequenceNumber,
            PlannedArrivalTime = route.PlannedDepartureTime.Add(TimeSpan.FromMinutes(30 * sequenceNumber)),
            DeliveryWindowStart = facility.DeliveryWindowStart, DeliveryWindowEnd = facility.DeliveryWindowEnd,
            ContactName = facility.ContactPerson, ContactPhone = facility.Phone, Note = facility.DeliveryRequirements,
            Status = RouteStopStatus.OFFEN, CreatedAt = DateTime.UtcNow,
        };
        db.RouteStops.Add(stop);

        var orderItems = await db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.FacilityId == facility.Id && BindingStatuses.Contains(oi.Order.Status) && oi.Date == route.Date)
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

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(routeId, ct);
    }

    /// <summary>Übernimmt eine komplette Woche (alle Routen von Montag–Sonntag) als Ausgangspunkt für
    /// eine andere Woche — gleicher Name/Fahrer/Standort/Abfahrt/Kundenliste, nur auf den
    /// entsprechenden Wochentag der Zielwoche verschoben. Stopps werden wie bei Create/Update frisch
    /// aus den aktuellen Einrichtungsdaten und Bestellungen der Zielwoche aufgebaut (nicht aus der
    /// Quellwoche kopiert), damit Schließtage/Bestellungen der Zielwoche korrekt berücksichtigt
    /// werden. Bereits existierende Routen (gleicher Name + Datum) in der Zielwoche werden
    /// übersprungen, damit ein versehentliches Doppelklicken keine Duplikate anlegt.</summary>
    public async Task<DuplicateWeekResultDto> DuplicateWeekAsync(DuplicateWeekDto dto, CancellationToken ct = default)
    {
        var sourceEnd = dto.SourceWeekStart.AddDays(6);
        var sourceRoutes = await db.Routes.Include(r => r.Stops)
            .Where(r => r.Date >= dto.SourceWeekStart && r.Date <= sourceEnd)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);

        var result = new DuplicateWeekResultDto();
        foreach (var source in sourceRoutes)
        {
            var offsetDays = source.Date.DayNumber - dto.SourceWeekStart.DayNumber;
            var targetDate = dto.TargetWeekStart.AddDays(offsetDays);

            if (await db.Routes.AnyAsync(r => r.Date == targetDate && r.Name == source.Name, ct))
            {
                result.SkippedExisting.Add($"{source.Name} ({targetDate:dd.MM.yyyy})");
                continue;
            }

            var facilityIds = source.Stops.OrderBy(s => s.SequenceNumber).Select(s => s.FacilityId).ToArray();
            var createDto = new CreateRouteDto
            {
                Name = source.Name, Date = targetDate, DriverId = source.DriverId, LocationId = source.LocationId,
                PlannedDepartureTime = source.PlannedDepartureTime, FacilityIds = facilityIds,
            };
            var newRoute = new DeliveryRoute
            {
                Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, Name = createDto.Name, Date = createDto.Date,
                DriverId = createDto.DriverId, LocationId = createDto.LocationId, PlannedDepartureTime = createDto.PlannedDepartureTime,
                Status = RouteStatus.GEPLANT,
            };
            db.Routes.Add(newRoute);
            await BuildStopsAsync(newRoute.Id, createDto, ct);
            result.CreatedCount++;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>Lets an admin edit a route they (or a colleague) created — name, date, driver,
    /// location, departure time and the facility/stop list. Only while still GEPLANT: once loading
    /// has started, stops carry driver progress (packed/loaded/delivered) that a rebuild would lose.
    /// Rebuilds the stop list from scratch the same way CreateAsync does (cascade-deletes the old
    /// stops and their items, see RouteStopConfiguration/RouteStopItemConfiguration).</summary>
    public async Task<DeliveryRouteDto> UpdateAsync(Guid id, CreateRouteDto dto, CancellationToken ct = default)
    {
        var route = await db.Routes.Include(r => r.Stops).FirstOrDefaultAsync(r => r.Id == id, ct) ?? throw new NotFoundException(nameof(DeliveryRoute), id);
        if (route.Status != RouteStatus.GEPLANT) throw new ConflictException("Nur eine noch nicht gestartete Route kann bearbeitet werden.");

        route.Name = dto.Name;
        route.Date = dto.Date;
        route.DriverId = dto.DriverId;
        route.LocationId = dto.LocationId;
        route.PlannedDepartureTime = dto.PlannedDepartureTime;
        route.UpdatedAt = DateTime.UtcNow;

        db.RouteStops.RemoveRange(route.Stops);
        var (skippedFacilityNames, windowWarnings) = await BuildStopsAsync(route.Id, dto, ct);

        await db.SaveChangesAsync(ct);
        var result = await GetByIdAsync(id, ct);
        result.SkippedClosedFacilities = skippedFacilityNames;
        result.ArrivalOutsideWindowWarnings = windowWarnings;
        return result;
    }

    /// <summary>Shared by CreateAsync/UpdateAsync: adds a RouteStop (+ its RouteStopItems from
    /// binding orders of that date) per requested facility, skipping ones closed that day and
    /// warning about ones whose estimated arrival misses their delivery window. Does not save.</summary>
    private async Task<(List<string> skippedFacilityNames, List<string> windowWarnings)> BuildStopsAsync(Guid routeId, CreateRouteDto dto, CancellationToken ct)
    {
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
                Id = Guid.NewGuid(), RouteId = routeId, FacilityId = facility.Id, SequenceNumber = sequenceNumber,
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

        return (skippedFacilityNames, windowWarnings);
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
