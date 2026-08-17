using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Logistics;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class DriverHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    public async Task<PagedResult<DriverDto>> ListAsync(int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Set<Driver>().Include(d => d.User).AsQueryable();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(d => d.User.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<DriverDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<DriverDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await db.Set<Driver>().Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException(nameof(Driver), id));

    public async Task<DriverDto> CreateAsync(SaveDriverDto dto, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == dto.UserId, ct) ?? throw new ValidationException("Benutzer existiert nicht.");
        if (user.Role != Role.DRIVER) throw new ValidationException("Der Benutzer muss die Rolle DRIVER haben.");

        var driver = new Driver { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, UserId = dto.UserId, Phone = dto.Phone, VehicleDescription = dto.VehicleDescription, LicensePlate = dto.LicensePlate };
        db.Set<Driver>().Add(driver);
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(driver.Id, ct);
    }

    public async Task<DriverDto> UpdateAsync(Guid id, SaveDriverDto dto, CancellationToken ct = default)
    {
        var driver = await db.Set<Driver>().FirstOrDefaultAsync(d => d.Id == id, ct) ?? throw new NotFoundException(nameof(Driver), id);
        driver.Phone = dto.Phone;
        driver.VehicleDescription = dto.VehicleDescription;
        driver.LicensePlate = dto.LicensePlate;
        driver.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    private static DriverDto ToDto(Driver d) => new() { Id = d.Id, UserId = d.UserId, UserName = d.User?.Name ?? string.Empty, Phone = d.Phone, VehicleDescription = d.VehicleDescription, LicensePlate = d.LicensePlate };
}
