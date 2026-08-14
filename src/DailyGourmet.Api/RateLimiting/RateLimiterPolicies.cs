namespace DailyGourmet.Api.RateLimiting;

/// <summary>Namen der Rate-Limiting-Policies (§41). Phase 1 registriert nur "Auth" — angewendet wird sie erst,
/// sobald in Phase 2 die Auth-Endpunkte (/auth/login, /auth/forgot-password, ...) existieren.</summary>
public static class RateLimiterPolicies
{
    public const string Auth = "auth";
}
