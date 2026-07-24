using ERP.Application.Common;
using ERP.Application.Common.Interfaces.SRI;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Único consumidor de <see cref="ISriAuthorizationClient"/> dentro de ElectronicDocuments.
/// Mismo patrón que <see cref="IElectronicDocumentReceptionService"/>: resuelve la
/// configuración SRI de la empresa, valida completitud y delega en el cliente ya existente.
/// </summary>
public interface IElectronicDocumentAuthorizationService
{
    /// <summary>
    /// Consulta la autorización de un comprobante ya recibido. Devuelve
    /// <see cref="Result{T}.Failure"/> únicamente ante un fallo de transporte o una respuesta
    /// no interpretable (sin configuración/URL SRI, sin conectividad, respuesta mal formada) —
    /// un resultado terminal oficial del SRI (AUTORIZADO/"NO AUTORIZADO") o el TIMEOUT propio del
    /// cliente tras agotar reintentos son siempre <see cref="Result{T}.Success"/>, porque la
    /// consulta en sí se completó; interpretar el resultado y transicionar el estado del
    /// documento es responsabilidad del llamador.
    /// </summary>
    Task<Result<SriAuthorizationResult>> CheckAsync(
        Guid companyId, string accessKey, CancellationToken ct = default);
}
