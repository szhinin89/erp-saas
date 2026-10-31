using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Resultado tipado de <see cref="ISriAuthorizationClient.CheckAsync"/> — refleja únicamente
/// respuestas oficiales del servicio AutorizacionComprobantesOffline del SRI (AUTORIZADO/
/// "NO AUTORIZADO", con espacio, literal confirmado contra el ambiente de Pruebas real —
/// ver Ficha Técnica SRI, sección "Respuesta de autorización"), el resultado
/// propio del cliente tras agotar reintentos (TIMEOUT), o un fallo de transporte
/// (ERROR_CONEXION/ERROR_RESPUESTA_INVALIDA/SIN_RESPUESTA). Nunca un estado inventado.
/// </summary>
public sealed class SriAuthorizationResult
{
    public required string Status { get; init; }
    public string? AuthorizationNumber { get; init; }
    public DateTime? AuthorizationDate { get; init; }
    public string? DocumentXml { get; init; }
    public IReadOnlyList<string> Messages { get; init; } = Array.Empty<string>();

    /// <summary>Mismos mensajes de <see cref="Messages"/>, sin aplanar — código/tipo/mensaje/información adicional por separado.</summary>
    public IReadOnlyList<SriMessage> StructuredMessages { get; init; } = Array.Empty<SriMessage>();
    public string? ErrorMessage { get; init; }
    public bool Authorized => Status.Equals("AUTORIZADO", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// R12 Fase B — todas las <c>&lt;autorizacion&gt;</c> de la respuesta (el SRI conserva los NO
    /// AUTORIZADO previos de la misma clave), en el orden recibido. Las propiedades de arriba describen
    /// la decisión seleccionada (R14: AUTORIZADO si existe; si no, la primera).
    /// </summary>
    public IReadOnlyList<SriAuthorizationDecision> Decisions { get; init; } =
        Array.Empty<SriAuthorizationDecision>();
}

/// <summary>
/// R12 Fase B — una <c>&lt;autorizacion&gt;</c> individual: estado literal del SRI, su
/// <c>fechaAutorizacion</c> en UTC (null si no se pudo interpretar) y sus mensajes.
/// </summary>
public sealed record SriAuthorizationDecision(
    string Status,
    DateTime? DecisionDateUtc,
    IReadOnlyList<SriMessage> Messages
)
{
    public bool IsAuthorized => Status.Equals("AUTORIZADO", StringComparison.OrdinalIgnoreCase);

    public bool IsNotAuthorized =>
        Status.Equals("NO AUTORIZADO", StringComparison.OrdinalIgnoreCase);

    /// <summary>Mismo formato aplanado que <see cref="SriAuthorizationResult.ErrorMessage"/>.</summary>
    public string Describe() =>
        string.Join("; ", Messages.Select(m => $"[{m.Code}] {m.Message}: {m.AdditionalInfo}"));
}

/// <summary>
/// R12 Fase B — resultado de UNA consulta (sin polling) a AutorizacionComprobantesOffline para conocer
/// las decisiones que el SRI ya tiene de una clave de acceso. <see cref="Succeeded"/> es true solo si
/// la respuesta fue interpretable y corresponde a esa clave (cero decisiones incluido: el SRI no guarda
/// los comprobantes DEVUELTOS); cualquier otro caso es incierto.
/// </summary>
public sealed record SriAuthorizationHistoryResult(
    bool Succeeded,
    IReadOnlyList<SriAuthorizationDecision> Decisions,
    string? ErrorMessage
)
{
    public static SriAuthorizationHistoryResult Failed(string error) => new(false, [], error);
}

/// <summary>
/// Consulta la autorización de un comprobante ya recibido por el SRI (esquema offline).
/// Abstrae el cliente SOAP concreto (<c>SriSoapClient</c>, en Infrastructure) para que
/// Application nunca dependa de HttpClient, XML SOAP ni namespaces del SRI directamente —
/// mismo patrón que <see cref="ISriReceptionClient"/>.
/// </summary>
public interface ISriAuthorizationClient
{
    /// <summary>
    /// Consulta el estado de autorización. El polling con espera exponencial ya ocurre dentro
    /// de esta llamada (reutiliza <c>SriSoapClient.CheckAuthorizationAsync</c>) — el llamador
    /// recibe siempre un resultado terminal (AUTORIZADO/"NO AUTORIZADO"/TIMEOUT) o un fallo de
    /// transporte, nunca un estado intermedio (PENDIENTE/EN_PROCESO). Nunca lanza una excepción.
    /// </summary>
    Task<SriAuthorizationResult> CheckAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    );

    /// <summary>
    /// R12 Fase B — una sola consulta (sin espera inicial ni polling) de las decisiones registradas para
    /// la clave. Solo consulta: nunca transmite. Nunca lanza por fallos de transporte.
    /// </summary>
    Task<SriAuthorizationHistoryResult> QueryDecisionsAsync(
        string accessKey,
        string wsdlUrl,
        CancellationToken ct = default
    );
}
