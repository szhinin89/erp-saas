using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Único consumidor de <see cref="ISriReceptionClient"/> dentro de ElectronicDocuments. Mismo
/// patrón que <see cref="IElectronicDocumentSigningService"/>: resuelve la configuración SRI
/// de la empresa, valida completitud y delega el envío en el cliente ya existente — nunca
/// reimplementa el cliente SOAP ni conoce HttpClient.
/// </summary>
public interface IElectronicDocumentReceptionService
{
    /// <summary>
    /// Envía el XML firmado al servicio de recepción del SRI. Devuelve
    /// <see cref="Result{T}.Failure"/> únicamente ante un fallo de transporte (sin configuración
    /// SRI, sin URL configurada, sin conectividad, respuesta no interpretable) — una respuesta
    /// oficial del SRI (RECIBIDA o DEVUELTA) es siempre <see cref="Result{T}.Success"/>, porque
    /// el envío en sí se completó; la interpretación de RECIBIDA/DEVUELTA es responsabilidad
    /// del llamador.
    /// </summary>
    Task<Result<SriReceptionResult>> SendAsync(
        Guid companyId, byte[] signedXmlBytes, CancellationToken ct = default);
}
