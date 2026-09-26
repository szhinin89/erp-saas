namespace ERP.API.Contracts;

/// <summary>Mensaje para usuario (seguro, siempre presente) y para desarrollador (detalle técnico, solo Development).</summary>
public sealed record ApiResponseMessage(string User, string? Dev);

/// <summary>
/// Metadatos de trazabilidad incluidos en cada respuesta. <c>Timestamp</c> es un INSTANTE del
/// contrato temporal único (ZH-TEMPORAL-CONTRACT-02J): <see cref="DateTime"/> Kind=Utc serializado
/// como ISO-8601 terminado en "Z" (nunca "+00:00").
/// </summary>
public sealed record ApiResponseMeta(
    string CorrelationId,
    DateTime Timestamp,
    string? TraceId = null
);

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
