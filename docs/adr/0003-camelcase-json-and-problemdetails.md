# ADR 0003 — camelCase JSON + ASP.NET Core ProblemDetails

## Status
Angenommen

## Kontext
`docs/api-endpunkte.md`/`docs/backend-architektur.md §8` planen ein eigenes Antwortformat (`{ data, meta? }` bzw. `{ error: { code, message, details? } }`). Die aktuelle Aufgabenstellung fordert stattdessen ASP.NET Core `ProblemDetails` für Fehler (§24) und erlaubt camelCase JSON, wenn das Frontend bereits camelCase verwendet (§22). Das Frontend (`frontend/src/lib/types.ts`, alle `frontend/src/features/*/types.ts`) verwendet durchgängig camelCase — keine einzige snake_case-Property wurde gefunden.

## Entscheidung
- `System.Text.Json` global auf `JsonNamingPolicy.CamelCase`.
- Fehler als `ProblemDetails` (`AddProblemDetails()` + `UseExceptionHandler`), erweitert um `code`, `requestId`, `details` (camelCase) statt eines eigenen `{ error: {...} }`-Envelopes.
- Erfolgs-Responses geben die Ressource/Liste direkt zurück (keine `{ data, meta }`-Hülle); Paginierung erfolgt über ein eigenes `PagedResult<T>`-Contract-Objekt (`items`, `page`, `pageSize`, `totalItems`, `totalPages` — camelCase, kein snake_case wie im generischen Beispiel der Aufgabenstellung).

## Konsequenzen
- Kleinere Abweichung von `docs/api-endpunkte.md` (dort noch `{ error: {...} }` dokumentiert) — dieses Dokument gilt ab jetzt als überholt zugunsten von `ProblemDetails`.
- Kein manuell gepflegtes zweites Fehlerformat; OpenAPI-Schema bleibt Quelle der Wahrheit.
