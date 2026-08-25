using DailyGourmet.Api.Models.DTOs.Recipes;

namespace DailyGourmet.Api.Services;

/// <summary>Evaluates the free-text "nährwertbezogene Angaben" a recipe carries (imported from the
/// Rezeptrechner's Artikeldaten export) against the fixed EU thresholds they refer to (Verordnung
/// (EG) Nr. 1924/2006, Anhang — solid-food row, which is what every recipe here is). Only covers the
/// claim texts actually observed in the customer's exports; an unrecognized claim text is still
/// returned (so nothing silently disappears) with no computed columns, rather than guessing at a
/// threshold. Formulas and label wording cross-checked against the customer's Rezeptrechner
/// screenshots (e.g. "Proteinquelle" → "Eiweiß Anteil in %" ≥ 12% (Energie); "Zuckerarm" →
/// "Zucker (g/100g)" ≤ 5g/100g) — both matched exactly.</summary>
public static class GermanNutritionClaims
{
    private static readonly Dictionary<string, Func<RecipeNutritionDto, (string Label, string Measured, string Threshold)>> Rules =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["fettarm"] = n => ("Fett (g/100g)", Fmt(n.FatG) + "g/100g", "<=3g/100g"),
            ["fettfrei"] = n => ("Fett (g/100g)", Fmt(n.FatG) + "g/100g", "<=0,5g/100g"),
            ["fettfrei/ohne fett"] = n => ("Fett (g/100g)", Fmt(n.FatG) + "g/100g", "<=0,5g/100g"),
            ["ohne fett"] = n => ("Fett (g/100g)", Fmt(n.FatG) + "g/100g", "<=0,5g/100g"),

            ["arm an gesättigten fettsäuren"] = n => ("Gesättigte Fettsäuren (g/100g)", Fmt(n.SaturatedFatG) + "g/100g", "<=1,5g/100g"),
            ["frei von gesättigten fettsäuren"] = n => ("Gesättigte Fettsäuren (g/100g)", Fmt(n.SaturatedFatG) + "g/100g", "<=0,1g/100g"),

            ["zuckerarm"] = n => ("Zucker (g/100g)", Fmt(n.SugarG) + "g/100g", "<=5g/100g"),
            ["zuckerfrei"] = n => ("Zucker (g/100g)", Fmt(n.SugarG) + "g/100g", "<=0,5g/100g"),
            ["frei von zucker"] = n => ("Zucker (g/100g)", Fmt(n.SugarG) + "g/100g", "<=0,5g/100g"),
            ["ohne zucker"] = n => ("Zucker (g/100g)", Fmt(n.SugarG) + "g/100g", "<=0,5g/100g"),

            ["natriumarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,3g/100g"),
            ["kochsalzarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,3g/100g"),
            ["natriumarm/kochsalzarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,3g/100g"),
            ["sehr natriumarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,1g/100g"),
            ["sehr kochsalzarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,1g/100g"),
            ["sehr natriumarm/sehr kochsalzarm"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,1g/100g"),
            ["natriumfrei"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,0125g/100g"),
            ["kochsalzfrei"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,0125g/100g"),
            ["natriumfrei/kochsalzfrei"] = n => ("Salz (g/100g)", Fmt(n.SaltG) + "g/100g", "<=0,0125g/100g"),

            ["ballaststoffquelle"] = n => ("Ballaststoffe (g/100g)", Fmt(n.FiberG) + "g/100g", ">=3g/100g"),
            ["hoher ballaststoffgehalt"] = n => ("Ballaststoffe (g/100g)", Fmt(n.FiberG) + "g/100g", ">=6g/100g"),

            ["proteinquelle"] = n => ("Eiweiß Anteil in %", Fmt(ProteinEnergyPercent(n)) + "%", ">=12% (Energie)"),
            ["hoher proteingehalt"] = n => ("Eiweiß Anteil in %", Fmt(ProteinEnergyPercent(n)) + "%", ">=20% (Energie)"),

            ["energiearm"] = n => ("Energie (kcal/100g)", Fmt(n.Kcal) + "kcal/100g", "<=40kcal/100g"),
            ["energiefrei"] = n => ("Energie (kcal/100g)", Fmt(n.Kcal) + "kcal/100g", "<=4kcal/100g"),
        };

    public static NutritionClaimEvaluationDto Evaluate(string claimText, RecipeNutritionDto per100g)
    {
        var key = claimText.Trim();
        if (Rules.TryGetValue(key, out var rule))
        {
            var (label, measured, threshold) = rule(per100g);
            return new NutritionClaimEvaluationDto { ClaimText = claimText, MeasureLabel = label, MeasuredValue = measured, Threshold = threshold };
        }
        return new NutritionClaimEvaluationDto { ClaimText = claimText };
    }

    private static decimal ProteinEnergyPercent(RecipeNutritionDto n) => n.Kcal <= 0 ? 0 : Math.Round(n.ProteinG * 4 / n.Kcal * 100, 2);

    private static string Fmt(decimal value) => value.ToString("0.##").Replace(".", ",");
}
