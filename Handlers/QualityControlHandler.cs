using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class QualityControlHandler(IRepository<QualityControl> controls, ITenantContext tenantContext)
{
    public async Task<PagedResult<QualityControlDto>> ListAsync(Guid? productionPlanId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = controls.Query().Include(c => c.PerformedByUser).AsQueryable();
        if (productionPlanId is { } pid) query = query.Where(c => c.ProductionPlanId == pid);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.PerformedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<QualityControlDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<QualityControlDto> CreateAsync(CreateQualityControlDto dto, CancellationToken ct = default)
    {
        if (!Enum.TryParse<ControlType>(dto.Type, out var type)) throw new ValidationException("Ungültiger Kontrolltyp.");
        if (!Enum.TryParse<ControlStatus>(dto.Status, out var status)) throw new ValidationException("Ungültiger Status.");

        var control = new QualityControl
        {
            Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, ProductionPlanId = dto.ProductionPlanId,
            Type = type, Area = dto.Area, TargetValue = dto.TargetValue, MeasuredValue = dto.MeasuredValue,
            PerformedByUserId = tenantContext.UserId!.Value, PerformedAt = DateTime.UtcNow, Status = status,
        };
        await controls.AddAsync(control, ct);
        await controls.SaveChangesAsync(ct);
        control = await controls.Query().Include(c => c.PerformedByUser).FirstAsync(c => c.Id == control.Id, ct);
        return ToDto(control);
    }

    private static QualityControlDto ToDto(QualityControl c) => new()
    {
        Id = c.Id, ProductionPlanId = c.ProductionPlanId, Type = c.Type.ToString(), Area = c.Area,
        TargetValue = c.TargetValue, MeasuredValue = c.MeasuredValue, PerformedByUserName = c.PerformedByUser?.Name ?? string.Empty,
        PerformedAt = c.PerformedAt, Status = c.Status.ToString(),
    };
}
