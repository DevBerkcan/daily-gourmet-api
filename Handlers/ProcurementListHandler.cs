using System.Text;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Procurement;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class ProcurementListHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly string[] StatusOrder = ["DRAFT", "REVIEWED", "ORDERED", "COMPLETED"];

    private static IQueryable<ProcurementList> FullQuery(DailyGourmetDbContext db) => db.ProcurementLists
        .Include(l => l.Location)
        .Include(l => l.Items).ThenInclude(i => i.Ingredient).ThenInclude(i => i.Category)
        .Include(l => l.Items).ThenInclude(i => i.Ingredient).ThenInclude(i => i.Supplier);

    public async Task<PagedResult<ProcurementListDto>> ListAsync(Guid? locationId, int? calendarWeek, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (locationId is { } lid) query = query.Where(l => l.LocationId == lid);
        if (calendarWeek is { } w) query = query.Where(l => l.CalendarWeek == w);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ProcurementListStatus>(status, out var s)) query = query.Where(l => l.Status == s);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(l => l.CalendarWeek).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ProcurementListDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<ProcurementListDto> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id));

    public async Task<ProcurementListDto> GenerateAsync(GenerateProcurementListDto dto, CancellationToken ct = default)
    {
        var plan = await db.ProductionPlans.Include(p => p.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == dto.ProductionPlanId, ct) ?? throw new NotFoundException(nameof(ProductionPlan), dto.ProductionPlanId);

        var list = new ProcurementList { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, LocationId = dto.LocationId, Label = dto.Label, CalendarWeek = dto.CalendarWeek, Status = ProcurementListStatus.DRAFT };
        db.ProcurementLists.Add(list);

        var aggregate = new Dictionary<(Guid IngredientId, Unit Unit), (decimal Total, decimal ConversionFactor)>();
        foreach (var item in plan.Items)
        {
            if (item.Recipe.StandardPortions <= 0) continue;
            var scaleFactor = (decimal)(item.OrderedQuantity + item.AdjustmentQuantity) / item.Recipe.StandardPortions;
            foreach (var ri in item.Recipe.Ingredients)
            {
                var key = (ri.IngredientId, ri.Unit);
                var scaled = ri.Quantity * scaleFactor;
                aggregate[key] = aggregate.TryGetValue(key, out var existing)
                    ? (existing.Total + scaled, existing.ConversionFactor)
                    : (scaled, ri.Ingredient.ConversionFactor);
            }
        }

        foreach (var kv in aggregate)
        {
            if (kv.Value.ConversionFactor <= 0) continue;
            db.ProcurementListItems.Add(new ProcurementListItem
            {
                Id = Guid.NewGuid(), ProcurementListId = list.Id, IngredientId = kv.Key.IngredientId, Unit = kv.Key.Unit,
                TotalQuantityBase = decimal.Round(kv.Value.Total, 3), PurchaseQuantity = Math.Ceiling(kv.Value.Total / kv.Value.ConversionFactor),
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(list.Id, ct);
    }

    public async Task<ProcurementListDto> UpdateItemAsync(Guid listId, Guid itemId, UpdateProcurementItemDto dto, CancellationToken ct = default)
    {
        var list = await db.ProcurementLists.FirstOrDefaultAsync(l => l.Id == listId, ct) ?? throw new NotFoundException(nameof(ProcurementList), listId);
        if (list.Status is not (ProcurementListStatus.DRAFT or ProcurementListStatus.REVIEWED))
            throw new ConflictException("Positionen können in diesem Status nicht mehr geändert werden.");

        var item = await db.ProcurementListItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ProcurementListId == listId, ct) ?? throw new NotFoundException(nameof(ProcurementListItem), itemId);
        item.PurchaseQuantity = dto.PurchaseQuantity;
        item.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(listId, ct);
    }

    public async Task<ProcurementListDto> UpdateStatusAsync(Guid id, UpdateStatusDto dto, CancellationToken ct = default)
    {
        var list = await db.ProcurementLists.FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id);
        if (!Enum.TryParse<ProcurementListStatus>(dto.Status, out var target)) throw new ValidationException("Ungültiger Status.");

        var currentIndex = Array.IndexOf(StatusOrder, list.Status.ToString());
        var targetIndex = Array.IndexOf(StatusOrder, target.ToString());
        if (targetIndex != currentIndex + 1) throw new ConflictException("Statuswechsel ist nur schrittweise vorwärts erlaubt.");

        list.Status = target;
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    public async Task<byte[]> ExportCsvAsync(Guid id, CancellationToken ct = default)
    {
        var list = await FullQuery(db).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id);
        var sb = new StringBuilder();
        sb.AppendLine("Artikelnummer;Zutat;Bedarf;Einheit;Bestellmenge;Lieferant");
        foreach (var item in list.Items)
            sb.AppendLine($"{item.Ingredient.ArticleNumber};{item.Ingredient.Name};{item.TotalQuantityBase};{item.Unit};{item.PurchaseQuantity};{item.Ingredient.Supplier?.Name}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static ProcurementListDto ToDto(ProcurementList l) => new()
    {
        Id = l.Id, Label = l.Label, CalendarWeek = l.CalendarWeek, LocationId = l.LocationId, LocationName = l.Location?.Name ?? string.Empty, Status = l.Status.ToString(),
        Items = l.Items.Select(i => new ProcurementListItemDto
        {
            Id = i.Id, IngredientId = i.IngredientId, IngredientName = i.Ingredient?.Name ?? string.Empty, IngredientArticleNumber = i.Ingredient?.ArticleNumber ?? string.Empty,
            CategoryName = i.Ingredient?.Category?.Name ?? string.Empty, SupplierName = i.Ingredient?.Supplier?.Name,
            Unit = i.Unit.ToString(), TotalQuantityBase = i.TotalQuantityBase, PurchaseQuantity = i.PurchaseQuantity,
        }).ToList(),
    };
}
