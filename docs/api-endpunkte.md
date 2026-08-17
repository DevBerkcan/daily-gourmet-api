# API-Endpunkte — /api/v1 (Phase 2)

> **Status 2026-08-15: durch `BACKEND_IMPLEMENTATION_PLAN.md` (Repo-Root, Abschnitt 6) ersetzt.**
> Der neue Plan nutzt Pfade ohne `/v1`-Präfix und ein `{ success, data, message }`-Antwortformat
> statt `{ error: { code, message, details? } }`. Die Endpunktliste unten war die Basis für die
> Analyse, ist aber nicht mehr die verbindliche Quelle — siehe `BACKEND_IMPLEMENTATION_PLAN.md`
> Abschnitt 6 für den aktuellen Stand.

Konventionen: JSON, Pagination via `?page&pageSize&sort&dir`, Fehlerformat `{ error: { code, message, details? } }`.
Jeder Endpunkt prüft **Authentifizierung → Mandant → Permission** in dieser Reihenfolge. In Klammern: benötigte Permission.

## Auth (`/auth`)
| Methode | Pfad | Zweck |
|---|---|---|
| POST | `/auth/login` | Login (E-Mail + Passwort), setzt Session-Cookie · Rate-limitiert |
| POST | `/auth/logout` | Session beenden |
| POST | `/auth/refresh` | Session verlängern |
| GET | `/auth/me` | Aktueller Benutzer inkl. Rollen, Permissions, Tenant, Facility-Zuordnung |
| POST | `/auth/forgot-password` | Reset-Mail anfordern · Rate-limitiert |
| POST | `/auth/reset-password` | Passwort mit Token setzen |
| POST | `/auth/invitations/accept` | Einladung annehmen (Token + Passwort) |
| POST | `/auth/sessions/revoke-all` | Alle eigenen Sessions beenden |

## Super-Admin (`/super-admin`) — nur SUPER_ADMIN
| Methode | Pfad | Zweck |
|---|---|---|
| GET | `/super-admin/dashboard` | Kennzahlen: Mandanten, Benutzer, Bestellungen, Fehler, Logins |
| GET/POST | `/super-admin/tenants` | Mandanten listen (Filter: status, q) / anlegen (inkl. Hauptansprechpartner) |
| GET/PUT | `/super-admin/tenants/{id}` | Detail / bearbeiten |
| POST | `/super-admin/tenants/{id}/lock` · `/unlock` · `/archive` | Statuswechsel (mit Begründung → Audit) |
| GET | `/super-admin/tenants/{id}/users` | Benutzer des Mandanten (lesend) |
| GET | `/super-admin/users` | Globale Benutzerliste (Filter: tenantId, role, status) |
| GET/PUT | `/super-admin/features` | Feature-Flags global |
| PUT | `/super-admin/tenants/{id}/features` | Feature-Flags je Mandant |
| POST | `/super-admin/tenants/{id}/support-session` | Supportzugriff starten (zeitlich begrenzt, sichtbar) |
| DELETE | `/super-admin/support-sessions/{id}` | Supportzugriff beenden |
| GET | `/super-admin/audit-logs` | Globales Audit-Log (Filter: tenantId, userId, action, from, to, entity) |
| GET | `/super-admin/system` | Systemkonfiguration, Version, Hintergrundjobs |

## Mandant — Stammdaten
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET/PUT | `/tenants/current` | Eigene Unternehmensdaten, Branding, Zeitzone (settings.manage) |
| GET/PUT | `/tenants/current/settings` | Fristen, Nummernkreise, Einheiten, Benachrichtigungen (settings.manage) |
| GET/POST | `/users` | Benutzer listen / einladen (users.write) |
| GET/PUT | `/users/{id}` | Detail / Rolle & Einrichtungszuordnung ändern (users.write) |
| POST | `/users/{id}/deactivate` · `/activate` · `/resend-invitation` · `/password-reset` | Aktionen (users.write) |
| GET/POST | `/locations` · GET/PUT `/locations/{id}` | Produktionsstandorte (settings.manage) |
| GET/POST | `/facilities` · GET/PUT `/facilities/{id}` | Einrichtungen inkl. Frist, Liefertage, Notizen (facilities.write) |

## Zutaten & Nährwerte
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET | `/ingredients` | Liste (Filter: q, category, allergen, active) (ingredients.read) |
| POST | `/ingredients` | Anlegen — optional mit übernommenen API-Nährwerten (ingredients.write) |
| GET/PUT | `/ingredients/{id}` | Detail / bearbeiten (ingredients.write) |
| POST | `/ingredients/{id}/deactivate` | Deaktivieren (ingredients.write) |
| GET/POST | `/ingredient-categories` | Kategorien (ingredients.write) |
| GET | `/allergens` | Allergenliste (Stammdaten) |
| GET | `/ingredients/export` | CSV-Export (ingredients.read) |
| POST | `/ingredients/import` | CSV-Import, validiert (ingredients.write) |
| **GET** | **`/nutrition/search?q=…`** | **Nährwertsuche über Lebensmittel-API (Open Food Facts / USDA), normalisiert** |
| **GET** | **`/nutrition/product/{ean}`** | **Direktabruf per EAN/Barcode** |
| POST | `/ingredients/{id}/nutrition/refresh` | Nährwerte neu von der API abrufen (ingredients.write) |

## Rezepte
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET/POST | `/recipes` | Liste (q, category, active) / erstellen (recipes.write) |
| GET/PUT | `/recipes/{id}` | Detail (inkl. berechneter Allergene & Nährwerte je Portion) / bearbeiten |
| POST | `/recipes/{id}/duplicate` · `/archive` | Aktionen (recipes.write) |
| GET | `/recipes/{id}/versions` · `/versions/{versionId}` | Änderungsverlauf, Snapshots (recipes.read) |
| GET | `/recipes/{id}/scale?portions=250` | Hochrechnung serverseitig, decimal-sicher (recipes.read) |

## Speisepläne
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET/POST | `/meal-plans` | Liste (Jahr/KW/Status) / Wochenplan anlegen (mealplans.write) |
| GET/PUT | `/meal-plans/{id}` | Detail / Tage, Gerichte, Menülinien, Einrichtungen (mealplans.write) |
| POST | `/meal-plans/{id}/duplicate` | Woche duplizieren |
| POST | `/meal-plans/{id}/submit-review` | Status → REVIEW |
| POST | `/meal-plans/{id}/publish` | Veröffentlichen — erzeugt Rezept-Snapshots, benachrichtigt Einrichtungen (mealplans.publish) |
| POST | `/meal-plans/{id}/unpublish` | Nur ohne vorliegende Bestellungen (mealplans.publish) |
| POST | `/meal-plans/{id}/archive` | Archivieren |
| GET | `/meal-plans/{id}/preview?facilityId=…` | Vorschau aus Einrichtungssicht |

## Kundenportal / Bestellungen
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET | `/portal/meal-plans` | Veröffentlichte Pläne der eigenen Einrichtung(en) |
| GET/POST | `/orders` | Eigene Bestellungen / Entwurf anlegen (orders.write) |
| GET/PUT | `/orders/{id}` | Detail / Positionen & Hinweise ändern — **serverseitige Fristprüfung**, Mengen ≥ 0 |
| POST | `/orders/{id}/submit` | Verbindlich absenden → SUBMITTED (orders.write) |
| POST | `/orders/{id}/override` | Korrektur nach Frist — nur Owner/Admin, Begründung Pflicht → Audit (orders.override) |
| GET | `/orders/{id}/history` | Änderungsverlauf |
| GET | `/facilities/current` · `/facilities/current/users` | Eigene Einrichtungsdaten / Benutzer (FACILITY_ADMIN) |

## Produktion
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| GET | `/production-plans?from&to&locationId` | Aggregation bestätigter Bestellungen je Tag/Gericht/Einrichtung/Standort (production.read) |
| GET | `/production-plans/{date}` | Tagesansicht inkl. hochgerechneter Zutatenmengen |
| PUT | `/production-plans/{date}/items/{id}` | Status pflegen (KITCHEN_MANAGER/STAFF) (production.manage) |
| POST | `/production-plans/{date}/items/{id}/adjustments` | Zusatz-/Korrekturmenge — Begründung Pflicht → Audit (production.manage) |
| GET | `/production-plans/{date}/export` | Druck/Export |

## Einkauf
| Methode | Pfad | Zweck (Permission) |
|---|---|---|
| POST | `/procurement/generate` | Bedarfsliste aus Produktionsplänen erzeugen (Tag/KW/Standort) (procurement.manage) |
| GET | `/procurement` · GET `/procurement/{id}` | Listen (Filter: Kategorie, Lieferant, Status) (procurement.read) |
| PUT | `/procurement/{id}/status` | DRAFT → REVIEWED → ORDERED → COMPLETED |
| GET | `/procurement/{id}/export` | CSV / Druck |

## Übergreifend
| Methode | Pfad | Zweck |
|---|---|---|
| GET/PUT | `/notifications` · `/notifications/{id}/read` | Interne Benachrichtigungen |
| GET | `/audit-logs` | Audit-Log des eigenen Mandanten (audit.read) |
| GET | `/health` · `/health/ready` | Liveness / Readiness (DB) |
| GET | `/forecast/demand?facilityId&recipeId` | Neutrale Bedarfsbasis aus bestätigten Bestellmengen (keine KI) |
