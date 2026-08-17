using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class StorageLocationHandler(IRepository<StorageLocation> locations, ITenantContext tenantContext)
{
    public async Task<List<StorageLocationDto>> ListAsync(CancellationToken ct = default) =>
        await locations.Query().OrderBy(l => l.Name).Select(l => new StorageLocationDto { Id = l.Id, Name = l.Name }).ToListAsync(ct);

    public async Task<StorageLocationDto> CreateAsync(SaveStorageLocationDto dto, CancellationToken ct = default)
    {
        var location = new StorageLocation { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, Name = dto.Name.Trim() };
        await locations.AddAsync(location, ct);
        await locations.SaveChangesAsync(ct);
        return new StorageLocationDto { Id = location.Id, Name = location.Name };
    }

    public async Task<StorageLocationDto> UpdateAsync(Guid id, SaveStorageLocationDto dto, CancellationToken ct = default)
    {
        var location = await locations.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(StorageLocation), id);
        location.Name = dto.Name.Trim();
        locations.Update(location);
        await locations.SaveChangesAsync(ct);
        return new StorageLocationDto { Id = location.Id, Name = location.Name };
    }
}
