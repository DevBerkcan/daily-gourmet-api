namespace DailyGourmet.Api.Models.Enums;

public enum Role
{
    SUPER_ADMIN,
    TENANT_OWNER,
    TENANT_ADMIN,
    FACILITY_ADMIN,
    FACILITY_USER,
    READ_ONLY,
    DRIVER
}

public enum UserStatus
{
    AKTIV,
    EINGELADEN,
    DEAKTIVIERT
}

public enum TenantStatus
{
    AKTIV,
    GESPERRT,
    ARCHIVIERT
}

public enum LocationStatus
{
    AKTIV,
    INAKTIV
}

public enum FacilityStatus
{
    AKTIV,
    INAKTIV
}

/// <summary>Frontend literal is "Stück" — DTO layer maps Stueck &lt;-&gt; "Stück".</summary>
public enum Unit
{
    g,
    kg,
    ml,
    l,
    Stueck
}

public enum Difficulty
{
    Einfach,
    Mittel,
    Anspruchsvoll
}

public enum NutriScore
{
    A,
    B,
    C,
    D,
    E
}

public enum NutritionSource
{
    OpenFoodFacts,
    Usda,
    Manuell,
    /// <summary>Bundeslebensmittelschlüssel (Max Rubner-Institut) — since Dec 2025 free under CC BY 4.0.
    /// See IngredientHandler.ApplyExternalNutritionAsync.</summary>
    Bls
}

/// <summary>Where an ingredient row originated — drives the sync-without-overwrite rule (see
/// Ingredient.IsManuallyEdited): only REZEPTRECHNER rows the user hasn't since edited are safe
/// for a re-sync to update.</summary>
public enum IngredientSource
{
    Rezeptrechner,
    Manuell
}

public enum MealPlanStatus
{
    DRAFT,
    REVIEW,
    PUBLISHED,
    CLOSED,
    ARCHIVED
}

/// <summary>Menülinie — which parallel diet track a MealPlanItem belongs to within a day. A day can
/// hold more than one item per line (e.g. a main dish plus a shared dessert).</summary>
public enum DietLine
{
    NORMALKOST,
    VEGGIE,
    GLUTENFREI_LAKTOSEFREI,
    ALTERNATIV
}

public enum OrderStatus
{
    DRAFT,
    SUBMITTED,
    CONFIRMED,
    LOCKED,
    CANCELLED
}

public enum ProductionItemStatus
{
    PLANNED,
    PREPARING,
    COMPLETED,
    CANCELLED
}

/// <summary>Unified admin+kitchen work status (see BACKEND_IMPLEMENTATION_PLAN.md gap #13).</summary>
public enum WorkStatus
{
    OFFEN,
    BEREITSTELLUNG,
    ZUBEREITUNG,
    FERTIG,
    VERPACKT,
    ABHOLBEREIT
}

public enum DeviationCategory
{
    Fehlbestand,
    Produktionsmenge,
    Qualitaet,
    Temperatur,
    Geraet,
    Verspaetung
}

public enum DeviationStatus
{
    OFFEN,
    GEKLAERT
}

public enum ControlType
{
    Kerntemperatur,
    Warmhaltetemperatur,
    Kuehltemperatur,
    Wareneingang
}

public enum ControlStatus
{
    OK,
    NOK
}

public enum ProcurementListStatus
{
    DRAFT,
    REVIEWED,
    READY_FOR_APPROVAL,
    APPROVED,
    ORDERED,
    COMPLETED
}

public enum RouteStatus
{
    GEPLANT,
    BELADUNG,
    UNTERWEGS,
    ABGESCHLOSSEN
}

public enum RouteStopStatus
{
    OFFEN,
    ZUGESTELLT,
    PROBLEM
}

public enum SupportCategory
{
    BUG,
    FRAGE,
    FEATURE
}

public enum SupportPriority
{
    NIEDRIG,
    NORMAL,
    HOCH,
    KRITISCH
}

public enum SupportStatus
{
    OFFEN,
    IN_BEARBEITUNG,
    GELOEST
}

public enum SupportReplyRole
{
    SUPER_ADMIN,
    TENANT_OWNER
}

public enum SupportSessionEndReason
{
    MANUAL,
    EXPIRED
}
