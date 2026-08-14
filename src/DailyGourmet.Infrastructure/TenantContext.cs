using DailyGourmet.Application.Abstractions;

namespace DailyGourmet.Infrastructure;

/// <summary>
/// Platzhalter für Phase 1: es existiert noch keine Authentifizierung/Session (Phase 2), aus der ein
/// Tenant/Benutzer aufgelöst werden könnte. Wirft bewusst statt einen erfundenen Wert zurückzugeben
/// (§64 der Vorgaben — keine Fake-Implementierungen). Wird in Phase 2 durch eine Implementierung
/// ersetzt, die TenantId/UserId aus der authentifizierten Session liest.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid TenantId => throw new NotSupportedException(
        "ITenantContext ist erst ab Phase 2 (Auth/Sessions) verfügbar.");

    public Guid UserId => throw new NotSupportedException(
        "ITenantContext ist erst ab Phase 2 (Auth/Sessions) verfügbar.");
}
