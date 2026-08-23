using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services.Pdf;

public record ProcurementListRowModel(string ArticleNumber, string IngredientName, decimal TotalQuantityBase, string Unit, decimal PurchaseQuantity);

public record ProcurementListModel(string Label, int CalendarWeek, string? SupplierName, IReadOnlyList<ProcurementListRowModel> Rows);

/// <summary>Per-supplier purchase list, downloadable/sendable individually once a ProcurementList
/// is APPROVED — see ProcurementListHandler.ApproveAsync and the Phase 3 plan note on the
/// "pro Einkaufsliste ein Lieferant" split.</summary>
public class ProcurementListDocument(ProcurementListModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(10));

            page.Header().Column(column =>
            {
                column.Item().Text(model.Label).FontSize(18).Bold();
                column.Item().Text($"KW {model.CalendarWeek}" + (model.SupplierName is { } s ? $" · Lieferant: {s}" : "")).FontSize(11).FontColor(Colors.Grey.Darken1);
            });

            page.Content().PaddingTop(15).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(4);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    void HeaderCell(string text) => header.Cell().BorderBottom(1).PaddingBottom(4).Text(text).Bold();
                    HeaderCell("Artikelnr.");
                    HeaderCell("Zutat");
                    HeaderCell("Bedarf");
                    HeaderCell("Einheit");
                    HeaderCell("Bestellmenge");
                });

                foreach (var row in model.Rows)
                {
                    table.Cell().PaddingVertical(3).Text(row.ArticleNumber);
                    table.Cell().PaddingVertical(3).Text(row.IngredientName);
                    table.Cell().PaddingVertical(3).Text($"{row.TotalQuantityBase:0.###}");
                    table.Cell().PaddingVertical(3).Text(row.Unit);
                    table.Cell().PaddingVertical(3).Text($"{row.PurchaseQuantity:0.###}");
                }
            });

            page.Footer().AlignCenter().Text(text => text.Span("Erstellt mit Daily Gourmet").FontSize(8).FontColor(Colors.Grey.Darken1));
        });
    }
}
