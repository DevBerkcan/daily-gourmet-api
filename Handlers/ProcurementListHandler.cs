using System.Security.Cryptography;
using System.Text;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Procurement;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Options;
using DailyGourmet.Api.Services;
using DailyGourmet.Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DailyGourmet.Api.Handlers;

public class ProcurementListHandler(DailyGourmetDbContext db, ITenantContext tenantContext, IEmailService emailService, IPdfService pdfService, IOptions<AppOptions> appOptions, IFeatureFlagService featureFlags)
{
    private static readonly string[] StatusOrder = ["DRAFT", "REVIEWED", "READY_FOR_APPROVAL", "APPROVED", "ORDERED", "COMPLETED"];

    private static IQueryable<ProcurementList> FullQuery(DailyGourmetDbContext db) => db.ProcurementLists
        .Include(l => l.Location)
        .Include(l => l.Supplier)
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

    /// <summary>Generates one ProcurementList per supplier ("pro Einkaufsliste ein Lieferant") —
    /// each aggregated ingredient line is assigned to its currently cheapest known supplier
    /// (IngredientSupplierPrice), enforced here at generation time rather than as a DB constraint.
    /// Ingredients with no supplier price at all land in a single catch-all list (SupplierId=null).
    /// Returns the first created list's id — callers list all of them via ListAsync(calendarWeek:).</summary>
    public async Task<ProcurementListDto> GenerateAsync(GenerateProcurementListDto dto, CancellationToken ct = default)
    {
        if (!await featureFlags.IsEnabledAsync(tenantContext.TenantId!.Value, "einkaufslisten", ct))
            throw new ForbiddenException("Einkaufslisten sind für Ihren Mandanten nicht aktiviert.");

        var plan = await db.ProductionPlans.Include(p => p.Items).ThenInclude(i => i.Recipe).ThenInclude(r => r.Ingredients).ThenInclude(ri => ri.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == dto.ProductionPlanId, ct) ?? throw new NotFoundException(nameof(ProductionPlan), dto.ProductionPlanId);

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

        var ingredientIds = aggregate.Keys.Select(k => k.IngredientId).Distinct().ToList();
        var cheapestSupplierByIngredient = await db.IngredientSupplierPrices
            .Where(p => ingredientIds.Contains(p.IngredientId))
            .Include(p => p.Supplier)
            .GroupBy(p => p.IngredientId)
            .Select(g => g.OrderBy(p => p.Price).First())
            .ToDictionaryAsync(p => p.IngredientId, p => (p.SupplierId, p.Supplier.Name), ct);

        var bySupplier = aggregate
            .Where(kv => kv.Value.ConversionFactor > 0)
            .GroupBy(kv => cheapestSupplierByIngredient.TryGetValue(kv.Key.IngredientId, out var s) ? s.SupplierId : (Guid?)null);

        ProcurementList? firstList = null;
        foreach (var group in bySupplier)
        {
            var supplierId = group.Key;
            var supplierName = supplierId is { } sid ? cheapestSupplierByIngredient.First(kv => kv.Value.SupplierId == sid).Value.Name : "ohne Lieferant";
            var list = new ProcurementList
            {
                Id = Guid.NewGuid(), TenantId = tenantContext.TenantId!.Value, LocationId = dto.LocationId, SupplierId = supplierId,
                Label = $"{dto.Label} – {supplierName}", CalendarWeek = dto.CalendarWeek, Status = ProcurementListStatus.DRAFT,
            };
            db.ProcurementLists.Add(list);
            firstList ??= list;

            foreach (var kv in group)
            {
                db.ProcurementListItems.Add(new ProcurementListItem
                {
                    Id = Guid.NewGuid(), ProcurementListId = list.Id, IngredientId = kv.Key.IngredientId, Unit = kv.Key.Unit,
                    TotalQuantityBase = decimal.Round(kv.Value.Total, 3), PurchaseQuantity = Math.Ceiling(kv.Value.Total / kv.Value.ConversionFactor),
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync(ct);
        return firstList is null ? throw new ValidationException("Keine Zutaten zum Einkaufen gefunden.") : await GetByIdAsync(firstList.Id, ct);
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
        var list = await db.ProcurementLists.Include(l => l.Tenant).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id);
        if (!Enum.TryParse<ProcurementListStatus>(dto.Status, out var target)) throw new ValidationException("Ungültiger Status.");

        var currentIndex = Array.IndexOf(StatusOrder, list.Status.ToString());
        var targetIndex = Array.IndexOf(StatusOrder, target.ToString());
        if (targetIndex != currentIndex + 1) throw new ConflictException("Statuswechsel ist nur schrittweise vorwärts erlaubt.");

        list.Status = target;
        list.UpdatedAt = DateTime.UtcNow;

        if (target == ProcurementListStatus.READY_FOR_APPROVAL)
        {
            list.ApprovalToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            list.ApprovalTokenExpiresAt = DateTime.UtcNow.AddHours(48);
            await db.SaveChangesAsync(ct);
            await SendApprovalRequestEmailAsync(list, ct);
            return await GetByIdAsync(id, ct);
        }

        await db.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct);
    }

    /// <summary>Token-authorized approval — deliberately bypasses the normal [Authorize] pipeline
    /// (see the [AllowAnonymous] controller action) so Armin can approve straight from the emailed
    /// link without logging in, matching how he actually works today (phone/print, not the app).
    /// Trade-off: a bearer token in a URL is simpler than a true magic-link login but risks exposure
    /// via forwarding/logs — acceptable given the short expiry and single-use invalidation below;
    /// revisit if this ever needs to be audit-grade. IgnoreQueryFilters() is required because an
    /// anonymous request has no tenant claim for the normal query filter to match against — the
    /// token itself (not tenant membership) is what proves the caller is authorized here.</summary>
    public async Task<ProcurementListDto> ApproveAsync(Guid id, string token, CancellationToken ct = default)
    {
        var list = await db.ProcurementLists.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id);
        if (list.Status != ProcurementListStatus.READY_FOR_APPROVAL)
            throw new ConflictException("Diese Einkaufsliste wartet nicht (mehr) auf eine Freigabe.");
        if (string.IsNullOrEmpty(list.ApprovalToken) || list.ApprovalToken != token)
            throw new ForbiddenException("Ungültiger Freigabe-Link.");
        if (list.ApprovalTokenExpiresAt is null || list.ApprovalTokenExpiresAt < DateTime.UtcNow)
            throw new ConflictException("Dieser Freigabe-Link ist abgelaufen.");

        list.Status = ProcurementListStatus.APPROVED;
        list.ApprovalToken = null;
        list.ApprovalTokenExpiresAt = null;
        list.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var result = await db.ProcurementLists.IgnoreQueryFilters()
            .Include(l => l.Location).Include(l => l.Supplier)
            .Include(l => l.Items).ThenInclude(i => i.Ingredient).ThenInclude(i => i.Category)
            .Include(l => l.Items).ThenInclude(i => i.Ingredient).ThenInclude(i => i.Supplier)
            .FirstAsync(l => l.Id == id, ct);
        return ToDto(result);
    }

    private async Task SendApprovalRequestEmailAsync(ProcurementList list, CancellationToken ct)
    {
        var itemCount = await db.ProcurementListItems.CountAsync(i => i.ProcurementListId == list.Id, ct);
        var baseUrl = appOptions.Value.PublicBaseUrl.TrimEnd('/');
        var approveUrl = $"{baseUrl}/procurement/approve?id={list.Id}&token={list.ApprovalToken}";
        var html = $"""
            <p>Für Kalenderwoche {list.CalendarWeek} ist eine Bestellung offen: <strong>{list.Label}</strong> ({itemCount} Positionen).</p>
            <p><a href="{approveUrl}">Bestellung freigeben</a></p>
            <p>Der Link ist 48 Stunden gültig.</p>
            """;
        var text = $"Für Kalenderwoche {list.CalendarWeek} ist eine Bestellung offen: {list.Label} ({itemCount} Positionen).\nFreigeben: {approveUrl}\nGültig für 48 Stunden.";
        await emailService.SendAsync(list.Tenant.MainContactEmail, list.Tenant.MainContactName, $"Bestellung freigeben – KW {list.CalendarWeek}", html, text);
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

    public async Task<byte[]> ExportPdfAsync(Guid id, CancellationToken ct = default)
    {
        var list = await FullQuery(db).FirstOrDefaultAsync(l => l.Id == id, ct) ?? throw new NotFoundException(nameof(ProcurementList), id);
        var model = new ProcurementListModel(
            Label: list.Label,
            CalendarWeek: list.CalendarWeek,
            SupplierName: list.Supplier?.Name,
            Rows: list.Items.Select(i => new ProcurementListRowModel(i.Ingredient.ArticleNumber, i.Ingredient.Name, i.TotalQuantityBase, i.Unit.ToString(), i.PurchaseQuantity)).ToList());
        return pdfService.Render(new ProcurementListDocument(model));
    }

    private static ProcurementListDto ToDto(ProcurementList l) => new()
    {
        Id = l.Id, Label = l.Label, CalendarWeek = l.CalendarWeek, LocationId = l.LocationId, LocationName = l.Location?.Name ?? string.Empty,
        SupplierId = l.SupplierId, SupplierName = l.Supplier?.Name, Status = l.Status.ToString(),
        Items = l.Items.Select(i => new ProcurementListItemDto
        {
            Id = i.Id, IngredientId = i.IngredientId, IngredientName = i.Ingredient?.Name ?? string.Empty, IngredientArticleNumber = i.Ingredient?.ArticleNumber ?? string.Empty,
            CategoryName = i.Ingredient?.Category?.Name ?? string.Empty, SupplierName = i.Ingredient?.Supplier?.Name,
            Unit = i.Unit.ToString(), TotalQuantityBase = i.TotalQuantityBase, PurchaseQuantity = i.PurchaseQuantity,
        }).ToList(),
    };
}
