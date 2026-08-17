using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class SupplierHandler(IRepository<Supplier> suppliers, ITenantContext tenantContext)
{
    public async Task<PagedResult<SupplierDto>> ListAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = suppliers.Query();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(s => s.Name.Contains(search));
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.Name).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SupplierDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<SupplierDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await suppliers.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Supplier), id));

    public async Task<SupplierDto> CreateAsync(SaveSupplierDto dto, CancellationToken ct = default)
    {
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value,
            Name = dto.Name.Trim(), ContactPerson = dto.ContactPerson, Phone = dto.Phone, Email = dto.Email,
        };
        await suppliers.AddAsync(supplier, ct);
        await suppliers.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, SaveSupplierDto dto, CancellationToken ct = default)
    {
        var supplier = await suppliers.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Supplier), id);
        supplier.Name = dto.Name.Trim();
        supplier.ContactPerson = dto.ContactPerson;
        supplier.Phone = dto.Phone;
        supplier.Email = dto.Email;
        suppliers.Update(supplier);
        await suppliers.SaveChangesAsync(ct);
        return ToDto(supplier);
    }

    private static SupplierDto ToDto(Supplier s) => new() { Id = s.Id, Name = s.Name, ContactPerson = s.ContactPerson, Phone = s.Phone, Email = s.Email };
}
