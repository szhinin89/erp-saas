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
}
