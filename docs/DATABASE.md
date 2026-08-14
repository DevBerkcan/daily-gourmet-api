# Datenmodell — Daily Gourmet Backend

Zielplattform: **MS SQL Server** (MonsterASP-Hosting, siehe [ADR 0002](adr/0002-mssql-over-postgresql.md)), EF Core Code First, Migrations.

Dieses Dokument ist die **Grundlage zur Abstimmung, bevor die echte Datenbank aufgebaut wird**. Es zeigt das vollständige Zieldatenmodell über alle Phasen (§59) hinweg — implementiert wird es schrittweise, phasenweise, nicht auf einmal.

## Namenskonvention (wichtig, wurde direkt aus dem Frontend-Code abgeleitet)

Wo das Frontend (`frontend/src/lib/types.ts`, `frontend/src/features/*/types.ts`) bereits einen Typnamen für ein Konzept definiert, übernimmt das Backend **exakt diesen Namen** (z. B. `Rezept`, `Speiseplan`, `Bestellung`, `Einrichtung`, `Standort`, `Zutat`, `Tenant`, `ProduktionsPlan`, `Einkaufsliste`). Das Frontend mischt bewusst Deutsch (fachliche Catering-Begriffe) und Englisch (`Tenant` bleibt `Tenant`, nicht `Mandant`) — das Backend übernimmt diese Mischung 1:1, um Übersetzungsfehler zu vermeiden. Rein backend-seitige Infrastruktur-Konzepte, für die es **keine** Entsprechung im Frontend gibt (Sessions, Rollen/Permissions als eigenständiges RBAC, Invitations, Outbox, Support-Sessions, BLS-Referenzdaten), bekommen klare englische Namen.

Allgemeine Konventionen für alle Tabellen:
- `Id UNIQUEIDENTIFIER` (Guid) als Primärschlüssel.
- `TenantId` (+ Index) auf jeder mandantenbezogenen Tabelle (Ausnahme: global gültige Referenzdaten wie `Allergen`, `BlsLebensmittel`).
- `CreatedAt` / `UpdatedAt` als `datetimeoffset`.
- `Version` (Concurrency Token, `rowversion` oder `int`) auf allen veränderbaren Stammdaten-Aggregaten (§26).
- Geldbeträge/Mengen: `decimal`, nie `float`.
- String-Arrays (z. B. `AktiveWochentage`, `Zielgruppen`, `Zubereitungsschritte`) werden als **JSON-Spalte** abgebildet (EF Core Primitive Collections → JSON, SQL Server nativer `JSON`-Typ) — kein eigenes Join-Table-Overengineering für reine Werte-Listen ohne eigene Identität.

---

## 1. Identity & Tenant

```mermaid
erDiagram
    Tenant ||--o{ Benutzer : "hat"
    Tenant ||--|| TenantSettings : "hat"
    Tenant ||--o{ TenantFeatureFlag : "hat"
    FeatureFlag ||--o{ TenantFeatureFlag : "gilt für"
    Benutzer ||--o{ Session : "hat"
    Benutzer ||--o{ PasswordResetToken : "hat"
    Benutzer ||--o{ UserRole : "zugewiesen"
    Role ||--o{ UserRole : "zugewiesen an"
    Role ||--o{ RolePermission : "hat"
    Permission ||--o{ RolePermission : "gehört zu"
    Benutzer ||--o{ UserFacility : "zugeordnet"
    Tenant ||--o{ Invitation : "lädt ein"
    Tenant ||--o{ SupportSession : "Supportzugriff auf"
    Benutzer ||--o{ SupportSession : "SuperAdmin startet"

    Tenant {
        uuid Id PK
        string Name
        string TenantStatus "AKTIV|GESPERRT|ARCHIVIERT"
        string Ansprechpartner
        string Email
        datetimeoffset ErstelltAm
        int Version
    }
    TenantSettings {
        uuid Id PK
        uuid TenantId FK
        string Branding_json
        string Zeitzone
        string Nummernkreise_json
    }
    FeatureFlag {
        uuid Id PK
        string Key
        string Label
    }
    TenantFeatureFlag {
        uuid TenantId FK
        uuid FeatureFlagId FK
        bool Enabled
    }
    Benutzer {
        uuid Id PK
        uuid TenantId FK "nullable — null = SUPER_ADMIN"
        string Name
        string Email
        string BenutzerStatus "AKTIV|EINGELADEN|DEAKTIVIERT"
        datetimeoffset_null LetzteAnmeldung
        int FehlgeschlageneLogins
        int Version
    }
    Role {
        uuid Id PK
        uuid_null TenantId FK "null = System-Rolle (SUPER_ADMIN, TENANT_OWNER, ...)"
        string Name
    }
    Permission {
        uuid Id PK
        string Key "z.B. recipes.write"
    }
    RolePermission {
        uuid RoleId FK
        uuid PermissionId FK
    }
    UserRole {
        uuid BenutzerId FK
        uuid RoleId FK
    }
    UserFacility {
        uuid BenutzerId FK
        uuid EinrichtungId FK
    }
    Session {
        uuid Id PK
        uuid BenutzerId FK
        string TokenHash
        string IpAddress
        string UserAgent
        datetimeoffset CreatedAt
        datetimeoffset LastSeenAt
        datetimeoffset ExpiresAt
        datetimeoffset_null RevokedAt
    }
    PasswordResetToken {
        uuid Id PK
        uuid BenutzerId FK
        string TokenHash
        datetimeoffset ExpiresAt
        datetimeoffset_null UsedAt
    }
    Invitation {
        uuid Id PK
        uuid TenantId FK
        string Email
        string Rolle
        uuid_null EinrichtungId FK
        string TokenHash
        datetimeoffset ExpiresAt
        datetimeoffset_null AcceptedAt
    }
    SupportSession {
        uuid Id PK
        uuid TenantId FK
        uuid SuperAdminBenutzerId FK
        string Reason
        datetimeoffset StartedAt
        datetimeoffset ExpiresAt
        datetimeoffset_null EndedAt
    }
```

> `Benutzer.Rolle` (fixes Enum aus dem Frontend: `SUPER_ADMIN, TENANT_OWNER, TENANT_ADMIN, KITCHEN_MANAGER, KITCHEN_STAFF, FACILITY_ADMIN, FACILITY_USER, READ_ONLY`) wird als **Systemrolle geseedet** (`Role.TenantId = null`) und dem Benutzer über `UserRole` zugewiesen — kein separates redundantes Enum-Feld auf `Benutzer`. Die API liefert im `GET /auth/me`-Response trotzdem ein einzelnes `rolle`-Feld (primäre/höchste Rolle), damit das Frontend unverändert bleibt.

## 2. Facilities

```mermaid
erDiagram
    Tenant ||--o{ Standort : "hat"
    Tenant ||--o{ Einrichtung : "hat"
    Standort ||--o{ Einrichtung : "beliefert"
    Einrichtung ||--|| EinrichtungEinstellungen : "hat"

    Standort {
        uuid Id PK
        uuid TenantId FK
        string Name
        string Anschrift
        string Kontaktperson
        int KapazitaetPortionen
        string Status "AKTIV|INAKTIV"
        int Version
    }
    Einrichtung {
        uuid Id PK
        uuid TenantId FK
        string Name
        string Kundennummer
        string Anschrift
        string Ansprechpartner
        string Email
        string Telefon
        uuid StandortId FK
        time Bestellfrist
        json AktiveWochentage
        decimal Portionspreis
        string Status "AKTIV|INAKTIV"
        string_null Notizen
        int Version
    }
    EinrichtungEinstellungen {
        uuid Id PK
        uuid EinrichtungId FK
        json Einstellungen
    }
```

## 3. Catalog (Zutaten) + BLS-Referenzdaten

```mermaid
erDiagram
    Tenant ||--o{ Zutat : "hat"
    Tenant ||--o{ ZutatKategorie : "hat"
    ZutatKategorie ||--o{ Zutat : "kategorisiert"
    Zutat ||--o{ ZutatAllergen : "enthält"
    Allergen ||--o{ ZutatAllergen : "in"
    Zutat ||--|| ZutatNaehrwert : "hat"
    Zutat }o--o| BlsLebensmittel : "referenziert optional"
    BlsLebensmittel ||--o{ BlsNaehrwert : "hat"

    ZutatKategorie {
        uuid Id PK
        uuid TenantId FK
        string Name
    }
    Allergen {
        uuid Id PK
        string Name "14 EU-Allergene, global geseedet"
    }
    Zutat {
        uuid Id PK
        uuid TenantId FK
        string Name
        string Artikelnummer
        uuid ZutatKategorieId FK
        string Basiseinheit "g|kg|ml|l|Stück"
        string Einkaufseinheit
        decimal Umrechnungsfaktor
        decimal_null Einkaufspreis
        string Lieferant
        bool Vegetarisch
        bool Vegan
        bool Bio
        bool Regional
        json Zusatzstoffe
        string Status "ACTIVE|INACTIVE|ARCHIVED"
        uuid_null BlsLebensmittelId FK
        int Version
    }
    ZutatAllergen {
        uuid ZutatId FK
        uuid AllergenId FK
    }
    ZutatNaehrwert {
        uuid Id PK
        uuid ZutatId FK
        decimal Kcal
        decimal EiweissG
        decimal FettG
        decimal KohlenhydrateG
        decimal ZuckerG
        decimal SalzG
        string Quelle "OpenFoodFacts|Usda|Manual|Bls"
        string_null ExternalId
        datetimeoffset_null FetchedAt
    }
    BlsLebensmittel {
        uuid Id PK
        string Code "Bundeslebensmittelschlüssel, global, nicht tenant-gebunden"
        string Bezeichnung
    }
    BlsNaehrwert {
        uuid Id PK
        uuid BlsLebensmittelId FK
        string NaehrstoffCode
        decimal Wert
        string Einheit
    }
```

> Zwei Nährwert-Strategien existieren nebeneinander (`Quelle`-Enum deckt beide ab) — siehe [OPEN_QUESTIONS.md](OPEN_QUESTIONS.md) zur Priorisierung Open-Food-Facts/USDA vs. BLS.

## 4. Recipes & Versionierung

```mermaid
erDiagram
    Tenant ||--o{ Rezept : "hat"
    Rezept ||--o{ RezeptVersion : "hat"
    RezeptVersion ||--o{ RezeptZutat : "enthält"
    Zutat ||--o{ RezeptZutat : "verwendet in"
    RezeptVersion ||--|| RezeptNaehrwerte100 : "hat"

    Rezept {
        uuid Id PK
        uuid TenantId FK
        string Name
        string Beschreibung
        string Kategorie
        string_null Rezeptnummer
        int StandardPortionen
        decimal_null PortionsgewichtG
        int ZubereitungszeitMin
        string Schwierigkeitsgrad "Einfach|Mittel|Anspruchsvoll"
        bool Vegetarisch
        bool Vegan
        string_null Produktionshinweise
        json Zielgruppen
        json Zubereitungsschritte
        string_null BildUrl
        decimal_null KerntemperaturC
        string_null Lagerhinweis
        string_null Haltbarkeit
        uuid ErstelltVonBenutzerId FK
        string Status "ACTIVE|ARCHIVED"
        int AktuelleVersionNummer
        int Version
    }
    RezeptVersion {
        uuid Id PK
        uuid RezeptId FK
        int VersionNummer
        datetimeoffset ErstelltAm
        uuid ErstelltVonBenutzerId FK
        bool IstUnveraenderlich "true sobald in einem veröffentlichten Speiseplan referenziert — §28"
    }
    RezeptZutat {
        uuid Id PK
        uuid RezeptVersionId FK
        uuid ZutatId FK
        decimal Menge
        string Einheit
    }
    RezeptNaehrwerte100 {
        uuid Id PK
        uuid RezeptVersionId FK
        decimal Kcal
        decimal Kj
        decimal FettG
        decimal GesFettSaeurenG
        decimal KohlenhydrateG
        decimal ZuckerG
        decimal BallaststoffeG
        decimal EiweissG
        decimal SalzG
        decimal AlkoholG
        string_null NutriScore "A|B|C|D|E"
    }
```

> Zentrale Geschäftsregel (§28): jede Änderung an einem Rezept erzeugt eine **neue** `RezeptVersion`; eine bereits veröffentlichte Version wird nie verändert (`IstUnveraenderlich = true`). `Speiseplan`-Einträge referenzieren immer eine konkrete `RezeptVersionId`, nie das Live-`Rezept`.

## 5. Meal Planning (Speisepläne)

```mermaid
erDiagram
    Tenant ||--o{ Speiseplan : "hat"
    Speiseplan ||--o{ SpeiseplanStandort : "gilt für"
    Standort ||--o{ SpeiseplanStandort : "in"
    Speiseplan ||--o{ SpeiseplanEinrichtung : "gilt für"
    Einrichtung ||--o{ SpeiseplanEinrichtung : "in"
    Speiseplan ||--o{ SpeiseplanTag : "hat"
    SpeiseplanTag ||--o{ SpeiseplanTagRezept : "enthält"
    RezeptVersion ||--o{ SpeiseplanTagRezept : "geplant als"

    Speiseplan {
        uuid Id PK
        uuid TenantId FK
        int Kalenderwoche
        int Jahr
        string SpeiseplanStatus "DRAFT|REVIEW|PUBLISHED|CLOSED|ARCHIVED"
        int Version
    }
    SpeiseplanStandort {
        uuid SpeiseplanId FK
        uuid StandortId FK
    }
    SpeiseplanEinrichtung {
        uuid SpeiseplanId FK
        uuid EinrichtungId FK
    }
    SpeiseplanTag {
        uuid Id PK
        uuid SpeiseplanId FK
        string Wochentag
        date Datum
        string_null Hinweis
    }
    SpeiseplanTagRezept {
        uuid Id PK
        uuid SpeiseplanTagId FK
        uuid RezeptVersionId FK
        int Reihenfolge
    }
```

## 6. Orders → Production → Procurement (mit Traceability, §38/§39)

```mermaid
erDiagram
    Einrichtung ||--o{ Bestellung : "gibt auf"
    Speiseplan ||--o{ Bestellung : "bezieht sich auf"
    Bestellung ||--o{ BestellPosition : "enthält"
    RezeptVersion ||--o{ BestellPosition : "bestellt als"
    Bestellung ||--o{ BestellVerlauf : "History"

    Standort ||--o{ ProduktionsPlan : "für"
    ProduktionsPlan ||--o{ ProduktionsPosition : "enthält"
    RezeptVersion ||--o{ ProduktionsPosition : "produziert"
    ProduktionsPosition ||--o{ ProduktionsPositionBestellQuelle : "aggregiert aus"
    BestellPosition ||--o{ ProduktionsPositionBestellQuelle : "Quelle für"

    Standort ||--o{ Einkaufsliste : "für"
    Einkaufsliste ||--o{ EinkaufslistenPosition : "enthält"
    Zutat ||--o{ EinkaufslistenPosition : "beschafft"
    EinkaufslistenPosition ||--o{ EinkaufslistenPositionQuelle : "aggregiert aus"
    ProduktionsPosition ||--o{ EinkaufslistenPositionQuelle : "Quelle für"

    Bestellung {
        uuid Id PK
        uuid TenantId FK
        uuid EinrichtungId FK
        uuid SpeiseplanId FK
        string BestellStatus "DRAFT|SUBMITTED|CONFIRMED|LOCKED|CANCELLED"
        datetimeoffset_null AbgesendetAm
        datetimeoffset Frist
        int Version
    }
    BestellPosition {
        uuid Id PK
        uuid BestellungId FK
        date Datum
        uuid RezeptVersionId FK
        int Portionen
        string_null Hinweis
    }
    BestellVerlauf {
        uuid Id PK
        uuid BestellungId FK
        json Snapshot
        string_null Grund "Pflicht bei nachträglicher Änderung nach Fristablauf — §29"
        uuid GeaendertVonBenutzerId FK
        datetimeoffset GeaendertAm
    }
    ProduktionsPlan {
        uuid Id PK
        uuid TenantId FK
        date Datum
        uuid StandortId FK
        string PlanStatus "PLANNED|IN_PROGRESS|COMPLETED|CANCELLED"
        int Version
    }
    ProduktionsPosition {
        uuid Id PK
        uuid ProduktionsPlanId FK
        uuid RezeptVersionId FK
        decimal BestellteMenge
        decimal ZusatzMenge
        string ProduktionsStatus "PLANNED|PREPARING|COMPLETED|CANCELLED"
        string_null Begruendung
    }
    ProduktionsPositionBestellQuelle {
        uuid Id PK
        uuid ProduktionsPositionId FK
        uuid BestellPositionId FK
        int Portionen
    }
    Einkaufsliste {
        uuid Id PK
        uuid TenantId FK
        string Bezeichnung
        int Kalenderwoche
        uuid StandortId FK
        string EinkaufslistenStatus "DRAFT|REVIEWED|ORDERED|COMPLETED"
        int Version
    }
    EinkaufslistenPosition {
        uuid Id PK
        uuid EinkaufslisteId FK
        uuid ZutatId FK
        decimal GesamtmengeBasis
        decimal Einkaufsmenge
    }
    EinkaufslistenPositionQuelle {
        uuid Id PK
        uuid EinkaufslistenPositionId FK
        uuid ProduktionsPositionId FK
        decimal Menge
    }
```

## 7. Logistics (Lieferrouten — Fahrer-App)

> Existiert bereits als eigener Frontend-Bereich (`frontend/src/app/driver/*`, `frontend/src/features/logistics`), war aber nicht Teil des ursprünglichen Endpunkt-Katalogs (§34) — als Erweiterung ergänzt, damit Frontend und Backend deckungsgleich bleiben.

```mermaid
erDiagram
    Tenant ||--o{ Fahrer : "hat"
    Tenant ||--o{ LieferRoute : "hat"
    Fahrer ||--o{ LieferRoute : "fährt"
    LieferRoute ||--o{ RoutenStopp : "hat"
    Einrichtung ||--o{ RoutenStopp : "Ziel"
    RoutenStopp ||--o{ LieferPosition : "enthält"
    RezeptVersion ||--o{ LieferPosition : "liefert"

    Fahrer {
        uuid Id PK
        uuid TenantId FK
        string Name
        string Telefon
        string Fahrzeug
        string Kennzeichen
    }
    LieferRoute {
        uuid Id PK
        uuid TenantId FK
        string Name
        date Datum
        uuid FahrerId FK
        time Start
        time Rueckkehr
        decimal Kilometer
        string RouteStatus "GEPLANT|BELADUNG|UNTERWEGS|ABGESCHLOSSEN"
    }
    RoutenStopp {
        uuid Id PK
        uuid LieferRouteId FK
        uuid EinrichtungId FK
        int Reihenfolge
        datetimeoffset_null Ankunft
        string Zeitfenster
        string Kontakt
        string Telefon
        string_null Hinweis
        string StoppStatus "OFFEN|ZUGESTELLT|PROBLEM"
    }
    LieferPosition {
        uuid Id PK
        uuid RoutenStoppId FK
        uuid RezeptVersionId FK
        int Portionen
        string Behaelter
        string Temperatur
        string_null Hinweis
    }
```

## 8. Platform (Notifications, Audit, Outbox, Support-Tickets)

```mermaid
erDiagram
    Benutzer ||--o{ Benachrichtigung : "erhält"
    Tenant ||--o{ AuditEintrag : "protokolliert"
    Tenant ||--o{ OutboxEvent : "erzeugt"
    Tenant ||--o{ SupportTicket : "erstellt"
    SupportTicket ||--o{ SupportAntwort : "hat"

    Benachrichtigung {
        uuid Id PK
        uuid BenutzerId FK
        string Titel
        string Text
        datetimeoffset Zeitpunkt
        bool Gelesen
    }
    AuditEintrag {
        uuid Id PK
        uuid_null TenantId FK
        uuid_null ActorBenutzerId FK
        uuid_null SupportSessionId FK
        string Aktion
        string EntityTyp
        uuid EntityId
        json_null BeforeData
        json_null AfterData
        string_null Begruendung
        string_null IpAddress
        string_null UserAgent
        string_null RequestId
        datetimeoffset OccurredAt
    }
    OutboxEvent {
        uuid Id PK
        uuid_null TenantId FK
        string EventType
        string AggregateType
        uuid AggregateId
        json Payload
        string Status "PENDING|PROCESSING|SENT|FAILED"
        int AttemptCount
        datetimeoffset AvailableAt
        datetimeoffset_null ProcessedAt
        string_null LastError
        datetimeoffset CreatedAt
    }
    SupportTicket {
        uuid Id PK
        uuid TenantId FK
        uuid ErstelltVonBenutzerId FK
        string Kategorie "BUG|FRAGE|FEATURE"
        string Prioritaet "NIEDRIG|NORMAL|HOCH|KRITISCH"
        string Titel
        string Nachricht
        string_null Seite
        string SupportStatus "OFFEN|IN_BEARBEITUNG|GELOEST"
        datetimeoffset ErstelltAm
    }
    SupportAntwort {
        uuid Id PK
        uuid SupportTicketId FK
        uuid AutorBenutzerId FK
        string Rolle
        string Text
        datetimeoffset Zeitpunkt
    }
```

`AuditEintrag` ist append-only (kein UPDATE/DELETE über die API, §33). `OutboxEvent` wird von einem `BackgroundService` verarbeitet (§31/§32), nie synchron innerhalb einer Geschäftstransaktion befüllt und sofort verschickt.

## Indexe (Mindestanforderung, §47)

`Benutzer(Email)` · `UserRole(BenutzerId, RoleId)` · `Einrichtung(TenantId, Status)` · `Standort(TenantId, Status)` · `Zutat(TenantId, Status, Name)` · `Rezept(TenantId, Status)` · `RezeptVersion(RezeptId, VersionNummer)` · `Speiseplan(TenantId, Jahr, Kalenderwoche, SpeiseplanStatus)` · `Bestellung(TenantId, EinrichtungId, BestellStatus)` · `Bestellung(SpeiseplanId, BestellStatus)` · `ProduktionsPlan(TenantId, StandortId, Datum)` · `Einkaufsliste(TenantId, StandortId, Kalenderwoche)` · `Benachrichtigung(BenutzerId, Gelesen, Zeitpunkt)` · `OutboxEvent(Status, AvailableAt)`.

## Phasenzuordnung

Dieses Dokument zeigt das **Zielbild**. Tatsächlich per EF-Core-Migration angelegt wird schrittweise:
- **Phase 1 (dieser Durchlauf):** keine der oben genannten Tabellen — nur die leere `AppDbContext`-Migrationspipeline (§59 Phase 1 enthält bewusst noch keine Fachlichkeit).
- **Phase 2:** Abschnitt 1 (Identity & Tenant, ohne Invitation/SupportSession — die kommen mit Phase 3/§14).
- **Phase 3:** Abschnitt 2 (Facilities) + Invitation/SupportSession aus Abschnitt 1.
- **Phase 4:** Abschnitt 3 (Catalog/Zutaten; BLS-Referenzdaten erst nach Klärung der offenen Frage).
- **Phase 5:** Abschnitt 4 (Recipes & Versionierung).
- **Phase 6:** Abschnitt 5 (Meal Planning).
- **Phase 7:** Abschnitt 6, Bestellungen-Teil (Orders).
- **Phase 8/9:** Abschnitt 6, Production/Procurement-Teil.
- **Phase 10:** Abschnitt 8 (Platform) + Abschnitt 7 (Logistics, sofern priorisiert).
