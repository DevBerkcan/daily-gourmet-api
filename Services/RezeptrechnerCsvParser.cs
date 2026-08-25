using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

namespace DailyGourmet.Api.Services;

/// <summary>One ingredient line for one recipe, from the "Rezepte-Zutaten-Mengen" export. Quantity
/// is for the recipe's full standard-portion batch (not per single portion) — matches how
/// RecipeIngredient.Quantity is scaled elsewhere (see RecipeHandler.ScaleAsync).</summary>
public record RezeptrechnerZutatZeile(string RecipeName, string IngredientExternalRefId, string IngredientName, decimal Quantity, int PortionsInFile);

/// <summary>One recipe's labeling/nutrition row from the "Artikeldaten-Kennzeichnung" export.
/// CategoryRaw is free text — sometimes a real category ("Hauptgericht"), sometimes dietary tags
/// ("Glutenfrei, Lactosefrei"), sometimes both comma-joined; see RecipeHandler.ResolveCategory.</summary>
public record RezeptrechnerArtikelZeile(
    string RecipeName, string? RecipeNumber, string? CategoryRaw,
    decimal KcalPer100, decimal KjPer100, decimal FatPer100, decimal SaturatedFatPer100, decimal CarbsPer100,
    decimal SugarPer100, decimal FiberPer100, decimal ProteinPer100, decimal SaltPer100, decimal AlcoholPer100,
    string? IngredientListText, string? AllergensText, string? AdditivesText,
    decimal? PortionWeightG, int? StandardPortions, string? NutriScoreCategory, string? NutriScore, string? NutritionClaimsText);

/// <summary>One recipe's row from the "Allergene-Liste" export — a structured X-marked matrix of
/// the 14 EU-declarable allergen groups (with sub-types, e.g. "Gluten/ Weizen") that's far more
/// precise than the free-text "Allergene" column in the Artikeldaten export, so RecipeHandler
/// prefers this list for any recipe it covers.</summary>
public record RezeptrechnerAllergenZeile(string RecipeName, List<string> MarkedAllergens);

/// <summary>Parses the two Rezeptrechner export CSVs used by RecipeHandler.ImportFromRezeptrechnerAsync.
/// Both files use a non-standard shape: every row is one giant field wrapped in a single pair of
/// double quotes, with the real columns separated by "|" (Artikeldaten) or ";" (Zutaten-Mengen)
/// inside that quoted field — not a normal multi-column CSV, so a standard CSV reader configured
/// with that delimiter wouldn't split it (the quotes would swallow the whole line as one field).
/// Reading line-by-line and splitting by hand is simpler and correct for this shape.</summary>
public static class RezeptrechnerCsvParser
{
    public static List<RezeptrechnerZutatZeile> ParseZutatenMengen(Stream content)
    {
        var result = new List<RezeptrechnerZutatZeile>();
        foreach (var fields in ReadQuotedDelimitedLines(content, ';'))
        {
            if (fields.Length < 8) continue;
            var recipeName = fields[2].Trim();
            var ingredientRefId = fields[5].Trim();
            var ingredientName = fields[6].Trim();
            if (recipeName.Length == 0 || ingredientRefId.Length == 0) continue;

            var portions = (int)Math.Round(ParseGermanDecimal(fields[3]));
            var quantity = ParseGermanDecimal(fields[7]);
            result.Add(new RezeptrechnerZutatZeile(recipeName, ingredientRefId, ingredientName, quantity, portions));
        }
        return result;
    }

    public static List<RezeptrechnerArtikelZeile> ParseArtikeldaten(Stream content)
    {
        var result = new List<RezeptrechnerArtikelZeile>();
        var isFirstLine = true;
        foreach (var fields in ReadQuotedDelimitedLines(content, '|'))
        {
            if (isFirstLine) { isFirstLine = false; continue; } // header row
            if (fields.Length < 26) continue;

            var name = fields[0].Trim();
            if (name.Length == 0) continue;

            result.Add(new RezeptrechnerArtikelZeile(
                RecipeName: name,
                RecipeNumber: NullIfEmpty(fields[1]),
                CategoryRaw: NullIfEmpty(fields[2]),
                KcalPer100: ParseGermanDecimal(fields[6]),
                KjPer100: ParseGermanDecimal(fields[7]),
                FatPer100: ParseGermanDecimal(fields[8]),
                SaturatedFatPer100: ParseGermanDecimal(fields[9]),
                CarbsPer100: ParseGermanDecimal(fields[10]),
                SugarPer100: ParseGermanDecimal(fields[11]),
                FiberPer100: ParseGermanDecimal(fields[12]),
                ProteinPer100: ParseGermanDecimal(fields[13]),
                SaltPer100: ParseGermanDecimal(fields[14]),
                AlcoholPer100: ParseGermanDecimal(fields[15]),
                IngredientListText: NullIfEmpty(fields[16]),
                AllergensText: NullIfEmpty(fields[17]),
                AdditivesText: NullIfEmpty(fields[18]),
                PortionWeightG: ParseGermanDecimalNullable(fields[19]),
                StandardPortions: ParseGermanDecimalNullable(fields[21]) is { } p ? (int)Math.Round(p) : null,
                NutriScoreCategory: NullIfEmpty(fields[24]),
                NutriScore: NullIfEmpty(fields[25]),
                NutritionClaimsText: fields.Length > 26 ? NullIfEmpty(fields[26]) : null));
        }
        return result;
    }

    /// <summary>The "Allergene-Liste" export is a normal (comma-delimited, RFC4180-quoted) CSV —
    /// unlike the other two exports — but its first line is a merged-cell "Allergene" group heading
    /// with no real column names, so it's skipped before handing the rest to CsvHelper.</summary>
    public static List<RezeptrechnerAllergenZeile> ParseAllergeneListe(Stream content)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        reader.ReadLine(); // group heading row ("Allergene" spanning many empty cells) — not real headers
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true, MissingFieldFound = null, BadDataFound = null });
        csv.Read();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? [];

        var result = new List<RezeptrechnerAllergenZeile>();
        while (csv.Read())
        {
            var recipeName = csv.GetField(0)?.Trim();
            if (string.IsNullOrWhiteSpace(recipeName)) continue;

            var marked = new List<string>();
            for (var i = 1; i < headers.Length; i++)
            {
                if (string.Equals(csv.GetField(i)?.Trim(), "X", StringComparison.OrdinalIgnoreCase))
                    marked.Add(headers[i].Trim());
            }
            result.Add(new RezeptrechnerAllergenZeile(recipeName, marked));
        }
        return result;
    }

    private static IEnumerable<string[]> ReadQuotedDelimitedLines(Stream content, char delimiter)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var value = line;
            if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
                value = value[1..^1];
            value = value.Replace("\"\"", "\"");
            yield return value.Split(delimiter);
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Handles the German "1234,56" comma-decimal style used throughout these exports.
    /// Returns 0 for blank/unparsable input — every numeric column in this format is optional.</summary>
    private static decimal ParseGermanDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return 0;
        var s = raw.Trim();
        if (s.Contains(',')) s = s.Replace(".", "").Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static decimal? ParseGermanDecimalNullable(string? raw) => string.IsNullOrWhiteSpace(raw) ? null : ParseGermanDecimal(raw);
}
