namespace DailyGourmet.Api.Helpers;

/// <summary>Maps to 404 in ExceptionMiddleware.</summary>
public class NotFoundException(string entity, object id) : Exception($"{entity} '{id}' wurde nicht gefunden.");

/// <summary>Maps to 400 in ExceptionMiddleware.</summary>
public class ValidationException(string message) : Exception(message);

/// <summary>Maps to 403 in ExceptionMiddleware.</summary>
public class ForbiddenException(string message = "Für diese Aktion fehlt die Berechtigung.") : Exception(message);

/// <summary>Maps to 401 in ExceptionMiddleware.</summary>
public class UnauthorizedException(string message = "Anmeldung fehlgeschlagen.") : Exception(message);

/// <summary>Maps to 409 in ExceptionMiddleware — e.g. deadline passed, status conflict.</summary>
public class ConflictException(string message) : Exception(message);
