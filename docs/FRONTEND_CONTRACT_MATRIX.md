# Frontend ↔ Backend Contract-Matrix

Quelle: `frontend/src/app/**` (Routen), `frontend/src/lib/types.ts` + `frontend/src/features/*/types.ts` (Contracts), `frontend/src/features/*/data.ts` (Mock-Shapes), abgeglichen mit `docs/api-endpunkte.md`. Alle Endpunkte sind aktuell **TODO** — es existiert noch kein Backend. Feldnamen in "Response" sind die exakten Frontend-Interface-Namen (§21/§58: Contracts spiegeln das Frontend 1:1).

Permission-Kürzel wie in `docs/backend-architektur.md §5` / instructions §18.

## Auth (`frontend/src/app/login`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Login | `app/login/page.tsx` (aktuell statisches Formular ohne `onSubmit`) | `POST /api/v1/auth/login` | `{ email, password }` | `204` + Set-Cookie | TODO |
| Logout | — | `POST /api/v1/auth/logout` | — | `204` | TODO |
| Aktueller Benutzer | jedes `layout.tsx` hardcodet aktuell `userName`/`userRole` | `GET /api/v1/auth/me` | — | `Benutzer` + `permissions[]` + `tenant` | TODO |
| Sessions | — | `GET/DELETE /api/v1/auth/sessions`, `POST /auth/sessions/revoke-all` | — | `Session[]` | TODO |
| Einladung annehmen | — | `GET/POST /api/v1/auth/invitations/{token}` | `{ password }` | `204` | TODO |

## Super-Admin (`frontend/src/app/super-admin/*`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Dashboard | `super-admin/dashboard` | `GET /api/v1/super-admin/dashboard` | — | Kennzahlen | TODO |
| Mandanten-Liste | `super-admin/tenants` | `GET /api/v1/super-admin/tenants` | Filter `status,q` | `Tenant[]` (`features/tenants/types.ts`) | TODO |
| Mandant Detail | `super-admin/tenants/[id]` | `GET/PATCH /api/v1/super-admin/tenants/{id}` | — | `Tenant` | TODO |
| Mandant sperren/entsperren/archivieren | Tenant-Detail Aktionen | `POST .../activate|suspend|archive|restore` | `{ reason }` | `204` | TODO |
| Benutzer (global) | `super-admin/users` | `GET /api/v1/super-admin/users` | Filter `tenantId,role,status` | `Benutzer[]` | TODO |
| Standorte (global) | `super-admin/locations` | `GET /api/v1/super-admin/tenants/{id}/facilities` (o. globale Sicht) | — | `Standort[]` | TODO |
| Audit | `super-admin/audit` | `GET /api/v1/super-admin/audit-logs` | Filter | `AuditEintrag[]` | TODO |
| Feature-Flags | `super-admin/features` (`featureFlagsSeed` in `features/support/data.ts`) | `GET/PUT /api/v1/super-admin/features`, `PUT .../tenants/{id}/features` | `Record<string,bool>` | — | TODO |
| System | `super-admin/system` | `GET /api/v1/super-admin/system/health|version|outbox` | — | — | TODO |
| Support-Zugriff | `super-admin/support` (`SupportSitzung`) | `POST .../tenants/{id}/support-session`, `DELETE .../support-sessions/{id}` | `{ reason }` | `SupportSession` | TODO |

## Mandant-Stammdaten & Benutzerverwaltung (`frontend/src/app/admin/company`, `app/admin/facilities`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Unternehmensdaten | `admin/company` | `GET/PATCH /api/v1/tenant`, `/tenant/settings` | — | — | TODO |
| Standorte | `admin/facilities` (Standort-Teil) | `GET/POST /api/v1/locations`, `GET/PATCH .../{id}`, `.../activate|deactivate|archive` | `Standort` | `Standort` | TODO |
| Einrichtungen | `admin/facilities` (Einrichtung-Teil) | `GET/POST /api/v1/facilities`, `GET/PATCH .../{id}` | `Einrichtung` | `Einrichtung` | TODO |
| Benutzer & Rollen | (kein eigener Screen im aktuellen Frontend-Scope, aber `Benutzer.rolle`/`einrichtungId` sind bereits modelliert) | `GET/POST /api/v1/users`, `PUT .../{id}/roles`, `PUT .../{id}/facilities` | `Benutzer` | `Benutzer` | TODO |

## Zutaten (`frontend/src/app/admin/ingredients`, `[id]`, `new`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Liste | `admin/ingredients` | `GET /api/v1/ingredients` | Filter `q,kategorie,allergen,aktiv` | `Zutat[]` | TODO |
| Anlegen | `admin/ingredients/new` | `POST /api/v1/ingredients` | `Zutat` (ohne `id`) | `Zutat` | TODO |
| Detail/Bearbeiten | `admin/ingredients/[id]` | `GET/PATCH /api/v1/ingredients/{id}` | `Zutat` | `Zutat` | TODO |
| Deaktivieren/Archivieren | Detail-Aktion | `POST .../archive`, `.../restore` | — | `204` | TODO |
| Kategorien | `ZUTAT_KATEGORIEN` (statische Konstante) | `GET/POST /api/v1/ingredient-categories` | — | `ZutatKategorie[]` | TODO |
| Allergene | `ALLERGENE_LISTE` (statische Konstante, 14 EU-Allergene) | `GET /api/v1/allergens` | — | `Allergen[]` (global geseedet) | TODO |
| Nährwerte | `Naehrwerte` (Quelle: "Open Food Facts"/"USDA"/"Manuell") | `GET /api/v1/nutrition/search`, `/nutrition/product/{ean}` | `q` oder `ean` | normalisierte Nährwerte | TODO — abhängig von [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) |

## Rezepte (`frontend/src/app/admin/recipes`, `[id]`, `new`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Liste | `admin/recipes` | `GET /api/v1/recipes` | Filter `q,kategorie,aktiv` | `Rezept[]` | TODO |
| Anlegen | `admin/recipes/new` | `POST /api/v1/recipes` | `Rezept` (ohne `id`) | `Rezept` + `version=1` | TODO |
| Detail/Bearbeiten | `admin/recipes/[id]` | `GET/PATCH /api/v1/recipes/{id}` | `Rezept` | `Rezept` (inkl. berechneter `naehrwertePro100g`, `allergeneErfasst`) | TODO |
| Duplizieren/Archivieren | Detail-Aktion | `POST .../duplicate`, `.../archive` | — | `Rezept` bzw. `204` | TODO |
| Versionen | (noch kein UI, aber `Rezept.version` existiert bereits) | `GET .../versions`, `.../versions/{versionId}` | — | `RezeptVersion[]` | TODO |
| Skalierung | (noch kein UI) | `GET .../scale?portions=` | `portions` | hochgerechnete `RezeptZutat[]` | TODO |

## Speisepläne (`frontend/src/app/admin/meal-plans`, `[id]`, `new`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Liste | `admin/meal-plans` | `GET /api/v1/meal-plans` | Filter `jahr,kalenderwoche,status` | `Speiseplan[]` | TODO |
| Anlegen | `admin/meal-plans/new` | `POST /api/v1/meal-plans` | `{ kalenderwoche, jahr, standortIds, einrichtungIds }` | `Speiseplan` | TODO |
| Detail/Bearbeiten | `admin/meal-plans/[id]` | `GET/PATCH /api/v1/meal-plans/{id}` | `SpeiseplanTag[]` | `Speiseplan` | TODO |
| Duplizieren | Detail-Aktion | `POST .../duplicate` | — | `Speiseplan` | TODO |
| Review/Publish/Unpublish/Archive | Status-Aktionen | `POST .../submit-review|publish|unpublish|archive` | — | `204` | TODO |
| Standorte/Einrichtungen zuordnen | Detail-Formular | `PUT .../facilities`, `.../locations` | `string[]` | — | TODO |
| Vorschau | — | `GET .../preview?facilityId=` | — | Einrichtungssicht | TODO |

## Kundenportal (`frontend/src/app/portal/*`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Dashboard | `portal/dashboard` | `GET /api/v1/portal/facilities/{facilityId}/dashboard` | — | Kennzahlen | TODO |
| Veröffentlichte Pläne | `portal/meal-plans` | `GET /api/v1/portal/facilities/{facilityId}/meal-plans[/{id}]` | — | `Speiseplan` (nur PUBLISHED) | TODO |
| Bestellungen | `portal/orders` | `GET /api/v1/portal/facilities/{facilityId}/orders` | — | `Bestellung[]` | TODO |
| Bestellung bearbeiten | Bestell-Formular je Plan | `GET/PUT .../meal-plans/{id}/order` | `BestellPosition[]` | `Bestellung` — **serverseitige Fristprüfung** | TODO |
| Bestellung absenden | — | `POST .../order/submit` | — | `Bestellung` (`SUBMITTED`) | TODO |
| Stornieren | — | `POST /api/v1/portal/orders/{orderId}/cancel` | `{ reason? }` | `204` | TODO |
| Verlauf | — | `GET /api/v1/portal/orders/{orderId}/history` | — | `BestellVerlauf[]` | TODO |
| Profil | `portal/profile` | `GET /api/v1/facilities/current` | — | `Einrichtung` | TODO |

## Administrative Bestellungen (`frontend/src/app/admin/orders`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Übersicht (Board) | `admin/orders` (`components/orders-board.tsx`, `orders/store.ts`) | `GET /api/v1/orders` | Filter `status,einrichtungId` | `Bestellung[]` | TODO |
| Fehlende Bestellungen | Board-Filter | `GET /api/v1/orders/missing` | — | `Einrichtung[]` ohne Bestellung | TODO |
| Bestätigen/Sperren/Stornieren | Board-Aktionen | `POST .../confirm|lock|cancel` | — | `204` | TODO |
| Korrektur nach Fristablauf | — | `POST .../adjustments` | `{ positionen, begruendung }` **Pflichtfeld** | `Bestellung` + `BestellVerlauf` | TODO |

## Produktion (`frontend/src/app/admin/production`, `[id]`, `new`, `app/kitchen/*`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Pläne (Admin) | `admin/production` | `GET /api/v1/production-plans` | Filter `from,to,standortId` | `ProduktionsPlan[]` | TODO |
| Generieren | `admin/production/new` | `POST /api/v1/production-plans/generate` | `{ datum, standortId }` | `ProduktionsPlan` | TODO |
| Detail | `admin/production/[id]` | `GET .../{id}` | — | `ProduktionsPlan` inkl. `ProduktionsPosition[]` | TODO |
| Neuberechnen/Start/Abschließen/Stornieren | Detail-Aktionen | `POST .../recalculate|start|complete|cancel` | — | `204` | TODO |
| Kitchen-Board (heute) | `kitchen/page.tsx` (`KitchenWorkStatus`) | eigener Kitchen-Bereich, liest `ProduktionsPosition` + Status-Erweiterung | — | — | TODO — Kitchen-Status (`OFFEN…ABHOLBEREIT`) ist feingranularer als `ProduktionsStatus`; Mapping in Application-Layer, kein eigenes DB-Modell nötig |
| Kitchen: Wochenpläne, Rezepte, Anforderungen, Kontrollen, Abweichungen, Packing | `kitchen/plans`, `/requirements`, `/controls`, `/deviations`, `/packing` | erweitert obige Endpunkte um Kitchen-spezifische Projektionen | — | — | TODO |

## Einkauf (`frontend/src/app/admin/procurement`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Listen | `admin/procurement` | `GET /api/v1/procurement-lists` | Filter | `Einkaufsliste[]` | TODO |
| Generieren | Listen-Aktion | `POST /api/v1/procurement-lists/generate` | `{ standortId, kalenderwoche }` | `Einkaufsliste` | TODO |
| Detail | — | `GET .../{id}` | — | `Einkaufsliste` inkl. `EinkaufslistenPosition[]` | TODO |
| Status ändern | Detail-Aktionen | `POST .../review|mark-ordered|complete|cancel` | — | `204` | TODO |
| Position bearbeiten | — | `PATCH .../items/{itemId}` | — | — | TODO |
| Export | — | `GET .../export`, `.../print-data` | — | CSV/Druck | TODO |

## Logistik / Fahrer (`frontend/src/app/driver/*`) — nicht im ursprünglichen Endpunkt-Katalog, ergänzt

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Routenübersicht | `driver/page.tsx`, `driver/routes` | `GET /api/v1/logistics/routes` (neu, Phase 10+) | Filter `datum,fahrerId` | `LieferRoute[]` | TODO |
| Routendetail | `driver/routes/[id]` | `GET .../{id}` | — | `LieferRoute` inkl. `RoutenStopp[]` | TODO |
| Stopp-Status | Stopp-Aktion | `PATCH .../stops/{id}` | `{ status }` | `204` | TODO |

## Reporting (`frontend/src/app/admin/revenue`, `app/admin/routes`)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Umsatz | `admin/revenue` (`UmsatzZeile`, abgeleitet aus `bestellungUmsatz`) | reine Read-Projection über `Bestellung`+`Einrichtung.portionspreis`, kein eigenes Aggregat | — | `UmsatzZeile[]` | TODO |

## Notifications & Audit (übergreifend)

| Feature | Frontend | Backend Endpoint | Request | Response | Status |
|---|---|---|---|---|---|
| Benachrichtigungen | `Benachrichtigung` in `lib/types.ts`, in `AppShell` genutzt | `GET /api/v1/notifications`, `PATCH .../{id}/read`, `POST .../read-all` | — | `Benachrichtigung[]` | TODO |
| Audit (Mandant) | `AuditEintrag` in `lib/types.ts` | `GET /api/v1/audit-logs` | Filter | `AuditEintrag[]` | TODO |

---

**Hinweis:** Diese Matrix wird bei jedem abgeschlossenen Feature aktualisiert (Status `TODO → IN PROGRESS → DONE`), siehe §58. Bis Phase 2 abgeschlossen ist, bleibt alles auf `TODO`.
