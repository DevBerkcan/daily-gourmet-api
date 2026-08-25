using System.Reflection;
using System.Text.RegularExpressions;
using DailyGourmet.Api.Models.DTOs.Recipes;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace DailyGourmet.Api.Services.Pdf;

/// <summary>Physical label orientation — Quer (100mm × 60mm, table and Zutaten side by side) mirrors
/// a wide shelf/container label; Hoch (60mm × 100mm, table full-width above Zutaten) mirrors a
/// narrow bag/tin label. Matches the two reference layouts supplied for this feature.</summary>
public enum EtikettOrientierung { Quer, Hoch }

/// <summary>What the label should show — mirrors the "Was möchtest du drucken?" choice in the
/// reference Etiketten-Generator: the full LMIV-style label, only the nutrition table, or only the
/// Zutaten/allergen declaration without nutrition figures.</summary>
public enum EtikettInhalt { Vollstaendig, NurNaehrwerte, OhneNaehrwerte }

/// <summary>What a recipe label PDF needs — assembled by RecipeHandler.RenderLabelAsync from a
/// Recipe entity so this class stays a pure layout concern with no DB/EF dependency.</summary>
public record RecipeLabelModel(
    string RecipeName,
    EtikettOrientierung Orientierung,
    EtikettInhalt Inhalt,
    string NaehrwerteBasisLabel,
    RecipeNutritionDto? Nutrition,
    string IngredientsText,
    string? MindestensHaltbarBisText);

/// <summary>Small physical food label (shelf/container sticker) — recipe name, the German "Big 7"
/// nutrition declaration, a Zutaten/allergen declaration with allergens bolded inline, best-before
/// date, and the Daily Gourmet logo. Styled after the customer-supplied reference labels
/// (etiket.pdf / etikethochformat.pdf), not the generic A4 sheet this class produced before.</summary>
public partial class RecipeLabelDocument(RecipeLabelModel model) : IDocument
{
    private static readonly byte[] LogoBytes = LoadLogo();

    private static byte[] LoadLogo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("DailyGourmet.Api.Assets.logo-daily-gourmet.png")
            ?? throw new InvalidOperationException("Logo-Ressource 'Assets/logo-daily-gourmet.png' wurde nicht gefunden.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        var widthMm = model.Orientierung == EtikettOrientierung.Quer ? 100f : 60f;

        container.Page(page =>
        {
            // Ein Etikett ist ein einzelner Aufkleber, keine mehrseitige Seite — die Breite ist durch
            // das Etikettenformat fest vorgegeben, die Höhe wächst mit dem Inhalt (wie bei einem
            // Etikettendrucker auf Endlosrolle), statt bei 60/100mm hart auf eine zweite Seite
            // umzubrechen.
            page.ContinuousSize(widthMm, Unit.Millimetre);
            page.Margin(3, Unit.Millimetre);
            page.DefaultTextStyle(x => x.FontSize(6.5f).FontFamily(Fonts.Arial));

            page.Content().Column(column =>
            {
                column.Spacing(2, Unit.Millimetre);

                column.Item().Row(row =>
                {
                    row.RelativeItem().AlignMiddle().Text(model.RecipeName).FontSize(9).Bold();
                    row.ConstantItem(22, Unit.Millimetre).AlignRight().Image(LogoBytes).FitWidth();
                });

                var showNutrition = model.Inhalt != EtikettInhalt.OhneNaehrwerte;
                var showIngredients = model.Inhalt != EtikettInhalt.NurNaehrwerte;

                if (showNutrition && showIngredients && model.Orientierung == EtikettOrientierung.Quer)
                {
                    // Querformat, vollständiges Etikett: Tabelle und Zutaten nebeneinander (siehe etiket.pdf).
                    column.Item().Row(row =>
                    {
                        row.RelativeItem(1).Element(c => ComposeNutritionTable(c));
                        row.ConstantItem(2, Unit.Millimetre);
                        row.RelativeItem(1).Element(c => ComposeIngredients(c));
                    });
                }
                else
                {
                    // Hochformat, oder nur eine der beiden Sektionen: untereinander (siehe etikethochformat.pdf).
                    if (showNutrition) column.Item().Element(c => ComposeNutritionTable(c));
                    if (showIngredients) column.Item().Element(c => ComposeIngredients(c));
                }

                if (model.MindestensHaltbarBisText is { } mhd)
                {
                    column.Item().Border(0.5f).Padding(1.5f, Unit.Millimetre).AlignCenter()
                        .Text($"Mindestens haltbar bis: {mhd}").FontSize(6.5f);
                }
            });
        });
    }

    private void ComposeNutritionTable(IContainer container)
    {
        var n = model.Nutrition;
        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                });

                static IContainer HeaderCell(IContainer c) => c.Border(0.5f).Padding(1, Unit.Millimetre).BorderColor(Colors.Black);
                static IContainer Cell(IContainer c) => c.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(0.8f, Unit.Millimetre);

                table.Cell().Element(HeaderCell).Text("Durchschnittliche Nährwerte").Bold();
                table.Cell().Element(HeaderCell).AlignRight().Text(model.NaehrwerteBasisLabel).Bold();

                if (n is null)
                {
                    table.Cell().ColumnSpan(2).Element(Cell).Text("Nährwerte für dieses Rezept nicht hinterlegt.").Italic();
                    return;
                }

                void Row(string label, string value, bool bold = false)
                {
                    table.Cell().Element(Cell).Text(t => { if (bold) t.Span(label).Bold(); else t.Span(label); });
                    table.Cell().Element(Cell).AlignRight().Text(t => { if (bold) t.Span(value).Bold(); else t.Span(value); });
                }

                Row("Brennwert", $"{n.Kj:0} kJ/ {n.Kcal:0} kcal", bold: true);
                Row("Fett", $"{n.FatG:0.#} g", bold: true);
                Row("davon gesättigte Fettsäuren", $"{n.SaturatedFatG:0.#} g");
                Row("Kohlenhydrate", $"{n.CarbsG:0.#} g", bold: true);
                Row("davon Zucker", $"{n.SugarG:0.#} g");
                Row("Eiweiß", $"{n.ProteinG:0.#} g", bold: true);
                Row("Salz", $"{n.SaltG:0.#} g", bold: true);
            });
        });
    }

    private void ComposeIngredients(IContainer container)
    {
        container.Text(text =>
        {
            text.Span("Zutaten: ").Bold();
            ComposeWithBoldAllergens(text, model.IngredientsText);
        });
    }

    /// <summary>The Rezeptrechner ingredient declaration already marks every allergen-relevant word
    /// in ALL CAPS at the exact point it occurs (e.g. "...Paniermehl (WEIZENMEHL, Speisesalz, Hefe)
    /// ... (GLUTEN, WEIZEN, EIER, MILCH)") — the LMIV-mandated way of emphasizing allergens within an
    /// ingredient list. Bolding each maximal uppercase run reproduces that emphasis without needing
    /// to re-match against the separately-resolved allergen list (which over- or under-matches:
    /// e.g. bolding just the "WEIZEN" substring inside "WEIZENMEHL", or "milch" inside "Vollmilch",
    /// which the source text deliberately does not capitalize).</summary>
    private static void ComposeWithBoldAllergens(TextDescriptor text, string ingredientsText)
    {
        if (string.IsNullOrEmpty(ingredientsText)) { text.Span("—"); return; }

        var cursor = 0;
        foreach (Match match in UppercaseRun().Matches(ingredientsText))
        {
            if (match.Index > cursor) text.Span(ingredientsText[cursor..match.Index]);
            text.Span(match.Value).Bold();
            cursor = match.Index + match.Length;
        }
        if (cursor < ingredientsText.Length) text.Span(ingredientsText[cursor..]);
    }

    [GeneratedRegex(@"[A-ZÄÖÜß]{2,}(?:[\-/][A-ZÄÖÜß]{2,})*")]
    private static partial Regex UppercaseRun();
}
