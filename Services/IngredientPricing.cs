using DailyGourmet.Api.Models.Entities;
using DailyGourmet.Api.Models.Enums;

namespace DailyGourmet.Api.Services;

/// <summary>One place for "what does this much of an ingredient cost" — shared by the recipe cost
/// estimate (RecipeHandler), the Einkauf week overview (ProcurementOverviewHandler) and the
/// per-supplier Einkaufslisten (ProcurementListHandler), so all three always agree. Callers must have
/// loaded Ingredient.SupplierPrices (and .Supplier, if they want the supplier name).</summary>
public static class IngredientPricing
{
    public readonly record struct UnitPrice(decimal Price, Unit Unit, string? SupplierName);

    /// <summary>Cheapest known price for an ingredient, and the unit it's priced in: the given
    /// supplier's offer if <paramref name="preferredSupplierId"/> is set and has one, else the cheapest
    /// supplier offer, else the ingredient's standard PurchasePrice (per PurchaseUnit, converted to a
    /// per-BaseUnit price via ConversionFactor). Null when no price is known at all.</summary>
    public static UnitPrice? Resolve(Ingredient ingredient, Guid? preferredSupplierId = null)
    {
        var offer = (preferredSupplierId is { } sid ? ingredient.SupplierPrices.FirstOrDefault(p => p.SupplierId == sid) : null)
            ?? ingredient.SupplierPrices.OrderBy(p => p.Price).FirstOrDefault();
        if (offer != null) return new UnitPrice(offer.Price, offer.Unit, offer.Supplier?.Name);
        if (ingredient.PurchasePrice is { } purchasePrice && ingredient.ConversionFactor > 0)
            return new UnitPrice(purchasePrice / ingredient.ConversionFactor, ingredient.BaseUnit, null);
        return null;
    }

    /// <summary>Converts a quantity between units within the same family (g&lt;-&gt;kg, ml&lt;-&gt;l);
    /// returns null for cross-family or Stueck conversions, since there's no general unit-conversion
    /// system (e.g. density) anywhere in this codebase to fall back on.</summary>
    public static decimal? ConvertQuantity(decimal quantity, Unit from, Unit to)
    {
        if (from == to) return quantity;
        return (from, to) switch
        {
            (Unit.g, Unit.kg) => quantity / 1000m,
            (Unit.kg, Unit.g) => quantity * 1000m,
            (Unit.ml, Unit.l) => quantity / 1000m,
            (Unit.l, Unit.ml) => quantity * 1000m,
            _ => null,
        };
    }

    /// <summary>Cost of <paramref name="quantity"/> <paramref name="unit"/> of the ingredient at its
    /// resolved price. Null when there's no price or the units can't be converted.</summary>
    public static decimal? Cost(Ingredient ingredient, decimal quantity, Unit unit, Guid? preferredSupplierId = null)
    {
        if (Resolve(ingredient, preferredSupplierId) is not { } price) return null;
        return ConvertQuantity(quantity, unit, price.Unit) is { } qty ? qty * price.Price : null;
    }
}
