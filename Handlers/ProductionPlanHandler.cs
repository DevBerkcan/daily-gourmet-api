using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Production;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace DailyGourmet.Api.Handlers;

public class ProductionPlanHandler(DailyGourmetDbContext db, ITenantContext tenantContext)
{
    private static readonly OrderStatus[] BindingStatuses = [OrderStatus.SUBMITTED, OrderStatus.CONFIRMED, OrderStatus.LOCKED];

    private static IQueryable<ProductionPlan> FullQuery(DailyGourmetDbContext db) => db.ProductionPlans
        .Include(p => p.Location).Include(p => p.Items).ThenInclude(i => i.Recipe);

    public async Task<PagedResult<ProductionPlanDto>> ListAsync(DateOnly? from, DateOnly? to, Guid? locationId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = FullQuery(db).AsQueryable();
        if (from is { } f) query = query.Where(p => p.Date >= f);
        if (to is { } t) query = query.Where(p => p.Date <= t);
        if (locationId is { } lid) query = query.Where(p => p.LocationId == lid);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(p => p.Date).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ProductionPlanDto> { Items = items.Select(ToDto).ToList(), Total = total, Page = page, PageSize = pageSize };
    }

    public async Task<ProductionPlanDto> GetByDateAsync(DateOnly date, Guid locationId, CancellationToken ct = default) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(p => p.Date == date && p.LocationId == locationId, ct) ?? throw new NotFoundException(nameof(ProductionPlan), date));

    public async Task<ProductionPlanDto> CreateAsync(CreateProductionPlanDto dto, CancellationToken ct = default)
    {
        var plan = new ProductionPlan { Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, Date = dto.Date, LocationId = dto.LocationId };
        db.ProductionPlans.Add(plan);

        var quantities = await AggregateOrderedQuantitiesAsync(dto.Date, dto.LocationId, ct);
        foreach (var (recipeId, quantity) in quantities)
            db.ProductionPlanItems.Add(new ProductionPlanItem { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, RecipeId = recipeId, OrderedQuantity = quantity, Status = ProductionItemStatus.PLANNED, WorkStatus = WorkStatus.OFFEN, CreatedAt = DateTime.UtcNow });

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("IX_ProductionPlans_TenantId_Date_LocationId") == true)
        {
            throw new ConflictException("Für diesen Tag/Standort existiert bereits ein Produktionsplan.");
        }
        return await GetByIdInternalAsync(plan.Id, ct);
    }

    public async Task<ProductionPlanDto> UpdateItemAsync(Guid planId, Guid itemId, UpdateProductionPlanItemDto dto, CancellationToken ct = default)
    {
        var item = await db.ProductionPlanItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ProductionPlanId == planId, ct)
            ?? throw new NotFoundException(nameof(ProductionPlanItem), itemId);

        if (dto.Status is not null && Enum.TryParse<ProductionItemStatus>(dto.Status, out var status)) item.Status = status;
        if (dto.WorkStatus is not null && Enum.TryParse<WorkStatus>(dto.WorkStatus, out var workStatus)) item.WorkStatus = workStatus;
        if (dto.StagedQuantity is not null) item.StagedQuantity = dto.StagedQuantity;
        if (dto.Workstation is not null) item.Workstation = dto.Workstation;
        if (dto.Equipment is not null) item.Equipment = dto.Equipment;
        if (dto.StartTime is not null) item.StartTime = dto.StartTime;
        if (dto.FinishByTime is not null) item.FinishByTime = dto.FinishByTime;
        if (dto.BatchCount is not null) item.BatchCount = dto.BatchCount;
        if (dto.PortionsPerBatch is not null) item.PortionsPerBatch = dto.PortionsPerBatch;
        if (dto.ResponsiblePerson is not null) item.ResponsiblePerson = dto.ResponsiblePerson;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdInternalAsync(planId, ct);
    }

    public async Task<ProductionPlanDto> AddAdjustmentAsync(Guid planId, Guid itemId, ProductionAdjustmentRequestDto dto, CancellationToken ct = default)
    {
        var item = await db.ProductionPlanItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ProductionPlanId == planId, ct)
            ?? throw new NotFoundException(nameof(ProductionPlanItem), itemId);

        if (dto.Quantity != item.AdjustmentQuantity && dto.Quantity != 0 && string.IsNullOrWhiteSpace(dto.Reason))
            throw new ValidationException("Eine Begründung ist für Zusatzmengen erforderlich.");

        db.ProductionAdjustments.Add(new ProductionAdjustment
        {
            Id = Guid.NewGuid(), ProductionPlanItemId = item.Id, OldQuantity = item.AdjustmentQuantity, NewQuantity = dto.Quantity,
            Reason = dto.Reason, ChangedByUserId = tenantContext.UserId!.Value, ChangedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow,
        });
        item.AdjustmentQuantity = dto.Quantity;
        item.AdjustmentReason = dto.Reason;
        item.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return await GetByIdInternalAsync(planId, ct);
    }

    public async Task<ProductionPlanDto> RefreshAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.ProductionPlans.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw new NotFoundException(nameof(ProductionPlan), planId);
        var quantities = await AggregateOrderedQuantitiesAsync(plan.Date, plan.LocationId, ct);

        foreach (var item in plan.Items)
        {
            item.OrderedQuantity = quantities.TryGetValue(item.RecipeId, out var qty) ? qty : 0;
            item.UpdatedAt = DateTime.UtcNow;
        }
        foreach (var (recipeId, quantity) in quantities)
            if (!plan.Items.Any(i => i.RecipeId == recipeId))
                db.ProductionPlanItems.Add(new ProductionPlanItem { Id = Guid.NewGuid(), ProductionPlanId = plan.Id, RecipeId = recipeId, OrderedQuantity = quantity, Status = ProductionItemStatus.PLANNED, WorkStatus = WorkStatus.OFFEN, CreatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync(ct);
        return await GetByIdInternalAsync(planId, ct);
    }

    public async Task<List<IngredientRequirementDto>> RequirementsAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.ProductionPlans.Include(p => p.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient).ThenInclude(i => i.Category).ThenInclude(c => c.DefaultStorageLocation)
            .FirstOrDefaultAsync(p => p.Id == planId, ct) ?? throw new NotFoundException(nameof(ProductionPlan), planId);

        var aggregate = new Dictionary<(Guid IngredientId, Unit Unit), (decimal Total, Ingredient Ingredient, HashSet<string> Recipes)>();
        foreach (var item in plan.Items)
        {
            if (item.Recipe.StandardPortions <= 0) continue;
            var scaleFactor = (decimal)(item.OrderedQuantity + item.AdjustmentQuantity) / item.Recipe.StandardPortions;
            foreach (var ri in item.Recipe.Ingredients)
            {
                var key = (ri.IngredientId, ri.Unit);
                var scaledQuantity = ri.Quantity * scaleFactor;
                if (aggregate.TryGetValue(key, out var existing))
                {
                    existing.Recipes.Add(item.Recipe.Name);
                    aggregate[key] = (existing.Total + scaledQuantity, existing.Ingredient, existing.Recipes);
                }
                else
                {
                    aggregate[key] = (scaledQuantity, ri.Ingredient, [item.Recipe.Name]);
                }
            }
        }

        return aggregate.Select(kv => new IngredientRequirementDto
        {
            IngredientId = kv.Key.IngredientId,
            IngredientName = kv.Value.Ingredient.Name,
            CategoryName = kv.Value.Ingredient.Category?.Name ?? string.Empty,
            Unit = kv.Key.Unit.ToString(),
            TotalQuantity = decimal.Round(kv.Value.Total, 2, MidpointRounding.AwayFromZero),
            StorageLocationName = kv.Value.Ingredient.Category?.DefaultStorageLocation?.Name,
            ContributingRecipeNames = kv.Value.Recipes.ToArray(),
        }).OrderBy(r => r.StorageLocationName).ThenBy(r => r.IngredientName).ToList();
    }

    private async Task<Dictionary<Guid, int>> AggregateOrderedQuantitiesAsync(DateOnly date, Guid locationId, CancellationToken ct)
    {
        var rows = await db.OrderItems
            .Where(oi => oi.Date == date && BindingStatuses.Contains(oi.Order.Status) && oi.Order.Facility.LocationId == locationId)
            .GroupBy(oi => oi.RecipeId)
            .Select(g => new { RecipeId = g.Key, Total = g.Sum(x => x.Portions) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.RecipeId, r => r.Total);
    }

    private async Task<ProductionPlanDto> GetByIdInternalAsync(Guid id, CancellationToken ct) =>
        ToDto(await FullQuery(db).FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException(nameof(ProductionPlan), id));

    private static ProductionPlanDto ToDto(ProductionPlan p) => new()
    {
        Id = p.Id, Date = p.Date, LocationId = p.LocationId, LocationName = p.Location?.Name ?? string.Empty,
        Items = p.Items.Select(i => new ProductionPlanItemDto
        {
            Id = i.Id, RecipeId = i.RecipeId, RecipeName = i.Recipe?.Name ?? string.Empty,
            OrderedQuantity = i.OrderedQuantity, AdjustmentQuantity = i.AdjustmentQuantity, AdjustmentReason = i.AdjustmentReason,
            Status = i.Status.ToString(), WorkStatus = i.WorkStatus.ToString(), StagedQuantity = i.StagedQuantity,
            Workstation = i.Workstation, Equipment = i.Equipment, StartTime = i.StartTime, FinishByTime = i.FinishByTime,
            BatchCount = i.BatchCount, PortionsPerBatch = i.PortionsPerBatch, ResponsiblePerson = i.ResponsiblePerson,
        }).ToList(),
    };
}
