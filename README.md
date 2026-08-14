# Daily Gourmet — Backend

.NET 10 / ASP.NET Core Backend für die Daily-Gourmet-Catering-SaaS. Clean-Architecture-Solution mit vier Layern (`Domain → Application → Infrastructure → Api`), MS SQL Server (EF Core), Minimal APIs.

> Ausführliche Architektur-, Datenbank- und Planungsdokumentation lebt bewusst im **Frontend-Repo** unter `docs/backend/` (`ARCHITECTURE_PLAN.md`, `DATABASE.md` mit dem vollständigen Mermaid-ER-Diagramm, `FRONTEND_CONTRACT_MATRIX.md`, `OPEN_QUESTIONS.md`, `adr/`) — sie ist eng mit den Frontend-Typen verzahnt (Contract-Matrix) und dort leichter aktuell zu halten. Dieses Repo enthält nur den Code plus die ADRs, die direkt Code-Entscheidungen dieses Repos betreffen.

## Aktueller Stand: Phase 1 — Foundation

Es existiert noch **keine Fachlichkeit** (keine Tenants, Users, Recipes, …) — nur die technische Grundlage: Solution-Struktur, leerer `AppDbContext` mit funktionierender Migrationspipeline, ProblemDetails-Fehlerbehandlung, Request-ID, OpenAPI, Health Checks, CORS, Rate-Limiting-Grundgerüst, Cookie-Auth-Registrierung (noch ohne Login-Logik), `ITenantContext`-Interface (Implementierung wirft bewusst `NotSupportedException` bis Phase 2). Siehe `docs/backend/ARCHITECTURE_PLAN.md` im Frontend-Repo für die vollständige Phasenplanung.

## Struktur

```
backend/
  DailyGourmet.slnx
  Directory.Build.props        # Nullable, TreatWarningsAsErrors, Analyzers
  Directory.Packages.props     # zentrale Paketverwaltung
  Dockerfile
  docker-compose.yml           # mssql + api für lokale Entwicklung
  src/
    DailyGourmet.Domain/
    DailyGourmet.Application/
    DailyGourmet.Infrastructure/
    DailyGourmet.Api/
  tests/
    DailyGourmet.Domain.UnitTests/
    DailyGourmet.Application.UnitTests/
    DailyGourmet.Api.IntegrationTests/    # WebApplicationFactory + Testcontainers.MsSql
    DailyGourmet.ArchitectureTests/       # NetArchTest.Rules — erzwingt die Layer-Regeln automatisiert
```

## Voraussetzungen

- .NET 10 SDK
- Docker Desktop (für lokale SQL-Server-Instanz und die Integrationstests)

## Lokale Entwicklung

```bash
# Restore/Build/Test
dotnet restore
dotnet build
dotnet test

# API + SQL Server lokal starten
docker compose up --build
# API erreichbar unter http://localhost:5080
# /health/live, /health/ready, /version, /openapi/v1.json
```

Für `dotnet run` **ohne** docker-compose (z. B. direkt aus der IDE) muss `ConnectionStrings:Default` separat gesetzt werden (User Secrets oder Umgebungsvariable `ConnectionStrings__Default`) — es ist bewusst **kein** Connection String in `appsettings*.json` hinterlegt (§43: keine Connection Strings im Repository).

## Migrationen

```bash
dotnet ef migrations add <Name> \
  --project src/DailyGourmet.Infrastructure \
  --startup-project src/DailyGourmet.Infrastructure \
  --output-dir Persistence/Migrations

dotnet ef database update \
  --project src/DailyGourmet.Infrastructure \
  --startup-project src/DailyGourmet.Infrastructure
```

Migrationen werden **nicht** automatisch beim Container-Start ausgeführt (§56) — `dotnet ef database update` ist ein expliziter, separater Schritt (lokal oder als CI-Deployment-Schritt).

## Wichtige Architekturentscheidungen

Siehe `docs/backend/adr/` im Frontend-Repo, insbesondere:
- [0002 — MS SQL Server statt PostgreSQL](../docs/backend/adr/0002-mssql-over-postgresql.md) (MonsterASP-Hosting)
- [0003 — camelCase JSON + ProblemDetails](../docs/backend/adr/0003-camelcase-json-and-problemdetails.md)
- [0004 — Minimal APIs statt Controller](../docs/backend/adr/0004-minimal-apis.md)
