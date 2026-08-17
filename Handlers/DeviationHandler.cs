using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class DeviationHandler(IRepository<Deviation> deviations, ITenantContext tenantContext)
{
    public async Task<PagedResult<DeviationDto>> ListAsync(Guid? productionPlanId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = deviations.Query().Include(d => d.ReportedByUser).AsQueryable();
        if (productionPlanId is { } pid) query = query.Where(d => d.ProductionPlanId == pid);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<DeviationStatus>(status, out var s)) query = query.Where(d => d.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(d => d.ReportedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<DeviationDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<DeviationDto> CreateAsync(CreateDeviationDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<DeviationCategory>(dto.Category, out var category)) throw new ValidationException("Ungültige Kategorie.");
        var deviation = new Deviation
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, ProductionPlanId = dto.ProductionPlanId,
            Category = category, Subject = dto.Subject, Quantity = dto.Quantity, Action = dto.Action,
            ReportedByUserId = tenantContext.UserId!.Value, ReportedAt = DateTime.UtcNow, Status = DeviationStatus.OFFEN,
        };
        await deviations.AddAsync(deviation, ct);
        await deviations.SaveChangesAsync(ct);
        deviation = await deviations.Query().Include(d => d.ReportedByUser).FirstAsync(d => d.Id == deviation.Id, ct);
        return ToDto(deviation);
    }

    public async Task<DeviationDto> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        var deviation = await deviations.GetByIdAsync(id, ct) ?? throw new NotFoundException(nameof(Deviation), id);
        deviation.Status = DeviationStatus.GEKLAERT;
        deviation.ResolvedAt = DateTime.UtcNow;
        deviation.ResolvedByUserId = tenantContext.UserId;
        deviations.Update(deviation);
        await deviations.SaveChangesAsync(ct);
        deviation = await deviations.Query().Include(d => d.ReportedByUser).FirstAsync(d => d.Id == id, ct);
        return ToDto(deviation);
    }

    private static DeviationDto ToDto(Deviation d) => new()
    {
        Id = d.Id, ProductionPlanId = d.ProductionPlanId, Category = d.Category.ToString(), Subject = d.Subject,
        Quantity = d.Quantity, Action = d.Action, ReportedByUserName = d.ReportedByUser?.Name ?? string.Empty,
        ReportedAt = d.ReportedAt, Status = d.Status.ToString(), ResolvedAt = d.ResolvedAt,
    };
}
