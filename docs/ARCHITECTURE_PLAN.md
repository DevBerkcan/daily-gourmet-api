# Backend-Architektur-Plan — Daily Gourmet

Zielbild für das Backend unter `backend/` (eigenes Git-Repo, siehe unten). Dieses Dokument beschreibt die Architektur so, wie sie tatsächlich gebaut wird — es ersetzt nicht `docs/backend-architektur.md`/`docs/api-endpunkte.md`, sondern präzisiert sie dort, wo diese Aufgabenstellung bewusst abweicht (siehe ADRs unter `adr/`).

## 1. Warum diese Architektur?

Die Anwendung ist eine Multi-Tenant-SaaS mit langlebigen Geschäftsprozessen (Rezeptversionierung, Bestellfristen, Produktions-/Einkaufs-Traceability) und mehreren sehr unterschiedlichen Frontend-Rollen (Super-Admin, Tenant-Admin, Küche, Kundenportal, Fahrer). Clean Architecture trennt Fachlogik (Domain/Application) von Technik (Infrastructure/Api), damit:
- Geschäftsregeln (Fristen, Versionierung, Mengenberechnung) unabhängig von EF Core/HTTP testbar sind,
- die Datenbank (aktuell MS SQL Server, siehe [ADR 0002](adr/0002-mssql-over-postgresql.md)) austauschbar bleibt,
- neue Bereiche (BLS-Nährwerte, Lagerverwaltung, Rechnungen, …) ohne Umbau der bestehenden Schichten andocken können.

## 2. Layer & Projekte

```
DailyGourmet.Domain          → keine Abhängigkeiten (kein EF Core, kein ASP.NET Core, kein JSON)
DailyGourmet.Application     → referenziert nur Domain (Use Cases, DTOs, Interfaces: ITenantContext, ICurrentUser, IEmailSender, INutritionProvider, IFileStorage)
DailyGourmet.Infrastructure  → referenziert Application + Domain (EF Core, AppDbContext, TenantContext-Implementierung, Outbox-Worker)
DailyGourmet.Api             → Composition Root, referenziert Application + Infrastructure (Minimal-API-Endpunkte, Program.cs, Middleware)
```

Abhängigkeitsrichtung wird durch `DailyGourmet.ArchitectureTests` (NetArchTest.Rules) automatisiert erzwungen — ein Verstoß lässt den Build/Test-Lauf fehlschlagen, nicht nur eine Doku-Regel.

## 3. Vertical Slices in Application

Organisiert nach den tatsächlichen Frontend-Feature-Bereichen (siehe [FRONTEND_CONTRACT_MATRIX.md](FRONTEND_CONTRACT_MATRIX.md)), nicht nach der generischen Beispielliste der Aufgabenstellung:

```
Application/
├── Auth/            Login, Sessions, Invitations, PasswordReset
├── Tenants/          (inkl. Super-Admin-Tenant-Verwaltung)
├── Users/
├── Facilities/       Standorte + Einrichtungen
├── Ingredients/       Zutaten, Kategorien, Allergene, Nährwerte
├── Recipes/           Rezepte + RezeptVersion
├── MealPlans/         Speisepläne
├── Orders/            Bestellungen (Portal + Admin)
├── Production/
├── Procurement/
├── Logistics/          Lieferrouten (Fahrer-App — Erweiterung ggü. dem ursprünglichen Endpunkt-Katalog)
├── Support/            Tickets + Feature-Flags (Super-Admin)
├── Notifications/
└── Audit/
```

Jeder Slice folgt dem Use-Case-Schema aus der Aufgabenstellung, z. B.:

```
Recipes/
├── CreateRecipe/       CreateRecipeCommand + Handler
├── UpdateRecipe/       → erzeugt neue RezeptVersion, nie Überschreiben (§28)
├── GetRecipe/
├── GetRecipes/
├── ScaleRecipe/         reine Berechnung, keine Mutation
├── DuplicateRecipe/
└── ArchiveRecipe/
```

Kein MediatR in Phase 1/2 — Handler werden als normale Services per Built-in-DI registriert (`IRequestHandler`-artiges Interface nur falls sich später ein klarer Vorteil zeigt, siehe Aufgabenstellung §8).

## 4. Multi-Tenancy

`ITenantContext` (in Application) wird aus der authentifizierten Session aufgelöst (Infrastructure-Implementierung), nie aus einem Request-Body/Query-Parameter. Durchsetzung mehrschichtig (§13):
1. `ITenantContext` pro Scope (Request).
2. EF Core Global Query Filter auf jeder mandantenbezogenen Entität (`HasQueryFilter(e => e.TenantId == _tenantContext.TenantId)`); Super-Admin-Abfragen heben ihn explizit + protokolliert per `IgnoreQueryFilters()` auf.
3. `SaveChanges`-Interceptor erzwingt `TenantId` beim Schreiben und verhindert Cross-Tenant-Writes.
4. Dedizierte Integrationstest-Suite „Mandantentrennung" (Phase 2): Zugriff auf fremden Tenant-Datensatz → `404`, nie `403` mit Existenz-Hinweis.

**Phase 1** definiert nur das `ITenantContext`-Interface und einen Platzhalter, der bewusst mit `NotSupportedException` fehlschlägt (§64 — keine Fake-Implementierung), bis Phase 2 die echte Session-Auflösung liefert.

## 5. Transactional Outbox

Seiteneffekte (E-Mails, künftige Integrationen) laufen nie innerhalb der fachlichen DB-Transaktion. Schema: `OutboxEvent` (siehe [DATABASE.md](DATABASE.md) Abschnitt 8). Ein `BackgroundService` (kein Hangfire, §32) pollt periodisch, verarbeitet idempotent, unterstützt mehrere API-Instanzen über Row-Locking (`UPDATE ... WHERE Status = 'PENDING'` mit `READPAST`/`OUTPUT`-Pattern). Wird ab Phase 10 implementiert; Tabelle wird bereits in Phase 2/3 mitgeführt, sobald die ersten Seiteneffekte (Einladungs-Mails) entstehen.

## 6. Recipe-Versionierung

Siehe [DATABASE.md](DATABASE.md) Abschnitt 4. Jede Änderung an `Rezept` erzeugt eine neue `RezeptVersion`; `SpeiseplanTagRezept` referenziert immer eine konkrete `RezeptVersionId`. Beim Veröffentlichen eines Speiseplans wird die referenzierte Version `IstUnveraenderlich = true` gesetzt (Snapshot einfrieren, §28/§30).

## 7. Wie fügt man ein neues Feature hinzu?

1. Prüfen, ob das Feature bereits im Frontend existiert (`frontend/src/features/<name>`) — Feldnamen von dort übernehmen, nicht neu erfinden.
2. `docs/FRONTEND_CONTRACT_MATRIX.md` aktualisieren (Endpoint, Request/Response, Status `TODO`).
3. Domain-Entität/-Regel in `DailyGourmet.Domain` (falls neu).
4. Use Case in `DailyGourmet.Application/<Slice>/<UseCase>/` (Command/Query + Handler + Validation).
5. `IEntityTypeConfiguration<T>` in `DailyGourmet.Infrastructure/Persistence/Configurations/`, Migration erzeugen.
6. Minimal-API-Endpunkt in `DailyGourmet.Api/Endpoints/<Slice>/`, `RequireAuthorization("permission.key")`.
7. Unit-Tests (Domain-Regel), Integrationstest (Endpoint + Tenant-Isolation), OpenAPI prüfen.
8. Matrix-Status auf `DONE` setzen.

## 8. Implementierungsphasen (§59, konkretisiert für dieses Repo)

| Phase | Inhalt | Diese Runde? |
|---|---|---|
| 1 – Foundation | Solution, DbContext (leer), ProblemDetails, OpenAPI, Health, Docker, Tests-Infra | **Ja** |
| 2 – Identity & Multi-Tenancy | Users, Sessions, Tenants, Memberships, Roles, Permissions, TenantContext real, Mandantentrennungs-Tests | Nein — nächste Runde |
| 3 – Administration | Super-Admin, Tenant-Verwaltung, Facilities, Locations, Settings, Invitations, SupportSessions | Nein |
| 4 – Ingredients | Zutaten, Kategorien, Allergene, Lieferanten, Nährwert-Strategie (siehe [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md)) | Nein |
| 5 – Recipes | Rezepte, Versionierung, Skalierung, Allergen-Snapshot | Nein |
| 6 – Meal Planning | Speisepläne, Tage, Positionen, Publish/Freeze | Nein |
| 7 – Orders | Portal, Draft, Submit, Fristen, History, Adjustments | Nein |
| 8 – Production | Generieren, Aggregation, Traceability | Nein |
| 9 – Procurement | Generieren, Unit-Normalisierung, Traceability | Nein |
| 10 – Platform | Notifications, Outbox-Worker, Mail, Audit, Media, Logistics-Erweiterung | Nein |

Jede Phase endet erst, wenn `dotnet build` und `dotnet test` grün sind (§60) — keine Phase wird als fertig markiert, solange das nicht der Fall ist.

## 9. Repo-Struktur & Git

`daily-gourmet/` (das Frontend-Repository) enthält zwei Ordner: `frontend/` (Next.js-App, Teil dieses Repos) und `backend/` (dieses Repository). `backend/` ist ein **eigenständiges Git-Repository** mit `origin` → `https://github.com/DevBerkcan/daily-gourmet-api.git` — physisch im selben Arbeitsverzeichnis wie `frontend/`, aber git-technisch getrennt. Das Frontend-`.gitignore` hat einen `/backend/`-Eintrag, damit das Frontend-Repo den Ordner nie trackt.
