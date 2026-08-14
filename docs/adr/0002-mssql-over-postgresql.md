# ADR 0002 — MS SQL Server statt PostgreSQL

## Status
Angenommen

## Kontext
Die generische Aufgabenstellung für dieses Backend nennt PostgreSQL als primäre Datenbank (UUID, `timestamptz`, `jsonb`, `citext`, Row Level Security). Das Repository enthält jedoch bereits eine konkrete Backend-Planung (`docs/backend-architektur.md`), die explizit MS SQL Server wählt, **weil die Anwendung auf MonsterASP gehostet wird** — dort ist MSSQL nativ verfügbar, PostgreSQL nicht. Diese Diskrepanz wurde mit dem Nutzer direkt geklärt (Rückfrage während der Planungsphase): MonsterASP bleibt das reale Hosting-Ziel.

## Entscheidung
MS SQL Server über `Microsoft.EntityFrameworkCore.SqlServer`. Postgres-spezifische Vorgaben werden auf SQL-Server-Äquivalente übertragen:
- `UUID` → `UNIQUEIDENTIFIER` (Guid)
- `timestamptz` → `datetimeoffset`
- `numeric` → `decimal`
- `jsonb` → nativer SQL-Server-`JSON`-Typ bzw. EF-Core-Primitive-Collections auf JSON-Spalten (für reine Werte-Listen wie `Zielgruppen`, `AktiveWochentage`)
- `citext` (case-insensitive E-Mail) → `datetimeoffset`-… nein: case-insensitive Vergleich über eine `SqlServer`-Collation (`Latin1_General_CI_AS`) auf der `Email`-Spalte statt eines eigenen Typs
- PostgreSQL Row Level Security → entfällt als zusätzliche DB-Ebene; die in der Aufgabenstellung geforderte Defense-in-Depth für Mandantentrennung wird stattdessen über EF Core Global Query Filters + `SaveChanges`-Interceptor + dedizierte Integrationstests realisiert (§13) — funktional gleichwertig, ohne postgres-spezifischen Mechanismus.

## Konsequenzen
- EF Core abstrahiert den Provider; ein späterer Wechsel zu PostgreSQL bleibt möglich, ist aber nicht der aktuelle Plan.
- Kein `Npgsql`-Paket im Solution-Tree.
- Docker-Compose nutzt `mcr.microsoft.com/mssql/server` statt `postgres`.
