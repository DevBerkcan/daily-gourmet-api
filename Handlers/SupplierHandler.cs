using ClosedXML.Excel;
using CsvHelper;
using CsvHelper.Configuration;
using DailyGourmet.Api.Authentication;
using DailyGourmet.Api.Data;
using DailyGourmet.Api.Helpers;
using DailyGourmet.Api.Models.DTOs;
using DailyGourmet.Api.Models.DTOs.Ingredients;
using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;
using DailyGourmet.Api.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace DailyGourmet.Api.Handlers;

public class SupplierHandler(IRepository<Supplier> suppliers, DailyGourmetDbContext db, ITenantContext tenantContext)
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

    // ---- Supplier price-list import (CSV/XLSX) ----
    //
    // Default column mapping, semicolon-delimited (matches the delimiter already used by
    // ProcurementListHandler's CSV export): SupplierArtikelnummer;Artikelnummer;Preis;Einheit.
    // "Artikelnummer" is our Ingredient.ArticleNumber — the match key. Not confirmed against a
    // real supplier file yet; swap the column mapping here once one exists.

    private record ImportRow(int RowNumber, string? SupplierArticleNumber, string? ArticleNumber, string? PriceRaw, string? UnitRaw);

    public async Task<ImportResultDto> ImportPriceListAsync(Guid supplierId, Stream content, string fileName, CancellationToken ct = default)
    {
        var supplierExists = await suppliers.Query().AnyAsync(s => s.Id == supplierId, ct);
        if (!supplierExists) throw new NotFoundException(nameof(Supplier), supplierId);

        var rows = fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? ReadXlsxRows(content)
            : ReadCsvRows(content);

        var ingredientsByArticleNumber = await db.Ingredients.ToDictionaryAsync(i => i.ArticleNumber, i => i, StringComparer.OrdinalIgnoreCase, ct);
        var existingPrices = await db.IngredientSupplierPrices.Where(p => p.SupplierId == supplierId).ToDictionaryAsync(p => p.IngredientId, ct);

        var result = new ImportResultDto();
        var now = DateTime.UtcNow;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.ArticleNumber))
            {
                result.Unmatched.Add(new UnmatchedRowDto { RowNumber = row.RowNumber, Reason = "Keine Artikelnummer angegeben." });
                continue;
            }
            if (!ingredientsByArticleNumber.TryGetValue(row.ArticleNumber.Trim(), out var ingredient))
            {
                result.Unmatched.Add(new UnmatchedRowDto { RowNumber = row.RowNumber, Reason = $"Artikelnummer '{row.ArticleNumber}' nicht gefunden." });
                continue;
            }
            if (!decimal.TryParse((row.PriceRaw ?? "").Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var price))
            {
                result.Unmatched.Add(new UnmatchedRowDto { RowNumber = row.RowNumber, Reason = $"Preis '{row.PriceRaw}' konnte nicht gelesen werden." });
                continue;
            }
            var unit = ParseUnit(row.UnitRaw) ?? ingredient.BaseUnit;

            if (existingPrices.TryGetValue(ingredient.Id, out var existingPrice))
            {
                existingPrice.SupplierArticleNumber = row.SupplierArticleNumber?.Trim() ?? existingPrice.SupplierArticleNumber;
                existingPrice.Price = price;
                existingPrice.Unit = unit;
                existingPrice.UpdatedAt = now;
            }
            else
            {
                var newPrice = new IngredientSupplierPrice
                {
                    Id = Guid.NewGuid(),
                    IngredientId = ingredient.Id,
                    SupplierId = supplierId,
                    SupplierArticleNumber = row.SupplierArticleNumber?.Trim() ?? string.Empty,
                    Price = price,
                    Unit = unit,
                    CreatedAt = now,
                };
                db.IngredientSupplierPrices.Add(newPrice);
                existingPrices[ingredient.Id] = newPrice;
            }
            result.Matched++;
        }

        await db.SaveChangesAsync(ct);
        return result;
    }

    private static Unit? ParseUnit(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = raw.Trim();
        if (normalized.Equals("Stück", StringComparison.OrdinalIgnoreCase) || normalized.Equals("Stk", StringComparison.OrdinalIgnoreCase))
            return Unit.Stueck;
        return Enum.TryParse<Unit>(normalized, true, out var unit) ? unit : null;
    }

    private static List<ImportRow> ReadCsvRows(Stream content)
    {
        using var reader = new StreamReader(content);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { Delimiter = ";", HasHeaderRecord = true, MissingFieldFound = null });
        csv.Read();
        csv.ReadHeader();
        var rows = new List<ImportRow>();
        var rowNumber = 1;
        while (csv.Read())
        {
            rowNumber++;
            rows.Add(new ImportRow(rowNumber, csv.GetField("SupplierArtikelnummer"), csv.GetField("Artikelnummer"), csv.GetField("Preis"), csv.GetField("Einheit")));
        }
        return rows;
    }

    private static List<ImportRow> ReadXlsxRows(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.First();
        var headerRow = sheet.Row(1);
        var columns = headerRow.CellsUsed().ToDictionary(c => c.GetString().Trim(), c => c.Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);

        string? Cell(IXLRow row, string header) => columns.TryGetValue(header, out var col) ? row.Cell(col).GetString() : null;

        var rows = new List<ImportRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            if (row.IsEmpty()) continue;
            rows.Add(new ImportRow(r, Cell(row, "SupplierArtikelnummer"), Cell(row, "Artikelnummer"), Cell(row, "Preis"), Cell(row, "Einheit")));
        }
        return rows;
    }
}
