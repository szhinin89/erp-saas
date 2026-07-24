namespace ERP.API.Contracts;

/// <summary>Mensaje para usuario (seguro, siempre presente) y para desarrollador (detalle técnico, solo Development).</summary>
public sealed record ApiResponseMessage(string User, string? Dev);

/// <summary>Metadatos de trazabilidad incluidos en cada respuesta.</summary>
public sealed record ApiResponseMeta(
    string CorrelationId,
    DateTimeOffset Timestamp,
    string? TraceId = null);

/// <summary>
/// Envelope estándar de respuesta de la API. <see cref="Code"/> es la única fuente de
/// verdad: <see cref="Severity"/> y <see cref="Message"/> se derivan de él vía
/// <c>MessageCatalog</c> + <c>ResponseFactory</c>. El detalle dinámico de una
/// instancia (campos de validación, valores específicos, etc.) viaja dentro de
/// <see cref="Data"/> (p. ej. <c>data.errors: string[]</c>).
/// </summary>
public sealed record ApiResponse<T>(
    string Code,
    string Severity,
    ApiResponseMessage Message,
    T? Data,
    ApiResponseMeta Meta
);
