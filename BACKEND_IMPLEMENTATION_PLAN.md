# Backend Implementation Plan — DailyGourmet

Produced from a full read of the frontend (`src/app`, `src/features`, `src/lib`) plus the existing
planning docs (`docs/ARCHITECTURE.md`, `docs/backend-architektur.md`, `docs/api-endpunkte.md`,
`docs/mvp-angebotsabdeckung.md`). Those docs described an earlier, more elaborate plan (Clean
Architecture across 4 projects, HTTP-only cookie sessions, ASP.NET Core 8). **This plan supersedes
that one** per an explicit decision on 2026-08-15: single `DailyGourmet.Api` project, JWT bearer
auth, ASP.NET Core 10, SQL Server + EF Core. The domain model (multi-tenant catering SaaS, German
field names, roles) is unchanged — only the backend's internal structure and auth mechanism differ
from the older docs. `docs/backend-architektur.md` and `docs/api-endpunkte.md` will be rewritten to
match this plan once implementation starts.

The frontend is 100% dummy data today (`src/lib/data/index.ts`, `src/features/*/data.ts`) — no
`fetch`/`axios` calls exist anywhere. Every type in `src/lib/types.ts` and `src/features/*/types.ts`
is explicitly documented in-repo as "mirrors the planned backend DTOs 1:1", so those types are the
primary source of truth for entity shape, alongside the actual form fields, store mutators, and
computed values found in each feature.

---

## 1. Architecture Summary

```
Frontend (Next.js, unchanged UI)
    → HTTP REST API (/api/...)
    → DailyGourmet.Api (ASP.NET Core 10, single project)
        Controllers → Handlers → Repositories → EF Core → SQL Server
    → JWT bearer auth, roles from a fixed enum (no separate permissions table)
```

- **One project**: `DailyGourmet.Api`, folders per the standard layout (Controllers, Data, Models,
  Repositories, Handlers, Services, Authentication, Middleware, Options, Migrations, Helpers).
- **Auth**: JWT bearer tokens (`Microsoft.AspNetCore.Authentication.JwtBearer`), password hashing via
  `PasswordHasher<User>`. No self-registration (frontend login page has no register form and
  `docs/mvp-angebotsabdeckung.md` explicitly states tenants are platform-managed, not self-service).
- **DB naming convention**: entity/table/column names are **English PascalCase** in C#/SQL even
  though the frontend's domain types use German property names (`Rezept`, `Zutat`, `Bestellung`,
  ...). DTOs are the translation layer; this matches the naming already used in the earlier
  `docs/backend-architektur.md` table list (`recipes`, `ingredients`, `orders`, ...) and is
  idiomatic for a C# codebase. The frontend keeps its German UI vocabulary; only the wire-format
  DTO property names need to line up (see §10, per-feature mapping).
- **IDs**: `Guid` (`uniqueidentifier`) surrogate keys everywhere, generated server-side. The
  frontend's current client-generated IDs (`r-001`, `z-107`, `Date.now()`-based, sequential
  counters) are Phase-1 mock artifacts and are not carried into the schema — see gaps below.
- **Money**: `decimal(12,2)` for prices/revenue, `decimal(12,3)` for ingredient quantities (matches
  the ROADMAP intent already stated in `docs/backend-architektur.md` §6: "Mengen immer decimal(12,3)
  — niemals float").
- **Multi-tenancy**: hard, server-enforced. Every tenant-scoped entity carries a `TenantId` column;
  an EF Core global query filter restricts all queries to the caller's tenant (from the JWT `tenantId`
  claim). `SUPER_ADMIN` requests explicitly use `IgnoreQueryFilters()` on super-admin-only endpoints.

---

## 2. Key Assumptions & Resolved Gaps

The frontend is thorough but Phase-1/mock-only in specific ways. Per the "document assumptions
instead of silently inventing" instruction, every deviation from a literal 1:1 mock-to-schema
mapping is listed here with its reasoning.

| # | Gap found in frontend | Resolution in this plan |
|---|---|---|
| 1 | No `DRIVER` role exists in `Rolle`; `app/driver` hardcodes `userName="Markus Becker"` with zero auth wiring; `Fahrer` (driver) is a standalone entity disconnected from `Benutzer`. | Add `DRIVER` to the `Role` enum. New `Driver` entity has a required 1:1 `UserId` FK to `User`, so drivers get real login like every other role. |
| 2 | `Standort`/`Einrichtung` (Location/Facility) have no `tenantId` field anywhere, despite being core tenant-scoped data. | Add `TenantId` to `Location` (facilities inherit tenant scope transitively via their `LocationId`, but also get a direct `TenantId` column for simpler EF query-filter performance — standard multi-tenant pattern). |
| 3 | `AuditEintrag.tenant`/`.benutzer` are display-name strings, not FKs; no `OldValues`/`NewValues`/`IpAddress` columns; nothing in the actual mutators (tenant lock, feature-flag toggle, order correction) writes to it — only hand-authored seed rows exist. | `AuditLog` gets real `TenantId`/`UserId` FKs (nullable for platform-level/system actions) plus `OldValues`/`NewValues` (JSON), `Reason`, `IpAddress`. Every state-changing handler that the mock's own seed data implies should be audited (tenant lock/unlock/archive, feature-flag toggle, order correction, production adjustment, support session start/end) writes a row. |
| 4 | Tenant lock/unlock/archive has no reason-capture UI in the frontend, but `docs/api-endpunkte.md`'s own endpoint list says "mit Begründung → Audit", and the one seed audit row for a lock (`a-9004`) has a `begruendung`. | Backend requires `reason` on `POST /tenants/{id}/lock|unlock|archive`. Frontend integration (Phase 5) adds a small reason-prompt dialog — this is a frontend gap being closed, not new business logic being invented. |
| 5 | `TenantStatus.ARCHIVIERT` exists in the type but no code path ever sets it. | Endpoint implemented per §7; frontend gets the missing "Archivieren" button in Phase 5. |
| 6 | Feature flags are a single global `Record<string, boolean>` — no per-tenant override exists anywhere despite `docs/api-endpunkte.md` documenting both a global and a per-tenant endpoint. | `FeatureFlag` (global catalog) + `TenantFeatureFlag` (override) tables, matching the documented two-endpoint design. Per-tenant override UI is a fast-follow, not required for MVP parity with today's frontend. |
| 7 | `SupportTicket`/`SupportEreignis` are a fully-built frontend feature (widget + admin center) but are **absent** from every table list in the old docs. | `SupportTicket` + `SupportTicketReply` tables added. `SupportEreignis` (tenant activity feed) is served from `AuditLog` filtered by tenant instead of a separate table — same data, one less table. |
| 8 | `SupportSitzung` (support session) is a single nullable client-side value with a JS `setTimeout` for 60-minute expiry — resets on page reload, no history. | Real `SupportSession` table (start/expire/end timestamps, one row per session, queryable history). Expiry is enforced **server-side** on every request during an active session, never trusted to a client timer. |
| 9 | Every admin/portal/super-admin dashboard KPI is a hardcoded literal string, not computed from the mock arrays (confirmed by the agent for all 3 dashboards). | Real aggregation queries defined in §9. |
| 10 | UI copy says a published meal plan "friert die Rezeptdaten als Snapshot ein" (freezes recipe data as a snapshot), but zero snapshot logic exists in the frontend — `MealPlanItem` only ever holds a live `rezeptId`. | `MealPlanItem` gets a nullable `RecipeSnapshotJson` column, populated at publish time with the recipe's name/nutrition/allergens as of that moment. Orders/production always resolve through the snapshot once one exists, so later recipe edits can't retroactively change a published week. |
| 11 | "Menülinien" (menu lines) are mentioned only in `docs/api-endpunkte.md`'s prose, never in any frontend type, store, or component. | **Out of scope for this plan.** Not implemented — would be inventing business logic with zero frontend basis. |
| 12 | `RezeptZutat` (recipe↔ingredient line) has no own id — the frontend uses array index / `zutatId` as an ad-hoc key, which collides if the same ingredient appears twice in one recipe. | `RecipeIngredient` gets its own surrogate `Id` PK; `(RecipeId, IngredientId)` is **not** unique (duplicates are structurally allowed, matching current frontend behavior). |
| 13 | `KitchenWorkStatus` (kitchen-facing) and `ProduktionsStatus` (admin-facing) are two parallel, never-synced enums on the same underlying position. | Unified into one `WorkStatus` column on `ProductionPlanItem` that both admin and kitchen views read/write — closes a real bug class the mock has today. |
| 14 | `bereitgestellt` (staged quantity) in the ingredient-requirements view is entirely fake (`menge * 0.72` hardcoded for one specific ingredient id, `menge` for everything else) — no real "how much has been staged" write path exists. | `ProductionPlanItem`-adjacent `StagedQuantity` becomes a real, kitchen-staff-editable field (simple quantity, no batch/lot tracking — matches the level of detail actually present in the mock's *intent*, not the fake value). |
| 15 | HACCP "Kontrolle" (control) records only ever have `status: "OK"` in the mock — no fail path modeled. | Add `NOK` to the status enum. A pass-only HACCP check has no operational value; this is a minimal, obviously-intended completion, not new business logic. |
| 16 | `LieferPosition`/`RouteStopItem` has no FK back to the `Bestellung`/`BestellPosition` it was generated from — traceability is lost once a route is built. | Add nullable `OrderId`/`OrderItemId` to `RouteStopItem`. |
| 17 | Per-stop delivery status (`StoppStatus`) lives in a separate `Map`, not as a field on the stop; "Problem melden" captures no reason text even though a `hinweis` field exists on the stop for a different purpose. | `Status` moves onto `RouteStop` directly. A `ProblemNote` column is added so a reported problem's reason is actually captured (currently discarded by the mock). |
| 18 | No proof-of-delivery (signature, photo, delivered-vs-planned reconciliation) exists anywhere in the frontend. | **Out of scope.** Only `DeliveredAt`/`Status` are modeled (trivial, already implied). Signature/photo capture is a product decision with zero frontend precedent — not invented here. |
| 19 | `Zutat.lieferant` (supplier) is a free-text string; procurement counts "distinct suppliers" by grouping on that string. | Normalized `Supplier` entity (`Id`, `TenantId`, `Name`, optional contact fields), `Ingredient.SupplierId` FK. Existing free-text values become the seed `Supplier.Name`. |
| 20 | Ingredient/recipe categories, allergens, and additives are inconsistent free text in the mock data even though fixed option lists exist in the UI (`ZUTAT_KATEGORIEN`, `ALLERGENE_LISTE`, ...). | `RecipeCategory`/`IngredientCategory` and `Allergen` become seeded lookup tables (global, not tenant-scoped — they're industry/regulatory reference data). Additives stay as free-text child rows (`IngredientAdditive.Text`) since the mock data itself is inconsistent free text, not a clean fixed set. |
| 21 | Unit-conversion safety ("kg → l blocked without an individual factor") is asserted only in UI copy, never enforced in code. | Enforced server-side when saving an `Ingredient`'s `ConversionFactor`/unit pairing (see §9). |
| 22 | `Order.frist`/`Einrichtung.bestellfrist` are free-text deadline descriptions ("Vortag, 09:00 Uhr"); the portal's deadline lock is a bare date-string comparison with no actual time-of-day check, and multiple places note "serverseitige Fristprüfung" as explicitly deferred to Phase 2. | `Facility` gets structured `OrderDeadlineOffsetDays` (int) + `OrderDeadlineTime` (time); `Order.DeadlineAtUtc` is computed and stored at save time. Real server-side enforcement replaces the client-only date comparison — this is the single most repeated "Phase 2" TODO across the whole codebase. |
| 23 | `MealPlan` duplication (`kalenderwoche + 1`) has no uniqueness check and can silently create two plans for the same week. | `(TenantId, Year, CalendarWeek)` gets a unique constraint. If a real need for multiple plan variants per week emerges, that's a product decision to revisit — not assumed here. |
| 24 | Branding "Farben" (brand colors) are mentioned in UI copy (`branding-card.tsx` hint text) but no color field/picker exists anywhere. | **Out of scope.** Only `LogoUrl` is modeled. |
| 25 | Portal profile UI hints that a `FACILITY_ADMIN` should be able to invite users, but no invite form exists. | The general `POST /users` (invite) endpoint already covers this; no facility-scoped variant is added beyond what §7 lists, since the frontend gives no evidence of additional fields needed. |

---

## 3. Enumerations Reference

All enums below are stored as `nvarchar` in SQL Server (not `tinyint`) so audit-log values and ad-hoc
queries stay human-readable; EF Core maps them via `HasConversion<string>()`.

| Enum | Values |
|---|---|
| `Role` | `SUPER_ADMIN, TENANT_OWNER, TENANT_ADMIN, KITCHEN_MANAGER, KITCHEN_STAFF, FACILITY_ADMIN, FACILITY_USER, READ_ONLY, DRIVER` *(DRIVER added, see gap #1)* |
| `UserStatus` | `AKTIV, EINGELADEN, DEAKTIVIERT` |
| `TenantStatus` | `AKTIV, GESPERRT, ARCHIVIERT` |
| `LocationStatus` / `FacilityStatus` | `AKTIV, INAKTIV` |
| `Unit` | `g, kg, ml, l, Stueck` (frontend literal is `"Stück"`; DTO layer maps `Stueck` ↔ `"Stück"`) |
| `Difficulty` | `Einfach, Mittel, Anspruchsvoll` |
| `NutriScore` | `A, B, C, D, E` |
| `NutritionSource` | `OpenFoodFacts, Usda, Manuell` |
| `MealPlanStatus` | `DRAFT, REVIEW, PUBLISHED, CLOSED, ARCHIVED` |
| `OrderStatus` | `DRAFT, SUBMITTED, CONFIRMED, LOCKED, CANCELLED` |
| `ProductionItemStatus` | `PLANNED, PREPARING, COMPLETED, CANCELLED` |
| `WorkStatus` | `OFFEN, BEREITSTELLUNG, ZUBEREITUNG, FERTIG, VERPACKT, ABHOLBEREIT` *(unified, see gap #13)* |
| `DeviationCategory` | `Fehlbestand, Produktionsmenge, Qualitaet, Temperatur, Geraet, Verspaetung` |
| `DeviationStatus` | `OFFEN, GEKLAERT` |
| `ControlType` | `Kerntemperatur, Warmhaltetemperatur, Kuehltemperatur, Wareneingang` |
| `ControlStatus` | `OK, NOK` *(NOK added, see gap #15)* |
| `ProcurementListStatus` | `DRAFT, REVIEWED, ORDERED, COMPLETED` |
| `RouteStatus` | `GEPLANT, BELADUNG, UNTERWEGS, ABGESCHLOSSEN` |
| `RouteStopStatus` | `OFFEN, ZUGESTELLT, PROBLEM` |
| `SupportCategory` | `BUG, FRAGE, FEATURE` |
| `SupportPriority` | `NIEDRIG, NORMAL, HOCH, KRITISCH` |
| `SupportStatus` | `OFFEN, IN_BEARBEITUNG, GELOEST` |
| `SupportReplyRole` | `SUPER_ADMIN, TENANT_OWNER` |
| `SupportSessionEndReason` | `MANUAL, EXPIRED` |

---

## 4. Entity Catalog

Guid PKs and `CreatedAt`/`UpdatedAt` (`datetime2`, UTC) are omitted from the tables below where
standard — only non-obvious columns are called out per row. Unless noted, string fields are
`nvarchar` with a sensible length picked from observed mock data.

### 4.1 Identity & Tenancy

**Tenant** — a catering company (platform customer). *Purpose*: root of multi-tenant isolation.
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| Name | nvarchar(200) | No | Company name |
| Status | TenantStatus | No | Default `AKTIV` |
| MainContactName | nvarchar(200) | No | "Hauptansprechpartner" |
| MainContactEmail | nvarchar(256) | No | |
| CreatedAt | datetime2 | No | |

**TenantProfile** — 1:1 with Tenant. *Purpose*: company master data + branding (`admin/company`).
| Field | Type | Null | Notes |
|---|---|---|---|
| TenantId | Guid | PK, FK→Tenant | |
| VatId | nvarchar(50) | Yes | USt-IdNr. |
| Street | nvarchar(200) | Yes | |
| PostalCode | nvarchar(10) | Yes | |
| City | nvarchar(100) | Yes | |
| Phone | nvarchar(50) | Yes | |
| Email | nvarchar(256) | Yes | |
| Timezone | nvarchar(50) | No | Default `Europe/Berlin` |
| Currency | nvarchar(3) | No | Default `EUR` |
| LogoUrl | nvarchar(500) | Yes | |

**TenantSettings** — 1:1 with Tenant. *Purpose*: configurable business rules (`admin/settings`).
| Field | Type | Null | Notes |
|---|---|---|---|
| TenantId | Guid | PK, FK→Tenant | |
| DefaultOrderDeadlineOffsetDays | int | No | Default `1` (day before) |
| DefaultOrderDeadlineTime | time | No | Default `09:00` |
| ExcludeWeekendsFromDeadline | bit | No | Default `1` |
| RequireReviewBeforePublish | bit | No | Default `1` |
| UnpublishRequiresNoOrders | bit | No | Default `1` (fixed business rule, not user-editable) |
| FacilityNumberPrefix | nvarchar(10) | No | Default `DG-1` (pattern `DG-1###`) |
| ArticleNumberPrefix | nvarchar(10) | No | Default `ART-` |

**TenantNotificationSetting** — *Purpose*: per-event notification toggles.
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| EventKey | nvarchar(50) | No | `MealPlanPublished / DeadlineApproaching / OrderChangedAfterSubmit / ProductionPlanChanged` |
| Enabled | bit | No | |

Unique: `(TenantId, EventKey)`.

**FeatureFlag** — global catalog. **TenantFeatureFlag** — per-tenant override.
| FeatureFlag field | Type | Notes |
|---|---|---|
| Id, Key (nvarchar(50), unique), Name, Description, DefaultEnabled (bit) | | Seeded: Kundenportal, Naehrwert-API, Einkaufslisten, Bedarfsprognose, White-Label-Branding, Mehrsprachigkeit |

| TenantFeatureFlag field | Type | Notes |
|---|---|---|
| TenantId, FeatureFlagId | Guid, Guid | composite PK |
| Enabled | bit | |

**User** — *Purpose*: every login-capable identity across all roles.
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | Yes | Null only for `SUPER_ADMIN` (platform user) |
| FacilityId | Guid | Yes | FK→Facility, set only for `FACILITY_ADMIN`/`FACILITY_USER` |
| Name | nvarchar(200) | No | |
| Email | nvarchar(256) | No | Unique (login identifier) |
| PasswordHash | nvarchar(max) | No | `PasswordHasher<User>` output |
| Role | Role | No | |
| Status | UserStatus | No | Default `EINGELADEN` on invite |
| LastLoginAt | datetime2 | Yes | |
| FailedLoginCount | int | No | Default `0` |
| LockedUntil | datetime2 | Yes | Set after repeated failed logins |
| InvitationToken | nvarchar(200) | Yes | Set on invite, cleared on acceptance |
| InvitationExpiresAt | datetime2 | Yes | |

Unique: `Email`.

**Driver** — *Purpose*: driver-specific profile, 1:1 with a `User` whose `Role = DRIVER`.
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| UserId | Guid | FK→User, unique | |
| Phone | nvarchar(50) | No | |
| VehicleDescription | nvarchar(200) | No | e.g. "Mercedes Sprinter Kühl" |
| LicensePlate | nvarchar(20) | No | |

### 4.2 Locations & Facilities

**Location** (Standort) — a kitchen/production site.
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | *(gap #2)* |
| Name | nvarchar(200) | No | |
| Address | nvarchar(300) | No | |
| ContactPerson | nvarchar(200) | No | |
| CapacityPortions | int | No | |
| Status | LocationStatus | No | Default `AKTIV` |

**Facility** (Einrichtung) — a customer delivery site (school, clinic, ...).
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| LocationId | Guid | FK→Location | |
| Name | nvarchar(200) | No | |
| CustomerNumber | nvarchar(20) | No | Generated `{Prefix}{seq}`, e.g. `DG-1005` |
| Address | nvarchar(300) | No | |
| ContactPerson | nvarchar(200) | No | |
| Email | nvarchar(256) | No | |
| Phone | nvarchar(50) | No | |
| OrderDeadlineOffsetDays | int | Yes | Overrides `TenantSettings` default when set |
| OrderDeadlineTime | time | Yes | Overrides `TenantSettings` default when set |
| ActiveWeekdays | nvarchar(20) | No | CSV, e.g. `Mo,Di,Mi,Do,Fr` |
| PortionPrice | decimal(10,2) | No | Contracted €/portion, revenue basis |
| Status | FacilityStatus | No | Default `AKTIV` |
| Notes | nvarchar(1000) | Yes | |

Unique: `(TenantId, CustomerNumber)`.

### 4.3 Recipes & Ingredients

**RecipeCategory**, **IngredientCategory**, **Allergen**, **TargetAudienceGroup** — global seeded
lookup tables (`Id`, `Name`, unique `Name`). Seeded from the fixed lists found in
`REZEPT_KATEGORIEN`, `ZUTAT_KATEGORIEN`, the EU-14 `ALLERGENE_LISTE`, and `ZIELGRUPPEN_LISTE`.

**Supplier** — *Purpose*: normalizes `Zutat.lieferant` (gap #19).
| Field | Type | Null |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | FK→Tenant |
| Name | nvarchar(200) | No |
| ContactPerson | nvarchar(200) | Yes |
| Phone | nvarchar(50) | Yes |
| Email | nvarchar(256) | Yes |

**Ingredient** (Zutat)
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| Name | nvarchar(200) | No | |
| ArticleNumber | nvarchar(50) | No | |
| CategoryId | Guid | FK→IngredientCategory | |
| BaseUnit | Unit | No | |
| PurchaseUnit | nvarchar(100) | No | e.g. "Sack (25 kg)" |
| ConversionFactor | decimal(12,4) | No | Base units per purchase unit; `CHECK > 0` |
| PurchasePrice | decimal(12,2) | Yes | €/purchase unit |
| SupplierId | Guid | Yes | FK→Supplier |
| Vegetarian, Vegan, Bio, Regional, Active | bit | No | |
| Kcal, ProteinG, FatG, CarbsG, SugarG, SaltG | decimal(10,2) | No | Per 100 g/ml — EF owned type `IngredientNutrition` |
| NutritionSource | NutritionSource | No | |

Unique: `(TenantId, ArticleNumber)`.

**IngredientAllergen** (join: IngredientId, AllergenId — composite PK).
**IngredientAdditive** (`Id, IngredientId, Text nvarchar(100)`) — free text, per gap #20.

**Recipe** (Rezept)
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| Name | nvarchar(200) | No | |
| Description | nvarchar(2000) | No | |
| CategoryId | Guid | FK→RecipeCategory | |
| RecipeNumber | nvarchar(50) | Yes | |
| StandardPortions | int | No | |
| PortionWeightG | decimal(10,2) | Yes | |
| PrepTimeMinutes | int | No | |
| Difficulty | Difficulty | No | |
| Vegetarian, Vegan, Active | bit | No | |
| ProductionNotes | nvarchar(1000) | Yes | |
| ImageUrl | nvarchar(500) | Yes | |
| CoreTemperatureC | decimal(5,2) | Yes | |
| StorageNote | nvarchar(500) | Yes | |
| ShelfLifeAfterPrep | nvarchar(200) | Yes | |
| CreatedByUserId | Guid | FK→User | |
| Version | int | No | Incremented on every update |
| — Authoritative import fields (nullable; when present, override live-computed values) — | | | |
| Kcal, Kj, FatG, SaturatedFatG, CarbsG, SugarG, FiberG, ProteinG, SaltG, AlcoholG | decimal(10,2) | Yes | EF owned type `RecipeNutrition` |
| NutriScore | NutriScore | Yes | |
| NutriScoreCategory | nvarchar(100) | Yes | |

**RecipePrepStep** (`Id, RecipeId, StepNumber int, Text nvarchar(1000)`).
**RecipeIngredient** (RezeptZutat, own PK per gap #12): `Id, RecipeId, IngredientId, Quantity decimal(12,3), Unit`.
**RecipeAllergenOverride** / **RecipeAdditiveOverride** / **RecipeNutritionClaim** / **RecipeTargetGroup**:
simple child tables (`Id, RecipeId, Text`/`AllergenId`/`TargetAudienceGroupId`) holding the
authoritative imported values that take precedence over live ingredient-derived computation (see §9).

### 4.4 Meal Plans

**MealPlan** (Speiseplan)
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| CalendarWeek | int | No | ISO week |
| Year | int | No | ISO year |
| Status | MealPlanStatus | No | Default `DRAFT` |

Unique: `(TenantId, Year, CalendarWeek)` *(gap #23)*.

**MealPlanLocation** (join: MealPlanId, LocationId). **MealPlanFacility** (join: MealPlanId, FacilityId).
**MealPlanDay** (SpeiseplanTag): `Id, MealPlanId, Weekday nvarchar(20), Date date, Note nvarchar(500) nullable`.
**MealPlanItem**: `Id, MealPlanDayId, RecipeId, RecipeSnapshotJson nvarchar(max) nullable` *(gap #10 —
populated at publish time with the recipe's name/nutrition/allergens frozen as of that moment)*.

### 4.5 Orders

**Order** (Bestellung)
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| TenantId | Guid | FK→Tenant | |
| FacilityId | Guid | FK→Facility | |
| MealPlanId | Guid | FK→MealPlan | |
| Status | OrderStatus | No | Default `DRAFT` |
| SubmittedAt | datetime2 | Yes | |
| DeadlineAtUtc | datetime2 | No | Computed at save time from Facility/TenantSettings deadline config *(gap #22)* |

Unique: `(FacilityId, MealPlanId)` — upsert semantics, matching the mock's `saveBestellung`.

**OrderItem** (BestellPosition): `Id, OrderId, Date date, RecipeId, Portions int (CHECK >= 0), Note nvarchar(500) nullable`.

### 4.6 Production

**ProductionPlan** (Produktionsplan)
| Field | Type | Null |
|---|---|---|
| Id | Guid | PK |
| TenantId | Guid | FK→Tenant |
| Date | date | No |
| LocationId | Guid | FK→Location |

Unique: `(TenantId, Date, LocationId)`.

**ProductionPlanItem** (Produktionsposition)
| Field | Type | Null | Notes |
|---|---|---|---|
| Id | Guid | PK | |
| ProductionPlanId | Guid | FK→ProductionPlan | |
| RecipeId | Guid | FK→Recipe | |
| OrderedQuantity | int | No | `bestellteMenge`, refreshable from Orders (see §9) |
| AdjustmentQuantity | int | No | `zusatzMenge`, default `0` |
| AdjustmentReason | nvarchar(500) | Yes | |
| Status | ProductionItemStatus | No | Default `PLANNED` |
| WorkStatus | WorkStatus | No | Default `OFFEN` — unified admin+kitchen status *(gap #13)* |
| StagedQuantity | int | Yes | Real "how much is staged" field *(gap #14)* |
| Workstation | nvarchar(100) | Yes | Free text, matches `ProduktionsMetaEintrag.arbeitsplatz` |
| Equipment | nvarchar(100) | Yes | |
| StartTime | time | Yes | |
| FinishByTime | time | Yes | |
| BatchCount | int | Yes | |
| PortionsPerBatch | int | Yes | |
| ResponsiblePerson | nvarchar(200) | Yes | |

Unique: `(ProductionPlanId, RecipeId)`.

**ProductionAdjustment** — *Purpose*: audit trail for `AdjustmentQuantity` changes (gap #3).
`Id, ProductionPlanItemId, OldQuantity int, NewQuantity int, Reason nvarchar(500), ChangedByUserId, ChangedAt`.

### 4.7 Kitchen Operations

**Deviation** (Abweichung)
`Id, TenantId, ProductionPlanId nullable, Category (DeviationCategory), Subject nvarchar(200),
Quantity nvarchar(100) nullable, Action nvarchar(500), ReportedByUserId, ReportedAt,
Status (DeviationStatus), ResolvedAt nullable, ResolvedByUserId nullable`.

**QualityControl** (Kontrolle / HACCP)
`Id, TenantId, ProductionPlanId nullable, Type (ControlType), Area nvarchar(200),
TargetValue nvarchar(50), MeasuredValue nvarchar(50), PerformedByUserId, PerformedAt, Status (ControlStatus)`.

**StorageLocation** (Lagerort) — `Id, TenantId, Name nvarchar(100)`. `IngredientCategory` gets an
optional `DefaultStorageLocationId` FK.

*(Equipment/Workstation are kept as free-text columns on `ProductionPlanItem`, not their own
entities — the frontend has zero real CRUD for them, only display strings; see §2.)*

### 4.8 Procurement

**ProcurementList** (Einkaufsliste)
`Id, TenantId, Label nvarchar(200), CalendarWeek int, LocationId, Status (ProcurementListStatus)`.

**ProcurementListItem**
`Id, ProcurementListId, IngredientId, Unit, TotalQuantityBase decimal(12,3), PurchaseQuantity decimal(12,3)`.
Unique: `(ProcurementListId, IngredientId, Unit)` — matches the mock's aggregation key (ingredient +
unit, not unit-converted; see gap #21/§9). Contributing recipe names are derived via query at read
time (join through `ProductionPlanItem`/`RecipeIngredient`), not stored.

### 4.9 Logistics

**Route** (LieferRoute)
`Id, TenantId, Name nvarchar(200), Date date, DriverId, LocationId nullable (origin, gap #15* — see below),
PlannedDepartureTime time, PlannedReturnTime time nullable, DistanceKm decimal(6,1) nullable, Status (RouteStatus)`.

**RouteStop** (RoutenStopp)
`Id, RouteId, FacilityId, SequenceNumber int, PlannedArrivalTime time,
DeliveryWindowStart time nullable, DeliveryWindowEnd time nullable, ContactName nvarchar(200),
ContactPhone nvarchar(50), Note nvarchar(500) nullable, Status (RouteStopStatus) default OFFEN,
ProblemNote nvarchar(500) nullable, DeliveredAt datetime2 nullable`.

**RouteStopItem** (LieferPosition)
`Id, RouteStopId, RecipeId, OrderId nullable, OrderItemId nullable (gap #16), Portions int,
ContainerDescription nvarchar(100), TemperatureRequirement nvarchar(50), Note nvarchar(500) nullable,
IsPacked bit default 0, PackedAt datetime2 nullable, IsLoaded bit default 0, LoadedAt datetime2 nullable`.

### 4.10 Support & Platform Admin

**SupportTicket** — `Id, TenantId, CreatedByUserId, TicketNumber nvarchar(20) unique (e.g. SUP-1042,
via a SQL sequence), Category (SupportCategory), Priority (SupportPriority), Title nvarchar(200),
Message nvarchar(max), PageUrl nvarchar(500) nullable, Status (SupportStatus)`.
**SupportTicketReply** — `Id, TicketId, AuthorUserId, Role (SupportReplyRole), Text nvarchar(max)`.
**SupportSession** — `Id, TenantId, StartedByUserId, StartedAtUtc, ExpiresAtUtc, EndedAtUtc nullable,
EndedReason (SupportSessionEndReason) nullable`.

### 4.11 Cross-Cutting

**AuditLog** (AuditEintrag) — append-only, no update/delete endpoint.
`Id, TenantId nullable (null = platform-level), UserId nullable ("System" actions), Action nvarchar(200),
Entity nvarchar(100), EntityId nvarchar(100), OldValues nvarchar(max) nullable, NewValues nvarchar(max) nullable,
Reason nvarchar(500) nullable, IpAddress nvarchar(45) nullable, CreatedAtUtc datetime2`.

**Notification** (Benachrichtigung)
`Id, TenantId, RecipientUserId nullable (null = all tenant admins), Title nvarchar(200),
Text nvarchar(1000), IsRead bit default 0, CreatedAt`.

---

## 5. Relationships Overview

```
Tenant 1───1 TenantProfile
Tenant 1───1 TenantSettings
Tenant 1───N TenantNotificationSetting
Tenant 1───N TenantFeatureFlag ──N───1 FeatureFlag
Tenant 1───N User
User   1───1 Driver (nullable, only Role=DRIVER)
Tenant 1───N Location
Tenant 1───N Facility ──N───1 Location
Facility 1───N User (FACILITY_* roles)

Tenant 1───N Supplier
Tenant 1───N Ingredient ──N───1 IngredientCategory
Ingredient N───N Allergen (via IngredientAllergen)
Ingredient 1───N IngredientAdditive
Ingredient N───1 Supplier

Tenant 1───N Recipe ──N───1 RecipeCategory
Recipe  1───N RecipePrepStep
Recipe  1───N RecipeIngredient ──N───1 Ingredient
Recipe  1───N RecipeAllergenOverride / RecipeAdditiveOverride / RecipeNutritionClaim / RecipeTargetGroup
Recipe  1───N MealPlanItem (live pointer; snapshot frozen at publish)

Tenant 1───N MealPlan
MealPlan N───N Location (MealPlanLocation)
MealPlan N───N Facility (MealPlanFacility)
MealPlan 1───N MealPlanDay 1───N MealPlanItem ──N───1 Recipe
MealPlan 1───N Order ──N───1 Facility
Order   1───N OrderItem ──N───1 Recipe

Tenant 1───N ProductionPlan ──N───1 Location
ProductionPlan 1───N ProductionPlanItem ──N───1 Recipe
ProductionPlanItem 1───N ProductionAdjustment
ProductionPlan 1───N Deviation / QualityControl

Tenant 1───N ProcurementList ──N───1 Location
ProcurementList 1───N ProcurementListItem ──N───1 Ingredient

Tenant 1───N Route ──N───1 Driver, ──N───1 Location (origin, nullable)
Route   1───N RouteStop ──N───1 Facility
RouteStop 1───N RouteStopItem ──N───1 Recipe, ──0/1───Order/OrderItem

Tenant 1───N SupportTicket ──1───N SupportTicketReply
Tenant 1───N SupportSession

Tenant 0/1───N AuditLog (nullable Tenant = platform-level)
Tenant 1───N Notification
```

---

## 6. API Endpoints

Base path `/api`. All endpoints require `Authorization: Bearer <jwt>` unless marked **(anon)**.
Response envelope: `{ "success": true, "data": ..., "message": null }` /
`{ "success": false, "message": "..." }` per §21 of the brief. List endpoints support
`?page=&pageSize=&search=&sort=&dir=` where the frontend has a working search/filter/sort UI (noted
per-feature during analysis — many list views currently have none, so pagination is added
uniformly as good practice without inventing filter *semantics* the frontend doesn't have).

### Auth
| Method | Path | Auth | Notes |
|---|---|---|---|
| POST | `/auth/login` | anon | email + password → JWT + user/role/tenant info |
| GET | `/auth/me` | any | current user incl. role, tenant, facility |
| POST | `/auth/invitations/{token}/accept` | anon | sets password, activates invited user |
| POST | `/auth/forgot-password` | anon | rate-limited |
| POST | `/auth/reset-password` | anon | |

*No `/auth/register`* — platform has no self-registration (§2 intro).

### Super-Admin (Role=SUPER_ADMIN only)
| Method | Path | Notes |
|---|---|---|
| GET | `/super-admin/dashboard` | KPIs, §9 |
| GET, POST | `/super-admin/tenants` | list / create |
| GET, PUT | `/super-admin/tenants/{id}` | detail / edit |
| POST | `/super-admin/tenants/{id}/lock`, `/unlock`, `/archive` | body: `{ reason }` → AuditLog |
| GET | `/super-admin/tenants/{id}/users` | |
| GET | `/super-admin/users` | filter: tenantId, role, status |
| GET, PUT | `/super-admin/feature-flags` | global catalog |
| PUT | `/super-admin/tenants/{id}/feature-flags` | per-tenant override |
| POST | `/super-admin/tenants/{id}/support-sessions` | start |
| DELETE | `/super-admin/support-sessions/{id}` | end |
| GET | `/super-admin/audit-logs` | filter: tenantId, userId, action, entity, from, to |
| GET | `/super-admin/locations` | cross-tenant view of `Location` |
| GET | `/super-admin/system` | health checks + version |

### Tenant-scoped master data (role gates per §8)
| Method | Path | Permission |
|---|---|---|
| GET, PUT | `/tenants/current`, `/tenants/current/profile`, `/tenants/current/settings` | TENANT_OWNER/ADMIN |
| GET, POST | `/users` · GET, PUT `/users/{id}` | TENANT_OWNER/ADMIN, FACILITY_ADMIN (own facility only) |
| POST | `/users/{id}/deactivate`, `/activate`, `/resend-invitation` | TENANT_OWNER/ADMIN |
| GET, POST | `/locations` · GET, PUT `/locations/{id}` | TENANT_OWNER/ADMIN |
| GET, POST | `/facilities` · GET, PUT `/facilities/{id}` | TENANT_OWNER/ADMIN |
| GET, POST | `/drivers` · GET, PUT `/drivers/{id}` | TENANT_OWNER/ADMIN |

### Ingredients & Recipes
| Method | Path | Notes |
|---|---|---|
| GET, POST | `/ingredients` · GET, PUT `/ingredients/{id}` | search, category/allergen filter |
| POST | `/ingredients/{id}/deactivate` | |
| GET | `/ingredient-categories`, `/allergens` | lookups |
| GET | `/ingredients/export` (CSV) · POST `/ingredients/import` | |
| GET, POST | `/suppliers` · GET, PUT `/suppliers/{id}` | |
| GET, POST | `/recipes` · GET, PUT `/recipes/{id}` | search, category filter |
| POST | `/recipes/{id}/duplicate`, `/archive` | |
| GET | `/recipes/{id}/scale?portions=N` | server-side decimal scaling, §9 |
| GET | `/recipe-categories`, `/target-audience-groups` | lookups |

### Meal Plans & Orders
| Method | Path | Notes |
|---|---|---|
| GET, POST | `/meal-plans` · GET, PUT `/meal-plans/{id}` | |
| POST | `/meal-plans/{id}/duplicate`, `/submit-review`, `/publish`, `/unpublish`, `/archive` | publish creates recipe snapshots |
| GET | `/meal-plans/{id}/preview?facilityId=` | |
| GET | `/portal/meal-plans` | published plans for the caller's facility |
| GET, POST | `/orders` · GET, PUT `/orders/{id}` | server-enforced deadline check |
| POST | `/orders/{id}/submit`, `/override` | override requires `{ reason }` → AuditLog |
| GET | `/orders/{id}/history` | from AuditLog |
| GET | `/facilities/current`, `/facilities/current/users` | FACILITY_ADMIN |

### Production & Kitchen
| Method | Path | Notes |
|---|---|---|
| GET, POST | `/production-plans` · GET `/production-plans/{date}?locationId=` | |
| PUT | `/production-plans/{planId}/items/{id}` | status/work-status/staging |
| POST | `/production-plans/{planId}/items/{id}/adjustments` | body: `{ quantity, reason }` → ProductionAdjustment + AuditLog |
| GET | `/production-plans/{planId}/requirements` | ingredient aggregation, §9 |
| GET, POST | `/deviations` · POST `/deviations/{id}/resolve` | |
| GET, POST | `/quality-controls` | |

### Procurement
| Method | Path | Notes |
|---|---|---|
| POST | `/procurement-lists/generate` | body: `{ date/week, locationId }` |
| GET | `/procurement-lists` · GET `/procurement-lists/{id}` | |
| PUT | `/procurement-lists/{id}/items/{itemId}` | edit purchase quantity |
| PUT | `/procurement-lists/{id}/status` | |
| GET | `/procurement-lists/{id}/export` | CSV |

### Logistics
| Method | Path | Notes |
|---|---|---|
| GET, POST | `/routes` · GET, PUT `/routes/{id}` | admin creates, auto-populates stops from confirmed orders |
| PUT | `/routes/{id}/status` | |
| GET | `/drivers/current/routes` · GET `/drivers/current/routes/today` | driver-scoped |
| PUT | `/routes/{id}/stops/{stopId}/status` | body: `{ status, problemNote? }` |
| PUT | `/routes/{id}/stops/{stopId}/items/{itemId}/packed`, `/loaded` | |

### Revenue & Dashboards
| Method | Path | Notes |
|---|---|---|
| GET | `/dashboard/admin-summary`, `/dashboard/portal-summary` | real KPI formulas, §9 |
| GET | `/revenue?from=&to=&groupBy=week` | |

### Support & Notifications
| Method | Path | Notes |
|---|---|---|
| GET, POST | `/support/tickets` · GET `/support/tickets/{id}` | |
| POST | `/support/tickets/{id}/replies` | auto-transitions OFFEN→IN_BEARBEITUNG |
| PUT | `/support/tickets/{id}/status` | |
| GET, PUT | `/notifications` · POST `/notifications/{id}/read` | |

### Cross-cutting
| Method | Path | Notes |
|---|---|---|
| GET | `/audit-logs` | tenant-scoped (non-super-admin) |
| GET | `/health`, `/health/ready` | anon, liveness/DB check |

---

## 7. Authentication & Authorization

- **Login**: `POST /auth/login` with `{ email, password }` → verify via `PasswordHasher<User>`,
  increment `FailedLoginCount`/set `LockedUntil` after 5 failures (15-minute lock), reset on success,
  update `LastLoginAt`. Returns a JWT with claims: `sub` (UserId), `tenantId` (nullable), `role`,
  `facilityId` (nullable), `driverId` (nullable), `email`, `name`. Expiration from
  `Jwt:ExpirationMinutes` config (default 1440).
- **Validation**: standard `JwtBearerOptions` (issuer, audience, signing key from `Jwt:Secret` env
  var — never hardcoded, never committed).
- **No self-registration.** Users are created via `POST /users` (tenant admin) or
  `POST /super-admin/tenants` (initial tenant owner), both invitation-based: `InvitationToken`
  (72h expiry) is emailed, `POST /auth/invitations/{token}/accept` sets the password and activates.
- **Roles**: enforced with `[Authorize(Roles = "...")]` policies per §6. `FACILITY_ADMIN`/`FACILITY_USER`
  additionally get a facility-scope check (their `FacilityId` claim must match the resource) in the
  handler layer, since a role attribute alone can't express "own facility only".
- **Tenant isolation**: EF Core global query filter (`HasQueryFilter`) on every entity with a
  `TenantId` column, comparing against a scoped `ITenantContext` populated from the JWT claim in
  middleware. `SUPER_ADMIN` endpoints explicitly call `IgnoreQueryFilters()`.
- **Current-user endpoint**: `GET /auth/me` returns the full profile incl. role/tenant/facility —
  this is what the frontend's currently-hardcoded `layout.tsx` `userName`/`userRole` props get
  replaced with in Phase 5.
- **Passwords**: never returned in any DTO. `PasswordHasher<User>` (PBKDF2-based, framework-provided,
  per §15 of the brief).
- **Super-admin passphrase gate** (`SuperAdminGate`, `sessionStorage` + hardcoded `"gentle2026"`) is
  explicitly a Phase-1 placeholder per its own code comment — removed entirely in Phase 5, replaced
  by real `Role=SUPER_ADMIN` JWT authorization.

---

## 8. Business Rules & Computed Values

Several client-side calculations found during analysis move to the backend as-is (server-side
`decimal` arithmetic replacing the frontend's `Math.round`/float math, per the recipe-scaling
component's own comment: *"In Phase 2 rechnet das Backend mit decimal-Arithmetik"*):

- **Recipe scaling**: `factor = targetPortions / Recipe.StandardPortions`; each `RecipeIngredient.Quantity * factor`, rounded to 2 decimals via `decimal` rounding (not float).
- **Live allergen/additive union**: when `Recipe` has no authoritative override rows, union the `Allergen`/additive sets of all its `RecipeIngredient.Ingredient`s. When override rows exist, they take precedence (matches the frontend's `naehrwertePro100g`-precedence pattern exactly).
- **Nutrition per portion**: `value * (PortionWeightG / 100)`, kcal/kJ rounded to whole numbers, macros to 1 decimal, salt to 2 decimals — same rounding granularity the mock uses.
- **Bio/regional share**: mass-normalize each `RecipeIngredient` (`kg`/`l` → factor 1, `g`/`ml` → factor 0.001, `Stueck` excluded from the denominator — matches the mock's `MASSE_FAKTOR` table exactly), then `round(flaggedMass / totalMass * 100)`.
- **Recipe cost ("Wareneinsatz")**: `Σ Quantity * (Ingredient.PurchasePrice / Ingredient.ConversionFactor)`, guarding `ConversionFactor > 0` (enforced by a `CHECK` constraint at the schema level too).
- **Unit-compatibility validation** (gap #21): when saving an `Ingredient`, reject a `ConversionFactor` update whose purchase-unit/base-unit pairing crosses mass↔volume (`g`/`kg` ↔ `ml`/`l`) unless an explicit density factor is supplied — closing the gap where the frontend only asserts this in UI copy.
- **Ingredient requirement aggregation** (production → kitchen): for each `ProductionPlanItem`, `scaleFactor = (OrderedQuantity + AdjustmentQuantity) / Recipe.StandardPortions`; aggregate `RecipeIngredient.Quantity * scaleFactor` grouped by `(IngredientId, Unit)` — same key the mock uses (no cross-unit merging, per gap #21 — documented as a known limitation, not silently "fixed" beyond what's asked).
- **Procurement generation**: same aggregation as above, further converted to `PurchaseQuantity = ceil(TotalQuantityBase / Ingredient.ConversionFactor)`.
- **Order deadline** (gap #22): `DeadlineAtUtc` computed at order save time as `earliest OrderItem.Date − DeadlineOffsetDays` (Facility override or TenantSettings default) `@ DeadlineTime`, weekend-adjusted if `ExcludeWeekendsFromDeadline`. `POST /orders/{id}/submit` and item edits are rejected with `400` once `UtcNow > DeadlineAtUtc`, except via `POST /orders/{id}/override` (requires `reason`, `TENANT_OWNER`/`TENANT_ADMIN` only).
- **Revenue**: unchanged formula, now server-side — `Σ OrderItem.Portions` for orders with `Status ∈ {SUBMITTED, CONFIRMED, LOCKED}` `× Facility.PortionPrice`, rounded to cents. Grouped by ISO week for the revenue chart.
- **Dashboard KPIs** (gap #9), replacing the mock's hardcoded literals:
  - Admin: this-week order count + binding/draft split; today's total portions (`Σ OrderItem.Portions` where `Date = today` across confirmed production); facilities without a submitted order for the current week; current/next week's `MealPlan.Status`.
  - Portal: current published plan's week; own facility's next deadline; own order's status for the current plan.
  - Super-admin: tenant counts by status; total users + active-in-last-7-days (`LastLoginAt >= now-7d`); facility count across tenants; this-week order count platform-wide; failed logins in the last 24h.

---

## 9. Frontend → Backend Mapping

One row per frontend page/operation. `Handler`/`Repository` names are illustrative (Phase 4 may
adjust naming, not shape).

| Frontend page | Operation | Endpoint | Handler | Repository | Table(s) |
|---|---|---|---|---|---|
| `/login` | Sign in | `POST /auth/login` | `AuthHandler` | `UserRepository` | User |
| `/admin/recipes` | List/search recipes | `GET /recipes` | `RecipeHandler` | `RecipeRepository` | Recipe |
| `/admin/recipes/new` | Create recipe | `POST /recipes` | `RecipeHandler` | `RecipeRepository` | Recipe, RecipeIngredient, RecipePrepStep |
| `/admin/recipes/[id]` | Edit / duplicate / scale | `PUT /recipes/{id}`, `POST /recipes/{id}/duplicate`, `GET /recipes/{id}/scale` | `RecipeHandler` | `RecipeRepository` | Recipe, RecipeIngredient |
| `/admin/ingredients` | List/search/CSV export | `GET /ingredients`, `GET /ingredients/export` | `IngredientHandler` | `IngredientRepository` | Ingredient, Allergen |
| `/admin/ingredients/new`, `[id]` | Create/edit ingredient | `POST/PUT /ingredients` | `IngredientHandler` | `IngredientRepository` | Ingredient, Supplier |
| `/admin/meal-plans` | List/duplicate plans | `GET /meal-plans`, `POST /meal-plans/{id}/duplicate` | `MealPlanHandler` | `MealPlanRepository` | MealPlan |
| `/admin/meal-plans/new` | Create weekly plan | `POST /meal-plans` | `MealPlanHandler` | `MealPlanRepository` | MealPlan, MealPlanDay, MealPlanLocation, MealPlanFacility |
| `/admin/meal-plans/[id]` | Edit days/status/publish | `PUT /meal-plans/{id}`, `POST .../publish` etc. | `MealPlanHandler` | `MealPlanRepository` | MealPlan, MealPlanItem |
| `/portal/meal-plans` | View + place order | `GET /portal/meal-plans`, `POST/PUT /orders` | `OrderHandler` | `OrderRepository` | Order, OrderItem |
| `/admin/orders` | Board: confirm/lock/correct/CSV | `PUT` status routes, `POST /orders/{id}/override` | `OrderHandler` | `OrderRepository` | Order, AuditLog |
| `/portal/orders` | Order history | `GET /orders?facilityId=` | `OrderHandler` | `OrderRepository` | Order, OrderItem |
| `/admin/production` | List/create plans | `GET/POST /production-plans` | `ProductionHandler` | `ProductionRepository` | ProductionPlan |
| `/admin/production/[id]` | Adjustments, refresh | `PUT items/{id}`, `POST .../adjustments` | `ProductionHandler` | `ProductionRepository` | ProductionPlanItem, ProductionAdjustment |
| `/kitchen`, `/kitchen/plans` | Work status | `PUT production-plans/{id}/items/{id}` | `ProductionHandler` | `ProductionRepository` | ProductionPlanItem |
| `/kitchen/requirements` | Ingredient aggregation | `GET /production-plans/{id}/requirements` | `ProductionHandler` | `ProductionRepository`, `IngredientRepository` | ProductionPlanItem, RecipeIngredient |
| `/kitchen/packing` | Pack/load toggles | `PUT stops/{id}/items/{id}/packed|loaded` | `LogisticsHandler` | `RouteRepository` | RouteStopItem |
| `/kitchen/controls` | HACCP checks | `GET/POST /quality-controls` | `KitchenOpsHandler` | `QualityControlRepository` | QualityControl |
| `/kitchen/deviations` | Deviations | `GET/POST /deviations`, `.../resolve` | `KitchenOpsHandler` | `DeviationRepository` | Deviation |
| `/admin/procurement` | Generate/edit/CSV | `POST .../generate`, `PUT items/{id}` | `ProcurementHandler` | `ProcurementRepository` | ProcurementList, ProcurementListItem |
| `/admin/revenue` | Revenue chart | `GET /revenue` | `DashboardHandler` | `OrderRepository` | Order, Facility |
| `/admin/routes` | Route manager | `GET/POST /routes` | `LogisticsHandler` | `RouteRepository` | Route, RouteStop, RouteStopItem |
| `/driver`, `/driver/routes` | Driver dashboard | `GET /drivers/current/routes*` | `LogisticsHandler` | `RouteRepository` | Route |
| `/driver/routes/[id]` | Stop delivery/problem | `PUT stops/{id}/status` | `LogisticsHandler` | `RouteRepository` | RouteStop |
| `/admin/facilities` | Facilities CRUD | `GET/POST/PUT /facilities` | `FacilityHandler` | `FacilityRepository` | Facility |
| `/admin/company` | Profile + branding | `GET/PUT /tenants/current/profile` | `TenantHandler` | `TenantRepository` | TenantProfile |
| `/admin/settings` | Tenant settings | `GET/PUT /tenants/current/settings` | `TenantHandler` | `TenantRepository` | TenantSettings, TenantNotificationSetting |
| `/portal/profile` | Own facility + users | `GET /facilities/current`, `.../users` | `FacilityHandler` | `FacilityRepository`, `UserRepository` | Facility, User |
| `/admin/dashboard` | KPIs | `GET /dashboard/admin-summary` | `DashboardHandler` | multiple | Order, MealPlan, Facility, Notification |
| `/portal/dashboard` | KPIs | `GET /dashboard/portal-summary` | `DashboardHandler` | multiple | Order, MealPlan |
| `/super-admin/tenants` | Tenant CRUD/lock/archive | `/super-admin/tenants*` | `SuperAdminTenantHandler` | `TenantRepository` | Tenant, AuditLog |
| `/super-admin/tenants/[id]` | Detail, support access | `.../support-sessions` | `SuperAdminTenantHandler`, `SupportHandler` | `TenantRepository`, `SupportSessionRepository` | Tenant, SupportSession |
| `/super-admin/users` | Global user list | `GET /super-admin/users` | `SuperAdminUserHandler` | `UserRepository` | User |
| `/super-admin/locations` | Cross-tenant locations | `GET /super-admin/locations` | `SuperAdminHandler` | `LocationRepository` | Location |
| `/super-admin/audit` | Audit log | `GET /super-admin/audit-logs` | `AuditHandler` | `AuditLogRepository` | AuditLog |
| `/super-admin/features` | Feature flags | `GET/PUT /super-admin/feature-flags` | `FeatureFlagHandler` | `FeatureFlagRepository` | FeatureFlag, TenantFeatureFlag |
| `/super-admin/system` | Health/version | `GET /super-admin/system` | `SystemHandler` | — | live checks, no table |
| `/super-admin/support`, `TenantSupportWidget` | Tickets | `/support/tickets*` | `SupportHandler` | `SupportTicketRepository` | SupportTicket, SupportTicketReply |
| `SupportModeBanner` | Session status | `GET /auth/me` (includes active session flag) | `AuthHandler` | `SupportSessionRepository` | SupportSession |

---

## 10. Seed Data Plan

Seed data is derived directly from `src/lib/data/index.ts` and every `src/features/*/data.ts`,
preserving names, categories, statuses, prices, and relationships exactly as found (per the brief's
"preserve names/categories/statuses/prices" instruction) — only IDs become real GUIDs and
placeholder/mock-marker values (e.g. `"— (folgt über offene Zutaten-API)"` supplier names, `z-107`'s
fake staged-quantity) are seeded as their honest equivalents (`Supplier = null`, `StagedQuantity = null`)
rather than reproducing the mock's fakery.

- 1 Tenant ("Daily Gourmet") + 1 Location ("Daily Gourmet", Wuppertal) + 4 Facilities (`DG-1001..1004`).
- 8 Users, one per role in the original `benutzer[]` seed, **plus** one new `DRIVER` user (Markus
  Becker, matching the frontend's hardcoded driver identity) linked to a `Driver` row.
- 91 Recipes + ~153 Ingredients from the real labeling-export data already in `features/recipes/data.ts`
  / `features/ingredients/data.ts` (these are the most complete, real-world seed data in the repo).
- 6 Orders, 2 MealPlans (current + prior week), matching current statuses.
- Documented dev/test accounts (email + role, no plaintext passwords committed — generated on first
  seed run and printed once, or set via a seed-time env var) listed in `README.md`.

---

## 11. Explicitly Out of Scope / Deferred

Per §4 of the brief ("do not invent business logic"), the following are **not** built now because
the frontend gives no basis for them:

- **Menu lines ("Menülinien")** — mentioned only in prose docs, zero frontend implementation.
- **Proof of delivery** (signature/photo capture, delivered-vs-planned reconciliation) beyond a
  simple status + timestamp.
- **Brand colors** on `TenantProfile` — only a logo field is modeled.
- **Batch/lot tracking ("Chargen")** as a first-class entity — kept as free-text fields, matching
  the frontend's own level of detail.
- **Per-tenant feature-flag override UI** — backend supports it; frontend screen is a fast-follow.
- **Nutrition API integration** (Open Food Facts / USDA) — the frontend's own comments mark this as
  a later phase ("kommen erst mit Anbindung der offenen Zutaten-API"); `NutritionSource` enum and
  manual-entry fields are ready to receive it, but the live API proxy is not built in this pass
  unless requested separately.
