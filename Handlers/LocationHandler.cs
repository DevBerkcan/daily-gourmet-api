using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Tenants;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class LocationHandler(IRepository<Location> locations, ITenantContext tenantContext)
{
    public async Task<PagedResult<LocationDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = locations.Query();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(l => l.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<LocationDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<LocationDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await locations.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Location), id));

    public async Task<LocationDto> CreateAsync(SaveLocationDto dto, CancellationToken ct = default)
    {
        var location = new Location { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, Name = dto.Name.Trim(), Address = dto.Address, ContactPerson = dto.ContactPerson, CapacityPortions = dto.CapacityPortions, Status = LocationStatus.AKTIV };
        await locations.AddAsync(location, ct);
        await locations.SaveChangesAsync(ct);
        return ToDto(location);
    }

    public async Task<LocationDto> UpdateAsync(Guid id, UpdateLocationDto dto, CancellationToken ct = default)
    {
        var location = await locations.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Location), id);
        if (!Enum.TryParse<LocationStatus>(dto.Status, out var status)) throw new ValidationException("Ungültiger Status.");
        location.Name = dto.Name.Trim();
        location.Address = dto.Address;
        location.ContactPerson = dto.ContactPerson;
        location.CapacityPortions = dto.CapacityPortions;
        location.Status = status;
        locations.Update(location);
        await locations.SaveChangesAsync(ct);
        return ToDto(location);
    }

    private static LocationDto ToDto(Location l) => new() { Id = l.Id, Name = l.Name, Address = l.Address, ContactPerson = l.ContactPerson, CapacityPortions = l.CapacityPortions, Status = l.Status.ToString() };
}
