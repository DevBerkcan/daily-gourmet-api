using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Facilities;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class FacilityHandler(
    IRepository<Facility> facilities,
    IRepository<Location> locations,
    ITenantSettingsRepository tenantSettings,
    ITenantContext tenantContext)
{
    public async Task<PagedResult<FacilityDto>> ListAsync(string? search, Guid? locationId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = facilities.Query().Include(f => f.Location).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(f => f.Name.Contains(search) || f.ContactPerson.Contains(search));
        if (locationId is { } lid)
            query = query.Where(f => f.LocationId == lid);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(f => f.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => ToDto(f))
            .ToListAsync(ct);

        return new PagedResult<FacilityDto> { Items = items, Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<FacilityDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var facility = await facilities.Query().Include(f => f.Location).FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new NotFoundException(nameof(Facility), id);
        if (tenantContext.FacilityId is { } own && own != id)
            throw new ForbiddenException("Kein Zugriff auf eine andere Einrichtung.");
        return ToDto(facility);
    }

    public async Task<FacilityDto> CreateAsync(Guid tenantId, CreateFacilityDto dto, CancellationToken ct = default)
    {
        if (await locations.GetByIdAsync(dto.LocationId, ct) is null)
            throw new ValidationException("Der ausgewählte Standort existiert nicht.");

        var settings = await tenantSettings.GetAsync(tenantId, ct);
        var prefix = settings?.FacilityNumberPrefix ?? "DG-1";
        var nextSequence = await facilities.Query().CountAsync(ct) + 1;
        var customerNumber = $"{prefix}{nextSequence:000}";

        var facility = new Facility
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LocationId = dto.LocationId,
            Name = dto.Name.Trim(),
            CustomerNumber = customerNumber,
            Address = dto.Address.Trim(),
            ContactPerson = dto.ContactPerson.Trim(),
            Email = dto.Email.Trim(),
            Phone = dto.Phone.Trim(),
            ActiveWeekdays = dto.ActiveWeekdays,
            PortionPrice = dto.PortionPrice,
            Status = FacilityStatus.AKTIV,
            Notes = dto.Notes,
        };
        await facilities.AddAsync(facility, ct);
        await facilities.SaveChangesAsync(ct);

        facility.Location = (await locations.GetByIdAsync(dto.LocationId, ct))!;
        return ToDto(facility);
    }

    public async Task<FacilityDto> UpdateAsync(Guid id, UpdateFacilityDto dto, CancellationToken ct = default)
    {
        var facility = await facilities.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Facility), id);
        if (await locations.GetByIdAsync(dto.LocationId, ct) is null)
            throw new ValidationException("Der ausgewählte Standort existiert nicht.");
        if (!Enum.TryParse<FacilityStatus>(dto.Status, out var status))
            throw new ValidationException("Ungültiger Status.");

        facility.LocationId = dto.LocationId;
        facility.Name = dto.Name.Trim();
        facility.Address = dto.Address.Trim();
        facility.ContactPerson = dto.ContactPerson.Trim();
        facility.Email = dto.Email.Trim();
        facility.Phone = dto.Phone.Trim();
        facility.ActiveWeekdays = dto.ActiveWeekdays;
        facility.PortionPrice = dto.PortionPrice;
        facility.Status = status;
        facility.Notes = dto.Notes;

        facilities.Update(facility);
        await facilities.SaveChangesAsync(ct);

        facility.Location = (await locations.GetByIdAsync(dto.LocationId, ct))!;
        return ToDto(facility);
    }

    private static FacilityDto ToDto(Facility f) => new()
    {
        Id = f.Id,
        Name = f.Name,
        CustomerNumber = f.CustomerNumber,
        Address = f.Address,
        ContactPerson = f.ContactPerson,
        Email = f.Email,
        Phone = f.Phone,
        LocationId = f.LocationId,
        LocationName = f.Location?.Name ?? string.Empty,
        ActiveWeekdays = f.ActiveWeekdays,
        PortionPrice = f.PortionPrice,
        Status = f.Status.ToString(),
        Notes = f.Notes,
    };
}
