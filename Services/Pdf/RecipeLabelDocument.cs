using DailyGourmet.Api.Models.DTOs.Recipes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services.Pdf;

/// <summary>What a recipe label PDF needs — assembled by RecipeHandler.RenderLabelAsync from a
/// Recipe entity so this class stays a pure layout concern with no DB/EF dependency.</summary>
public record RecipeLabelModel(
    string RecipeName,
    decimal? PortionWeightG,
    IReadOnlyList<string> Ingredients,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<string> Additives,
    RecipeNutritionDto? Nutrition,
    string NutritionLabel);

/// <summary>DIN A4 recipe label — portion weight, full ingredient list, allergens, and full
/// per-portion nutrition declaration, styled like a supermarket product label. See Phase 1 of the
/// implementation plan (Etikett).</summary>
public class RecipeLabelDocument(RecipeLabelModel model) : IDocument
{
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(2, Unit.Centimetre);
            page.DefaultTextStyle(x => x.FontSize(11));

            page.Header().Text(model.RecipeName).FontSize(20).Bold();

            page.Content().PaddingTop(15).Column(column =>
            {
                column.Spacing(12);

                if (model.PortionWeightG is { } weight)
                    column.Item().Text($"Portionsgewicht: {weight:0.#} g").FontSize(12).Bold();

                column.Item().Column(ingredientsColumn =>
                {
                    ingredientsColumn.Item().Text("Zutaten").Bold();
                    ingredientsColumn.Item().Text(model.Ingredients.Count > 0 ? string.Join(", ", model.Ingredients) : "—");
                });

                column.Item().Column(allergenColumn =>
                {
                    allergenColumn.Item().Text("Allergene").Bold();
                    allergenColumn.Item().Text(model.Allergens.Count > 0 ? string.Join(", ", model.Allergens) : "keine angegeben");
                });

                if (model.Additives.Count > 0)
                {
                    column.Item().Column(additiveColumn =>
                    {
                        additiveColumn.Item().Text("Zusatzstoffe").Bold();
                        additiveColumn.Item().Text(string.Join(", ", model.Additives));
                    });
                }

                column.Item().Column(nutritionColumn =>
                {
                    nutritionColumn.Item().Text(model.NutritionLabel).Bold();
                    if (model.Nutrition is { } n)
                    {
                        nutritionColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(3);
                                columns.RelativeColumn(1);
                            });

                            void Row(string label, string value)
                            {
                                table.Cell().Text(label);
                                table.Cell().AlignRight().Text(value);
                            }

                            Row("Energie", $"{n.Kcal:0} kcal / {n.Kj:0} kJ");
                            Row("Fett", $"{n.FatG:0.#} g");
                            Row("davon gesättigte Fettsäuren", $"{n.SaturatedFatG:0.#} g");
                            Row("Kohlenhydrate", $"{n.CarbsG:0.#} g");
                            Row("davon Zucker", $"{n.SugarG:0.#} g");
                            Row("Ballaststoffe", $"{n.FiberG:0.#} g");
                            Row("Eiweiß", $"{n.ProteinG:0.#} g");
                            Row("Salz", $"{n.SaltG:0.#} g");
                        });
                    }
                    else
                    {
                        nutritionColumn.Item().Text("Nährwerte für dieses Rezept nicht hinterlegt.").Italic();
                    }
                });
            });

            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Erstellt mit Daily Gourmet").FontSize(8).FontColor(Colors.Grey.Darken1);
            });
        });
    }
}
