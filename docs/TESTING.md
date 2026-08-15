# Teststrategie

Ziel: Mit wachsender Feature-Zahl weiter sicher entwickeln können — ein grüner `dotnet test`-Lauf (lokal wie in CI) ist die Bedingung dafür, dass eine Änderung als fertig gilt, nicht eine Meinung.

## Die vier Testprojekte und wofür sie stehen

| Projekt | Prüft | Läuft gegen |
|---|---|---|
| `DailyGourmet.ArchitectureTests` | Layer-Abhängigkeitsrichtung (`Domain` hat keine Abhängigkeiten, `Application` nur `Domain`, `Infrastructure` nicht `Api`) **und** dass `Domain`/`Application` keine verbotenen Framework-Namespaces (`Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore`, `System.Text.Json`) referenzieren. Automatisiert per `NetArchTest.Rules` — ein Verstoß lässt den Test fehlschlagen, nicht nur eine Doku-Regel verletzen. | reine Reflection über die gebauten Assemblies, kein externer Dienst nötig |
| `DailyGourmet.Domain.UnitTests` | Domain-Regeln: Rezeptskalierung, Fristlogik, Statusübergänge, Mengen-/Einheitenumrechnung, Produktions-/Einkaufsaggregation (§52). Noch leer — es existiert noch keine Domain-Logik (Phase 1). Füllt sich ab Phase 2/5. | keine Abhängigkeiten, reine Objektkonstruktion |
| `DailyGourmet.Application.UnitTests` | Use-Case-Verhalten: Permissions, Validierung, Business-Regeln pro Command/Query-Handler. Noch leer aus demselben Grund. | gemockte/gefakte `Application`-Interfaces (`ITenantContext` etc.), kein echtes EF Core |
| `DailyGourmet.Api.IntegrationTests` | Echte HTTP-Roundtrips über `WebApplicationFactory<Program>` gegen eine **echte, containerisierte SQL-Server-Instanz** (`Testcontainers.MsSql`) — kein In-Memory-Provider, weil sich Migrations/Constraints/Concurrency damit anders verhalten als produktiv (§52). Deckt insbesondere die verpflichtenden Security-Tests ab (§52): Mandantentrennung, Facility-Scoping, Rollen-Eskalation — sobald es in Phase 2 echte Auth/Tenants gibt. | Docker (lokal: Docker Desktop; CI: im GitHub-Actions-Ubuntu-Runner bereits vorinstalliert) |

## Regel: kein Feature ohne Tests (Definition of Done, §69)

Ein Feature ist erst fertig, wenn zusätzlich zu Domain-Regel/Use-Case/Endpoint/Autorisierung/Validierung/Audit auch **Unit-Tests und Integrationstests** existieren — inklusive eines expliziten Mandantentrennungs-Tests, sobald Tenant-Daten im Spiel sind (§52: "Tenant A darf Recipe von Tenant B NICHT lesen" usw. sind verpflichtend, nicht optional). Siehe `ARCHITECTURE_PLAN.md` §7 ("Wie fügt man ein neues Feature hinzu?") für den vollständigen Ablauf pro Use Case.

Platzhalter-Tests ("1+1=2"-Filler, damit ein Testprojekt nicht leer aussieht) sind ausdrücklich **nicht** erwünscht (§64) — ein leeres Testprojekt ist ehrlicher als ein bedeutungsloser grüner Haken. `Domain.UnitTests`/`Application.UnitTests` bleiben deshalb bewusst leer, bis es echte Logik zu testen gibt.

## CI-Gate

`.github/workflows/ci.yml` läuft bei jedem Push und jeder Pull-Request nach `main`: `dotnet restore` → `dotnet build` (Warnungen sind Fehler, siehe `Directory.Build.props`) → Unit-/Architekturtests → Integrationstests (mit Docker/Testcontainers, auf GitHub-Ubuntu-Runnern von Haus aus verfügbar). Ein roter Lauf zeigt sich direkt am Commit/PR in GitHub.

**Damit das auch tatsächlich blockiert und nicht nur informiert:** In den GitHub-Repo-Einstellungen unter *Settings → Branches → Branch protection rule* für `main` sollte "Require status checks to pass before merging" mit dem `build-and-test`-Job aktiviert werden. Das ist eine einmalige manuelle Einstellung im GitHub-Web-UI (nicht Teil dieses Repos) — ohne sie läuft die CI zwar bei jedem Push, verhindert aber nicht das Mergen bei Rot.

## Lokal ausführen

```bash
dotnet test --filter "FullyQualifiedName!~IntegrationTests"   # schnell, kein Docker nötig
dotnet test                                                     # vollständig, Docker Desktop muss laufen
```
