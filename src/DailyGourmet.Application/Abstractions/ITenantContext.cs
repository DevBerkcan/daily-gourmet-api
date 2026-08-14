namespace DailyGourmet.Application.Abstractions;

/// <summary>
/// Liefert Tenant und Benutzer der aktuell authentifizierten Anfrage. Wird ausschließlich aus der
/// Session aufgelöst — niemals aus einem Request-Body/Query-Parameter (siehe §13 der Vorgaben).
/// Implementiert in Infrastructure, sobald Phase 2 (Auth/Sessions) echte Werte liefern kann.
/// </summary>
public interface ITenantContext
{
    Guid TenantId { get; }

    Guid UserId { get; }
}
