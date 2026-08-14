# ADR 0004 — Minimal APIs statt Controller

## Status
Angenommen

## Kontext
`docs/backend-architektur.md §1` skizziert "dünne Controller je Modul". Die aktuelle Aufgabenstellung fordert Minimal APIs mit `RouteGroupBuilder`, außer das Projekt würde bereits bewusst Controller verwenden (§20). Da noch kein Backend-Code existiert, gibt es keinen bestehenden Controller-Stil, an den anzuknüpfen wäre.

## Entscheidung
Minimal APIs. Endpunkte werden nicht alle in `Program.cs` gesammelt, sondern in `Api/Endpoints/<Bereich>/` als statische `MapXyzEndpoints(this RouteGroupBuilder group)`-Erweiterungsmethoden organisiert, z. B.:

```csharp
var recipes = app.MapGroup("/api/v1/recipes").RequireAuthorization();
recipes.MapRecipeEndpoints();
```

`TypedResults` wird verwendet, wo es die OpenAPI-Generierung verbessert.

## Konsequenzen
- Kein Swashbuckle/Controller-Attribut-Routing; OpenAPI wird über `Microsoft.AspNetCore.OpenApi` (built-in) generiert.
- Validierung über eingebaute Minimal-API-/DataAnnotations-Mechanismen statt FluentValidation (Instructions §1: eingebaute Funktionen bevorzugen).
