using ERP.Application.Common;
using ERP.Application.Modules.ElectronicDocuments.DTOs;
using ERP.Domain.Modules.ElectronicDocuments.Enums;

namespace ERP.Application.Modules.ElectronicDocuments.Services;

/// <summary>
/// Petición para registrar un documento de negocio ante ElectronicDocuments. Contiene únicamente
/// la referencia al documento de origen — nunca datos comerciales (cliente, ítems, impuestos,
/// totales, forma de pago). TenantId/CompanyId van explícitos (no vía ICurrentTenant/ICurrentCompany)
/// porque este servicio también debe poder invocarse desde contextos sin HttpContext (jobs futuros).
/// </summary>
public sealed record RegisterElectronicDocumentRequest(
    Guid TenantId,
    Guid CompanyId,
    ElectronicDocumentType DocumentType,
    string SourceModule,
    Guid SourceEntityId,
    Guid UserId);

/// <summary>
/// Única puerta de entrada pública del módulo ElectronicDocuments para el resto del ERP.
/// Ningún módulo de negocio debe conocer XML, SOAP, certificados ni códigos del SRI —
/// todos consumen esta interfaz.
///
/// El registro exige que exista un <see cref="IElectronicDocumentDataProvider"/> capaz de
/// producir el modelo común (<see cref="ElectronicDocumentData"/>) del documento de origen; si
/// no existe, o el documento de origen no existe, el registro falla explícitamente.
///
/// El <c>ElectronicDocument</c> se persiste en Draft ANTES de correr el pipeline — así ninguna
/// factura de punto de emisión electrónico queda invisible en el Monitor si algo falla. Pipeline
/// ejecutado por <see cref="RegisterAsync"/>: obtiene el modelo común, genera el XML, lo valida
/// contra XSD, lo firma (XAdES-BES), almacena ambos XML, lo envía a Recepción del SRI y consulta
/// su Autorización — terminando en Authorized, Rejected, Failed (si alguna etapa previa a la
/// firma falló) o Signed/Received si algún paso de red no resolvió (esos quedan para
/// <see cref="RetryAsync"/>, que también reanuda el pipeline completo desde Draft/Failed).
/// </summary>
public interface IElectronicDocumentIssuer
{
    Task<Result<ElectronicDocumentDto>> RegisterAsync(
        RegisterElectronicDocumentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Reintenta un documento ya existente varado en Draft/Failed (el pipeline previo a la firma
    /// nunca resolvió), Signed (nunca se envió) o Received (se envió pero la autorización no
    /// resolvió). Si el documento está en DeadLetter, lo reactiva primero
    /// (<see cref="ERP.Domain.Modules.ElectronicDocuments.Entities.ElectronicDocument.Reactivate"/>)
    /// y continúa desde el estado restaurado. Incrementa RetryCount antes del intento; si no
    /// resuelve el documento y se agotó <see cref="ElectronicDocumentRetryPolicy.MaxAttempts"/>,
    /// lo pasa a DeadLetter. Llamable desde un job sin HttpContext (tenantId/userId explícitos).
    /// </summary>
    Task<Result<ElectronicDocumentDto>> RetryAsync(
        Guid tenantId, Guid electronicDocumentId, Guid userId, CancellationToken ct = default);
}
