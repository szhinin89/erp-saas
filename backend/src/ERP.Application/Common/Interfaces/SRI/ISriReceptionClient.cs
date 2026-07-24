using ERP.Domain.Modules.ElectronicDocuments.ValueObjects;

namespace ERP.Application.Common.Interfaces.SRI;

/// <summary>
/// Resultado tipado de <see cref="ISriReceptionClient.SendAsync"/> — refleja únicamente
/// respuestas oficiales del servicio RecepcionComprobantesOffline del SRI (RECIBIDA/DEVUELTA)
/// o un fallo de transporte (ERROR_CONEXION/ERROR_RESPUESTA_INVALIDA). Nunca un estado inventado.
/// </summary>
public sealed class SriReceptionResult
{
    public required string Status { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    /// <summary>Mismos mensajes de <see cref="Errors"/>, sin aplanar — código/tipo/mensaje/información adicional por separado.</summary>
    public IReadOnlyList<SriMessage> StructuredMessages { get; init; } = Array.Empty<SriMessage>();
    public bool Received => Status.Equals("RECIBIDA", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Envía un comprobante ya firmado al servicio de recepción del SRI (esquema offline).
/// Abstrae el cliente SOAP concreto (<c>SriSoapClient</c>, en Infrastructure) para que
/// Application nunca dependa de HttpClient, XML SOAP ni namespaces del SRI directamente.
/// </summary>
public interface ISriReceptionClient
{
    /// <summary>
    /// Envía el XML firmado a RecepcionComprobantesOffline. Nunca lanza una excepción por
    /// fallos de red, timeout o respuesta mal formada — los traduce a <see cref="SriReceptionResult.Status"/>
    /// ("ERROR_CONEXION"/"ERROR_RESPUESTA_INVALIDA").
    /// </summary>
    Task<SriReceptionResult> SendAsync(
        byte[] signedXmlBytes, string wsdlUrl, CancellationToken ct = default);
}
