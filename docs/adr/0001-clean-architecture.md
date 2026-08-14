# ADR 0001 — Clean Architecture mit vier Projekten

## Status
Angenommen

## Kontext
Das Backend muss langfristig wartbar sein, Fachlogik unabhängig von EF Core/ASP.NET Core testbar halten, und mehrere sehr unterschiedliche Frontend-Rollen (Super-Admin, Tenant-Admin, Küche, Kundenportal, Fahrer) sowie künftige Erweiterungen (BLS-Nährwerte, Lagerverwaltung, Rechnungen) ohne Architekturumbau tragen.

## Entscheidung
Vier Projekte mit strikter Abhängigkeitsrichtung: `Domain` (keine Abhängigkeiten) ← `Application` (→ Domain) ← `Infrastructure` (→ Application, Domain) ← `Api` (Composition Root, → Application, Infrastructure). Kein generisches `IRepository<T>`/`IUnitOfWork`/`BaseService` — `AppDbContext` übernimmt diese Rolle direkt. Application-Code ist fachlich nach Vertical Slices organisiert (siehe `ARCHITECTURE_PLAN.md`), nicht technisch nach CRUD-Schicht.

## Konsequenzen
- Dependency-Direction wird durch `DailyGourmet.ArchitectureTests` (NetArchTest.Rules) automatisiert erzwungen, nicht nur dokumentiert.
- Mehr Projekte/Dateien als ein einzelnes Web-API-Projekt, aber klare Testbarkeit und Austauschbarkeit der Infrastruktur (z. B. späterer DB-Wechsel, siehe ADR 0002).
