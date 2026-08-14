# Backend-Architektur — Daily Gourmet (Phase 2)

Zielplattform: **ASP.NET Core 8 (C#)**, Datenbank: **MS SQL Server auf MonsterASP**, Frontend: Next.js (Phase 1, bereits vorhanden).

> Hinweis zur Datenbank: Der ursprüngliche Anforderungskatalog nennt PostgreSQL.
> Da das Hosting auf **MonsterASP** erfolgt, wird **MS SQL Server** verwendet
> (dort nativ enthalten und für .NET-Hosting der Standard). EF Core abstrahiert
> die Datenbank — ein späterer Wechsel zu PostgreSQL bleibt mit geringem Aufwand möglich.

---

## 1. Lösungstruktur (Clean Architecture)

```
DailyGourmet.sln
├── src/
│   ├── DailyGourmet.Api/            ASP.NET Core Web API (Composition Root)
│   │   ├── Controllers/             dünne Controller je Modul (/api/v1/…)
│   │   ├── Middleware/              TenantMiddleware, ExceptionMiddleware, AuditMiddleware
│   │   ├── Auth/                    Cookie-Auth, Policies, PermissionHandler
│   │   └── Program.cs               DI, Pipeline, Swagger, Rate Limiting, CORS
│   ├── DailyGourmet.Application/    Use Cases (Services), DTOs, Validierung (FluentValidation),
│   │                                Interfaces (IRepository, IEmailSender, INutritionProvider,
│   │                                IDemandForecastProvider)
│   ├── DailyGourmet.Domain/         Entitäten, Enums, Domain-Regeln (Fristen, Mengen,
│   │                                Versionierung), keine Framework-Abhängigkeiten
│   └── DailyGourmet.Infrastructure/ EF Core (SQL Server), Repositories, Migrations,
│                                    OpenFoodFacts-/USDA-Client, SMTP/Mailpit, Argon2id-Hasher
└── tests/
    ├── DailyGourmet.UnitTests/          Domain- & Service-Tests (xUnit)
    ├── DailyGourmet.IntegrationTests/   API-Tests mit WebApplicationFactory + Testcontainern
    └── e2e/ (Playwright)                im Frontend-Repo
```

**Abhängigkeitsrichtung:** Api → Application → Domain; Infrastructure implementiert Application-Interfaces. Kein Layer greift „nach oben".

## 2. Wichtige NuGet-Pakete

| Zweck | Paket |
|---|---|
| ORM | `Microsoft.EntityFrameworkCore.SqlServer` |
| Validierung | `FluentValidation.AspNetCore` |
| Passwort-Hashing | `Konscious.Security.Cryptography.Argon2` (Argon2id) |
| OpenAPI | `Swashbuckle.AspNetCore` |
| Rate Limiting | `Microsoft.AspNetCore.RateLimiting` (built-in) |
| Logging | `Serilog.AspNetCore` (strukturierte Logs, JSON) |
| Hintergrundjobs (E-Mail) | `Hangfire` mit SQL-Server-Storage — oder `IHostedService` + Outbox-Tabelle |

## 3. Mandantenfähigkeit (hart, serverseitig)

1. **Auth-Cookie** enthält die Session-Referenz; die Session-Tabelle liefert `TenantId` + Rollen.
2. **TenantMiddleware** setzt pro Request einen `ITenantContext` (Scoped).
3. **EF Core Global Query Filter** auf *jeder* mandantenbezogenen Entität:
   `modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);`
   → Ein Vergessen der Filterung ist damit technisch ausgeschlossen; SUPER_ADMIN-Abfragen heben den Filter explizit und protokolliert per `IgnoreQueryFilters()` auf.
4. **SaveChanges-Interceptor** erzwingt beim Schreiben `TenantId = aktueller Tenant` und verhindert Cross-Tenant-Writes.
5. **Integrationstests**: dedizierte Testsuite „Mandantentrennung" (User A darf Datensatz von Tenant B weder lesen noch schreiben → 404, niemals 403 mit Existenz-Hinweis).

## 4. Authentifizierung & Sicherheit

- **Login:** E-Mail + Passwort → Session-Datensatz in DB, **HTTP-only, Secure, SameSite=Lax Cookie** mit opakem Session-Token (Server-Side-Sessions, jederzeit revozierbar).
- **Passwörter:** Argon2id (Memory 64 MB, Iterations 3, Parallelism 2), Pepper aus Umgebungsvariable.
- **Kontosperrung:** nach 5 Fehlversuchen 15 Min. Sperre; Zähler in `users`.
- **Rate Limiting:** `/auth/login`, `/auth/forgot-password`: 5 req/Min/IP (Fixed Window).
- **CSRF:** Double-Submit-Cookie (`X-CSRF-Token`-Header) für alle mutierenden Requests, da Cookie-Auth.
- **CORS:** ausschließlich die Frontend-Origin (per Env-Variable).
- **Keine Selbstregistrierung.** Mandanten legt nur SUPER_ADMIN an; Benutzer kommen per Einladung (Token, 72 h gültig).
- **Supportzugriff:** `support_sessions`-Tabelle; Start/Ende als Audit-Events, Banner-Flag im Session-Kontext, automatisches Ablaufen (60 Min.), kein unsichtbares Impersonieren.
- **Secrets:** ausschließlich Umgebungsvariablen / MonsterASP-Konfiguration; `.env.example` dokumentiert alle Schlüssel.

## 5. RBAC — Rollen & Permissions

Rollen: `SUPER_ADMIN, TENANT_OWNER, TENANT_ADMIN, KITCHEN_MANAGER, KITCHEN_STAFF, FACILITY_ADMIN, FACILITY_USER, READ_ONLY`

Permissions (DB-gestützt, Rollen → Permissions per `role_permissions`), Auszug:
`tenants.read/write · users.read/write · facilities.read/write · ingredients.read/write · recipes.read/write · mealplans.read/write/publish · orders.read/write/override · production.read/manage · procurement.read/manage · settings.manage · audit.read · features.manage · support.start`

Durchsetzung über ASP.NET-**Authorization Policies**:
`[Authorize(Policy = "recipes.write")]` — der `PermissionHandler` prüft gegen die geladenen Berechtigungen der Session. Facility-Rollen werden zusätzlich auf ihre `user_facilities`-Zuordnung eingeschränkt.

## 6. Datenmodell (SQL Server, EF Core Migrations)

Alle Tabellen: `Id UNIQUEIDENTIFIER (UUID v7)`, `CreatedAt`, `UpdatedAt`, ggf. `CreatedBy/UpdatedBy`, mandantenbezogen `TenantId` (+ Index).

Tabellen (wie im Anforderungskatalog):
`tenants, tenant_settings, feature_flags, tenant_feature_flags, users, roles, permissions, user_roles, role_permissions, user_facilities, sessions, password_reset_tokens, invitations, locations, facilities, facility_settings, ingredient_categories, ingredients, ingredient_nutrition, allergens, ingredient_allergens, recipes, recipe_versions, recipe_ingredients, meal_plans, meal_plan_days, meal_plan_items, meal_plan_facilities, orders, order_items, order_notes, order_history, production_plans, production_plan_items, production_adjustments, procurement_lists, procurement_list_items, notifications, audit_logs, support_sessions, system_settings`

Besonderheiten:
- **`recipe_versions`**: Beim Veröffentlichen eines Speiseplans wird je Rezept ein unveränderlicher Snapshot (inkl. Zutaten & Nährwerten) geschrieben; `meal_plan_items` referenzieren die Version, nie das Live-Rezept. Vergangene Bestellungen/Produktionen bleiben dadurch stabil.
- **`ingredient_nutrition`**: Nährwerte je 100 g/ml + `Source` (`OpenFoodFacts | Usda | Manual`) + `ExternalId` (z. B. EAN) + `FetchedAt`.
- **Mengen** immer `decimal(12,3)` — niemals `float` (Rezepthochrechnung, Einkaufsaggregation).
- **Einheiten**: Enum `g|kg|ml|l|piece`; zentrale `UnitConverter`-Domain-Klasse. Masse↔Volumen nur mit individuellem Faktor der Zutat, sonst Domain-Exception.
- **Soft Delete** nur bei `recipes`, `ingredients`, `facilities` (fachlich nötig, da referenziert); sonst hartes Löschen bzw. Statusfeld.
- **Transaktionen** für: Bestellung absenden, Speiseplan veröffentlichen (inkl. Snapshots), Produktionskorrektur (+Audit), Einkaufslistengenerierung.
- **audit_logs**: Append-only; kein UPDATE/DELETE über die API; enthält `OldValues/NewValues` (JSON), `Reason`, `IpAddress`.

## 7. Nährwert-Integration (Zutaten über externe API)

Die Zutaten beziehen ihre Nährstoffe über eine externe Lebensmittel-API. **Der Abruf läuft ausschließlich über das Backend** (Proxy) — nie direkt aus dem Browser (API-Keys, Caching, Rate Limits, einheitliches Format).

**Provider-Strategie** (Interface `INutritionProvider` in Application):

1. **Open Food Facts** (primär) — kostenlos, EAN-Suche + Volltextsuche, gute Abdeckung deutscher Produkte.
   `GET https://world.openfoodfacts.org/api/v2/product/{ean}` bzw. Volltextsuche.
2. **USDA FoodData Central** (Fallback für Grundzutaten) — kostenloser API-Key.
3. **Manuell** — Werte immer überschreibbar; `Source = Manual`.

**Ablauf beim Anlegen einer Zutat:**
```
Frontend ── GET /api/v1/nutrition/search?q=vollmilch  (oder ?ean=…)
Backend  ── fragt Provider ab, normalisiert auf: kcal, Eiweiß, Fett, KH, Zucker, Salz je 100 g/ml
Frontend ── Benutzer wählt Treffer, Werte werden in das Zutatenformular übernommen
Backend  ── POST /api/v1/ingredients speichert Zutat + ingredient_nutrition (inkl. Quelle & ExternalId)
```
- Ergebnisse werden **24 h serverseitig gecacht** (MemoryCache + DB-Persistenz); Ausfälle der externen API blockieren das Anlegen nicht (Zutat kann ohne bzw. mit manuellen Werten gespeichert werden).
- Rezept-Nährwerte je Portion werden aus den Zutaten-Nährwerten berechnet und im `recipe_versions`-Snapshot eingefroren.

## 8. Weitere Querschnittsthemen

- **API-Versionierung:** alles unter `/api/v1`; Antwortformat einheitlich `{ data, meta? }` bzw. `{ error: { code, message, details? } }` (RFC-7807-nah).
- **Pagination:** `?page=1&pageSize=25&sort=name&dir=asc` + `meta: { total, page, pageSize }`.
- **OpenAPI:** Swagger UI unter `/swagger` (nur Nicht-Produktion), generiertes Schema als Quelle für Frontend-Typen.
- **Benachrichtigungen:** `notifications`-Tabelle + Outbox-Pattern für E-Mails; Versand asynchron (Hangfire/HostedService), lokal via **Mailpit** — der Versand blockiert nie die Haupttransaktion.
- **Health:** `GET /api/v1/health` (liveness) und `/api/v1/health/ready` (DB-Check) für das Super-Admin-Dashboard.
- **DemandForecastProvider:** neutrale Basis-Implementierung (Ø bestätigter Bestellmengen der letzten N Wochen). Wird bewusst **nicht** als KI bezeichnet; die Schnittstelle ist der spätere Andockpunkt für Prognosedienste.

## 9. Deployment auf MonsterASP

- **App:** Publish als ASP.NET Core 8, Deployment per WebDeploy/ZIP oder GitHub Actions auf die MonsterASP-Website.
- **DB:** MS-SQL-Datenbank im MonsterASP-Panel anlegen; Connection String als Umgebungsvariable/Konfigurations-Override (nie im Repo).
- **Migrationen:** `dotnet ef database update` als CI-Schritt beim Deployment — nicht automatisch beim App-Start in Produktion.
- **Frontend:** Next.js separat (z. B. Vercel) mit `NEXT_PUBLIC_API_BASE_URL` auf die MonsterASP-API; CORS entsprechend setzen.
- **Lokal:** `docker-compose.yml` mit `api`, `mssql` (azure-sql-edge / mssql-server), `mailpit`, `web` — inkl. Health Checks und `.env.example`.

## 10. Teststrategie

| Ebene | Werkzeug | Schwerpunkt |
|---|---|---|
| Unit | xUnit + FluentAssertions | UnitConverter, Rezeptskalierung (decimal!), Fristlogik (Zeitzone Europe/Berlin, Wochenenden), Produktionsaggregation |
| Integration | WebApplicationFactory + SQL-Testcontainer | RBAC je Endpunkt, **Mandantentrennung**, Fristdurchsetzung, Snapshot bei Veröffentlichung |
| E2E | Playwright (Frontend-Repo) | kompletter Geschäftsprozess: Mandant anlegen → Einladung → Zutat/Rezept → Plan veröffentlichen → Bestellung → Frist sperrt → Korrektur mit Begründung → Produktion → Einkaufsliste |
