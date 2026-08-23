using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services.Pdf;

public record ProductionPlanColumnModel(string DietLineLabel, string DishLabel);

public record ProductionPlanRowModel(string Tour, string FacilityName, IReadOnlyList<int?> PortionsByColumn, string? Bemerkungen);

public record ProductionPlanModel(
    int CalendarWeek,
    string WeekdayLabel,
    DateOnly Date,
    IReadOnlyList<ProductionPlanColumnModel> Columns,
    IReadOnlyList<ProductionPlanRowModel> Rows);

/// <summary>Replaces the paper-taped-to-the-wall Google-Sheets process the kitchen used before the
/// Küche module was removed from scope — one PDF per weekday, grouped by Tour (Facility.RouteNumber,
/// deliberately not that day's DeliveryRoute — see the Phase 3 plan note on why), one column per
/// diet line, matching Input/Produktionsplan Beispiel.pdf's exact layout.</summary>
public class ProductionPlanDocument(ProductionPlanModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(1.5f, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(8));

            page.Header().Row(row =>
            {
                row.RelativeItem().Text($"KW {model.CalendarWeek}").FontSize(16).Bold();
                row.RelativeItem().AlignRight().Text($"{model.WeekdayLabel} - {model.Date:dd.MM}").FontSize(16).Bold();
            });

            page.Content().PaddingTop(10).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(30);
                    columns.RelativeColumn(3);
                    foreach (var _ in model.Columns) columns.RelativeColumn(2);
                    columns.RelativeColumn(3);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Tour").Bold();
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Kita Name").Bold();
                    foreach (var column in model.Columns)
                    {
                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Column(c =>
                        {
                            c.Item().Text(column.DietLineLabel).Bold().FontSize(8);
                            c.Item().Text(column.DishLabel).FontSize(7).FontColor(Colors.Grey.Darken2);
                        });
                    }
                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Bemerkungen").Bold();
                });

                string? previousTour = null;
                foreach (var rowModel in model.Rows)
                {
                    var newTourGroup = previousTour != null && previousTour != rowModel.Tour;
                    previousTour = rowModel.Tour;

                    table.Cell().BorderTop(newTourGroup ? 1.5f : 0.5f).Padding(3).Text(rowModel.Tour);
                    table.Cell().BorderTop(newTourGroup ? 1.5f : 0.5f).Padding(3).Text(rowModel.FacilityName);
                    foreach (var portions in rowModel.PortionsByColumn)
                        table.Cell().BorderTop(newTourGroup ? 1.5f : 0.5f).Padding(3).AlignCenter().Text(portions?.ToString() ?? "");
                    table.Cell().BorderTop(newTourGroup ? 1.5f : 0.5f).Padding(3).Text(rowModel.Bemerkungen ?? "").FontSize(7);
                }

                table.Cell().BorderTop(1.5f).Padding(3).Text("");
                table.Cell().BorderTop(1.5f).Padding(3).Text("Insgesamt").Bold();
                for (var col = 0; col < model.Columns.Count; col++)
                {
                    var total = model.Rows.Sum(r => r.PortionsByColumn[col] ?? 0);
                    table.Cell().BorderTop(1.5f).Padding(3).AlignCenter().Text(total.ToString()).Bold();
                }
                table.Cell().BorderTop(1.5f).Padding(3).Text("");
            });

            page.Footer().AlignCenter().Text(text => text.Span("Erstellt mit Daily Gourmet").FontSize(7).FontColor(Colors.Grey.Darken1));
        });
    }
}
